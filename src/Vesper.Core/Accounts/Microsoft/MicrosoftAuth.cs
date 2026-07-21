using System.Net.Http.Json;
using System.Text.Json;

namespace Vesper.Core.Accounts.Microsoft;

public sealed class MicrosoftAuth
{
    public const string DefaultClientId = "00000000402b5328";
    public const string XboxLiveScope = "service::user.auth.xboxlive.com::MBI_SSL";

    private const string DeviceCodeEndpoint = "https://login.live.com/oauth20_connect.srf";
    private const string TokenEndpoint = "https://login.live.com/oauth20_token.srf";
    private const string XboxAuthEndpoint = "https://user.auth.xboxlive.com/user/authenticate";
    private const string XstsEndpoint = "https://xsts.auth.xboxlive.com/xsts/authorize";
    private const string MinecraftLoginEndpoint = "https://api.minecraftservices.com/authentication/login_with_xbox";
    private const string MinecraftProfileEndpoint = "https://api.minecraftservices.com/minecraft/profile";

    private readonly HttpClient _http;
    private readonly string _clientId;

    public MicrosoftAuth(HttpClient? http = null, string? clientId = null)
    {
        _http = http ?? new HttpClient();
        _clientId = string.IsNullOrWhiteSpace(clientId) ? DefaultClientId : clientId;

        if (!_http.DefaultRequestHeaders.UserAgent.TryParseAdd(VesperInfo.UserAgent))
            _http.DefaultRequestHeaders.Add("User-Agent", VesperInfo.UserAgent);
    }

    public async Task<DeviceCodeInfo> RequestDeviceCodeAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsync(
            DeviceCodeEndpoint,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = _clientId,
                ["scope"] = XboxLiveScope,
                ["response_type"] = "device_code",
            }),
            cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new MicrosoftAuthException("Device code request failed: " + body);

        var parsed = JsonSerializer.Deserialize<DeviceCodeResponse>(body)
                     ?? throw new MicrosoftAuthException("Device code response was empty");

        return new DeviceCodeInfo(
            parsed.DeviceCode,
            parsed.UserCode,
            parsed.VerificationUri,
            TimeSpan.FromSeconds(Math.Max(1, parsed.Interval)),
            DateTimeOffset.UtcNow.AddSeconds(parsed.ExpiresIn));
    }

    public async Task<MicrosoftAuthResult> PollForTokenAsync(
        DeviceCodeInfo code,
        CancellationToken cancellationToken = default)
    {
        var interval = code.Interval;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (DateTimeOffset.UtcNow >= code.ExpiresAt)
                throw new MicrosoftAuthException("The sign-in code expired before it was approved", "expired_token");

            await Task.Delay(interval, cancellationToken);

            var token = await PostTokenAsync(new Dictionary<string, string>
            {
                ["client_id"] = _clientId,
                ["device_code"] = code.DeviceCode,
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
            }, cancellationToken);

            switch (token.Error)
            {
                case null or "":
                    return await CompleteChainAsync(token, cancellationToken);
                case "authorization_pending":
                    continue;
                case "slow_down":
                    interval += TimeSpan.FromSeconds(5);
                    continue;
                default:
                    throw new MicrosoftAuthException(
                        token.ErrorDescription ?? token.Error, token.Error);
            }
        }
    }

    public async Task<MicrosoftAuthResult> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        var token = await PostTokenAsync(new Dictionary<string, string>
        {
            ["client_id"] = _clientId,
            ["refresh_token"] = refreshToken,
            ["grant_type"] = "refresh_token",
            ["scope"] = XboxLiveScope,
        }, cancellationToken);

        if (!string.IsNullOrEmpty(token.Error))
            throw new MicrosoftAuthException(token.ErrorDescription ?? token.Error, token.Error);

        return await CompleteChainAsync(token, cancellationToken);
    }

    public async Task<MinecraftProfileResponse> GetProfileAsync(
        string minecraftAccessToken,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, MinecraftProfileEndpoint);
        request.Headers.Authorization = new("Bearer", minecraftAccessToken);

        using var response = await _http.SendAsync(request, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            throw new MicrosoftAuthException(
                "This Microsoft account does not own Minecraft Java Edition", "no_profile");

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<MinecraftProfileResponse>(cancellationToken)
               ?? throw new MicrosoftAuthException("Minecraft profile response was empty");
    }

    private async Task<TokenResponse> PostTokenAsync(
        Dictionary<string, string> form,
        CancellationToken cancellationToken)
    {
        using var response = await _http.PostAsync(
            TokenEndpoint, new FormUrlEncodedContent(form), cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        return JsonSerializer.Deserialize<TokenResponse>(body)
               ?? throw new MicrosoftAuthException("Token response was empty");
    }

    private async Task<MicrosoftAuthResult> CompleteChainAsync(
        TokenResponse token,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(token.AccessToken))
            throw new MicrosoftAuthException("Microsoft did not return an access token");

        var (xblToken, userHash) = await AuthenticateXboxLiveAsync(token.AccessToken, cancellationToken);
        var xstsToken = await AuthorizeXstsAsync(xblToken, cancellationToken);
        var minecraft = await LoginWithXboxAsync(userHash, xstsToken, cancellationToken);
        var profile = await GetProfileAsync(minecraft.AccessToken, cancellationToken);

        return new MicrosoftAuthResult(
            profile.Name,
            profile.Id,
            minecraft.AccessToken,
            token.RefreshToken ?? string.Empty,
            DateTimeOffset.UtcNow.AddSeconds(minecraft.ExpiresIn));
    }

    private async Task<(string Token, string UserHash)> AuthenticateXboxLiveAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        var payload = new
        {
            Properties = new
            {
                AuthMethod = "RPS",
                SiteName = "user.auth.xboxlive.com",
                RpsTicket = accessToken,
            },
            RelyingParty = "http://auth.xboxlive.com",
            TokenType = "JWT",
        };

        var result = await PostXboxAsync(XboxAuthEndpoint, payload, cancellationToken);
        return (result.Token, ExtractUserHash(result));
    }

    private async Task<string> AuthorizeXstsAsync(string xblToken, CancellationToken cancellationToken)
    {
        var payload = new
        {
            Properties = new
            {
                SandboxId = "RETAIL",
                UserTokens = new[] { xblToken },
            },
            RelyingParty = "rp://api.minecraftservices.com/",
            TokenType = "JWT",
        };

        var result = await PostXboxAsync(XstsEndpoint, payload, cancellationToken);
        return result.Token;
    }

    private async Task<XboxAuthResponse> PostXboxAsync(
        string endpoint,
        object payload,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(payload),
        };
        request.Headers.Accept.Add(new("application/json"));

        using var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        var parsed = JsonSerializer.Deserialize<XboxAuthResponse>(body);

        if (!response.IsSuccessStatusCode)
            throw new MicrosoftAuthException(DescribeXboxError(parsed?.XErr ?? 0, body));

        if (parsed is null || string.IsNullOrEmpty(parsed.Token))
            throw new MicrosoftAuthException("Xbox Live did not return a token");

        return parsed;
    }

    private async Task<MinecraftLoginResponse> LoginWithXboxAsync(
        string userHash,
        string xstsToken,
        CancellationToken cancellationToken)
    {
        using var response = await _http.PostAsJsonAsync(
            MinecraftLoginEndpoint,
            new { identityToken = $"XBL3.0 x={userHash};{xstsToken}" },
            cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new MicrosoftAuthException(
                "Minecraft Services rejected the Xbox token: " +
                await response.Content.ReadAsStringAsync(cancellationToken));

        return await response.Content.ReadFromJsonAsync<MinecraftLoginResponse>(cancellationToken)
               ?? throw new MicrosoftAuthException("Minecraft login response was empty");
    }

    private static string ExtractUserHash(XboxAuthResponse response)
    {
        var claim = response.DisplayClaims?.Xui.FirstOrDefault();

        if (claim is null || !claim.TryGetValue("uhs", out var hash) || string.IsNullOrEmpty(hash))
            throw new MicrosoftAuthException("Xbox Live did not return a user hash");

        return hash;
    }

    private static string DescribeXboxError(long xerr, string body) => xerr switch
    {
        2148916233 => "This Microsoft account has no Xbox profile. Create one at xbox.com and try again.",
        2148916235 => "Xbox Live is not available in this account's region.",
        2148916236 or 2148916237 => "This account needs adult verification before it can sign in.",
        2148916238 => "This account is a child account and must be added to a family group.",
        _ => "Xbox Live authentication failed: " + body,
    };
}

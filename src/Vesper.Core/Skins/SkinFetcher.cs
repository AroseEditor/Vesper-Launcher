using System.Text;
using System.Text.Json;

namespace Vesper.Core.Skins;

public sealed record RemoteSkin(byte[] Png, bool Slim, string? CapeUrl, string Source);

public sealed class SkinFetcher
{
    public const string ProfileByName = "https://api.mojang.com/users/profiles/minecraft/";
    public const string SessionProfile = "https://sessionserver.mojang.com/session/minecraft/profile/";
    public const string OwnProfile = "https://api.minecraftservices.com/minecraft/profile";

    private readonly HttpClient _http;

    public SkinFetcher(HttpClient? http = null)
    {
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(20) };

        if (!_http.DefaultRequestHeaders.UserAgent.TryParseAdd(VesperInfo.UserAgent))
            _http.DefaultRequestHeaders.Add("User-Agent", VesperInfo.UserAgent);
    }

    public async Task<RemoteSkin?> FetchByUsernameAsync(
        string username,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username))
            return null;

        var uuid = await ResolveUuidAsync(username, cancellationToken);

        return uuid is null ? null : await FetchByUuidAsync(uuid, cancellationToken);
    }

    public async Task<string?> ResolveUuidAsync(
        string username,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _http.GetAsync(
                ProfileByName + Uri.EscapeDataString(username.Trim()), cancellationToken);

            if (!response.IsSuccessStatusCode)
                return null;

            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(body))
                return null;

            using var document = JsonDocument.Parse(body);

            return document.RootElement.TryGetProperty("id", out var id) ? id.GetString() : null;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
        {
            return null;
        }
    }

    public async Task<RemoteSkin?> FetchByUuidAsync(
        string uuid,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var json = await _http.GetStringAsync(
                SessionProfile + uuid.Replace("-", string.Empty), cancellationToken);

            using var document = JsonDocument.Parse(json);

            if (!document.RootElement.TryGetProperty("properties", out var properties))
                return null;

            foreach (var property in properties.EnumerateArray())
            {
                if (property.TryGetProperty("name", out var name) &&
                    name.GetString() == "textures" &&
                    property.TryGetProperty("value", out var value))
                {
                    var decoded = Encoding.UTF8.GetString(
                        Convert.FromBase64String(value.GetString() ?? string.Empty));

                    return await ReadTexturesAsync(decoded, "Mojang", cancellationToken);
                }
            }

            return null;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException
                                      or JsonException or FormatException)
        {
            return null;
        }
    }

    public async Task<RemoteSkin?> FetchForMicrosoftAsync(
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, OwnProfile);
            request.Headers.Authorization = new("Bearer", accessToken);

            using var response = await _http.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            string? skinUrl = null;
            var slim = false;

            if (root.TryGetProperty("skins", out var skins))
            {
                foreach (var skin in skins.EnumerateArray())
                {
                    if (!IsActive(skin))
                        continue;

                    skinUrl = Text(skin, "url");
                    slim = Text(skin, "variant").Equals("SLIM", StringComparison.OrdinalIgnoreCase);
                    break;
                }
            }

            string? capeUrl = null;

            if (root.TryGetProperty("capes", out var capes))
            {
                foreach (var cape in capes.EnumerateArray())
                {
                    if (IsActive(cape))
                    {
                        capeUrl = Text(cape, "url");
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(skinUrl))
                return null;

            var png = await _http.GetByteArrayAsync(skinUrl, cancellationToken);
            return new RemoteSkin(png, slim, capeUrl, "your Microsoft account");
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
        {
            return null;
        }
    }

    private async Task<RemoteSkin?> ReadTexturesAsync(
        string decoded,
        string source,
        CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(decoded);

        if (!document.RootElement.TryGetProperty("textures", out var textures))
            return null;

        if (!textures.TryGetProperty("SKIN", out var skin))
            return null;

        var url = Text(skin, "url");

        if (string.IsNullOrEmpty(url))
            return null;

        var slim = false;

        if (skin.TryGetProperty("metadata", out var metadata))
            slim = Text(metadata, "model").Equals("slim", StringComparison.OrdinalIgnoreCase);

        string? capeUrl = null;

        if (textures.TryGetProperty("CAPE", out var cape))
            capeUrl = Text(cape, "url");

        var png = await _http.GetByteArrayAsync(url, cancellationToken);
        return new RemoteSkin(png, slim, capeUrl, source);
    }

    private static bool IsActive(JsonElement element) =>
        Text(element, "state").Equals("ACTIVE", StringComparison.OrdinalIgnoreCase);

    private static string Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
}

using System.Text.Json.Serialization;

namespace Vesper.Core.Accounts.Microsoft;

public sealed record DeviceCodeInfo(
    string DeviceCode,
    string UserCode,
    string VerificationUri,
    TimeSpan Interval,
    DateTimeOffset ExpiresAt);

public sealed record MicrosoftAuthResult(
    string Username,
    string Uuid,
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt);

public sealed class MicrosoftAuthException : Exception
{
    public MicrosoftAuthException(string message, string? code = null) : base(message) => Code = code;

    public string? Code { get; }
}

internal sealed class DeviceCodeResponse
{
    [JsonPropertyName("device_code")] public string DeviceCode { get; set; } = string.Empty;
    [JsonPropertyName("user_code")] public string UserCode { get; set; } = string.Empty;
    [JsonPropertyName("verification_uri")] public string VerificationUri { get; set; } = string.Empty;
    [JsonPropertyName("interval")] public int Interval { get; set; } = 5;
    [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; } = 900;
}

internal sealed class TokenResponse
{
    [JsonPropertyName("access_token")] public string? AccessToken { get; set; }
    [JsonPropertyName("refresh_token")] public string? RefreshToken { get; set; }
    [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
    [JsonPropertyName("error")] public string? Error { get; set; }
    [JsonPropertyName("error_description")] public string? ErrorDescription { get; set; }
}

internal sealed class XboxAuthResponse
{
    [JsonPropertyName("Token")] public string Token { get; set; } = string.Empty;
    [JsonPropertyName("DisplayClaims")] public XboxDisplayClaims? DisplayClaims { get; set; }
    [JsonPropertyName("XErr")] public long XErr { get; set; }
}

internal sealed class XboxDisplayClaims
{
    [JsonPropertyName("xui")] public List<Dictionary<string, string>> Xui { get; set; } = [];
}

internal sealed class MinecraftLoginResponse
{
    [JsonPropertyName("access_token")] public string AccessToken { get; set; } = string.Empty;
    [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
}

public sealed class MinecraftProfileResponse
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("skins")] public List<MinecraftTexture> Skins { get; set; } = [];
    [JsonPropertyName("capes")] public List<MinecraftTexture> Capes { get; set; } = [];
}

public sealed class MinecraftTexture
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("state")] public string State { get; set; } = string.Empty;
    [JsonPropertyName("url")] public string Url { get; set; } = string.Empty;
    [JsonPropertyName("variant")] public string? Variant { get; set; }
    [JsonPropertyName("alias")] public string? Alias { get; set; }

    [JsonIgnore]
    public bool IsActive => State.Equals("ACTIVE", StringComparison.OrdinalIgnoreCase);
}

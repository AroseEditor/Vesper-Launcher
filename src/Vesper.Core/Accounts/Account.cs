using System.Text.Json.Serialization;

namespace Vesper.Core.Accounts;

public enum AccountKind
{
    Local,
    Microsoft,
}

public enum SkinModel
{
    Classic,
    Slim,
}

public sealed class Account
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public AccountKind Kind { get; set; } = AccountKind.Local;

    public string Username { get; set; } = string.Empty;

    public string Uuid { get; set; } = string.Empty;

    public SkinModel SkinModel { get; set; } = SkinModel.Classic;

    public bool HasCustomSkin { get; set; }

    public bool HasCustomCape { get; set; }

    public string? ProtectedRefreshToken { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? LastUsedAt { get; set; }

    [JsonIgnore]
    public string? AccessToken { get; set; }

    [JsonIgnore]
    public DateTimeOffset? AccessTokenExpiresAt { get; set; }

    [JsonIgnore]
    public bool IsLocal => Kind == AccountKind.Local;

    [JsonIgnore]
    public bool NeedsRefresh =>
        Kind == AccountKind.Microsoft &&
        (string.IsNullOrEmpty(AccessToken) ||
         AccessTokenExpiresAt is null ||
         AccessTokenExpiresAt <= DateTimeOffset.UtcNow.AddMinutes(2));

    [JsonIgnore]
    public string DashedUuid => Uuid.Length == 32
        ? string.Join('-', Uuid[..8], Uuid[8..12], Uuid[12..16], Uuid[16..20], Uuid[20..])
        : Uuid;
}

public sealed class AccountStore
{
    public List<Account> Accounts { get; set; } = [];

    public string? SelectedId { get; set; }
}

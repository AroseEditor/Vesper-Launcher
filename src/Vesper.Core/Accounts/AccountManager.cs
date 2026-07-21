using CmlLib.Core.Auth;
using Vesper.Core.Storage;

namespace Vesper.Core.Accounts;

public sealed class AccountManager
{
    private readonly VesperPaths _paths;
    private readonly ISecretProtector _protector;
    private AccountStore _store;

    public AccountManager(VesperPaths paths, ISecretProtector? protector = null)
    {
        _paths = paths;
        _protector = protector ?? SecretProtector.Create();
        _store = VesperJson.Read<AccountStore>(_paths.AccountsFile) ?? new AccountStore();
    }

    public IReadOnlyList<Account> All => _store.Accounts;

    public Account? Selected =>
        _store.Accounts.FirstOrDefault(a => a.Id == _store.SelectedId) ?? _store.Accounts.FirstOrDefault();

    public Account AddLocal(string username)
    {
        username = username.Trim();

        if (string.IsNullOrEmpty(username))
            throw new ArgumentException("Username cannot be empty", nameof(username));

        var account = new Account
        {
            Kind = AccountKind.Local,
            Username = username,
            Uuid = OfflineUuid.Hex(username),
        };

        _store.Accounts.Add(account);
        _store.SelectedId ??= account.Id;
        Save();
        return account;
    }

    public Account AddOrUpdateMicrosoft(string username, string uuid, string accessToken, string refreshToken, DateTimeOffset expiresAt)
    {
        var account = _store.Accounts.FirstOrDefault(
            a => a.Kind == AccountKind.Microsoft && a.Uuid == uuid);

        if (account is null)
        {
            account = new Account { Kind = AccountKind.Microsoft, Uuid = uuid };
            _store.Accounts.Add(account);
        }

        account.Username = username;
        account.AccessToken = accessToken;
        account.AccessTokenExpiresAt = expiresAt;
        account.ProtectedRefreshToken = _protector.Protect(refreshToken);

        _store.SelectedId ??= account.Id;
        Save();
        return account;
    }

    public string? GetRefreshToken(Account account) =>
        string.IsNullOrEmpty(account.ProtectedRefreshToken)
            ? null
            : _protector.Unprotect(account.ProtectedRefreshToken);

    public void Select(string id)
    {
        if (_store.Accounts.All(a => a.Id != id))
            throw new ArgumentException("Unknown account " + id, nameof(id));

        _store.SelectedId = id;
        Save();
    }

    public void Remove(string id)
    {
        _store.Accounts.RemoveAll(a => a.Id == id);

        if (_store.SelectedId == id)
            _store.SelectedId = _store.Accounts.FirstOrDefault()?.Id;

        var skinDir = _paths.SkinDir(id);
        if (Directory.Exists(skinDir))
            Directory.Delete(skinDir, recursive: true);

        Save();
    }

    public void MarkUsed(Account account)
    {
        account.LastUsedAt = DateTimeOffset.UtcNow;
        Save();
    }

    public void Save() => VesperJson.Write(_paths.AccountsFile, _store);

    public void Reload() =>
        _store = VesperJson.Read<AccountStore>(_paths.AccountsFile) ?? new AccountStore();

    public static MSession CreateSession(Account account) => account.Kind switch
    {
        AccountKind.Local => new MSession
        {
            Username = account.Username,
            UUID = account.Uuid,
            AccessToken = "0",
            UserType = "legacy",
        },
        AccountKind.Microsoft => new MSession
        {
            Username = account.Username,
            UUID = account.Uuid,
            AccessToken = account.AccessToken,
            UserType = "msa",
        },
        _ => throw new ArgumentOutOfRangeException(nameof(account)),
    };
}

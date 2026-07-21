using Vesper.Core.Accounts;
using Xunit;

namespace Vesper.Core.Tests;

public class AccountManagerTests
{
    [Fact]
    public void LocalAccountNeedsNoMicrosoftAccount()
    {
        using var root = new TempRoot();
        var manager = new AccountManager(root.Paths, new UnencryptedSecretProtector());

        var account = manager.AddLocal("Steve");

        Assert.Equal(AccountKind.Local, account.Kind);
        Assert.Equal("Steve", account.Username);
        Assert.Equal(OfflineUuid.Hex("Steve"), account.Uuid);
        Assert.Single(manager.All);
    }

    [Fact]
    public void FirstAccountBecomesSelected()
    {
        using var root = new TempRoot();
        var manager = new AccountManager(root.Paths, new UnencryptedSecretProtector());

        var first = manager.AddLocal("First");
        manager.AddLocal("Second");

        Assert.Equal(first.Id, manager.Selected?.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void LocalAccountRejectsEmptyUsername(string username)
    {
        using var root = new TempRoot();
        var manager = new AccountManager(root.Paths, new UnencryptedSecretProtector());

        Assert.Throws<ArgumentException>(() => manager.AddLocal(username));
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("a_very_long_name_beyond_sixteen")]
    [InlineData("weird name!")]
    public void LocalAccountAppliesNoNameRestrictions(string username)
    {
        using var root = new TempRoot();
        var manager = new AccountManager(root.Paths, new UnencryptedSecretProtector());

        var account = manager.AddLocal(username);

        Assert.Equal(username, account.Username);
    }

    [Fact]
    public void AccountsPersistAcrossReload()
    {
        using var root = new TempRoot();

        var first = new AccountManager(root.Paths, new UnencryptedSecretProtector());
        first.AddLocal("Persisted");

        var second = new AccountManager(root.Paths, new UnencryptedSecretProtector());

        Assert.Single(second.All);
        Assert.Equal("Persisted", second.All[0].Username);
    }

    [Fact]
    public void RemoveClearsSelectionAndFallsBack()
    {
        using var root = new TempRoot();
        var manager = new AccountManager(root.Paths, new UnencryptedSecretProtector());

        var first = manager.AddLocal("First");
        var second = manager.AddLocal("Second");
        manager.Remove(first.Id);

        Assert.Equal(second.Id, manager.Selected?.Id);
        Assert.Single(manager.All);
    }

    [Fact]
    public void MicrosoftAccountRoundTripsRefreshToken()
    {
        using var root = new TempRoot();
        var manager = new AccountManager(root.Paths, new UnencryptedSecretProtector());

        var account = manager.AddOrUpdateMicrosoft(
            "MsUser", new string('a', 32), "access", "refresh-secret", DateTimeOffset.UtcNow.AddHours(1));

        Assert.Equal("refresh-secret", manager.GetRefreshToken(account));
        Assert.NotEqual("refresh-secret", account.ProtectedRefreshToken);
    }

    [Fact]
    public void MicrosoftSignInUpdatesInsteadOfDuplicating()
    {
        using var root = new TempRoot();
        var manager = new AccountManager(root.Paths, new UnencryptedSecretProtector());
        var uuid = new string('b', 32);

        manager.AddOrUpdateMicrosoft("OldName", uuid, "a1", "r1", DateTimeOffset.UtcNow.AddHours(1));
        manager.AddOrUpdateMicrosoft("NewName", uuid, "a2", "r2", DateTimeOffset.UtcNow.AddHours(1));

        Assert.Single(manager.All);
        Assert.Equal("NewName", manager.All[0].Username);
    }

    [Fact]
    public void LocalSessionIsValidForLaunching()
    {
        using var root = new TempRoot();
        var manager = new AccountManager(root.Paths, new UnencryptedSecretProtector());

        var session = AccountManager.CreateSession(manager.AddLocal("Steve"));

        Assert.True(session.CheckIsValid());
        Assert.Equal("Steve", session.Username);
    }

    [Fact]
    public void ExpiredMicrosoftTokenNeedsRefresh()
    {
        var account = new Account
        {
            Kind = AccountKind.Microsoft,
            AccessToken = "token",
            AccessTokenExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1),
        };

        Assert.True(account.NeedsRefresh);
    }

    [Fact]
    public void LocalAccountNeverNeedsRefresh()
    {
        using var root = new TempRoot();
        var manager = new AccountManager(root.Paths, new UnencryptedSecretProtector());

        Assert.False(manager.AddLocal("Steve").NeedsRefresh);
    }

    [Fact]
    public void DashedUuidIsFormattedForApis()
    {
        var account = new Account { Uuid = OfflineUuid.Hex("Notch") };

        Assert.Equal(OfflineUuid.Dashed("Notch"), account.DashedUuid);
    }
}

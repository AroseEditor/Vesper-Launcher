using Vesper.Core.Accounts;
using Xunit;

namespace Vesper.Core.Tests;

public class OfflineUuidTests
{
    [Theory]
    [InlineData("Notch", "b50ad385-829d-3141-a216-7e7d7539ba7f")]
    [InlineData("Steve", "5627dd98-e6be-3c21-b8a8-e92344183641")]
    [InlineData("VesperTester", "d29998c7-83b7-340a-b5d8-32d469aec177")]
    public void MatchesJavaNameUuidFromBytes(string username, string expected) =>
        Assert.Equal(expected, OfflineUuid.Dashed(username));

    [Fact]
    public void HexHasNoDashesAndIsThirtyTwoChars()
    {
        var hex = OfflineUuid.Hex("Notch");
        Assert.Equal(32, hex.Length);
        Assert.DoesNotContain('-', hex);
    }

    [Fact]
    public void IsDeterministic() =>
        Assert.Equal(OfflineUuid.Hex("Repeatable"), OfflineUuid.Hex("Repeatable"));

    [Fact]
    public void IsCaseSensitive() =>
        Assert.NotEqual(OfflineUuid.Hex("Notch"), OfflineUuid.Hex("notch"));

    [Fact]
    public void UsesVersionThreeAndIetfVariant()
    {
        var hex = OfflineUuid.Hex("Notch");
        Assert.Equal('3', hex[12]);
        Assert.Contains(hex[16], "89ab");
    }
}

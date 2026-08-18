using Vesper.Core.Servers;
using Xunit;

namespace Vesper.Core.Tests;

public class PlayitTunnelTests
{
    [Fact]
    public void AssetNameTargetsTheCurrentPlatform()
    {
        var name = PlayitTunnel.AssetName();

        Assert.StartsWith("playit-", name);

        if (OperatingSystem.IsWindows())
            Assert.EndsWith(".exe", name);
    }

    [Theory]
    [InlineData("your-server.craft.playit.gg", "your-server.craft.playit.gg")]
    [InlineData("tunnel address: cool-cave.joinmc.link", "cool-cave.joinmc.link")]
    [InlineData("listening on 147.185.221.20:25565 now", "147.185.221.20:25565")]
    [InlineData("host abc.craft.playit.gg:26140 ready", "abc.craft.playit.gg:26140")]
    public void ParseAddressExtractsTunnelHosts(string line, string expected) =>
        Assert.Equal(expected, PlayitTunnel.ParseAddress(line));

    [Theory]
    [InlineData("just some log line")]
    [InlineData("connecting to control server")]
    public void ParseAddressReturnsNullForNoise(string line) =>
        Assert.Null(PlayitTunnel.ParseAddress(line));
}

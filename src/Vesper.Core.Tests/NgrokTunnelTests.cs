using Vesper.Core.Servers;
using Xunit;

namespace Vesper.Core.Tests;

public class NgrokTunnelTests
{
    [Fact]
    public void ParsePublicAddressStripsTcpScheme()
    {
        var json = """
        {"tunnels":[{"name":"command_line","public_url":"tcp://6.tcp.ngrok.io:14284","proto":"tcp"}]}
        """;

        Assert.Equal("6.tcp.ngrok.io:14284", NgrokTunnel.ParsePublicAddress(json));
    }

    [Fact]
    public void ParsePublicAddressReturnsNullWhenNoTunnels()
    {
        Assert.Null(NgrokTunnel.ParsePublicAddress("""{"tunnels":[]}"""));
    }

    [Fact]
    public void ParsePublicAddressReturnsNullForMalformed()
    {
        Assert.Null(NgrokTunnel.ParsePublicAddress("""{"other":true}"""));
    }

    [Fact]
    public void DownloadUrlTargetsThePlatform()
    {
        var url = NgrokTunnel.DownloadUrl();

        Assert.StartsWith("https://bin.equinox.io/", url);

        if (OperatingSystem.IsWindows())
            Assert.EndsWith(".zip", url);
    }
}

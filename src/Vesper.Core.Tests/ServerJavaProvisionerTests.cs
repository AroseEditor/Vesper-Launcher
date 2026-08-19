using Vesper.Core.Servers;
using Xunit;

namespace Vesper.Core.Tests;

public class ServerJavaProvisionerTests
{
    [Theory]
    [InlineData("1.16.5", 8)]
    [InlineData("1.12.2", 8)]
    [InlineData("1.8.9", 8)]
    [InlineData("1.17", 17)]
    [InlineData("1.17.1", 17)]
    [InlineData("1.18.2", 17)]
    [InlineData("1.19", 17)]
    [InlineData("1.20.4", 17)]
    [InlineData("1.20.5", 21)]
    [InlineData("1.20.6", 21)]
    [InlineData("1.21", 21)]
    [InlineData("1.21.4", 21)]
    public void RequiredMajor_maps_version_to_java(string version, int expected)
    {
        Assert.Equal(expected, ServerJavaProvisioner.RequiredMajor(version));
    }

    [Fact]
    public void DownloadUrl_targets_adoptium_jre()
    {
        var url = ServerJavaProvisioner.DownloadUrl(17);

        Assert.StartsWith("https://api.adoptium.net/v3/binary/latest/17/ga/", url);
        Assert.EndsWith("/jre/hotspot/normal/eclipse", url);
    }
}

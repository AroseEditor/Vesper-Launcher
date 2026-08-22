using Vesper.Core.Loaders;
using Vesper.Core.Profiles;
using Xunit;

namespace Vesper.Core.Tests;

public class EnhancedClientTests
{
    [Fact]
    public void Supports_matches_the_supported_versions()
    {
        Assert.True(EnhancedClient.Supports("1.21.1"));
        Assert.False(EnhancedClient.Supports("1.19.4"));
        Assert.False(EnhancedClient.Supports("1.8.9"));
    }

    [Fact]
    public void Bases_are_the_three_real_loaders()
    {
        Assert.Contains(LoaderKind.Fabric, EnhancedClient.Bases);
        Assert.Contains(LoaderKind.Forge, EnhancedClient.Bases);
        Assert.Contains(LoaderKind.NeoForge, EnhancedClient.Bases);
    }
}

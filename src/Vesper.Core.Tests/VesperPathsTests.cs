using Vesper.Core.Storage;
using Xunit;

namespace Vesper.Core.Tests;

public class VesperPathsTests
{
    [Fact]
    public void EnsureCreatedMakesEveryDirectory()
    {
        using var root = new TempRoot();

        Assert.True(Directory.Exists(root.Paths.SharedAssetsDir));
        Assert.True(Directory.Exists(root.Paths.SharedLibrariesDir));
        Assert.True(Directory.Exists(root.Paths.SharedVersionsDir));
        Assert.True(Directory.Exists(root.Paths.InstancesDir));
        Assert.True(Directory.Exists(root.Paths.ServersDir));
        Assert.True(Directory.Exists(root.Paths.RuntimeDir));
        Assert.True(Directory.Exists(root.Paths.ThemesDir));
    }

    [Fact]
    public void InstancePathsNestUnderTheInstanceDirectory()
    {
        using var root = new TempRoot();

        var dir = root.Paths.InstanceDir("demo");
        Assert.StartsWith(dir, root.Paths.InstanceGameDir("demo"));
        Assert.StartsWith(dir, root.Paths.InstanceFile("demo"));
        Assert.EndsWith(".minecraft", root.Paths.InstanceGameDir("demo"));
    }

    [Fact]
    public void MinecraftPathSharesLibrariesAssetsAndVersions()
    {
        using var root = new TempRoot();
        var path = new VesperMinecraftPath(root.Paths, "demo");

        Assert.Equal(root.Paths.SharedLibrariesDir, path.Library);
        Assert.Equal(root.Paths.SharedVersionsDir, path.Versions);
        Assert.Equal(root.Paths.SharedAssetsDir, path.Assets);
        Assert.Equal(root.Paths.RuntimeDir, path.Runtime);
    }

    [Fact]
    public void MinecraftPathKeepsGameDirectoryPerInstance()
    {
        using var root = new TempRoot();

        var first = new VesperMinecraftPath(root.Paths, "one");
        var second = new VesperMinecraftPath(root.Paths, "two");

        Assert.NotEqual(first.BasePath, second.BasePath);
        Assert.EndsWith(".minecraft", first.BasePath);
    }

    [Fact]
    public void NativesAreIsolatedPerInstance()
    {
        using var root = new TempRoot();

        var first = new VesperMinecraftPath(root.Paths, "one").GetNativePath("1.21.1");
        var second = new VesperMinecraftPath(root.Paths, "two").GetNativePath("1.21.1");

        Assert.NotEqual(first, second);
        Assert.DoesNotContain(root.Paths.SharedVersionsDir, first);
    }

    [Fact]
    public void NothingResolvesIntoTheDefaultMinecraftFolder()
    {
        using var root = new TempRoot();
        var path = new VesperMinecraftPath(root.Paths, "demo");

        foreach (var candidate in new[] { path.BasePath, path.Library, path.Versions, path.Assets })
            Assert.StartsWith(root.Paths.Root, candidate);
    }
}

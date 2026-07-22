using Vesper.Core.Launching;
using Vesper.Core.Loaders;
using Vesper.Core.Profiles;
using Xunit;

namespace Vesper.Core.Tests;

public class JvmTuningTests
{
    [Fact]
    public void RecommendedAlwaysSelectsG1()
    {
        var flags = JvmTuning.Recommended(4096);

        Assert.Contains("-XX:+UseG1GC", flags);
        Assert.Contains("-XX:+UnlockExperimentalVMOptions", flags);
    }

    [Fact]
    public void LargeHeapsGetLargerRegions()
    {
        var small = JvmTuning.Recommended(4096);
        var large = JvmTuning.Recommended(8192);

        Assert.Contains("-XX:G1HeapRegionSize=16M", small);
        Assert.Contains("-XX:G1HeapRegionSize=32M", large);
    }

    [Fact]
    public void UserArgumentsWinOverTunedDefaults()
    {
        var merged = JvmTuning.Merge(
            JvmTuning.Recommended(4096),
            ["-XX:MaxGCPauseMillis=200"]);

        Assert.Contains("-XX:MaxGCPauseMillis=200", merged);
        Assert.DoesNotContain("-XX:MaxGCPauseMillis=37", merged);
    }

    [Fact]
    public void UserCanDisableATunedToggle()
    {
        var merged = JvmTuning.Merge(
            JvmTuning.Recommended(4096),
            ["-XX:-AlwaysPreTouch"]);

        Assert.Contains("-XX:-AlwaysPreTouch", merged);
        Assert.DoesNotContain("-XX:+AlwaysPreTouch", merged);
    }

    [Fact]
    public void UnrelatedUserArgumentsAreKept()
    {
        var merged = JvmTuning.Merge(JvmTuning.Recommended(4096), ["-Dsomething=1"]);

        Assert.Contains("-Dsomething=1", merged);
        Assert.Contains("-XX:+UseG1GC", merged);
    }

    [Fact]
    public void NoDuplicateFlagsAreProduced()
    {
        var merged = JvmTuning.Merge(JvmTuning.Recommended(8192), ["-XX:+UseG1GC"]);

        Assert.Single(merged, f => f == "-XX:+UseG1GC");
    }

    [Theory]
    [InlineData(LoaderKind.Fabric, "fabric")]
    [InlineData(LoaderKind.NeoForge, "neoforge")]
    [InlineData(LoaderKind.Quilt, "quilt")]
    public void LoaderSlugsMatchModrinth(LoaderKind loader, string expected) =>
        Assert.Equal(expected, VesperProfileInstaller.LoaderSlug(loader));

    [Fact]
    public void BundleAlwaysIncludesTheApisTheModNeeds()
    {
        var slugs = VesperProfileInstaller.RequiredMods.Select(m => m.Slug).ToList();

        Assert.Contains("fabric-api", slugs);
        Assert.Contains("architectury-api", slugs);
        Assert.All(VesperProfileInstaller.RequiredMods, m => Assert.True(m.Required));
    }

    [Fact]
    public void PerformanceBundleIncludesSodiumAndLithium()
    {
        var slugs = VesperProfileInstaller.PerformanceMods.Select(m => m.Slug).ToList();

        Assert.Contains("sodium", slugs);
        Assert.Contains("lithium", slugs);
        Assert.Contains("ferrite-core", slugs);
    }

    [Fact]
    public void AlreadyInstalledDetectsAJarRegardlessOfNaming()
    {
        using var root = new TempRoot();
        var mods = Path.Combine(root.Directory, "mods");
        Directory.CreateDirectory(mods);
        File.WriteAllText(Path.Combine(mods, "ferrite_core-7.0.0-fabric.jar"), string.Empty);

        Assert.True(VesperProfileInstaller.IsAlreadyInstalled(mods, "ferrite-core"));
        Assert.False(VesperProfileInstaller.IsAlreadyInstalled(mods, "sodium"));
    }

    [Fact]
    public void MissingModsDirectoryIsNotAnError()
    {
        using var root = new TempRoot();

        Assert.False(VesperProfileInstaller.IsAlreadyInstalled(
            Path.Combine(root.Directory, "nope"), "sodium"));
    }
}

using Vesper.Core.Instances;
using Vesper.Core.Loaders;
using Xunit;

namespace Vesper.Core.Tests;

public class LoaderInstallerTests
{
    [Theory]
    [InlineData("0.16.9", true)]
    [InlineData("0.19.3", true)]
    [InlineData("0.20.0-beta.9", false)]
    [InlineData("1.0.0-rc.1", false)]
    [InlineData("0.5.0-alpha", false)]
    public void StabilityIsInferredWhenTheApiOmitsIt(string version, bool expected) =>
        Assert.Equal(expected, FabricLikeInstaller.IsStableVersionString(version));

    [Fact]
    public void FabricAndQuiltReportTheirOwnKinds()
    {
        using var root = new TempRoot();

        Assert.Equal(LoaderKind.Fabric, new FabricInstaller(root.Paths).Kind);
        Assert.Equal(LoaderKind.Quilt, new QuiltInstaller(root.Paths).Kind);
    }

    [Theory]
    [InlineData("0.16.9", false, "0.16.9 (beta)")]
    [InlineData("0.16.9", true, "0.16.9")]
    public void LoaderVersionLabelsMarkUnstableBuilds(string version, bool stable, string expected) =>
        Assert.Equal(expected, new LoaderVersion(version, stable).Label);
}

using Vesper.Core.Profiles;
using Xunit;

namespace Vesper.Core.Tests;

public class ProfileManagerTests
{
    [Theory]
    [InlineData("Vesper 1.21.1", "vesper-1-21-1")]
    [InlineData("  Spaced  Out  ", "spaced-out")]
    [InlineData("!!!", "profile")]
    [InlineData("Fabric/Forge", "fabric-forge")]
    public void SlugifyProducesCleanIds(string input, string expected) =>
        Assert.Equal(expected, ProfileManager.Slugify(input));

    [Fact]
    public void CreateRoundTripsThroughDisk()
    {
        using var root = new TempRoot();
        var manager = new ProfileManager(root.Paths);

        var created = manager.Create("My Pack", "1.21.1", LoaderKind.Fabric, "0.16.9");
        var loaded = manager.Load(created.Id);

        Assert.NotNull(loaded);
        Assert.Equal("My Pack", loaded.Name);
        Assert.Equal("1.21.1", loaded.MinecraftVersion);
        Assert.Equal(LoaderKind.Fabric, loaded.Loader);
        Assert.Equal("0.16.9", loaded.LoaderVersion);
    }

    [Fact]
    public void CreateAllocatesUniqueIdsForDuplicateNames()
    {
        using var root = new TempRoot();
        var manager = new ProfileManager(root.Paths);

        var first = manager.Create("Same Name", "1.21.1");
        var second = manager.Create("Same Name", "1.21.1");

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(2, manager.LoadAll().Count);
    }

    [Fact]
    public void CreateMakesGameAndModsDirectories()
    {
        using var root = new TempRoot();
        var manager = new ProfileManager(root.Paths);

        var profile = manager.Create("Dirs", "1.21.1");

        Assert.True(Directory.Exists(root.Paths.ProfileGameDir(profile.Id)));
        Assert.True(Directory.Exists(root.Paths.ProfileModsDir(profile.Id)));
    }

    [Fact]
    public void VesperProfileRequiresFabricOrForge()
    {
        using var root = new TempRoot();
        var manager = new ProfileManager(root.Paths);

        Assert.Throws<ArgumentException>(() =>
            manager.Create("Bad", "1.21.1", LoaderKind.Quilt, isVesperProfile: true));
    }

    [Fact]
    public void DeleteRemovesInstance()
    {
        using var root = new TempRoot();
        var manager = new ProfileManager(root.Paths);

        var profile = manager.Create("Doomed", "1.21.1");
        manager.Delete(profile.Id);

        Assert.Null(manager.Load(profile.Id));
        Assert.Empty(manager.LoadAll());
    }

    [Theory]
    [InlineData(LoaderKind.Vanilla, false, "Vanilla")]
    [InlineData(LoaderKind.Fabric, false, "Vanilla + Fabric")]
    [InlineData(LoaderKind.Forge, true, "Vesper + Forge")]
    [InlineData(LoaderKind.NeoForge, false, "Vanilla + NeoForge")]
    public void ProfileLabelDescribesTheStack(LoaderKind loader, bool vesper, string expected)
    {
        var profile = new Profile { Loader = loader, IsVesperProfile = vesper };
        Assert.Equal(expected, profile.ProfileLabel);
    }
}

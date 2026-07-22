using Vesper.Core.Profiles;
using Xunit;

namespace Vesper.Core.Tests;

public class ControlsSyncTests
{
    private static string WriteOptions(string directory, params string[] lines)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "options.txt");
        File.WriteAllLines(path, lines);
        return path;
    }

    [Theory]
    [InlineData("key_key.forward", true)]
    [InlineData("key_key.hotbar.5", true)]
    [InlineData("mouseSensitivity", true)]
    [InlineData("toggleSprint", true)]
    [InlineData("key_key.vesper.menu", false)]
    [InlineData("key_key.sodium.options", false)]
    [InlineData("renderDistance", false)]
    public void OnlyVanillaControlsAreSynced(string option, bool expected) =>
        Assert.Equal(expected, ControlsSync.IsSynced(option));

    [Theory]
    [InlineData("key_key.vesper.menu")]
    [InlineData("key_key.jei.showRecipe")]
    public void ModBindingsAreRecognised(string option) =>
        Assert.True(ControlsSync.IsModBinding(option));

    [Fact]
    public void VanillaBindingsAreNotTreatedAsModBindings() =>
        Assert.False(ControlsSync.IsModBinding("key_key.attack"));

    [Fact]
    public void CaptureThenApplyMovesBindingsBetweenProfiles()
    {
        using var root = new TempRoot();
        var sync = new ControlsSync(root.Paths);

        var source = WriteOptions(
            root.Paths.ProfileGameDir("one"),
            "key_key.forward:key.keyboard.i",
            "key_key.jump:key.keyboard.space",
            "mouseSensitivity:0.75");

        sync.CaptureFrom(source);

        var target = WriteOptions(
            root.Paths.ProfileGameDir("two"),
            "key_key.forward:key.keyboard.w",
            "mouseSensitivity:0.5");

        sync.ApplyTo(target);

        var result = File.ReadAllLines(target);
        Assert.Contains("key_key.forward:key.keyboard.i", result);
        Assert.Contains("mouseSensitivity:0.75", result);
        Assert.Contains("key_key.jump:key.keyboard.space", result);
    }

    [Fact]
    public void ApplyingNeverTouchesModBindingsInTheTarget()
    {
        using var root = new TempRoot();
        var sync = new ControlsSync(root.Paths);

        var source = WriteOptions(
            root.Paths.ProfileGameDir("one"),
            "key_key.forward:key.keyboard.i",
            "key_key.vesper.menu:key.keyboard.left.shift");

        sync.CaptureFrom(source);

        var target = WriteOptions(
            root.Paths.ProfileGameDir("two"),
            "key_key.forward:key.keyboard.w",
            "key_key.vesper.menu:key.keyboard.right.shift",
            "key_key.sodium.options:key.keyboard.o");

        sync.ApplyTo(target);

        var result = File.ReadAllLines(target);
        Assert.Contains("key_key.vesper.menu:key.keyboard.right.shift", result);
        Assert.Contains("key_key.sodium.options:key.keyboard.o", result);
        Assert.DoesNotContain("key_key.vesper.menu:key.keyboard.left.shift", result);
    }

    [Fact]
    public void ModBindingsAreNeverWrittenToTheMasterFile()
    {
        using var root = new TempRoot();
        var sync = new ControlsSync(root.Paths);

        var source = WriteOptions(
            root.Paths.ProfileGameDir("one"),
            "key_key.attack:key.mouse.left",
            "key_key.vesper.menu:key.keyboard.right.shift");

        sync.CaptureFrom(source);

        var master = File.ReadAllText(sync.MasterFile);
        Assert.Contains("key_key.attack", master);
        Assert.DoesNotContain("vesper", master);
    }

    [Fact]
    public void UnrelatedSettingsInTheTargetSurvive()
    {
        using var root = new TempRoot();
        var sync = new ControlsSync(root.Paths);

        sync.CaptureFrom(WriteOptions(
            root.Paths.ProfileGameDir("one"), "key_key.forward:key.keyboard.i"));

        var target = WriteOptions(
            root.Paths.ProfileGameDir("two"),
            "renderDistance:16",
            "fov:0.5",
            "key_key.forward:key.keyboard.w");

        sync.ApplyTo(target);

        var result = File.ReadAllLines(target);
        Assert.Contains("renderDistance:16", result);
        Assert.Contains("fov:0.5", result);
    }

    [Fact]
    public void ApplyingCreatesOptionsWhenTheProfileHasNone()
    {
        using var root = new TempRoot();
        var sync = new ControlsSync(root.Paths);

        sync.CaptureFrom(WriteOptions(
            root.Paths.ProfileGameDir("one"), "key_key.forward:key.keyboard.i"));

        var target = sync.OptionsFileFor("fresh");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        sync.ApplyTo(target);

        Assert.True(File.Exists(target));
        Assert.Contains("key_key.forward:key.keyboard.i", File.ReadAllLines(target));
    }

    [Fact]
    public void ApplyingWithNoMasterIsANoOp()
    {
        using var root = new TempRoot();
        var sync = new ControlsSync(root.Paths);

        var target = WriteOptions(
            root.Paths.ProfileGameDir("one"), "key_key.forward:key.keyboard.w");

        Assert.Equal(0, sync.ApplyTo(target));
        Assert.Contains("key_key.forward:key.keyboard.w", File.ReadAllLines(target));
    }

    [Fact]
    public void CaptureIgnoresMissingFiles()
    {
        using var root = new TempRoot();
        var sync = new ControlsSync(root.Paths);

        Assert.Equal(0, sync.CaptureFrom(Path.Combine(root.Directory, "nope", "options.txt")));
    }

    [Fact]
    public void ValuesContainingColonsSurvive()
    {
        using var root = new TempRoot();
        var sync = new ControlsSync(root.Paths);

        sync.CaptureFrom(WriteOptions(
            root.Paths.ProfileGameDir("one"), "key_key.forward:key.keyboard.a:b"));

        Assert.Contains("key_key.forward:key.keyboard.a:b", File.ReadAllLines(sync.MasterFile));
    }
}

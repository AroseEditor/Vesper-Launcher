using Vesper.Core.Servers;
using Xunit;

namespace Vesper.Core.Tests;

public class ServerPropertiesTests
{
    [Fact]
    public void MissingKeysFallBackToSchemaDefaults()
    {
        using var root = new TempRoot();
        var path = Path.Combine(root.Directory, "server.properties");

        var properties = ServerProperties.Load(path);

        Assert.Equal("20", properties.Get("max-players"));
        Assert.Equal("survival", properties.Get("gamemode"));
    }

    [Fact]
    public void ExistingValuesAreKept()
    {
        using var root = new TempRoot();
        var path = Path.Combine(root.Directory, "server.properties");
        File.WriteAllLines(path, ["max-players=64", "motd=Hello"]);

        var properties = ServerProperties.Load(path);

        Assert.Equal("64", properties.Get("max-players"));
        Assert.Equal("Hello", properties.Get("motd"));
    }

    [Fact]
    public void UnknownKeysSurviveARoundTrip()
    {
        using var root = new TempRoot();
        var path = Path.Combine(root.Directory, "server.properties");
        File.WriteAllLines(path, ["custom-plugin-setting=yes"]);

        var properties = ServerProperties.Load(path);
        properties.Save(path);

        Assert.Equal("yes", ServerProperties.Load(path).Get("custom-plugin-setting"));
    }

    [Fact]
    public void CommentsAndBlankLinesAreIgnored()
    {
        using var root = new TempRoot();
        var path = Path.Combine(root.Directory, "server.properties");
        File.WriteAllLines(path, ["#a comment", "", "pvp=false"]);

        Assert.False(ServerProperties.Load(path).GetBool("pvp"));
    }

    [Fact]
    public void ValuesContainingEqualsAreParsedWhole()
    {
        using var root = new TempRoot();
        var path = Path.Combine(root.Directory, "server.properties");
        File.WriteAllLines(path, ["resource-pack=https://example.com/a?b=c"]);

        Assert.Equal("https://example.com/a?b=c", ServerProperties.Load(path).Get("resource-pack"));
    }

    [Fact]
    public void SetAndSavePersists()
    {
        using var root = new TempRoot();
        var path = Path.Combine(root.Directory, "server.properties");

        var properties = ServerProperties.Load(path);
        properties.Set("difficulty", "hard");
        properties.Save(path);

        Assert.Equal("hard", ServerProperties.Load(path).Get("difficulty"));
    }

    [Fact]
    public void EverySchemaEntryHasAUniqueKey()
    {
        var keys = ServerPropertySchema.All.Select(s => s.Key).ToList();

        Assert.Equal(keys.Count, keys.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void EverySchemaEntryBelongsToAKnownGroup()
    {
        foreach (var spec in ServerPropertySchema.All)
            Assert.Contains(spec.Group, ServerPropertySchema.Groups);
    }

    [Fact]
    public void ChoicePropertiesListTheirDefault()
    {
        foreach (var spec in ServerPropertySchema.All.Where(s => s.Kind == ServerPropertyKind.Choice))
        {
            Assert.NotNull(spec.Options);
            Assert.Contains(spec.Default, spec.Options!);
        }
    }

    [Fact]
    public void GetIntFallsBackWhenTheValueIsNotANumber()
    {
        using var root = new TempRoot();
        var path = Path.Combine(root.Directory, "server.properties");
        File.WriteAllLines(path, ["view-distance=abc"]);

        Assert.Equal(10, ServerProperties.Load(path).GetInt("view-distance", 10));
    }
}

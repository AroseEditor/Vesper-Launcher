using Vesper.Core.Storage;

namespace Vesper.Core.Theming;

public sealed class ThemeStore
{
    private readonly VesperPaths _paths;

    public ThemeStore(VesperPaths paths) => _paths = paths;

    public IReadOnlyList<VesperTheme> LoadAll()
    {
        var themes = VesperTheme.BuiltIn().ToList();

        if (!Directory.Exists(_paths.ThemesDir))
            return themes;

        foreach (var file in Directory.EnumerateFiles(_paths.ThemesDir, "*.json"))
        {
            var theme = TryRead(file);
            if (theme is null || string.IsNullOrWhiteSpace(theme.Name))
                continue;

            theme.IsBuiltIn = false;
            themes.RemoveAll(t => t.Name.Equals(theme.Name, StringComparison.OrdinalIgnoreCase));
            themes.Add(theme);
        }

        return themes;
    }

    public VesperTheme Find(string name) =>
        LoadAll().FirstOrDefault(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
        ?? VesperTheme.MauveBlack();

    public void Save(VesperTheme theme)
    {
        theme.IsBuiltIn = false;
        VesperJson.Write(PathFor(theme.Name), theme);
    }

    public void Delete(string name)
    {
        var path = PathFor(name);
        if (File.Exists(path))
            File.Delete(path);
    }

    private string PathFor(string name) =>
        Path.Combine(_paths.ThemesDir, Instances.InstanceManager.Slugify(name) + ".json");

    private static VesperTheme? TryRead(string file)
    {
        try
        {
            return VesperJson.Read<VesperTheme>(file);
        }
        catch (Exception e) when (e is IOException or System.Text.Json.JsonException)
        {
            return null;
        }
    }
}

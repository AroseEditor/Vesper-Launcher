namespace Vesper.Core.Storage;

public sealed class VesperPaths
{
    public const string PortableMarker = "portable.txt";
    public const string DirectoryName = "VesperLauncher";

    public VesperPaths(string root) => Root = Path.GetFullPath(root);

    public string Root { get; }

    public string SettingsFile => Path.Combine(Root, "vesper.json");
    public string AccountsFile => Path.Combine(Root, "accounts.json");
    public string ThemesDir => Path.Combine(Root, "themes");
    public string SkinsDir => Path.Combine(Root, "skins");
    public string RuntimeDir => Path.Combine(Root, "runtime");
    public string SharedDir => Path.Combine(Root, "shared");
    public string SharedAssetsDir => Path.Combine(SharedDir, "assets");
    public string SharedLibrariesDir => Path.Combine(SharedDir, "libraries");
    public string SharedVersionsDir => Path.Combine(SharedDir, "versions");
    public string ProfilesDir => Path.Combine(Root, "profiles");
    public string ServersDir => Path.Combine(Root, "servers");
    public string CacheDir => Path.Combine(Root, "cache");
    public string LogsDir => Path.Combine(Root, "logs");

    public string ProfileDir(string id) => Path.Combine(ProfilesDir, id);

    public string ProfileFile(string id) => Path.Combine(ProfileDir(id), "profile.json");

    public string ProfileGameDir(string id) => Path.Combine(ProfileDir(id), ".minecraft");

    public string ProfileModsDir(string id) => Path.Combine(ProfileGameDir(id), "mods");

    public string SkinDir(string accountId) => Path.Combine(SkinsDir, accountId);

    public string ServerDir(string id) => Path.Combine(ServersDir, id);

    public static VesperPaths Resolve()
    {
        var beside = AppContext.BaseDirectory;
        if (File.Exists(Path.Combine(beside, PortableMarker)))
            return new VesperPaths(Path.Combine(beside, "VesperData"));

        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrEmpty(local))
            local = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        return new VesperPaths(Path.Combine(local, DirectoryName));
    }

    public void EnsureCreated()
    {
        foreach (var dir in new[]
                 {
                     Root, ThemesDir, SkinsDir, RuntimeDir, SharedDir, SharedAssetsDir,
                     SharedLibrariesDir, SharedVersionsDir, ProfilesDir, ServersDir,
                     CacheDir, LogsDir,
                 })
        {
            Directory.CreateDirectory(dir);
        }
    }
}

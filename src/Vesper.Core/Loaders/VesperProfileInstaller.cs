using Vesper.Core.Mods;
using Vesper.Core.Profiles;
using Vesper.Core.Storage;

namespace Vesper.Core.Loaders;

public sealed record BundledMod(string Slug, string Name, string Reason, bool Required);

public sealed record BundleResult(
    IReadOnlyList<string> Installed,
    IReadOnlyList<string> Skipped,
    IReadOnlyList<string> Failed);

public sealed class VesperProfileInstaller
{
    public const string VesperJarName = "vesper.jar";
    public const string ModsFolder = "mods";

    private readonly VesperPaths _paths;
    private readonly ModrinthApi _modrinth;

    public VesperProfileInstaller(VesperPaths paths, ModrinthApi? modrinth = null)
    {
        _paths = paths;
        _modrinth = modrinth ?? new ModrinthApi();
    }

    public static IReadOnlyList<BundledMod> RequiredMods { get; } =
    [
        new("fabric-api", "Fabric API", "Vesper is built against it", true),
        new("architectury-api", "Architectury API", "Vesper uses it for keybinds and events", true),
    ];

    public static IReadOnlyList<BundledMod> PerformanceMods { get; } =
    [
        new("sodium", "Sodium", "Replaces the chunk renderer, the single biggest frame rate win", false),
        new("lithium", "Lithium", "Optimises game logic and tick performance", false),
        new("ferrite-core", "FerriteCore", "Cuts memory use substantially", false),
        new("modernfix", "ModernFix", "Faster startup and lower memory pressure", false),
        new("immediatelyfast", "ImmediatelyFast", "Batches immediate mode rendering, helps HUD and UI", false),
        new("entityculling", "EntityCulling", "Skips entities hidden behind blocks", false),
    ];

    public static IReadOnlyList<BundledMod> All =>
        [.. RequiredMods, .. PerformanceMods];

    public string ModsDirectory(Profile profile) => _paths.ProfileModsDir(profile.Id);

    public static string LoaderSlug(LoaderKind loader) => loader switch
    {
        LoaderKind.Fabric => "fabric",
        LoaderKind.NeoForge => "neoforge",
        LoaderKind.Forge => "forge",
        LoaderKind.Quilt => "quilt",
        _ => "fabric",
    };

    public async Task<BundleResult> InstallBundleAsync(
        Profile profile,
        bool includePerformance = true,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var installed = new List<string>();
        var skipped = new List<string>();
        var failed = new List<string>();

        var directory = ModsDirectory(profile);
        Directory.CreateDirectory(directory);

        var loader = LoaderSlug(profile.Loader);
        var wanted = includePerformance ? All : RequiredMods;

        foreach (var mod in wanted)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IsAlreadyInstalled(directory, mod.Slug))
            {
                skipped.Add(mod.Name);
                continue;
            }

            progress?.Report("Installing " + mod.Name);

            try
            {
                var result = new ModSearchResult(
                    ModSource.Modrinth, mod.Slug, mod.Name, mod.Reason, string.Empty, 0, null);

                var fileName = await _modrinth.InstallAsync(
                    result, profile.MinecraftVersion, loader, directory, cancellationToken);

                installed.Add(fileName);
            }
            catch (Exception e) when (e is ModInstallException or HttpRequestException)
            {
                failed.Add(mod.Name + ": " + e.Message);
            }
        }

        InstallVesperJar(profile, directory, progress, failed);

        return new BundleResult(installed, skipped, failed);
    }

    public static bool IsAlreadyInstalled(string modsDirectory, string slug)
    {
        if (!Directory.Exists(modsDirectory))
            return false;

        var needle = slug.Replace("-", string.Empty);

        foreach (var file in Directory.EnumerateFiles(modsDirectory, "*.jar"))
        {
            var name = Path.GetFileNameWithoutExtension(file)
                .Replace("-", string.Empty)
                .Replace("_", string.Empty);

            if (name.Contains(needle, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private void InstallVesperJar(
        Profile profile,
        string modsDirectory,
        IProgress<string>? progress,
        List<string> failed)
    {
        var source = FindBundledVesperJar(profile.Loader);

        if (source is null)
        {
            failed.Add(
                "The Vesper mod jar was not found next to the launcher. Build it with " +
                "gradle in mod/ and put it in the launcher's mods folder.");
            return;
        }

        try
        {
            progress?.Report("Installing the Vesper mod");
            RemoveExistingVesperJars(modsDirectory);
            File.Copy(source, Path.Combine(modsDirectory, VesperJarName), overwrite: true);
        }
        catch (IOException e)
        {
            failed.Add("Vesper mod: " + e.Message);
        }
    }

    public static int RemoveExistingVesperJars(string modsDirectory)
    {
        if (!Directory.Exists(modsDirectory))
            return 0;

        var removed = 0;

        foreach (var file in Directory.EnumerateFiles(modsDirectory, "*.jar"))
        {
            var name = Path.GetFileName(file);

            if (!name.StartsWith("vesper", StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                File.Delete(file);
                removed++;
            }
            catch (IOException)
            {
            }
        }

        return removed;
    }

    public static string PlatformJarName(LoaderKind loader) =>
        loader == LoaderKind.NeoForge ? "vesper-neoforge.jar" : "vesper-fabric.jar";

    public IReadOnlyList<string> BundleDirectories() =>
    [
        Path.Combine(AppContext.BaseDirectory, ModsFolder),
        Path.Combine(_paths.Root, ModsFolder),
    ];

    public string? FindBundledVesperJar(LoaderKind loader)
    {
        var platform = PlatformJarName(loader);
        var token = loader == LoaderKind.NeoForge ? "neoforge" : "fabric";

        foreach (var directory in BundleDirectories())
        {
            if (!Directory.Exists(directory))
                continue;

            var exact = Path.Combine(directory, platform);

            if (File.Exists(exact))
                return exact;

            var match = Directory
                .EnumerateFiles(directory, "vesper*.jar")
                .Where(f => Path.GetFileName(f).Contains(token, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();

            if (match is not null)
                return match;
        }

        foreach (var directory in BundleDirectories())
        {
            var fallback = Path.Combine(directory, VesperJarName);

            if (File.Exists(fallback))
                return fallback;
        }

        return null;
    }
}

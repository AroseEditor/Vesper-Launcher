using CmlLib.Core;
using Vesper.Core.Profiles;
using Vesper.Core.Storage;
using CmlForge = CmlLib.Core.Installer.Forge;
using CmlNeoForge = CmlLib.Core.Installer.NeoForge;

namespace Vesper.Core.Loaders;

public static class SharedMinecraftPath
{
    public static MinecraftPath For(VesperPaths paths)
    {
        var path = new MinecraftPath(paths.SharedDir)
        {
            Runtime = paths.RuntimeDir,
        };

        return path;
    }
}

public sealed class ForgeInstaller : ILoaderInstaller
{
    private readonly VesperPaths _paths;
    private readonly HttpClient? _http;

    public ForgeInstaller(VesperPaths paths, HttpClient? http = null)
    {
        _paths = paths;
        _http = http;
    }

    public LoaderKind Kind => LoaderKind.Forge;

    private CmlForge.ForgeInstaller Create()
    {
        var launcher = new MinecraftLauncher(SharedMinecraftPath.For(_paths));
        return _http is null
            ? new CmlForge.ForgeInstaller(launcher)
            : new CmlForge.ForgeInstaller(launcher, _http);
    }

    public async Task<IReadOnlyList<LoaderVersion>> ListVersionsAsync(
        string minecraftVersion,
        CancellationToken cancellationToken = default)
    {
        // Retry once on transient network errors before giving up.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                var versions = await Create().GetForgeVersions(minecraftVersion);

                return versions
                    .Select(v => new LoaderVersion(
                        v.ForgeVersionName,
                        v.IsRecommendedVersion || v.IsLatestVersion))
                    .ToList();
            }
            catch (Exception e) when (e is not LoaderNotSupportedException)
            {
                if (attempt == 0 && !cancellationToken.IsCancellationRequested)
                {
                    // Wait 2 seconds then retry once for transient failures.
                    await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                    continue;
                }

                throw new LoaderNotSupportedException(
                    Kind,
                    $"no builds published for Minecraft {minecraftVersion}: {e.Message}",
                    e);
            }
        }

        // Unreachable, but satisfies compiler.
        throw new LoaderNotSupportedException(Kind,
            $"no builds published for Minecraft {minecraftVersion}");
    }

    public async Task<string> InstallAsync(
        string minecraftVersion,
        string loaderVersion,
        CancellationToken cancellationToken = default,
        IProgress<string>? progress = null)
    {
        var options = new CmlForge.ForgeInstallOptions
        {
            CancellationToken = cancellationToken,
            SkipIfAlreadyInstalled = true,
        };

        if (progress is not null)
        {
            options.FileProgress = new Progress<CmlLib.Core.Installers.InstallerProgressChangedEventArgs>(
                e => progress.Report(FormatFile("Forge " + loaderVersion, e)));
            options.ByteProgress = new Progress<ByteProgress>(
                e => progress.Report(FormatBytes("Forge " + loaderVersion, e)));
            options.InstallerOutput = new Progress<string>(
                line => progress.Report("Forge processor: " + line));
        }

        return await Create().Install(minecraftVersion, loaderVersion, options);
    }

    internal static string FormatFile(
        string label, CmlLib.Core.Installers.InstallerProgressChangedEventArgs e) =>
        e.TotalTasks > 0
            ? $"{label}: {e.Name} ({e.ProgressedTasks}/{e.TotalTasks})"
            : $"{label}: {e.Name}";

    internal static string FormatBytes(string label, ByteProgress e) =>
        e.TotalBytes > 0
            ? $"{label}: downloading {e.ProgressedBytes / 1048576}/{e.TotalBytes / 1048576} MB"
            : label + ": downloading";
}

public sealed class NeoForgeInstaller : ILoaderInstaller
{
    private readonly VesperPaths _paths;
    private readonly HttpClient? _http;

    public NeoForgeInstaller(VesperPaths paths, HttpClient? http = null)
    {
        _paths = paths;
        _http = http;
    }

    public LoaderKind Kind => LoaderKind.NeoForge;

    // Note: CmlLib's NeoForgeInstaller does not expose an HttpClient overload,
    // so _http cannot be forwarded here. Kept for future compatibility.
    private CmlNeoForge.NeoForgeInstaller Create() =>
        new(new MinecraftLauncher(SharedMinecraftPath.For(_paths)));

    public async Task<IReadOnlyList<LoaderVersion>> ListVersionsAsync(
        string minecraftVersion,
        CancellationToken cancellationToken = default)
    {
        // Retry once on transient network errors before giving up.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                var versions = await Create().GetForgeVersions(minecraftVersion);

                return versions
                    .Select(v => new LoaderVersion(
                        v.VersionName,
                        FabricLikeInstaller.IsStableVersionString(v.VersionName)))
                    .ToList();
            }
            catch (Exception e) when (e is not LoaderNotSupportedException)
            {
                if (attempt == 0 && !cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                    continue;
                }

                throw new LoaderNotSupportedException(
                    Kind,
                    $"no builds published for Minecraft {minecraftVersion}: {e.Message}",
                    e);
            }
        }

        throw new LoaderNotSupportedException(Kind,
            $"no builds published for Minecraft {minecraftVersion}");
    }

    public async Task<string> InstallAsync(
        string minecraftVersion,
        string loaderVersion,
        CancellationToken cancellationToken = default,
        IProgress<string>? progress = null)
    {
        progress?.Report($"NeoForge {loaderVersion}: downloading and installing");
        return await Create().Install(minecraftVersion, loaderVersion);
    }
}


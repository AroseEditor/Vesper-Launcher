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
            throw new LoaderNotSupportedException(
                Kind, $"no builds published for Minecraft {minecraftVersion}");
        }
    }

    public async Task<string> InstallAsync(
        string minecraftVersion,
        string loaderVersion,
        CancellationToken cancellationToken = default)
    {
        var options = new CmlForge.ForgeInstallOptions
        {
            CancellationToken = cancellationToken,
            SkipIfAlreadyInstalled = true,
        };

        return await Create().Install(minecraftVersion, loaderVersion, options);
    }
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

    private CmlNeoForge.NeoForgeInstaller Create() =>
        new(new MinecraftLauncher(SharedMinecraftPath.For(_paths)));

    public async Task<IReadOnlyList<LoaderVersion>> ListVersionsAsync(
        string minecraftVersion,
        CancellationToken cancellationToken = default)
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
            throw new LoaderNotSupportedException(
                Kind, $"no builds published for Minecraft {minecraftVersion}");
        }
    }

    public async Task<string> InstallAsync(
        string minecraftVersion,
        string loaderVersion,
        CancellationToken cancellationToken = default) =>
        await Create().Install(minecraftVersion, loaderVersion);
}

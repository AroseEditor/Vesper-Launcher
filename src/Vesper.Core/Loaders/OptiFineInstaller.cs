using System.Diagnostics;
using Vesper.Core.Profiles;
using Vesper.Core.Storage;

namespace Vesper.Core.Loaders;

public sealed class OptiFineInstaller : ILoaderInstaller
{
    public const string ImportHint =
        "OptiFine cannot be downloaded automatically because its licence forbids redistribution " +
        "and it has no public API. Download the installer jar from optifine.net and import it.";

    private readonly VesperPaths _paths;

    public OptiFineInstaller(VesperPaths paths) => _paths = paths;

    public LoaderKind Kind => LoaderKind.OptiFine;

    public Task<IReadOnlyList<LoaderVersion>> ListVersionsAsync(
        string minecraftVersion,
        CancellationToken cancellationToken = default)
    {
        var installed = FindInstalledVersions(minecraftVersion)
            .Select(id => new LoaderVersion(id, true))
            .ToList();

        return Task.FromResult<IReadOnlyList<LoaderVersion>>(installed);
    }

    public Task<string> InstallAsync(
        string minecraftVersion,
        string loaderVersion,
        CancellationToken cancellationToken = default)
    {
        var existing = FindInstalledVersions(minecraftVersion)
            .FirstOrDefault(id => id == loaderVersion);

        if (existing is not null)
            return Task.FromResult(existing);

        throw new LoaderNotSupportedException(Kind, ImportHint);
    }

    public async Task<string> ImportAsync(
        string minecraftVersion,
        string installerJarPath,
        string javaPath,
        IProgress<string>? output = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(installerJarPath))
            throw new FileNotFoundException("OptiFine installer not found", installerJarPath);

        var before = FindInstalledVersions(minecraftVersion).ToHashSet();

        var info = new ProcessStartInfo
        {
            FileName = javaPath,
            WorkingDirectory = _paths.SharedDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        info.ArgumentList.Add("-jar");
        info.ArgumentList.Add(installerJarPath);
        info.ArgumentList.Add("--install");

        using var process = new Process { StartInfo = info };
        process.Start();

        var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        output?.Report(stdout);

        if (!string.IsNullOrWhiteSpace(stderr))
            output?.Report(stderr);

        var added = FindInstalledVersions(minecraftVersion).FirstOrDefault(id => !before.Contains(id));

        if (added is null)
            throw new LoaderNotSupportedException(
                Kind,
                "the installer ran but produced no version. " +
                "Make sure the jar matches Minecraft " + minecraftVersion);

        return added;
    }

    private IEnumerable<string> FindInstalledVersions(string minecraftVersion)
    {
        if (!Directory.Exists(_paths.SharedVersionsDir))
            yield break;

        foreach (var directory in Directory.EnumerateDirectories(_paths.SharedVersionsDir))
        {
            var id = Path.GetFileName(directory);

            if (id.Contains("OptiFine", StringComparison.OrdinalIgnoreCase) &&
                id.Contains(minecraftVersion, StringComparison.OrdinalIgnoreCase))
            {
                yield return id;
            }
        }
    }
}

using Vesper.Core.Profiles;
using Vesper.Core.Storage;

namespace Vesper.Core.Loaders;

public sealed class LoaderRegistry
{
    private readonly Dictionary<LoaderKind, ILoaderInstaller> _installers;

    public LoaderRegistry(VesperPaths paths, HttpClient? http = null)
    {
        _installers = new Dictionary<LoaderKind, ILoaderInstaller>
        {
            [LoaderKind.Fabric] = new FabricInstaller(paths, http),
            [LoaderKind.Quilt] = new QuiltInstaller(paths, http),
            [LoaderKind.Forge] = new ForgeInstaller(paths, http),
            [LoaderKind.NeoForge] = new NeoForgeInstaller(paths, http),
            [LoaderKind.OptiFine] = new OptiFineInstaller(paths),
        };
    }

    public bool Supports(LoaderKind kind) =>
        kind == LoaderKind.Vanilla || _installers.ContainsKey(kind);

    public ILoaderInstaller For(LoaderKind kind) =>
        _installers.TryGetValue(kind, out var installer)
            ? installer
            : throw new LoaderNotSupportedException(kind, "this loader is not supported yet");

    public IReadOnlyList<LoaderKind> Supported => [LoaderKind.Vanilla, .. _installers.Keys];
}

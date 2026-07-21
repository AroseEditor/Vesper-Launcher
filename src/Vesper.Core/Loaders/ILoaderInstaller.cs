using Vesper.Core.Profiles;

namespace Vesper.Core.Loaders;

public sealed record LoaderVersion(string Version, bool IsStable)
{
    public string Label => IsStable ? Version : Version + " (beta)";
}

public interface ILoaderInstaller
{
    LoaderKind Kind { get; }

    Task<IReadOnlyList<LoaderVersion>> ListVersionsAsync(
        string minecraftVersion,
        CancellationToken cancellationToken = default);

    Task<string> InstallAsync(
        string minecraftVersion,
        string loaderVersion,
        CancellationToken cancellationToken = default);
}

public sealed class LoaderNotSupportedException : Exception
{
    public LoaderNotSupportedException(LoaderKind kind, string reason)
        : base($"{kind.DisplayName()}: {reason}") => Kind = kind;

    public LoaderKind Kind { get; }
}

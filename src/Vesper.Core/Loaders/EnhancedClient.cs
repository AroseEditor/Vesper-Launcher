using Vesper.Core.Profiles;

namespace Vesper.Core.Loaders;

public static class EnhancedClient
{
    public static readonly IReadOnlyList<string> SupportedVersions = ["1.21.1"];

    public static bool Supports(string minecraftVersion) =>
        SupportedVersions.Contains(minecraftVersion);

    public static IReadOnlyList<LoaderKind> Bases { get; } =
        [LoaderKind.Fabric, LoaderKind.Forge, LoaderKind.NeoForge];
}

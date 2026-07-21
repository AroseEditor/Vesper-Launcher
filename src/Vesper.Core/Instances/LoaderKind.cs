namespace Vesper.Core.Instances;

public enum LoaderKind
{
    Vanilla,
    Fabric,
    Forge,
    NeoForge,
    Quilt,
    OptiFine,
}

public static class LoaderKindExtensions
{
    public static string DisplayName(this LoaderKind loader) => loader switch
    {
        LoaderKind.Vanilla => "Vanilla",
        LoaderKind.Fabric => "Fabric",
        LoaderKind.Forge => "Forge",
        LoaderKind.NeoForge => "NeoForge",
        LoaderKind.Quilt => "Quilt",
        LoaderKind.OptiFine => "OptiFine",
        _ => loader.ToString(),
    };

    public static bool SupportsVesperProfile(this LoaderKind loader) =>
        loader is LoaderKind.Fabric or LoaderKind.Forge;
}

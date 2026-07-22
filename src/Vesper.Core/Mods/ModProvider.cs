namespace Vesper.Core.Mods;

public enum ModSource
{
    Modrinth,
    CurseForge,
}

public sealed record ModSearchResult(
    ModSource Source,
    string Id,
    string Title,
    string Description,
    string Author,
    long Downloads,
    string? IconUrl)
{
    public string SourceLabel => Source == ModSource.Modrinth ? "Modrinth" : "CurseForge";

    public string DownloadsLabel => Downloads switch
    {
        >= 1_000_000 => $"{Downloads / 1_000_000d:0.#}M downloads",
        >= 1_000 => $"{Downloads / 1_000d:0.#}K downloads",
        _ => $"{Downloads} downloads",
    };
}

public sealed class ModInstallException : Exception
{
    public ModInstallException(string message) : base(message)
    {
    }
}

public interface IModProvider
{
    ModSource Source { get; }

    bool IsAvailable { get; }

    string UnavailableReason { get; }

    Task<IReadOnlyList<ModSearchResult>> SearchAsync(
        string query,
        string minecraftVersion,
        string loader,
        CancellationToken cancellationToken = default);

    Task<string> InstallAsync(
        ModSearchResult result,
        string minecraftVersion,
        string loader,
        string modsDirectory,
        CancellationToken cancellationToken = default);
}

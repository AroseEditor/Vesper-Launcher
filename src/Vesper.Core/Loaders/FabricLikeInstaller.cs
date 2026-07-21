using System.Text.Json;
using System.Text.Json.Serialization;
using Vesper.Core.Instances;
using Vesper.Core.Storage;

namespace Vesper.Core.Loaders;

public abstract class FabricLikeInstaller : ILoaderInstaller
{
    private readonly VesperPaths _paths;
    private readonly HttpClient _http;

    protected FabricLikeInstaller(VesperPaths paths, HttpClient? http = null)
    {
        _paths = paths;
        _http = http ?? new HttpClient();
    }

    public abstract LoaderKind Kind { get; }

    protected abstract string MetaRoot { get; }

    public async Task<IReadOnlyList<LoaderVersion>> ListVersionsAsync(
        string minecraftVersion,
        CancellationToken cancellationToken = default)
    {
        var url = $"{MetaRoot}/versions/loader/{Uri.EscapeDataString(minecraftVersion)}";

        using var response = await _http.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new LoaderNotSupportedException(
                Kind, $"no builds published for Minecraft {minecraftVersion}");

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var entries = JsonSerializer.Deserialize<List<LoaderMetaEntry>>(body) ?? [];

        return entries
            .Where(e => e.Loader is not null && !string.IsNullOrEmpty(e.Loader.Version))
            .Select(e => new LoaderVersion(
                e.Loader!.Version,
                e.Loader.Stable ?? IsStableVersionString(e.Loader.Version)))
            .ToList();
    }

    public static bool IsStableVersionString(string version)
    {
        foreach (var marker in new[] { "beta", "alpha", "rc", "pre", "snapshot" })
        {
            if (version.Contains(marker, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    public async Task<string> InstallAsync(
        string minecraftVersion,
        string loaderVersion,
        CancellationToken cancellationToken = default)
    {
        var url = $"{MetaRoot}/versions/loader/" +
                  $"{Uri.EscapeDataString(minecraftVersion)}/" +
                  $"{Uri.EscapeDataString(loaderVersion)}/profile/json";

        using var response = await _http.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new LoaderNotSupportedException(
                Kind, $"build {loaderVersion} is not available for Minecraft {minecraftVersion}");

        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        using var document = JsonDocument.Parse(json);

        if (!document.RootElement.TryGetProperty("id", out var idElement))
            throw new LoaderNotSupportedException(Kind, "the profile response had no version id");

        var versionId = idElement.GetString();

        if (string.IsNullOrEmpty(versionId))
            throw new LoaderNotSupportedException(Kind, "the profile response had an empty version id");

        var directory = Path.Combine(_paths.SharedVersionsDir, versionId);
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, versionId + ".json"), json, cancellationToken);

        return versionId;
    }

    private sealed class LoaderMetaEntry
    {
        [JsonPropertyName("loader")] public LoaderMeta? Loader { get; set; }
    }

    private sealed class LoaderMeta
    {
        [JsonPropertyName("version")] public string Version { get; set; } = string.Empty;
        [JsonPropertyName("stable")] public bool? Stable { get; set; }
    }
}

public sealed class FabricInstaller : FabricLikeInstaller
{
    public FabricInstaller(VesperPaths paths, HttpClient? http = null) : base(paths, http)
    {
    }

    public override LoaderKind Kind => LoaderKind.Fabric;

    protected override string MetaRoot => "https://meta.fabricmc.net/v2";
}

public sealed class QuiltInstaller : FabricLikeInstaller
{
    public QuiltInstaller(VesperPaths paths, HttpClient? http = null) : base(paths, http)
    {
    }

    public override LoaderKind Kind => LoaderKind.Quilt;

    protected override string MetaRoot => "https://meta.quiltmc.org/v3";
}

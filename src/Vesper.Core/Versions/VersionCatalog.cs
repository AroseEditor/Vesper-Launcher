using System.Text.Json;
using System.Text.Json.Serialization;
using Vesper.Core.Storage;

namespace Vesper.Core.Versions;

public sealed record MinecraftVersionInfo(string Id, string Type, DateTimeOffset ReleaseTime)
{
    public bool IsRelease => Type.Equals("release", StringComparison.OrdinalIgnoreCase);

    public bool IsSnapshot => Type.Equals("snapshot", StringComparison.OrdinalIgnoreCase);

    public bool IsAncient =>
        Type.StartsWith("old_", StringComparison.OrdinalIgnoreCase);
}

public sealed record VersionGroup(string Name, IReadOnlyList<MinecraftVersionInfo> Versions)
{
    public MinecraftVersionInfo Newest => Versions[0];

    public int Count => Versions.Count;

    public string Subtitle => Count == 1 ? "1 version" : $"{Count} versions";
}

public sealed class VersionCatalog
{
    public const string ManifestUrl =
        "https://launchermeta.mojang.com/mc/game/version_manifest_v2.json";

    public const string SnapshotGroup = "Snapshots";
    public const string AncientGroup = "Beta and Alpha";

    private readonly HttpClient _http;
    private readonly string _cacheFile;

    public VersionCatalog(VesperPaths paths, HttpClient? http = null)
    {
        _http = http ?? new HttpClient();
        _cacheFile = Path.Combine(paths.CacheDir, "version_manifest.json");
    }

    public async Task<IReadOnlyList<MinecraftVersionInfo>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        string? json = null;

        try
        {
            json = await _http.GetStringAsync(ManifestUrl, cancellationToken);
            Directory.CreateDirectory(Path.GetDirectoryName(_cacheFile)!);
            await File.WriteAllTextAsync(_cacheFile, json, cancellationToken);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or IOException)
        {
            if (File.Exists(_cacheFile))
                json = await File.ReadAllTextAsync(_cacheFile, cancellationToken);
        }

        if (string.IsNullOrEmpty(json))
            return [];

        var manifest = JsonSerializer.Deserialize<Manifest>(json);

        return manifest?.Versions?
            .Where(v => !string.IsNullOrEmpty(v.Id))
            .Select(v => new MinecraftVersionInfo(v.Id, v.Type ?? "release", v.ReleaseTime))
            .ToList() ?? [];
    }

    public static IReadOnlyList<VersionGroup> Group(IEnumerable<MinecraftVersionInfo> versions)
    {
        var groups = new Dictionary<string, List<MinecraftVersionInfo>>();

        foreach (var version in versions)
        {
            var key = GroupKey(version);

            if (!groups.TryGetValue(key, out var list))
                groups[key] = list = [];

            list.Add(version);
        }

        foreach (var list in groups.Values)
            list.Sort((a, b) => b.ReleaseTime.CompareTo(a.ReleaseTime));

        return groups
            .Select(g => new VersionGroup(g.Key, g.Value))
            .OrderByDescending(g => SortWeight(g))
            .ToList();
    }

    public static string GroupKey(MinecraftVersionInfo version)
    {
        if (version.IsAncient)
            return AncientGroup;

        if (version.IsSnapshot)
            return SnapshotGroup;

        var parts = version.Id.Split('.');
        return parts.Length >= 2 ? parts[0] + "." + parts[1] : version.Id;
    }

    private static (int Rank, DateTimeOffset Newest) SortWeight(VersionGroup group) => group.Name switch
    {
        SnapshotGroup => (0, group.Newest.ReleaseTime),
        AncientGroup => (-1, group.Newest.ReleaseTime),
        _ => (1, group.Newest.ReleaseTime),
    };

    private sealed class Manifest
    {
        [JsonPropertyName("versions")] public List<ManifestVersion>? Versions { get; set; }
    }

    private sealed class ManifestVersion
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
        [JsonPropertyName("type")] public string? Type { get; set; }
        [JsonPropertyName("releaseTime")] public DateTimeOffset ReleaseTime { get; set; }
    }
}

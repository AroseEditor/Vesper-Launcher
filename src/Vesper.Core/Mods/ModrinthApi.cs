using System.Text.Json;

namespace Vesper.Core.Mods;

public sealed class ModrinthApi : IModProvider
{
    public const string Root = "https://api.modrinth.com/v2";

    private readonly HttpClient _http;

    public ModrinthApi(HttpClient? http = null)
    {
        _http = http ?? new HttpClient();

        if (!_http.DefaultRequestHeaders.UserAgent.TryParseAdd(VesperInfo.UserAgent))
            _http.DefaultRequestHeaders.Add("User-Agent", VesperInfo.UserAgent);
    }

    public ModSource Source => ModSource.Modrinth;

    public bool IsAvailable => true;

    public string UnavailableReason => string.Empty;

    public async Task<IReadOnlyList<ModSearchResult>> SearchAsync(
        string query,
        string minecraftVersion,
        string loader,
        CancellationToken cancellationToken = default)
    {
        var facets = new List<string> { "[\"project_type:mod\"]" };

        if (!string.IsNullOrWhiteSpace(minecraftVersion))
            facets.Add($"[\"versions:{minecraftVersion}\"]");

        if (!string.IsNullOrWhiteSpace(loader) && !loader.Equals("vanilla", StringComparison.OrdinalIgnoreCase))
            facets.Add($"[\"categories:{loader.ToLowerInvariant()}\"]");

        var url = $"{Root}/search" +
                  $"?query={Uri.EscapeDataString(query)}" +
                  $"&facets=[{string.Join(',', facets)}]" +
                  "&limit=30&index=relevance";

        var json = await _http.GetStringAsync(url, cancellationToken);

        using var document = JsonDocument.Parse(json);

        if (!document.RootElement.TryGetProperty("hits", out var hits))
            return [];

        var results = new List<ModSearchResult>();

        foreach (var hit in hits.EnumerateArray())
        {
            results.Add(new ModSearchResult(
                ModSource.Modrinth,
                Text(hit, "project_id"),
                Text(hit, "title"),
                Text(hit, "description"),
                Text(hit, "author"),
                hit.TryGetProperty("downloads", out var d) && d.TryGetInt64(out var downloads) ? downloads : 0,
                Text(hit, "icon_url")));
        }

        return results;
    }

    public async Task<string> InstallAsync(
        ModSearchResult result,
        string minecraftVersion,
        string loader,
        string modsDirectory,
        CancellationToken cancellationToken = default)
    {
        var url = $"{Root}/project/{result.Id}/version" +
                  $"?game_versions=[\"{minecraftVersion}\"]" +
                  $"&loaders=[\"{loader.ToLowerInvariant()}\"]";

        var json = await _http.GetStringAsync(url, cancellationToken);

        using var document = JsonDocument.Parse(json);
        var versions = document.RootElement;

        if (versions.ValueKind != JsonValueKind.Array || versions.GetArrayLength() == 0)
            throw new ModInstallException(
                $"{result.Title} has no build for {loader} {minecraftVersion}");

        foreach (var version in versions.EnumerateArray())
        {
            if (!version.TryGetProperty("files", out var files))
                continue;

            foreach (var file in files.EnumerateArray())
            {
                var primary = file.TryGetProperty("primary", out var p) && p.ValueKind == JsonValueKind.True;
                var fileName = Text(file, "filename");
                var downloadUrl = Text(file, "url");

                if (string.IsNullOrEmpty(downloadUrl) || string.IsNullOrEmpty(fileName))
                    continue;

                if (!primary && files.GetArrayLength() > 1)
                    continue;

                Directory.CreateDirectory(modsDirectory);
                var destination = Path.Combine(modsDirectory, fileName);

                var bytes = await _http.GetByteArrayAsync(downloadUrl, cancellationToken);
                await File.WriteAllBytesAsync(destination, bytes, cancellationToken);

                return fileName;
            }
        }

        throw new ModInstallException($"{result.Title} had no downloadable file");
    }

    private static string Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
}

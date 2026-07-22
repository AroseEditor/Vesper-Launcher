using System.Text.Json;

namespace Vesper.Core.Mods;

public sealed class CurseForgeApi : IModProvider
{
    public const string Root = "https://api.curseforge.com/v1";
    public const int MinecraftGameId = 432;
    public const int ModsClassId = 6;

    private static readonly Dictionary<string, int> LoaderIds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["forge"] = 1,
        ["fabric"] = 4,
        ["quilt"] = 5,
        ["neoforge"] = 6,
    };

    private readonly HttpClient _http;
    private readonly string? _apiKey;

    public CurseForgeApi(string? apiKey, HttpClient? http = null)
    {
        _apiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey.Trim();
        _http = http ?? new HttpClient();

        if (!_http.DefaultRequestHeaders.UserAgent.TryParseAdd(VesperInfo.UserAgent))
            _http.DefaultRequestHeaders.Add("User-Agent", VesperInfo.UserAgent);
    }

    public ModSource Source => ModSource.CurseForge;

    public bool IsAvailable => _apiKey is not null;

    public string UnavailableReason =>
        "CurseForge requires a personal API key. Create one at console.curseforge.com and paste it into Settings.";

    public async Task<IReadOnlyList<ModSearchResult>> SearchAsync(
        string query,
        string minecraftVersion,
        string loader,
        CancellationToken cancellationToken = default)
    {
        if (_apiKey is null)
            throw new ModInstallException(UnavailableReason);

        var url = $"{Root}/mods/search?gameId={MinecraftGameId}&classId={ModsClassId}" +
                  $"&searchFilter={Uri.EscapeDataString(query)}&pageSize=30&sortOrder=desc";

        if (!string.IsNullOrWhiteSpace(minecraftVersion))
            url += $"&gameVersion={Uri.EscapeDataString(minecraftVersion)}";

        if (LoaderIds.TryGetValue(loader, out var loaderId))
            url += $"&modLoaderType={loaderId}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("x-api-key", _apiKey);

        using var response = await _http.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new ModInstallException("CurseForge rejected the request. Check your API key.");

        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        using var document = JsonDocument.Parse(json);

        if (!document.RootElement.TryGetProperty("data", out var data))
            return [];

        var results = new List<ModSearchResult>();

        foreach (var mod in data.EnumerateArray())
        {
            var authors = string.Empty;

            if (mod.TryGetProperty("authors", out var authorArray) &&
                authorArray.ValueKind == JsonValueKind.Array)
            {
                authors = string.Join(", ", authorArray.EnumerateArray().Select(a => Text(a, "name")));
            }

            var icon = mod.TryGetProperty("logo", out var logo) ? Text(logo, "thumbnailUrl") : null;

            results.Add(new ModSearchResult(
                ModSource.CurseForge,
                mod.TryGetProperty("id", out var id) ? id.GetRawText() : string.Empty,
                Text(mod, "name"),
                Text(mod, "summary"),
                authors,
                mod.TryGetProperty("downloadCount", out var d) && d.TryGetInt64(out var downloads) ? downloads : 0,
                icon));
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
        if (_apiKey is null)
            throw new ModInstallException(UnavailableReason);

        var url = $"{Root}/mods/{result.Id}/files?gameVersion={Uri.EscapeDataString(minecraftVersion)}&pageSize=30";

        if (LoaderIds.TryGetValue(loader, out var loaderId))
            url += $"&modLoaderType={loaderId}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("x-api-key", _apiKey);

        using var response = await _http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        using var document = JsonDocument.Parse(json);

        if (!document.RootElement.TryGetProperty("data", out var files) || files.GetArrayLength() == 0)
            throw new ModInstallException(
                $"{result.Title} has no build for {loader} {minecraftVersion}");

        foreach (var file in files.EnumerateArray())
        {
            var downloadUrl = Text(file, "downloadUrl");
            var fileName = Text(file, "fileName");

            if (string.IsNullOrEmpty(downloadUrl) || string.IsNullOrEmpty(fileName))
                continue;

            Directory.CreateDirectory(modsDirectory);
            var destination = Path.Combine(modsDirectory, fileName);

            var bytes = await _http.GetByteArrayAsync(downloadUrl, cancellationToken);
            await File.WriteAllBytesAsync(destination, bytes, cancellationToken);

            return fileName;
        }

        throw new ModInstallException(
            $"{result.Title} did not expose a direct download. Some CurseForge authors disable it.");
    }

    private static string Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
}

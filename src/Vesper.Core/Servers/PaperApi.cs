using System.Text.Json;
using System.Text.Json.Serialization;

namespace Vesper.Core.Servers;

public sealed record PaperBuild(int Id, string Channel, string FileName, string Url, string Sha256)
{
    public bool IsStable => Channel.Equals("STABLE", StringComparison.OrdinalIgnoreCase);

    public string Label => IsStable ? $"Build {Id}" : $"Build {Id} ({Channel.ToLowerInvariant()})";
}

public sealed class PaperApi
{
    public const string Root = "https://fill.papermc.io/v3/projects";

    private readonly HttpClient _http;

    public PaperApi(HttpClient? http = null)
    {
        _http = http ?? new HttpClient();

        if (!_http.DefaultRequestHeaders.UserAgent.TryParseAdd(VesperInfo.UserAgent))
            _http.DefaultRequestHeaders.Add("User-Agent", VesperInfo.UserAgent);
    }

    public async Task<IReadOnlyList<string>> GetVersionsAsync(
        string project,
        CancellationToken cancellationToken = default)
    {
        var json = await _http.GetStringAsync($"{Root}/{project}", cancellationToken);
        var response = JsonSerializer.Deserialize<ProjectResponse>(json);

        if (response?.Versions is null)
            return [];

        return response.Versions
            .SelectMany(group => group.Value)
            .Where(v => !v.Contains('-'))
            .ToList();
    }

    public async Task<IReadOnlyList<PaperBuild>> GetBuildsAsync(
        string project,
        string version,
        CancellationToken cancellationToken = default)
    {
        var json = await _http.GetStringAsync(
            $"{Root}/{project}/versions/{Uri.EscapeDataString(version)}/builds", cancellationToken);

        var builds = JsonSerializer.Deserialize<List<BuildResponse>>(json) ?? [];
        var result = new List<PaperBuild>();

        foreach (var build in builds)
        {
            if (build.Downloads is null ||
                !build.Downloads.TryGetValue("server:default", out var download) ||
                string.IsNullOrEmpty(download.Url))
            {
                continue;
            }

            download.Checksums.TryGetValue("sha256", out var sha);

            result.Add(new PaperBuild(
                build.Id, build.Channel ?? "STABLE", download.Name, download.Url, sha ?? string.Empty));
        }

        return result;
    }

    public async Task DownloadAsync(
        PaperBuild build,
        string destination,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

        using var response = await _http.GetAsync(
            build.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? 0;
        var read = 0L;
        var buffer = new byte[81920];

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var target = File.Create(destination);

        int count;
        while ((count = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
            read += count;

            if (total > 0)
                progress?.Report((double)read / total);
        }
    }

    private sealed class ProjectResponse
    {
        [JsonPropertyName("versions")]
        public Dictionary<string, List<string>>? Versions { get; set; }
    }

    private sealed class BuildResponse
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("channel")] public string? Channel { get; set; }
        [JsonPropertyName("downloads")] public Dictionary<string, DownloadResponse>? Downloads { get; set; }
    }

    private sealed class DownloadResponse
    {
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("url")] public string Url { get; set; } = string.Empty;
        [JsonPropertyName("checksums")] public Dictionary<string, string> Checksums { get; set; } = [];
    }
}

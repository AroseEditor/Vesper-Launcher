namespace Vesper.Core.Mods;

public sealed class ModFile
{
    public required string Path { get; init; }

    public required string FileName { get; init; }

    public string ModId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Authors { get; set; } = string.Empty;

    public string Loader { get; set; } = "Unknown";

    public long SizeBytes { get; set; }

    public bool IsDisabled { get; set; }

    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? FileName : Name;

    public string Subtitle
    {
        get
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(Version))
                parts.Add(Version);

            parts.Add(Loader);

            if (!string.IsNullOrWhiteSpace(Authors))
                parts.Add(Authors);

            return string.Join("  ", parts);
        }
    }

    public string SizeLabel => SizeBytes >= 1024 * 1024
        ? $"{SizeBytes / 1024d / 1024d:0.0} MB"
        : $"{Math.Max(1, SizeBytes / 1024)} KB";
}

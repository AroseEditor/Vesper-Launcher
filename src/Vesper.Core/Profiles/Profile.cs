using System.Text.Json.Serialization;

namespace Vesper.Core.Profiles;

public sealed class Profile
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string MinecraftVersion { get; set; } = string.Empty;

    public LoaderKind Loader { get; set; } = LoaderKind.Vanilla;

    public string? LoaderVersion { get; set; }

    public bool IsVesperProfile { get; set; }

    public string? LaunchVersionId { get; set; }

    public int MaximumRamMb { get; set; } = 6144;

    public int MinimumRamMb { get; set; } = 2048;

    public int ScreenWidth { get; set; }

    public int ScreenHeight { get; set; }

    public bool FullScreen { get; set; }

    public string? JavaPath { get; set; }

    public List<string> ExtraJvmArguments { get; set; } = [];

    public bool UseOptimisedJvmArguments { get; set; } = true;

    public bool InstallPerformanceBundle { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? LastPlayedAt { get; set; }

    public string? CoverPath { get; set; }

    public string? IconUrl { get; set; }

    public string GroupName { get; set; } = string.Empty;

    public long TotalPlaySeconds { get; set; }

    [JsonIgnore]
    public string EffectiveVersionId => LaunchVersionId ?? MinecraftVersion;

    [JsonIgnore]
    public string PlaytimeLabel
    {
        get
        {
            if (TotalPlaySeconds <= 0)
                return "Never played";

            var hours = TotalPlaySeconds / 3600;
            var minutes = TotalPlaySeconds % 3600 / 60;
            return hours > 0 ? $"{hours}h {minutes}m played" : $"{minutes}m played";
        }
    }

    [JsonIgnore]
    public string LastPlayedLabel => LastPlayedAt is null
        ? "Not played yet"
        : $"Last played {Humanize(DateTimeOffset.UtcNow - LastPlayedAt.Value)}";

    private static string Humanize(TimeSpan span)
    {
        if (span.TotalDays >= 1)
            return $"{(int)span.TotalDays}d ago";
        if (span.TotalHours >= 1)
            return $"{(int)span.TotalHours}h ago";
        if (span.TotalMinutes >= 1)
            return $"{(int)span.TotalMinutes}m ago";
        return "just now";
    }

    [JsonIgnore]
    public string ProfileLabel
    {
        get
        {
            var prefix = IsVesperProfile ? "Enhanced" : "Vanilla";
            return Loader == LoaderKind.Vanilla && !IsVesperProfile
                ? "Vanilla"
                : $"{prefix} + {Loader.DisplayName()}";
        }
    }
}

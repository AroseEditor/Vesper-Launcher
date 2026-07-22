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

    public int MaximumRamMb { get; set; } = 4096;

    public int MinimumRamMb { get; set; } = 512;

    public int ScreenWidth { get; set; }

    public int ScreenHeight { get; set; }

    public bool FullScreen { get; set; }

    public string? JavaPath { get; set; }

    public List<string> ExtraJvmArguments { get; set; } = [];

    public bool UseOptimisedJvmArguments { get; set; } = true;

    public bool InstallPerformanceBundle { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? LastPlayedAt { get; set; }

    [JsonIgnore]
    public string EffectiveVersionId => LaunchVersionId ?? MinecraftVersion;

    [JsonIgnore]
    public string ProfileLabel
    {
        get
        {
            var prefix = IsVesperProfile ? "Vesper" : "Vanilla";
            return Loader == LoaderKind.Vanilla && !IsVesperProfile
                ? "Vanilla"
                : $"{prefix} + {Loader.DisplayName()}";
        }
    }
}

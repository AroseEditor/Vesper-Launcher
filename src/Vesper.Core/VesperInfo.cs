using System.Reflection;

namespace Vesper.Core;

public static class VesperInfo
{
    public const string Name = "Vesper Launcher";

    public const string LauncherId = "vesper";

    public static string Version =>
        typeof(VesperInfo).Assembly.GetName().Version?.ToString(3) ?? "0.1.0";

    public static string UserAgent => $"{LauncherId}/{Version}";
}

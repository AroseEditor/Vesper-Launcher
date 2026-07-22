namespace Vesper.Core.Launching;

public static class JvmTuning
{
    public const int SmallHeapMb = 6144;

    public static IReadOnlyList<string> Recommended(int maximumRamMb)
    {
        var regionSize = maximumRamMb >= SmallHeapMb ? "32M" : "16M";
        var newSizePercent = maximumRamMb >= SmallHeapMb ? "30" : "20";
        var maxNewSizePercent = maximumRamMb >= SmallHeapMb ? "40" : "30";

        return
        [
            "-XX:+UnlockExperimentalVMOptions",
            "-XX:+UseG1GC",
            "-XX:MaxGCPauseMillis=37",
            "-XX:+PerfDisableSharedMem",
            "-XX:+ParallelRefProcEnabled",
            "-XX:+DisableExplicitGC",
            "-XX:+AlwaysPreTouch",
            "-XX:G1NewSizePercent=" + newSizePercent,
            "-XX:G1MaxNewSizePercent=" + maxNewSizePercent,
            "-XX:G1HeapRegionSize=" + regionSize,
            "-XX:G1ReservePercent=20",
            "-XX:G1HeapWastePercent=5",
            "-XX:G1MixedGCCountTarget=4",
            "-XX:G1MixedGCLiveThresholdPercent=90",
            "-XX:G1RSetUpdatingPauseTimePercent=5",
            "-XX:InitiatingHeapOccupancyPercent=15",
            "-XX:SurvivorRatio=32",
            "-XX:MaxTenuringThreshold=1",
            "-XX:+UseNUMA",
            "-Dsun.rmi.dgc.server.gcInterval=2147483646",
            "-Dfml.ignoreInvalidMinecraftCertificates=true",
            "-Dfml.ignorePatchDiscrepancies=true",
        ];
    }

    public static IReadOnlyList<string> Merge(
        IEnumerable<string> tuned,
        IEnumerable<string> userArguments)
    {
        var result = new List<string>();
        var user = userArguments.ToList();

        foreach (var argument in tuned)
        {
            if (!user.Any(u => Conflicts(u, argument)))
                result.Add(argument);
        }

        result.AddRange(user);
        return result;
    }

    private static bool Conflicts(string userArgument, string tunedArgument)
    {
        var userKey = KeyOf(userArgument);
        var tunedKey = KeyOf(tunedArgument);

        return !string.IsNullOrEmpty(userKey) &&
               userKey.Equals(tunedKey, StringComparison.OrdinalIgnoreCase);
    }

    private static string KeyOf(string argument)
    {
        var separator = argument.IndexOf('=');
        var key = separator > 0 ? argument[..separator] : argument;

        if (key.StartsWith("-XX:+", StringComparison.Ordinal) ||
            key.StartsWith("-XX:-", StringComparison.Ordinal))
        {
            return "-XX:" + key[5..];
        }

        return key;
    }
}

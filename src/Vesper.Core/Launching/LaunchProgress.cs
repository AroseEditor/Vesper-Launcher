namespace Vesper.Core.Launching;

public enum LaunchPhase
{
    Preparing,
    Installing,
    Building,
    Started,
}

public readonly record struct LaunchProgress(
    LaunchPhase Phase,
    string? CurrentFile,
    int TotalFiles,
    int ProgressedFiles,
    long TotalBytes,
    long ProgressedBytes)
{
    public double Ratio => TotalBytes > 0
        ? Math.Clamp((double)ProgressedBytes / TotalBytes, 0, 1)
        : TotalFiles > 0
            ? Math.Clamp((double)ProgressedFiles / TotalFiles, 0, 1)
            : 0;

    public string Describe() => Phase switch
    {
        LaunchPhase.Preparing => "Preparing",
        LaunchPhase.Installing => string.IsNullOrEmpty(CurrentFile)
            ? "Downloading"
            : $"Downloading {CurrentFile}",
        LaunchPhase.Building => "Building launch command",
        LaunchPhase.Started => "Started",
        _ => Phase.ToString(),
    };
}

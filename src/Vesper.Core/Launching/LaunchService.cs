using System.Diagnostics;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.Installers;
using CmlLib.Core.ProcessBuilder;
using Vesper.Core.Profiles;
using Vesper.Core.Storage;

namespace Vesper.Core.Launching;

public sealed class LaunchService
{
    private readonly VesperPaths _paths;

    public LaunchService(VesperPaths paths) => _paths = paths;

    public MinecraftLauncher CreateLauncher(Profile profile) =>
        new(new VesperMinecraftPath(_paths, profile.Id));

    public async Task<IReadOnlyList<string>> GetAvailableVersionsAsync(
        Profile profile,
        CancellationToken cancellationToken = default)
    {
        var launcher = CreateLauncher(profile);
        var versions = await launcher.GetAllVersionsAsync(cancellationToken);
        return versions.Select(v => v.Name).ToList();
    }

    public async Task<Process> LaunchAsync(
        Profile profile,
        MSession session,
        IProgress<LaunchProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _paths.EnsureCreated();
        Directory.CreateDirectory(_paths.ProfileGameDir(profile.Id));

        var launcher = CreateLauncher(profile);
        var tracker = new ProgressTracker(progress);

        progress?.Report(tracker.Snapshot(LaunchPhase.Preparing));

        await launcher.InstallAsync(
            profile.EffectiveVersionId,
            tracker.Files,
            tracker.Bytes,
            cancellationToken);

        progress?.Report(tracker.Snapshot(LaunchPhase.Building));

        var process = await launcher.BuildProcessAsync(
            profile.EffectiveVersionId,
            BuildOption(profile, session),
            cancellationToken);

        process.Start();
        progress?.Report(tracker.Snapshot(LaunchPhase.Started));
        return process;
    }

    private static MLaunchOption BuildOption(Profile profile, MSession session)
    {
        var option = new MLaunchOption
        {
            Session = session,
            MaximumRamMb = profile.MaximumRamMb,
            MinimumRamMb = profile.MinimumRamMb,
            ScreenWidth = profile.ScreenWidth,
            ScreenHeight = profile.ScreenHeight,
            FullScreen = profile.FullScreen,
            GameLauncherName = VesperInfo.LauncherId,
            GameLauncherVersion = VesperInfo.Version,
        };

        if (!string.IsNullOrWhiteSpace(profile.JavaPath))
            option.JavaPath = profile.JavaPath;

        if (profile.ExtraJvmArguments.Count > 0)
        {
            option.ExtraJvmArguments = MLaunchOption.DefaultExtraJvmArguments
                .Concat(profile.ExtraJvmArguments.Select(a => new MArgument(a)))
                .ToList();
        }

        return option;
    }

    private sealed class ProgressTracker
    {
        private readonly IProgress<LaunchProgress>? _sink;
        private string? _currentFile;
        private int _totalFiles;
        private int _progressedFiles;
        private long _totalBytes;
        private long _progressedBytes;

        public ProgressTracker(IProgress<LaunchProgress>? sink)
        {
            _sink = sink;

            Files = new Progress<InstallerProgressChangedEventArgs>(e =>
            {
                _totalFiles = e.TotalTasks;
                _progressedFiles = e.ProgressedTasks;
                _currentFile = e.Name;
                _sink?.Report(Snapshot(LaunchPhase.Installing));
            });

            Bytes = new Progress<ByteProgress>(e =>
            {
                _totalBytes = e.TotalBytes;
                _progressedBytes = e.ProgressedBytes;
                _sink?.Report(Snapshot(LaunchPhase.Installing));
            });
        }

        public IProgress<InstallerProgressChangedEventArgs> Files { get; }

        public IProgress<ByteProgress> Bytes { get; }

        public LaunchProgress Snapshot(LaunchPhase phase) => new(
            phase,
            _currentFile,
            _totalFiles,
            _progressedFiles,
            _totalBytes,
            _progressedBytes);
    }
}

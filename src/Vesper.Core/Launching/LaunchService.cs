using System.Diagnostics;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.Installers;
using CmlLib.Core.ProcessBuilder;
using Vesper.Core.Instances;
using Vesper.Core.Storage;

namespace Vesper.Core.Launching;

public sealed class LaunchService
{
    private readonly VesperPaths _paths;

    public LaunchService(VesperPaths paths) => _paths = paths;

    public MinecraftLauncher CreateLauncher(Instance instance) =>
        new(new VesperMinecraftPath(_paths, instance.Id));

    public async Task<IReadOnlyList<string>> GetAvailableVersionsAsync(
        Instance instance,
        CancellationToken cancellationToken = default)
    {
        var launcher = CreateLauncher(instance);
        var versions = await launcher.GetAllVersionsAsync(cancellationToken);
        return versions.Select(v => v.Name).ToList();
    }

    public async Task<Process> LaunchAsync(
        Instance instance,
        MSession session,
        IProgress<LaunchProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _paths.EnsureCreated();
        Directory.CreateDirectory(_paths.InstanceGameDir(instance.Id));

        var launcher = CreateLauncher(instance);
        var tracker = new ProgressTracker(progress);

        progress?.Report(tracker.Snapshot(LaunchPhase.Preparing));

        await launcher.InstallAsync(
            instance.EffectiveVersionId,
            tracker.Files,
            tracker.Bytes,
            cancellationToken);

        progress?.Report(tracker.Snapshot(LaunchPhase.Building));

        var process = await launcher.BuildProcessAsync(
            instance.EffectiveVersionId,
            BuildOption(instance, session),
            cancellationToken);

        process.Start();
        progress?.Report(tracker.Snapshot(LaunchPhase.Started));
        return process;
    }

    private static MLaunchOption BuildOption(Instance instance, MSession session)
    {
        var option = new MLaunchOption
        {
            Session = session,
            MaximumRamMb = instance.MaximumRamMb,
            MinimumRamMb = instance.MinimumRamMb,
            ScreenWidth = instance.ScreenWidth,
            ScreenHeight = instance.ScreenHeight,
            FullScreen = instance.FullScreen,
            GameLauncherName = VesperInfo.LauncherId,
            GameLauncherVersion = VesperInfo.Version,
        };

        if (!string.IsNullOrWhiteSpace(instance.JavaPath))
            option.JavaPath = instance.JavaPath;

        if (instance.ExtraJvmArguments.Count > 0)
        {
            option.ExtraJvmArguments = MLaunchOption.DefaultExtraJvmArguments
                .Concat(instance.ExtraJvmArguments.Select(a => new MArgument(a)))
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

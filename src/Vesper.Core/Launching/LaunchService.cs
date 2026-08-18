using System.Diagnostics;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.Installers;
using CmlLib.Core.ProcessBuilder;
using Vesper.Core.Profiles;
using Vesper.Core.Skins;
using Vesper.Core.Storage;

namespace Vesper.Core.Launching;

public sealed class LaunchService
{
    private readonly VesperPaths _paths;

    public LaunchService(VesperPaths paths)
    {
        _paths = paths;
        _controls = new ControlsSync(paths);
        _skins = new SkinSync(paths);
    }

    private readonly ControlsSync _controls;
    private readonly SkinSync _skins;

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
        CancellationToken cancellationToken = default,
        Accounts.Account? account = null)
    {
        _paths.EnsureCreated();
        Directory.CreateDirectory(_paths.ProfileGameDir(profile.Id));

        if (account is not null)
        {
            try
            {
                _skins.Apply(profile, account);
            }
            catch (IOException)
            {
            }
        }

        var launcher = CreateLauncher(profile);
        var tracker = new ProgressTracker(progress);

        progress?.Report(tracker.Snapshot(LaunchPhase.Preparing));

        await launcher.InstallAsync(
            profile.EffectiveVersionId,
            tracker.Files,
            tracker.Bytes,
            cancellationToken);

        progress?.Report(tracker.Snapshot(LaunchPhase.Building));

        var optionsFile = _controls.OptionsFileFor(profile.Id);
        _controls.ApplyTo(optionsFile);

        var useTuning = profile.UseOptimisedJvmArguments;
        var launched = await StartWithCaptureAsync(
            launcher, profile, session, useTuning, optionsFile, cancellationToken);

        if (useTuning && await ExitedEarlyAsync(launched.Process))
        {
            Diagnostics.ErrorService.Shared.Report(
                "Minecraft closed immediately; retrying without the performance JVM flags",
                launched.Tail.ToString(),
                Diagnostics.ErrorSeverity.Warning);

            launched = await StartWithCaptureAsync(
                launcher, profile, session, false, optionsFile, cancellationToken);
        }

        progress?.Report(tracker.Snapshot(LaunchPhase.Started));
        return launched.Process;
    }

    private async Task<LaunchedProcess> StartWithCaptureAsync(
        MinecraftLauncher launcher,
        Profile profile,
        MSession session,
        bool useTuning,
        string optionsFile,
        CancellationToken cancellationToken)
    {
        var process = await launcher.BuildProcessAsync(
            profile.EffectiveVersionId,
            BuildOption(profile, session, useTuning),
            cancellationToken);

        var logPath = Path.Combine(
            _paths.LogsDir,
            $"launch-{profile.Id}-{DateTime.Now:yyyyMMdd-HHmmss}.log");

        Directory.CreateDirectory(_paths.LogsDir);

        var tail = new System.Text.StringBuilder();
        var writer = new StreamWriter(logPath, append: false) { AutoFlush = true };

        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.UseShellExecute = false;
        process.EnableRaisingEvents = true;

        void OnData(object? _, DataReceivedEventArgs e)
        {
            if (e.Data is null)
                return;

            lock (tail)
            {
                writer.WriteLine(e.Data);
                tail.AppendLine(e.Data);

                if (tail.Length > 8000)
                    tail.Remove(0, tail.Length - 6000);
            }
        }

        process.OutputDataReceived += OnData;
        process.ErrorDataReceived += OnData;

        process.Exited += (_, _) =>
        {
            try
            {
                writer.Dispose();
            }
            catch (Exception)
            {
            }

            try
            {
                _controls.CaptureFrom(optionsFile);
            }
            catch (IOException)
            {
            }

            if (process.ExitCode != 0)
            {
                Diagnostics.ErrorService.Shared.Report(
                    $"Minecraft exited with code {process.ExitCode}",
                    tail.ToString() + Environment.NewLine + "Full log: " + logPath);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        return new LaunchedProcess(process, tail, logPath);
    }

    private static async Task<bool> ExitedEarlyAsync(Process process)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await process.WaitForExitAsync(cts.Token);
            return process.ExitCode != 0;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private sealed record LaunchedProcess(
        Process Process,
        System.Text.StringBuilder Tail,
        string LogPath);

    private static MLaunchOption BuildOption(Profile profile, MSession session, bool useTuning)
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

        var extra = useTuning
            ? JvmTuning.Merge(
                JvmTuning.Recommended(profile.MaximumRamMb), profile.ExtraJvmArguments)
            : profile.ExtraJvmArguments;

        if (extra.Count > 0)
        {
            option.ExtraJvmArguments = MLaunchOption.DefaultExtraJvmArguments
                .Concat(extra.Select(a => new MArgument(a)))
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

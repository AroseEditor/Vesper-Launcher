using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vesper.Core.Servers;
using Vesper.Core.Storage;

namespace Vesper.App.ViewModels;

public partial class ServersViewModel : ObservableObject
{
    private const int MaxConsoleLines = 2000;

    private readonly VesperPaths _paths;
    private readonly ServerManager _manager;
    private readonly Dictionary<string, ServerProcess> _processes = [];

    [ObservableProperty]
    private ServerDefinition? _selectedServer;

    [ObservableProperty]
    private string? _selectedVersion;

    [ObservableProperty]
    private PaperBuild? _selectedBuild;

    [ObservableProperty]
    private string _newServerName = string.Empty;

    [ObservableProperty]
    private string _commandText = string.Empty;

    [ObservableProperty]
    private bool _isCreating;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private string _statusText = "No servers yet";

    public ServersViewModel(VesperPaths paths)
    {
        _paths = paths;
        _manager = new ServerManager(paths);
        Refresh();
    }

    public ObservableCollection<ServerDefinition> Servers { get; } = [];

    public ObservableCollection<string> Versions { get; } = [];

    public ObservableCollection<PaperBuild> Builds { get; } = [];

    public ObservableCollection<string> Console { get; } = [];

    public string ServersDirectory => _paths.ServersDir;

    public bool HasServers => Servers.Count > 0;

    public bool HasSelection => SelectedServer is not null;

    public bool IsInstalled => SelectedServer is not null && _manager.IsInstalled(SelectedServer);

    public bool CanStart => IsInstalled && !IsRunning && !IsBusy;

    public bool CanStop => IsRunning;

    public string SelectedAddress => SelectedServer?.Address ?? "not running";

    public async Task LoadVersionsAsync(CancellationToken cancellationToken = default)
    {
        if (Versions.Count > 0)
            return;

        try
        {
            var versions = await _manager.Paper.GetVersionsAsync("paper", cancellationToken);

            Versions.Clear();
            foreach (var version in versions)
                Versions.Add(version);

            SelectedVersion = Versions.FirstOrDefault();
        }
        catch (Exception e)
        {
            StatusText = "Could not reach PaperMC: " + e.Message;
        }
    }

    [RelayCommand]
    private void BeginCreate()
    {
        IsCreating = true;
        NewServerName = string.Empty;
        _ = LoadVersionsAsync();
    }

    [RelayCommand]
    private void CancelCreate() => IsCreating = false;

    [RelayCommand]
    private void ConfirmCreate()
    {
        if (string.IsNullOrWhiteSpace(SelectedVersion))
        {
            StatusText = "Pick a Minecraft version first";
            return;
        }

        var name = string.IsNullOrWhiteSpace(NewServerName) ? "Paper " + SelectedVersion : NewServerName;
        var server = _manager.Create(name, SelectedVersion!);

        Refresh();
        SelectedServer = Servers.FirstOrDefault(s => s.Id == server.Id);
        IsCreating = false;
        StatusText = "Created " + server.Name + ". Install it to download the jar.";
    }

    [RelayCommand]
    private async Task InstallAsync(CancellationToken cancellationToken)
    {
        if (SelectedServer is null || IsBusy)
            return;

        IsBusy = true;
        Progress = 0;

        try
        {
            var builds = await _manager.Paper.GetBuildsAsync(
                SelectedServer.Project, SelectedServer.MinecraftVersion, cancellationToken);

            var build = builds.FirstOrDefault(b => b.IsStable) ?? builds.FirstOrDefault();

            if (build is null)
            {
                StatusText = "No builds published for " + SelectedServer.MinecraftVersion;
                return;
            }

            StatusText = "Downloading " + build.FileName;

            var progress = new Progress<double>(value => Progress = value * 100);
            await _manager.InstallAsync(SelectedServer, build, progress, cancellationToken);

            StatusText = "Installed " + build.FileName;
            OnPropertyChanged(nameof(IsInstalled));
            OnPropertyChanged(nameof(CanStart));
        }
        catch (Exception e)
        {
            StatusText = "Install failed: " + e.Message;
        }
        finally
        {
            IsBusy = false;
            Progress = 0;
        }
    }

    [RelayCommand]
    private void Start()
    {
        if (SelectedServer is null || !CanStart)
            return;

        var server = SelectedServer;
        var process = ProcessFor(server);

        try
        {
            Console.Clear();
            process.Start(
                ResolveJava(server), _manager.DirectoryFor(server.Id), server.JarFileName, server.MemoryMb);

            _manager.MarkStarted(server);
            StatusText = "Started " + server.Name;
        }
        catch (Exception e)
        {
            StatusText = "Could not start: " + e.Message;
        }
    }

    [RelayCommand]
    private async Task StopAsync()
    {
        if (SelectedServer is null)
            return;

        await ProcessFor(SelectedServer).StopAsync();
    }

    [RelayCommand]
    private async Task RestartAsync()
    {
        if (SelectedServer is null)
            return;

        var server = SelectedServer;
        await ProcessFor(server).RestartAsync(
            ResolveJava(server), _manager.DirectoryFor(server.Id), server.JarFileName, server.MemoryMb);
    }

    [RelayCommand]
    private void SendCommand()
    {
        if (SelectedServer is null || string.IsNullOrWhiteSpace(CommandText))
            return;

        ProcessFor(SelectedServer).SendCommand(CommandText.Trim());
        CommandText = string.Empty;
    }

    [RelayCommand]
    private void DeleteServer(ServerDefinition server)
    {
        if (_processes.TryGetValue(server.Id, out var process))
        {
            process.Dispose();
            _processes.Remove(server.Id);
        }

        _manager.Delete(server.Id);

        if (SelectedServer?.Id == server.Id)
            SelectedServer = null;

        Refresh();
        StatusText = "Deleted " + server.Name;
    }

    [RelayCommand]
    private void SelectServer(ServerDefinition server) => SelectedServer = server;

    [RelayCommand]
    private void OpenFolder()
    {
        if (SelectedServer is null)
            return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _manager.DirectoryFor(SelectedServer.Id),
                UseShellExecute = true,
            });
        }
        catch (Exception e)
        {
            StatusText = "Could not open folder: " + e.Message;
        }
    }

    private ServerProcess ProcessFor(ServerDefinition server)
    {
        if (_processes.TryGetValue(server.Id, out var existing))
            return existing;

        var process = new ServerProcess();

        process.Output += (_, line) => Dispatcher.UIThread.Post(() =>
        {
            Console.Add(line);

            while (Console.Count > MaxConsoleLines)
                Console.RemoveAt(0);
        });

        process.RunningChanged += (_, running) => Dispatcher.UIThread.Post(() =>
        {
            if (SelectedServer?.Id == server.Id)
                IsRunning = running;
        });

        _processes[server.Id] = process;
        return process;
    }

    private string ResolveJava(ServerDefinition server)
    {
        if (!string.IsNullOrWhiteSpace(server.JavaPath) && File.Exists(server.JavaPath))
            return server.JavaPath;

        if (Directory.Exists(_paths.RuntimeDir))
        {
            var name = OperatingSystem.IsWindows() ? "javaw.exe" : "java";
            var candidate = Directory
                .EnumerateFiles(_paths.RuntimeDir, name, SearchOption.AllDirectories)
                .FirstOrDefault();

            if (candidate is not null)
                return OperatingSystem.IsWindows()
                    ? candidate.Replace("javaw.exe", "java.exe")
                    : candidate;
        }

        return OperatingSystem.IsWindows() ? "java.exe" : "java";
    }

    private void Refresh()
    {
        Servers.Clear();
        foreach (var server in _manager.LoadAll())
            Servers.Add(server);

        OnPropertyChanged(nameof(HasServers));
    }

    partial void OnSelectedServerChanged(ServerDefinition? value)
    {
        IsRunning = value is not null && _processes.TryGetValue(value.Id, out var p) && p.IsRunning;

        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(IsInstalled));
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(SelectedAddress));
    }

    partial void OnIsRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(CanStop));
    }

    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(CanStart));
}

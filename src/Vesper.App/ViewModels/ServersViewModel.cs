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
    private readonly PortForwarding _forwarding = new();

    private ServerProperties? _properties;

    [ObservableProperty]
    private ServerDefinition? _selectedServer;

    [ObservableProperty]
    private bool _showProperties;

    [ObservableProperty]
    private string? _selectedVersion;

    [ObservableProperty]
    private PaperBuild? _selectedBuild;

    [ObservableProperty]
    private string _newServerName = string.Empty;

    [ObservableProperty]
    private string _newServerTemplate = "paper";

    [ObservableProperty]
    private int _newServerSlots = 20;

    [ObservableProperty]
    private int _newServerRamMb = 2048;

    [ObservableProperty]
    private bool _newServerForwardPort = true;

    [ObservableProperty]
    private bool _isForwarding;

    [ObservableProperty]
    private string _publicAddress = string.Empty;

    [ObservableProperty]
    private string _forwardingHelp = string.Empty;

    [ObservableProperty]
    private bool _hasForwardingProblem;

    [ObservableProperty]
    private string _newServerMotd = "A Vesper server";

    [ObservableProperty]
    private int _newServerPort = 25565;

    [ObservableProperty]
    private bool _newServerOnlineMode;

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

    public ObservableCollection<ServerPropertyGroupViewModel> PropertyGroups { get; } = [];

    public IReadOnlyList<string> Templates { get; } = ["paper", "purpur", "folia", "velocity"];

    public bool IsPaperTemplate => NewServerTemplate == "paper";

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

        var name = string.IsNullOrWhiteSpace(NewServerName)
            ? $"{NewServerTemplate} {SelectedVersion}"
            : NewServerName;

        var server = _manager.Create(name, SelectedVersion!, NewServerTemplate);

        server.MaxPlayers = Math.Max(1, NewServerSlots);
        server.MemoryMb = Math.Max(512, NewServerRamMb);
        server.Port = NewServerPort;
        server.Motd = NewServerMotd;
        server.OnlineMode = NewServerOnlineMode;
        server.ForwardPort = NewServerForwardPort;

        _manager.Save(server);

        Refresh();
        SelectedServer = Servers.FirstOrDefault(s => s.Id == server.Id);
        IsCreating = false;
        StatusText = "Created " + server.Name + ". Install it to download the jar.";
    }

    [RelayCommand]
    private void SetTemplate(string template) => NewServerTemplate = template;

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

            if (server.ForwardPort)
                _ = OpenPortAsync();
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

        var server = SelectedServer;
        await ProcessFor(server).StopAsync();

        if (server.ForwardPort)
        {
            try
            {
                await _forwarding.CloseAsync(server.Port);
                PublicAddress = string.Empty;
            }
            catch (Exception)
            {
            }
        }
    }

    [RelayCommand]
    private async Task OpenPortAsync()
    {
        if (SelectedServer is null || IsForwarding)
            return;

        var server = SelectedServer;
        IsForwarding = true;
        StatusText = "Asking your router to open port " + server.Port;

        try
        {
            var result = await _forwarding.OpenAsync(server.Port);

            StatusText = result.Message;
            HasForwardingProblem = !result.Success;
            ForwardingHelp = HelpFor(result, server.Port);

            if (result.Success)
            {
                server.PublicAddress = result.ExternalAddress;
                PublicAddress = result.ShareableAddress ?? string.Empty;
                _manager.Save(server);
                OnPropertyChanged(nameof(SelectedAddress));
            }
        }
        catch (Exception e)
        {
            StatusText = "Port forwarding failed: " + e.Message;
        }
        finally
        {
            IsForwarding = false;
        }
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

    public static string HelpFor(PortForwardResult result, int port)
    {
        var steps = result.Outcome switch
        {
            PortForwardOutcome.NoRouterFound => new[]
            {
                "Your router did not answer the request.",
                "1. Open your router page, usually http://192.168.1.1 or http://192.168.0.1",
                "2. Find UPnP, often under Advanced, NAT or Firewall, and turn it on",
                "3. Save, reboot the router, then press Open port again",
                "No UPnP option? Forward port " + port + " to this computer by hand, TCP and UDP.",
            },

            PortForwardOutcome.RouterRefused => new[]
            {
                "Your router rejected the request.",
                "1. Something may already use port " + port + ". Try a different port.",
                "2. Some routers only accept UPnP over a wired connection.",
                "3. Restart the router to clear stale mappings, then try again.",
                "Otherwise forward port " + port + " manually, TCP and UDP, to this computer.",
            },

            PortForwardOutcome.BehindCarrierNat => new[]
            {
                "Your provider puts you behind their own network, known as CGNAT.",
                "No port forwarding can reach you on this connection, and no launcher can fix that.",
                "1. Ask your provider for a public or static IP address, often a small fee",
                "2. Use a tunnel such as playit.gg or ngrok",
                "3. Play over LAN, or rent a hosted server",
            },

            _ => [],
        };

        return string.Join(Environment.NewLine, steps);
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

        LoadProperties();
    }

    [RelayCommand]
    private void ToggleProperties() => ShowProperties = !ShowProperties;

    private void LoadProperties()
    {
        PropertyGroups.Clear();

        if (SelectedServer is null)
            return;

        var path = Path.Combine(_manager.DirectoryFor(SelectedServer.Id), "server.properties");
        _properties = ServerProperties.Load(path);

        foreach (var groupName in ServerPropertySchema.Groups)
        {
            var group = new ServerPropertyGroupViewModel(groupName);

            foreach (var spec in ServerPropertySchema.All.Where(s => s.Group == groupName))
                group.Items.Add(new ServerPropertyViewModel(spec, _properties.Get(spec.Key), SaveProperties));

            if (group.Items.Count > 0)
                PropertyGroups.Add(group);
        }
    }

    private void SaveProperties()
    {
        if (SelectedServer is null || _properties is null)
            return;

        foreach (var item in PropertyGroups.SelectMany(g => g.Items))
            _properties.Set(item.Key, item.Value);

        _properties.Save(Path.Combine(_manager.DirectoryFor(SelectedServer.Id), "server.properties"));
    }

    partial void OnIsRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(CanStop));
    }

    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(CanStart));

    partial void OnNewServerTemplateChanged(string value) =>
        OnPropertyChanged(nameof(IsPaperTemplate));
}

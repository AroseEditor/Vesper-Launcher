using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vesper.App.Theming;
using Vesper.Core;
using Vesper.Core.Accounts;
using Vesper.Core.Instances;
using Vesper.Core.Launching;
using Vesper.Core.Storage;
using Vesper.Core.Theming;

namespace Vesper.App.ViewModels;

public enum NavTab
{
    Play,
    Servers,
    Settings,
}

public partial class MainWindowViewModel : ObservableObject
{
    private readonly VesperPaths _paths;
    private readonly InstanceManager _instances;
    private readonly AccountManager _accounts;
    private readonly LaunchService _launcher;
    private readonly ThemeStore _themes;

    [ObservableProperty]
    private NavTab _selectedTab = NavTab.Play;

    [ObservableProperty]
    private Instance? _selectedInstance;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _newInstanceName = string.Empty;

    [ObservableProperty]
    private string _newInstanceVersion = "1.21.1";

    [ObservableProperty]
    private VesperTheme? _selectedTheme;

    public MainWindowViewModel()
    {
        _paths = VesperPaths.Resolve();
        _paths.EnsureCreated();

        _instances = new InstanceManager(_paths);
        _accounts = new AccountManager(_paths);
        _launcher = new LaunchService(_paths);
        _themes = new ThemeStore(_paths);

        Accounts = new AccountsViewModel(_accounts);

        foreach (var theme in _themes.LoadAll())
            Themes.Add(theme);

        SelectedTheme = Themes.FirstOrDefault();
        RefreshInstances();
    }

    public AccountsViewModel Accounts { get; }

    public ObservableCollection<Instance> Instances { get; } = [];

    public ObservableCollection<VesperTheme> Themes { get; } = [];

    public string Version => VesperInfo.Version;

    public string RootDirectory => _paths.Root;

    public bool IsPlayTab => SelectedTab == NavTab.Play;

    public bool IsServersTab => SelectedTab == NavTab.Servers;

    public bool IsSettingsTab => SelectedTab == NavTab.Settings;

    public bool HasInstances => Instances.Count > 0;

    [RelayCommand]
    private void SelectTab(string tab)
    {
        if (Enum.TryParse<NavTab>(tab, out var parsed))
            SelectedTab = parsed;
    }

    [RelayCommand]
    private void SelectInstance(Instance instance) => SelectedInstance = instance;

    [RelayCommand]
    private void CreateInstance()
    {
        var name = string.IsNullOrWhiteSpace(NewInstanceName) ? "New Instance" : NewInstanceName;
        var version = string.IsNullOrWhiteSpace(NewInstanceVersion) ? "1.21.1" : NewInstanceVersion.Trim();

        var instance = _instances.Create(name, version);
        RefreshInstances();
        SelectedInstance = Instances.FirstOrDefault(i => i.Id == instance.Id);
        NewInstanceName = string.Empty;
        StatusText = "Created " + instance.Name;
    }

    [RelayCommand]
    private void DeleteInstance(Instance instance)
    {
        _instances.Delete(instance.Id);
        RefreshInstances();
        StatusText = "Deleted " + instance.Name;
    }

    [RelayCommand]
    private async Task LaunchAsync(CancellationToken cancellationToken)
    {
        if (IsBusy)
            return;

        if (SelectedInstance is null)
        {
            StatusText = "Select an instance first";
            return;
        }

        var account = _accounts.Selected;

        if (account is null)
        {
            StatusText = "Add an account first";
            Accounts.IsOpen = true;
            return;
        }

        IsBusy = true;
        Progress = 0;

        try
        {
            var progress = new Progress<LaunchProgress>(p =>
            {
                Progress = p.Ratio * 100;
                StatusText = p.Describe();
            });

            var session = AccountManager.CreateSession(account);
            await _launcher.LaunchAsync(SelectedInstance, session, progress, cancellationToken);

            _instances.MarkPlayed(SelectedInstance);
            _accounts.MarkUsed(account);
            StatusText = "Launched " + SelectedInstance.Name;
        }
        catch (OperationCanceledException)
        {
            StatusText = "Launch cancelled";
        }
        catch (Exception e)
        {
            StatusText = "Launch failed: " + e.Message;
        }
        finally
        {
            IsBusy = false;
            Progress = 0;
        }
    }

    [RelayCommand]
    private void ApplyTheme(VesperTheme theme)
    {
        SelectedTheme = theme;
        ThemeManager.Instance.Apply(theme);
        StatusText = "Applied theme " + theme.Name;
    }

    private void RefreshInstances()
    {
        Instances.Clear();
        foreach (var instance in _instances.LoadAll())
            Instances.Add(instance);

        SelectedInstance ??= Instances.FirstOrDefault();
        OnPropertyChanged(nameof(HasInstances));
    }

    partial void OnSelectedTabChanged(NavTab value)
    {
        OnPropertyChanged(nameof(IsPlayTab));
        OnPropertyChanged(nameof(IsServersTab));
        OnPropertyChanged(nameof(IsSettingsTab));
    }
}

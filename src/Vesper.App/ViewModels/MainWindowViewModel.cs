using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vesper.App.Theming;
using Vesper.Core;
using Vesper.Core.Accounts;
using Vesper.Core.Launching;
using Vesper.Core.Profiles;
using Vesper.Core.Storage;
using Vesper.Core.Theming;

namespace Vesper.App.ViewModels;

public enum NavTab
{
    Play,
    Skins,
    Servers,
    Settings,
}

public partial class MainWindowViewModel : ObservableObject
{
    private readonly VesperPaths _paths;
    private readonly ThemeStore _themeStore;

    [ObservableProperty]
    private NavTab _selectedTab = NavTab.Play;

    [ObservableProperty]
    private object? _currentPage;

    [ObservableProperty]
    private VesperTheme? _selectedTheme;

    public MainWindowViewModel()
    {
        _paths = VesperPaths.Resolve();
        _paths.EnsureCreated();

        var profiles = new ProfileManager(_paths);
        var accounts = new AccountManager(_paths);
        var launcher = new LaunchService(_paths);

        _themeStore = new ThemeStore(_paths);

        Accounts = new AccountsViewModel(accounts);
        Play = new PlayViewModel(_paths, profiles, accounts, launcher);
        Skins = new SkinsViewModel(_paths, accounts);
        Servers = new ServersViewModel(_paths);
        Settings = new SettingsViewModel(_paths);

        foreach (var theme in _themeStore.LoadAll())
            Themes.Add(theme);

        SelectedTheme = Themes.FirstOrDefault();
        CurrentPage = Play;

        Play.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PlayViewModel.StatusText))
                OnPropertyChanged(nameof(StatusText));
        };
    }

    public AccountsViewModel Accounts { get; }

    public PlayViewModel Play { get; }

    public SkinsViewModel Skins { get; }

    public ServersViewModel Servers { get; }

    public SettingsViewModel Settings { get; }

    public ObservableCollection<VesperTheme> Themes { get; } = [];

    public string Version => VesperInfo.Version;

    public bool IsPlayTab => SelectedTab == NavTab.Play;

    public bool IsSkinsTab => SelectedTab == NavTab.Skins;

    public bool IsServersTab => SelectedTab == NavTab.Servers;

    public bool IsSettingsTab => SelectedTab == NavTab.Settings;

    public string StatusText => Play.StatusText;

    public async Task InitializeAsync()
    {
        await Play.LoadAsync();
        Skins.Load();
    }

    [RelayCommand]
    private void SelectTab(string tab)
    {
        if (Enum.TryParse<NavTab>(tab, out var parsed))
            SelectedTab = parsed;
    }

    [RelayCommand]
    private void ApplyTheme(VesperTheme theme)
    {
        SelectedTheme = theme;
        ThemeManager.Shared.Apply(theme);
    }

    partial void OnSelectedTabChanged(NavTab value)
    {
        OnPropertyChanged(nameof(IsPlayTab));
        OnPropertyChanged(nameof(IsSkinsTab));
        OnPropertyChanged(nameof(IsServersTab));
        OnPropertyChanged(nameof(IsSettingsTab));

        CurrentPage = value switch
        {
            NavTab.Play => Play,
            NavTab.Skins => Skins,
            NavTab.Servers => Servers,
            NavTab.Settings => Settings,
            _ => Play,
        };
    }
}

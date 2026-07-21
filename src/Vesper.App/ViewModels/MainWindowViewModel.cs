using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vesper.App.Theming;
using Vesper.Core;
using Vesper.Core.Accounts;
using Vesper.Core.Profiles;
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
    private readonly ProfileManager _profileManager;
    private readonly AccountManager _accounts;
    private readonly LaunchService _launcher;
    private readonly ThemeStore _themes;

    [ObservableProperty]
    private NavTab _selectedTab = NavTab.Play;

    [ObservableProperty]
    private Profile? _selectedProfile;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _newProfileName = string.Empty;

    [ObservableProperty]
    private string _newProfileVersion = "1.21.1";

    [ObservableProperty]
    private VesperTheme? _selectedTheme;

    public MainWindowViewModel()
    {
        _paths = VesperPaths.Resolve();
        _paths.EnsureCreated();

        _profileManager = new ProfileManager(_paths);
        _accounts = new AccountManager(_paths);
        _launcher = new LaunchService(_paths);
        _themes = new ThemeStore(_paths);

        Accounts = new AccountsViewModel(_accounts);

        foreach (var theme in _themes.LoadAll())
            Themes.Add(theme);

        SelectedTheme = Themes.FirstOrDefault();
        RefreshProfiles();
    }

    public AccountsViewModel Accounts { get; }

    public ObservableCollection<Profile> Profiles { get; } = [];

    public ObservableCollection<VesperTheme> Themes { get; } = [];

    public string Version => VesperInfo.Version;

    public string RootDirectory => _paths.Root;

    public bool IsPlayTab => SelectedTab == NavTab.Play;

    public bool IsServersTab => SelectedTab == NavTab.Servers;

    public bool IsSettingsTab => SelectedTab == NavTab.Settings;

    public bool HasProfiles => Profiles.Count > 0;

    [RelayCommand]
    private void SelectTab(string tab)
    {
        if (Enum.TryParse<NavTab>(tab, out var parsed))
            SelectedTab = parsed;
    }

    [RelayCommand]
    private void SelectProfile(Profile profile) => SelectedProfile = profile;

    [RelayCommand]
    private void CreateProfile()
    {
        var name = string.IsNullOrWhiteSpace(NewProfileName) ? "New Profile" : NewProfileName;
        var version = string.IsNullOrWhiteSpace(NewProfileVersion) ? "1.21.1" : NewProfileVersion.Trim();

        var profile = _profileManager.Create(name, version);
        RefreshProfiles();
        SelectedProfile = Profiles.FirstOrDefault(i => i.Id == profile.Id);
        NewProfileName = string.Empty;
        StatusText = "Created " + profile.Name;
    }

    [RelayCommand]
    private void DeleteProfile(Profile profile)
    {
        _profileManager.Delete(profile.Id);
        RefreshProfiles();
        StatusText = "Deleted " + profile.Name;
    }

    [RelayCommand]
    private async Task LaunchAsync(CancellationToken cancellationToken)
    {
        if (IsBusy)
            return;

        if (SelectedProfile is null)
        {
            StatusText = "Select a profile first";
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
            await _launcher.LaunchAsync(SelectedProfile, session, progress, cancellationToken);

            _profileManager.MarkPlayed(SelectedProfile);
            _accounts.MarkUsed(account);
            StatusText = "Launched " + SelectedProfile.Name;
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
        ThemeManager.Shared.Apply(theme);
        StatusText = "Applied theme " + theme.Name;
    }

    private void RefreshProfiles()
    {
        Profiles.Clear();
        foreach (var profile in _profileManager.LoadAll())
            Profiles.Add(profile);

        SelectedProfile ??= Profiles.FirstOrDefault();
        OnPropertyChanged(nameof(HasProfiles));
    }

    partial void OnSelectedTabChanged(NavTab value)
    {
        OnPropertyChanged(nameof(IsPlayTab));
        OnPropertyChanged(nameof(IsServersTab));
        OnPropertyChanged(nameof(IsSettingsTab));
    }
}

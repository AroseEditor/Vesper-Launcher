using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vesper.Core.Accounts;
using Vesper.Core.Launching;
using Vesper.Core.Loaders;
using Vesper.Core.Profiles;
using Vesper.Core.Storage;
using Vesper.Core.Versions;

namespace Vesper.App.ViewModels;

public enum VersionCategory
{
    Vanilla,
    Vesper,
}

public sealed record LoaderChoice(LoaderKind Kind, bool IsEnhanced, string Label);

public partial class PlayViewModel : ObservableObject
{
    private readonly VesperPaths _paths;
    private readonly ProfileManager _profiles;
    private readonly AccountManager _accounts;
    private readonly LaunchService _launcher;
    private readonly VersionCatalog _catalog;
    private readonly LoaderRegistry _loaders;

    private IReadOnlyList<MinecraftVersionInfo> _allVersions = [];

    [ObservableProperty]
    private VersionCategory _category = VersionCategory.Vanilla;

    [ObservableProperty]
    private VersionCardViewModel? _selectedCard;

    [ObservableProperty]
    private MinecraftVersionInfo? _selectedVersion;

    [ObservableProperty]
    private LoaderKind? _selectedLoader = LoaderKind.Vanilla;

    [ObservableProperty]
    private LoaderChoice? _selectedLoaderChoice;

    [ObservableProperty]
    private LoaderVersion? _selectedLoaderVersion;

    [ObservableProperty]
    private Profile? _selectedProfile;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _newProfileName = string.Empty;

    [ObservableProperty]
    private bool _isCreatingProfile;

    [ObservableProperty]
    private bool _isLoadingVersions;

    [ObservableProperty]
    private bool _isLoadingLoaders;

    [ObservableProperty]
    private bool _isBusy;

    public string LaunchButtonText => IsBusy ? "LOADING" : "PLAY";

    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(LaunchButtonText));

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private string _statusText = "Ready";

    public PlayViewModel(
        VesperPaths paths,
        ProfileManager profiles,
        AccountManager accounts,
        LaunchService launcher)
    {
        _paths = paths;
        _profiles = profiles;
        _accounts = accounts;
        _launcher = launcher;
        _catalog = new VersionCatalog(paths);
        _loaders = new LoaderRegistry(paths);
        Mods = new ModsViewModel(paths);

        RebuildLoaderOptions();
        RefreshProfiles();
    }

    public ModsViewModel Mods { get; }

    public ObservableCollection<VersionCardViewModel> Cards { get; } = [];

    public ObservableCollection<MinecraftVersionInfo> GroupVersions { get; } = [];

    public ObservableCollection<LoaderChoice> AvailableLoaders { get; } = [];

    public ObservableCollection<LoaderVersion> LoaderVersions { get; } = [];

    public ObservableCollection<Profile> Profiles { get; } = [];

    public bool IsVanilla => Category == VersionCategory.Vanilla;

    public bool IsVesper => Category == VersionCategory.Vesper;

    public bool HasSelection => SelectedProfile is not null || SelectedVersion is not null;

    public LoaderKind EffectiveLoader => SelectedLoader ?? LoaderKind.Vanilla;

    public bool NeedsLoaderVersion => EffectiveLoader != LoaderKind.Vanilla;

    public bool HasGroupVersions => GroupVersions.Count > 0;

    public bool HasLoaderVersions => LoaderVersions.Count > 0;

    public string VersionPlaceholder => IsLoadingVersions ? "Loading versions" : "Pick a version";

    public string LoaderVersionPlaceholder => IsLoadingLoaders
        ? "Loading builds"
        : HasLoaderVersions
            ? "Pick a build"
            : "No builds for this version";

    public string SelectedSummary => SelectedProfile is not null
        ? SelectedProfile.Name
        : SelectedVersion is null
            ? "Pick a version"
            : EffectiveLoader == LoaderKind.Vanilla
                ? "Vanilla " + SelectedVersion.Id
                : $"{(Category == VersionCategory.Vesper ? "Enhanced" : "Vanilla")} + " +
                  $"{EffectiveLoader.DisplayName()} {SelectedVersion.Id}";

    public string CategoryBlurb => Category == VersionCategory.Vesper
        ? "Enhanced instances bundle our client mod and a curated performance pack. Fabric, Forge or NeoForge on supported versions."
        : "Every version Mojang has published, with any loader you like.";

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsLoadingVersions = true;
        StatusText = "Loading versions";

        try
        {
            _allVersions = await _catalog.GetAllAsync(cancellationToken);
            RebuildVersionDropdown();
            RebuildCards();
            StatusText = _allVersions.Count > 0
                ? $"{_allVersions.Count} versions available (from 1.21 down to 1.8.2 & older)"
                : "Could not reach Mojang. Check your connection.";
        }
        catch (Exception e)
        {
            StatusText = "Could not load versions: " + e.Message;
        }
        finally
        {
            IsLoadingVersions = false;
        }
    }

    [RelayCommand]
    private void SetCategory(string category)
    {
        if (Enum.TryParse<VersionCategory>(category, out var parsed))
            Category = parsed;
    }

    [RelayCommand]
    private void SelectCard(VersionCardViewModel card) => SelectedCard = card;

    [RelayCommand]
    private void SelectProfile(Profile profile)
    {
        SelectedProfile = profile;
        Category = profile.IsVesperProfile ? VersionCategory.Vesper : VersionCategory.Vanilla;

        var version = GroupVersions.FirstOrDefault(v => v.Id == profile.MinecraftVersion);
        if (version is not null)
            SelectedVersion = version;

        SelectedLoader = profile.Loader;
        StatusText = "Selected " + profile.Name;
    }

    [RelayCommand]
    private void OpenMods()
    {
        var profile = SelectedProfile ?? ResolveEphemeralProfile();
        RefreshProfiles();
        Mods.Open(profile);
    }

    [RelayCommand]
    private void BeginCreateProfile()
    {
        IsCreatingProfile = true;
        NewProfileName = string.Empty;
        if (SelectedVersion is null && GroupVersions.Count > 0)
            SelectedVersion = GroupVersions.FirstOrDefault();
    }

    [RelayCommand]
    private void CancelCreateProfile()
    {
        IsCreatingProfile = false;
        NewProfileName = string.Empty;
    }

    [RelayCommand]
    private void ConfirmCreateProfile()
    {
        var mcVersion = SelectedVersion?.Id ?? "1.21.1";

        var name = string.IsNullOrWhiteSpace(NewProfileName)
            ? $"{EffectiveLoader.DisplayName()} {mcVersion}"
            : NewProfileName.Trim();

        var profile = _profiles.Create(
            name,
            mcVersion,
            EffectiveLoader,
            SelectedLoaderVersion?.Version,
            SelectedLoaderChoice?.IsEnhanced ?? false);

        RefreshProfiles();
        SelectedProfile = Profiles.FirstOrDefault(p => p.Id == profile.Id);
        IsCreatingProfile = false;
        NewProfileName = string.Empty;
        StatusText = "Created instance " + profile.Name;
    }

    [RelayCommand]
    private void DeleteProfile(Profile profile)
    {
        _profiles.Delete(profile.Id);

        if (SelectedProfile?.Id == profile.Id)
            SelectedProfile = null;

        RefreshProfiles();
        StatusText = "Deleted " + profile.Name;
    }

    [RelayCommand]
    private async Task LaunchAsync(CancellationToken cancellationToken)
    {
        if (IsBusy)
            return;

        var account = _accounts.Selected;

        if (account is null)
        {
            StatusText = "Add an account first to play";
            return;
        }

        var launchingSaved = SelectedProfile is not null;

        if (!launchingSaved && SelectedVersion is null)
        {
            StatusText = "Select an instance or version first";
            return;
        }

        IsBusy = true;
        Progress = 0;

        try
        {
            var profile = SelectedProfile ?? ResolveEphemeralProfile();

            if (!launchingSaved)
            {
                profile.MinecraftVersion = SelectedVersion!.Id;
                profile.Loader = EffectiveLoader;
                profile.LoaderVersion = SelectedLoaderVersion?.Version;
                profile.IsVesperProfile = Category == VersionCategory.Vesper;
            }

            if (profile.Loader == LoaderKind.Vanilla && !profile.IsVesperProfile)
            {
                profile.LaunchVersionId = null;
            }
            else if (string.IsNullOrEmpty(profile.LaunchVersionId))
            {
                if (!_loaders.Supports(profile.Loader))
                    throw new InvalidOperationException(
                        profile.Loader.DisplayName() + " is not supported yet");

                var loaderVersion = profile.LoaderVersion;

                if (string.IsNullOrEmpty(loaderVersion))
                {
                    var builds = await _loaders.For(profile.Loader)
                        .ListVersionsAsync(profile.MinecraftVersion, cancellationToken);

                    loaderVersion = builds.FirstOrDefault(b => b.IsStable)?.Version
                        ?? builds.FirstOrDefault()?.Version
                        ?? throw new InvalidOperationException(
                            "No " + profile.Loader.DisplayName() +
                            " build is available for Minecraft " + profile.MinecraftVersion);

                    profile.LoaderVersion = loaderVersion;
                }

                StatusText = $"Installing {profile.Loader.DisplayName()} {loaderVersion}";
                var installProgress = new Progress<string>(m => StatusText = m);
                profile.LaunchVersionId = await _loaders.For(profile.Loader)
                    .InstallAsync(profile.MinecraftVersion, loaderVersion, cancellationToken, installProgress);
            }

            if (Category == VersionCategory.Vesper)
            {
                var bundle = new VesperProfileInstaller(_paths);
                var bundleProgress = new Progress<string>(message => StatusText = message);

                var report = await bundle.InstallBundleAsync(
                    profile, profile.InstallPerformanceBundle, bundleProgress, cancellationToken);

                if (report.Failed.Count > 0)
                    StatusText = report.Failed[0];
            }

            _profiles.Save(profile);

            var progress = new Progress<LaunchProgress>(p =>
            {
                Progress = p.Ratio * 100;
                StatusText = p.Describe();
            });

            var session = AccountManager.CreateSession(account);
            await _launcher.LaunchAsync(profile, session, progress, cancellationToken, account);

            _profiles.MarkPlayed(profile);
            _accounts.MarkUsed(account);
            RefreshProfiles();
            StatusText = "Launched " + profile.Name;
        }
        catch (OperationCanceledException)
        {
            StatusText = "Launch cancelled";
        }
        catch (Exception e)
        {
            StatusText = "Launch failed: " + e.Message;
            Vesper.Core.Diagnostics.ErrorService.Shared.Report("Launch failed", e);
        }
        finally
        {
            IsBusy = false;
            Progress = 0;
        }
    }

    private Profile ResolveEphemeralProfile()
    {
        var version = SelectedVersion?.Id ?? "1.21.1";
        var existing = _profiles.LoadAll().FirstOrDefault(p =>
            p.MinecraftVersion == version &&
            p.Loader == EffectiveLoader &&
            p.IsVesperProfile == (Category == VersionCategory.Vesper));

        return existing ?? _profiles.Create(
            SelectedSummary,
            version,
            EffectiveLoader,
            SelectedLoaderVersion?.Version,
            Category == VersionCategory.Vesper);
    }

    public bool HasNoProfiles => Profiles.Count == 0;

    private void RefreshProfiles()
    {
        Profiles.Clear();
        var all = _profiles.LoadAll();

        foreach (var profile in all)
            Profiles.Add(profile);

        if (SelectedProfile is null || !Profiles.Any(p => p.Id == SelectedProfile.Id))
            SelectedProfile = Profiles.FirstOrDefault();

        OnPropertyChanged(nameof(HasNoProfiles));
    }

    private void RebuildVersionDropdown()
    {
        GroupVersions.Clear();
        var releases = _allVersions.Where(v => v.IsRelease).ToList();

        foreach (var release in releases)
            GroupVersions.Add(release);

        OnPropertyChanged(nameof(HasGroupVersions));
        OnPropertyChanged(nameof(VersionPlaceholder));

        if (SelectedVersion is null && GroupVersions.Count > 0)
            SelectedVersion = GroupVersions.FirstOrDefault();
    }

    private void RebuildCards()
    {
        var filtered = _allVersions.AsEnumerable();

        if (Category == VersionCategory.Vesper)
            filtered = filtered.Where(v => v.IsRelease && IsModernRelease(v.Id));

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var term = SearchText.Trim();
            filtered = filtered.Where(v => v.Id.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        var groups = VersionCatalog.Group(filtered);

        Cards.Clear();
        foreach (var group in groups)
            Cards.Add(new VersionCardViewModel(group));

        SelectedCard = Cards.FirstOrDefault(c => c.Name == SelectedCard?.Name) ?? Cards.FirstOrDefault();
    }

    public static bool IsModernRelease(string id)
    {
        var parts = id.Split('.');

        if (parts.Length < 2 || !int.TryParse(parts[0], out var major))
            return false;

        if (!int.TryParse(parts[1], out var minor))
            return false;

        return major > 1 || minor >= 21;
    }

    public bool EnhancedUnavailable =>
        SelectedVersion is not null && !EnhancedClient.Supports(SelectedVersion.Id);

    public string EnhancedHint =>
        "The Enhanced client supports " + string.Join(", ", EnhancedClient.SupportedVersions) +
        ". Pick one of those versions to use it.";

    private void RebuildLoaderOptions()
    {
        var previousKind = SelectedLoaderChoice?.Kind ?? SelectedLoader;
        var previousEnhanced = SelectedLoaderChoice?.IsEnhanced ?? false;

        AvailableLoaders.Clear();

        foreach (var kind in Enum.GetValues<LoaderKind>())
            AvailableLoaders.Add(new LoaderChoice(kind, false, kind.DisplayName()));

        if (SelectedVersion is not null && EnhancedClient.Supports(SelectedVersion.Id))
        {
            foreach (var kind in new[] { LoaderKind.Fabric, LoaderKind.NeoForge })
                AvailableLoaders.Add(new LoaderChoice(kind, true, "Enhanced (" + kind.DisplayName() + ")"));
        }

        SelectedLoaderChoice =
            AvailableLoaders.FirstOrDefault(c => c.Kind == previousKind && c.IsEnhanced == previousEnhanced)
            ?? AvailableLoaders.FirstOrDefault();
    }

    private async Task RefreshLoaderVersionsAsync()
    {
        LoaderVersions.Clear();
        SelectedLoaderVersion = null;

        if (SelectedVersion is null || EffectiveLoader == LoaderKind.Vanilla)
        {
            OnPropertyChanged(nameof(NeedsLoaderVersion));
            return;
        }

        if (!_loaders.Supports(EffectiveLoader))
        {
            StatusText = EffectiveLoader.DisplayName() + " support is not wired up yet";
            return;
        }

        IsLoadingLoaders = true;

        try
        {
            var versions = await _loaders.For(EffectiveLoader).ListVersionsAsync(SelectedVersion.Id);

            foreach (var version in versions)
                LoaderVersions.Add(version);

            SelectedLoaderVersion = LoaderVersions.FirstOrDefault(v => v.IsStable)
                                    ?? LoaderVersions.FirstOrDefault();
        }
        catch (Exception e)
        {
            StatusText = e.Message;
        }
        finally
        {
            IsLoadingLoaders = false;
            OnPropertyChanged(nameof(NeedsLoaderVersion));
            OnPropertyChanged(nameof(HasLoaderVersions));
            OnPropertyChanged(nameof(LoaderVersionPlaceholder));
        }
    }

    partial void OnIsLoadingVersionsChanged(bool value) =>
        OnPropertyChanged(nameof(VersionPlaceholder));

    partial void OnIsLoadingLoadersChanged(bool value) =>
        OnPropertyChanged(nameof(LoaderVersionPlaceholder));

    partial void OnCategoryChanged(VersionCategory value)
    {
        OnPropertyChanged(nameof(IsVanilla));
        OnPropertyChanged(nameof(IsVesper));
        OnPropertyChanged(nameof(EnhancedUnavailable));
        OnPropertyChanged(nameof(CategoryBlurb));
        RebuildCards();
        RebuildLoaderOptions();
    }

    partial void OnSearchTextChanged(string value) => RebuildCards();

    partial void OnSelectedCardChanged(VersionCardViewModel? value)
    {
        if (value is null)
            return;

        var v = GroupVersions.FirstOrDefault(ver => ver.Id == value.Group.Newest.Id);
        if (v is not null)
            SelectedVersion = v;
    }

    partial void OnSelectedVersionChanged(MinecraftVersionInfo? value)
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectedSummary));
        OnPropertyChanged(nameof(EnhancedUnavailable));
        RebuildLoaderOptions();
        _ = RefreshLoaderVersionsAsync();
    }

    partial void OnSelectedLoaderChanged(LoaderKind? value)
    {
        OnPropertyChanged(nameof(EffectiveLoader));
        OnPropertyChanged(nameof(SelectedSummary));
        OnPropertyChanged(nameof(NeedsLoaderVersion));
        _ = RefreshLoaderVersionsAsync();
    }

    partial void OnSelectedLoaderChoiceChanged(LoaderChoice? value) =>
        SelectedLoader = value?.Kind ?? LoaderKind.Vanilla;
}

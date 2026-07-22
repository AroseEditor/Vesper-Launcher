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

    public ObservableCollection<LoaderKind> AvailableLoaders { get; } = [];

    public ObservableCollection<LoaderVersion> LoaderVersions { get; } = [];

    public ObservableCollection<Profile> Profiles { get; } = [];

    public bool IsVanilla => Category == VersionCategory.Vanilla;

    public bool IsVesper => Category == VersionCategory.Vesper;

    public bool HasSelection => SelectedVersion is not null;

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

    public string SelectedSummary => SelectedVersion is null
        ? "Pick a version"
        : EffectiveLoader == LoaderKind.Vanilla
            ? "Vanilla " + SelectedVersion.Id
            : $"{(Category == VersionCategory.Vesper ? "Vesper" : "Vanilla")} + " +
              $"{EffectiveLoader.DisplayName()} {SelectedVersion.Id}";

    public string CategoryBlurb => Category == VersionCategory.Vesper
        ? "Vesper profiles bundle our client mod and a curated performance pack. Fabric or NeoForge, 1.21 and newer."
        : "Every version Mojang has published, with any loader you like.";

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsLoadingVersions = true;
        StatusText = "Loading versions";

        try
        {
            _allVersions = await _catalog.GetAllAsync(cancellationToken);
            RebuildCards();
            StatusText = _allVersions.Count > 0
                ? $"{_allVersions.Count} versions available"
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

        var card = Cards.FirstOrDefault(c =>
            c.Group.Versions.Any(v => v.Id == profile.MinecraftVersion));

        if (card is not null)
        {
            SelectedCard = card;
            SelectedVersion = GroupVersions.FirstOrDefault(v => v.Id == profile.MinecraftVersion);
        }

        SelectedLoader = profile.Loader;
        StatusText = "Loaded profile " + profile.Name;
    }

    [RelayCommand]
    private void OpenMods()
    {
        if (SelectedVersion is null)
        {
            StatusText = "Pick a version first";
            return;
        }

        if (EffectiveLoader == LoaderKind.Vanilla)
        {
            StatusText = "Pick a mod loader before adding mods";
            return;
        }

        var profile = ResolveEphemeralProfile();
        RefreshProfiles();
        Mods.Open(profile);
    }

    [RelayCommand]
    private void BeginCreateProfile()
    {
        IsCreatingProfile = true;
        NewProfileName = string.Empty;
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
        if (SelectedVersion is null)
        {
            StatusText = "Pick a version first";
            return;
        }

        var name = string.IsNullOrWhiteSpace(NewProfileName)
            ? SelectedSummary
            : NewProfileName.Trim();

        var profile = _profiles.Create(
            name,
            SelectedVersion.Id,
            EffectiveLoader,
            SelectedLoaderVersion?.Version,
            Category == VersionCategory.Vesper);

        RefreshProfiles();
        SelectedProfile = Profiles.FirstOrDefault(p => p.Id == profile.Id);
        IsCreatingProfile = false;
        NewProfileName = string.Empty;
        StatusText = "Created profile " + profile.Name;
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
            StatusText = "Add an account first";
            return;
        }

        if (SelectedVersion is null)
        {
            StatusText = "Pick a version first";
            return;
        }

        IsBusy = true;
        Progress = 0;

        try
        {
            var profile = SelectedProfile ?? ResolveEphemeralProfile();

            profile.MinecraftVersion = SelectedVersion.Id;
            profile.Loader = EffectiveLoader;
            profile.LoaderVersion = SelectedLoaderVersion?.Version;
            profile.IsVesperProfile = Category == VersionCategory.Vesper;

            if (EffectiveLoader == LoaderKind.Vanilla)
            {
                profile.LaunchVersionId = null;
            }
            else
            {
                StatusText = $"Installing {EffectiveLoader.DisplayName()}";
                var installer = _loaders.For(EffectiveLoader);
                profile.LaunchVersionId = await installer.InstallAsync(
                    SelectedVersion.Id,
                    SelectedLoaderVersion?.Version
                        ?? throw new InvalidOperationException("Select a loader version"),
                    cancellationToken);
            }

            _profiles.Save(profile);

            var progress = new Progress<LaunchProgress>(p =>
            {
                Progress = p.Ratio * 100;
                StatusText = p.Describe();
            });

            var session = AccountManager.CreateSession(account);
            await _launcher.LaunchAsync(profile, session, progress, cancellationToken);

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
        }
        finally
        {
            IsBusy = false;
            Progress = 0;
        }
    }

    private Profile ResolveEphemeralProfile()
    {
        var existing = _profiles.LoadAll().FirstOrDefault(p =>
            p.MinecraftVersion == SelectedVersion!.Id &&
            p.Loader == EffectiveLoader &&
            p.IsVesperProfile == (Category == VersionCategory.Vesper));

        return existing ?? _profiles.Create(
            SelectedSummary,
            SelectedVersion!.Id,
            EffectiveLoader,
            SelectedLoaderVersion?.Version,
            Category == VersionCategory.Vesper);
    }

    private void RefreshProfiles()
    {
        Profiles.Clear();
        foreach (var profile in _profiles.LoadAll())
            Profiles.Add(profile);
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

    private void RebuildLoaderOptions()
    {
        var desired = Category == VersionCategory.Vesper
            ? new[] { LoaderKind.Fabric, LoaderKind.NeoForge }
            : Enum.GetValues<LoaderKind>();

        var previous = SelectedLoader;

        for (var i = AvailableLoaders.Count - 1; i >= 0; i--)
        {
            if (!desired.Contains(AvailableLoaders[i]))
                AvailableLoaders.RemoveAt(i);
        }

        foreach (var kind in desired)
        {
            if (!AvailableLoaders.Contains(kind))
                AvailableLoaders.Add(kind);
        }

        SelectedLoader = previous is not null && AvailableLoaders.Contains(previous.Value)
            ? previous
            : AvailableLoaders.FirstOrDefault();
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
        OnPropertyChanged(nameof(CategoryBlurb));
        RebuildCards();
        RebuildLoaderOptions();
    }

    partial void OnSearchTextChanged(string value) => RebuildCards();

    partial void OnSelectedCardChanged(VersionCardViewModel? value)
    {
        GroupVersions.Clear();

        if (value is null)
        {
            SelectedVersion = null;
            return;
        }

        foreach (var version in value.Group.Versions)
            GroupVersions.Add(version);

        SelectedVersion = GroupVersions.FirstOrDefault();
        OnPropertyChanged(nameof(HasGroupVersions));
    }

    partial void OnSelectedVersionChanged(MinecraftVersionInfo? value)
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectedSummary));
        _ = RefreshLoaderVersionsAsync();
    }

    partial void OnSelectedLoaderChanged(LoaderKind? value)
    {
        OnPropertyChanged(nameof(EffectiveLoader));
        OnPropertyChanged(nameof(SelectedSummary));
        OnPropertyChanged(nameof(NeedsLoaderVersion));
        _ = RefreshLoaderVersionsAsync();
    }
}

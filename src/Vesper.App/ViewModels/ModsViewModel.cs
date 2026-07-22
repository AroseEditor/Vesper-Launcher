using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vesper.Core.Mods;
using Vesper.Core.Profiles;
using Vesper.Core.Storage;

namespace Vesper.App.ViewModels;

public partial class ModsViewModel : ObservableObject
{
    private readonly VesperPaths _paths;
    private readonly ModrinthApi _modrinth = new();

    private Profile? _profile;

    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    private bool _isBrowserOpen;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private ModSource _selectedSource = ModSource.Modrinth;

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private bool _isInstalling;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private string _curseForgeApiKey = string.Empty;

    public ModsViewModel(VesperPaths paths) => _paths = paths;

    public ObservableCollection<ModFile> Mods { get; } = [];

    public ObservableCollection<ModSearchResult> SearchResults { get; } = [];

    public string ProfileName => _profile?.Name ?? "No profile";

    public string TargetLabel => _profile is null
        ? string.Empty
        : $"{_profile.MinecraftVersion}  {_profile.Loader.DisplayName()}";

    public bool HasMods => Mods.Count > 0;

    public bool IsModrinth => SelectedSource == ModSource.Modrinth;

    public bool IsCurseForge => SelectedSource == ModSource.CurseForge;

    public string ModsDirectory => _profile is null ? string.Empty : _paths.ProfileModsDir(_profile.Id);

    public void Open(Profile? profile)
    {
        _profile = profile;

        OnPropertyChanged(nameof(ProfileName));
        OnPropertyChanged(nameof(TargetLabel));
        OnPropertyChanged(nameof(ModsDirectory));

        if (profile is null)
        {
            StatusText = "Create or pick a profile first";
            Mods.Clear();
            OnPropertyChanged(nameof(HasMods));
            IsOpen = true;
            return;
        }

        Refresh();
        IsOpen = true;
    }

    [RelayCommand]
    private void Close()
    {
        IsBrowserOpen = false;
        IsOpen = false;
    }

    [RelayCommand]
    private void Refresh()
    {
        Mods.Clear();

        if (_profile is null)
        {
            OnPropertyChanged(nameof(HasMods));
            return;
        }

        var directory = _paths.ProfileModsDir(_profile.Id);
        Directory.CreateDirectory(directory);

        foreach (var mod in ModScanner.Scan(directory))
            Mods.Add(mod);

        OnPropertyChanged(nameof(HasMods));
        StatusText = Mods.Count == 1 ? "1 mod installed" : $"{Mods.Count} mods installed";
    }

    [RelayCommand]
    private void OpenFolder()
    {
        if (_profile is null)
            return;

        var directory = _paths.ProfileModsDir(_profile.Id);
        Directory.CreateDirectory(directory);

        try
        {
            Process.Start(new ProcessStartInfo { FileName = directory, UseShellExecute = true });
        }
        catch (Exception e)
        {
            StatusText = "Could not open the folder: " + e.Message;
        }
    }

    [RelayCommand]
    private void RemoveMod(ModFile mod)
    {
        try
        {
            if (File.Exists(mod.Path))
                File.Delete(mod.Path);

            Mods.Remove(mod);
            OnPropertyChanged(nameof(HasMods));
            StatusText = "Removed " + mod.DisplayName;
        }
        catch (Exception e)
        {
            StatusText = "Could not remove it: " + e.Message;
        }
    }

    [RelayCommand]
    private void ToggleMod(ModFile mod)
    {
        try
        {
            var target = mod.IsDisabled
                ? mod.Path[..^ModScanner.DisabledSuffix.Length]
                : mod.Path + ModScanner.DisabledSuffix;

            File.Move(mod.Path, target, overwrite: true);
            Refresh();
        }
        catch (Exception e)
        {
            StatusText = "Could not change it: " + e.Message;
        }
    }

    [RelayCommand]
    private void OpenBrowser()
    {
        if (_profile is null)
        {
            StatusText = "Create or pick a profile first";
            return;
        }

        IsBrowserOpen = true;
        StatusText = "Search for a mod to add";
    }

    [RelayCommand]
    private void CloseBrowser()
    {
        IsBrowserOpen = false;
        Refresh();
    }

    [RelayCommand]
    private void SetSource(string source)
    {
        if (Enum.TryParse<ModSource>(source, out var parsed))
            SelectedSource = parsed;
    }

    [RelayCommand]
    private async Task SearchAsync(CancellationToken cancellationToken)
    {
        if (_profile is null || IsSearching)
            return;

        IsSearching = true;
        SearchResults.Clear();

        try
        {
            var provider = Provider();

            if (!provider.IsAvailable)
            {
                StatusText = provider.UnavailableReason;
                return;
            }

            var results = await provider.SearchAsync(
                SearchQuery, _profile.MinecraftVersion, LoaderSlug(), cancellationToken);

            foreach (var result in results)
                SearchResults.Add(result);

            StatusText = results.Count == 0
                ? "Nothing matched that search"
                : $"{results.Count} results from {provider.Source}";
        }
        catch (Exception e)
        {
            StatusText = "Search failed: " + e.Message;
        }
        finally
        {
            IsSearching = false;
        }
    }

    [RelayCommand]
    private async Task InstallAsync(ModSearchResult result)
    {
        if (_profile is null || IsInstalling)
            return;

        IsInstalling = true;
        StatusText = "Downloading " + result.Title;

        try
        {
            var directory = _paths.ProfileModsDir(_profile.Id);
            var fileName = await Provider().InstallAsync(
                result, _profile.MinecraftVersion, LoaderSlug(), directory);

            StatusText = "Added " + fileName;
            Refresh();
        }
        catch (Exception e)
        {
            StatusText = e.Message;
        }
        finally
        {
            IsInstalling = false;
        }
    }

    private IModProvider Provider() => SelectedSource == ModSource.Modrinth
        ? _modrinth
        : new CurseForgeApi(CurseForgeApiKey);

    private string LoaderSlug() => _profile?.Loader switch
    {
        LoaderKind.Fabric => "fabric",
        LoaderKind.Forge => "forge",
        LoaderKind.NeoForge => "neoforge",
        LoaderKind.Quilt => "quilt",
        _ => "fabric",
    };

    partial void OnSelectedSourceChanged(ModSource value)
    {
        OnPropertyChanged(nameof(IsModrinth));
        OnPropertyChanged(nameof(IsCurseForge));
        SearchResults.Clear();
    }
}

using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vesper.App.Theming;
using Vesper.Core;
using Vesper.Core.Storage;
using Vesper.Core.Theming;

namespace Vesper.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly VesperPaths _paths;
    private readonly ThemeStore _themeStore;

    [ObservableProperty]
    private VesperTheme? _activeTheme;

    [ObservableProperty]
    private int _maximumRamMb = 4096;

    [ObservableProperty]
    private int _serverRamMb = 2048;

    [ObservableProperty]
    private int _screenWidth = 1280;

    [ObservableProperty]
    private int _screenHeight = 720;

    [ObservableProperty]
    private bool _fullScreen;

    [ObservableProperty]
    private string _customClientId = string.Empty;

    [ObservableProperty]
    private string _statusText = string.Empty;

    public SettingsViewModel(VesperPaths paths)
    {
        _paths = paths;
        _themeStore = new ThemeStore(paths);
        Editor = new ThemeEditorViewModel(paths);
        Editor.Saved += (_, _) => ReloadThemes();

        foreach (var theme in _themeStore.LoadAll())
            Themes.Add(theme);

        ActiveTheme = Themes.FirstOrDefault();
    }

    public ObservableCollection<VesperTheme> Themes { get; } = [];

    public ThemeEditorViewModel Editor { get; }

    private void ReloadThemes()
    {
        Themes.Clear();

        foreach (var theme in _themeStore.LoadAll())
            Themes.Add(theme);
    }

    public string RootDirectory => _paths.Root;

    public string Version => VesperInfo.Version;

    public string Author => "ayush.ue5";

    public int TotalRamMb => 16384;

    [RelayCommand]
    private void ApplyTheme(VesperTheme theme)
    {
        ActiveTheme = theme;
        ThemeManager.Shared.Apply(theme);
        StatusText = "Applied " + theme.Name;
    }

    [RelayCommand]
    private void CustomiseTheme() =>
        Editor.Open(ActiveTheme ?? ThemeManager.Shared.Current);

    [RelayCommand]
    private void OpenRootDirectory()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _paths.Root,
                UseShellExecute = true,
            });
        }
        catch (Exception e)
        {
            StatusText = "Could not open folder: " + e.Message;
        }
    }
}

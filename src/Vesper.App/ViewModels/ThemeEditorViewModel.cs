using System.Collections.ObjectModel;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vesper.App.Controls;
using Vesper.App.Theming;
using Vesper.Core.Storage;
using Vesper.Core.Theming;

namespace Vesper.App.ViewModels;

public partial class ThemeColorEntry : ObservableObject
{
    private readonly Action<ThemeColorEntry> _changed;

    [ObservableProperty]
    private string _value;

    public ThemeColorEntry(string token, string value, Action<ThemeColorEntry> changed)
    {
        Token = token;
        _value = value;
        _changed = changed;
    }

    public string Token { get; }

    public string Label => VesperTheme.Label(Token);

    public string Group => VesperTheme.Group(Token);

    public IBrush Swatch => Color.TryParse(Value, out var colour)
        ? new SolidColorBrush(colour)
        : Brushes.Magenta;

    public bool IsValid => Color.TryParse(Value, out _);

    partial void OnValueChanged(string value)
    {
        OnPropertyChanged(nameof(Swatch));
        OnPropertyChanged(nameof(IsValid));

        if (IsValid)
            _changed(this);
    }
}

public sealed class ThemeGroupEntry
{
    public ThemeGroupEntry(string name) => Name = name;

    public string Name { get; }

    public ObservableCollection<ThemeColorEntry> Colors { get; } = [];
}

public partial class ThemeEditorViewModel : ObservableObject
{
    private readonly ThemeStore _store;
    private VesperTheme _working = VesperTheme.MauveBlack();
    private bool _suppress;

    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    private string _themeName = "My Theme";

    [ObservableProperty]
    private double _logoHue;

    [ObservableProperty]
    private Bitmap? _logoPreview;

    [ObservableProperty]
    private string _statusText = string.Empty;

    public ThemeEditorViewModel(VesperPaths paths) => _store = new ThemeStore(paths);

    public ObservableCollection<ThemeGroupEntry> Groups { get; } = [];

    public event EventHandler? Saved;

    public void Open(VesperTheme source)
    {
        _suppress = true;

        _working = source.Clone(source.IsBuiltIn ? source.Name + " custom" : source.Name);
        ThemeName = _working.Name;
        LogoHue = _working.LogoHue;

        Groups.Clear();

        foreach (var groupName in VesperTheme.Groups)
        {
            var group = new ThemeGroupEntry(groupName);

            foreach (var token in VesperTheme.Tokens.Where(t => VesperTheme.Group(t) == groupName))
                group.Colors.Add(new ThemeColorEntry(token, _working.Resolve(token), OnColorChanged));

            if (group.Colors.Count > 0)
                Groups.Add(group);
        }

        _suppress = false;
        RefreshLogo();
        StatusText = "Changes preview live. Save to keep them.";
        IsOpen = true;
    }

    [RelayCommand]
    private void Close()
    {
        IsOpen = false;
        ThemeManager.Shared.Apply(ThemeManager.Shared.Current);
    }

    [RelayCommand]
    private void Save()
    {
        if (string.IsNullOrWhiteSpace(ThemeName))
        {
            StatusText = "Give the theme a name first";
            return;
        }

        _working.Name = ThemeName.Trim();
        _working.LogoHue = LogoHue;

        foreach (var entry in Groups.SelectMany(g => g.Colors))
            _working.Colors[entry.Token] = entry.Value;

        try
        {
            _store.Save(_working);
            ThemeManager.Shared.Apply(_working);
            StatusText = "Saved " + _working.Name;
            Saved?.Invoke(this, EventArgs.Empty);
            IsOpen = false;
        }
        catch (Exception e)
        {
            StatusText = "Could not save: " + e.Message;
        }
    }

    [RelayCommand]
    private void ResetToBase()
    {
        var defaults = VesperTheme.MauveBlack();

        _suppress = true;

        foreach (var entry in Groups.SelectMany(g => g.Colors))
            entry.Value = defaults.Resolve(entry.Token);

        LogoHue = 0;
        _suppress = false;

        Preview();
        StatusText = "Reset to the Mauve Black palette";
    }

    [RelayCommand]
    private void ShiftHue(string amount)
    {
        if (double.TryParse(amount, out var delta))
            LogoHue = Math.Round((LogoHue + delta + 360) % 360);
    }

    private void OnColorChanged(ThemeColorEntry entry)
    {
        if (_suppress)
            return;

        _working.Colors[entry.Token] = entry.Value;
        Preview();
    }

    private void Preview()
    {
        _working.LogoHue = LogoHue;
        ThemeManager.Shared.Apply(_working, persistCurrent: false);
    }

    private void RefreshLogo()
    {
        try
        {
            LogoPreview = LogoTint.Rotate(LogoHue);
        }
        catch (Exception)
        {
            LogoPreview = null;
        }
    }

    partial void OnLogoHueChanged(double value)
    {
        RefreshLogo();

        if (!_suppress)
            Preview();
    }
}

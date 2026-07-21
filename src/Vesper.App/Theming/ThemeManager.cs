using Avalonia;
using Avalonia.Media;
using Vesper.Core.Theming;

namespace Vesper.App.Theming;

public sealed class ThemeManager
{
    public static ThemeManager Instance { get; } = new();

    public VesperTheme Current { get; private set; } = VesperTheme.MauveBlack();

    public event EventHandler<VesperTheme>? Changed;

    public void Apply(VesperTheme theme)
    {
        var resources = Application.Current?.Resources;
        if (resources is null)
            return;

        foreach (var token in VesperTheme.Tokens)
        {
            var key = VesperTheme.ResourceKey(token);
            var color = ParseColor(theme.Resolve(token));

            resources[key + "Color"] = color;
            resources[key + "Brush"] = new SolidColorBrush(color);
        }

        resources["AccentGlowBrush"] = new SolidColorBrush(
            ParseColor(theme.Resolve("accent")), 0.28);

        Current = theme;
        Changed?.Invoke(this, theme);
    }

    private static Color ParseColor(string value) =>
        Color.TryParse(value, out var color) ? color : Colors.Magenta;
}

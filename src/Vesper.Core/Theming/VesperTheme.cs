namespace Vesper.Core.Theming;

public sealed class VesperTheme
{
    public const string DefaultName = "Mauve Black";

    public string Name { get; set; } = DefaultName;

    public bool IsBuiltIn { get; set; }

    public Dictionary<string, string> Colors { get; set; } = [];

    public double LogoHue { get; set; }

    public static IReadOnlyList<string> Tokens { get; } =
    [
        "bgBase",
        "bgElevated",
        "bgCard",
        "bgHover",
        "borderSubtle",
        "borderStrong",
        "accent",
        "accentHover",
        "accentDeep",
        "accentBreath",
        "accentContrast",
        "textPrimary",
        "textMuted",
        "textFaint",
        "success",
        "warning",
        "danger",
    ];

    public static VesperTheme MauveBlack() => new()
    {
        Name = DefaultName,
        IsBuiltIn = true,
        Colors = new Dictionary<string, string>
        {
            ["bgBase"] = "#0A0A0C",
            ["bgElevated"] = "#121216",
            ["bgCard"] = "#1A1A20",
            ["bgHover"] = "#23232B",
            ["borderSubtle"] = "#26262F",
            ["borderStrong"] = "#3A3A47",
            ["accent"] = "#B57EDC",
            ["accentHover"] = "#C9A0DC",
            ["accentDeep"] = "#9A5FC4",
            ["accentBreath"] = "#D14FE8",
            ["accentContrast"] = "#12060F",
            ["textPrimary"] = "#F2EEF6",
            ["textMuted"] = "#A9A2B5",
            ["textFaint"] = "#6E6880",
            ["success"] = "#63D29B",
            ["warning"] = "#E0B252",
            ["danger"] = "#E5646B",
        },
    };

    public static VesperTheme MidnightEmber() => new()
    {
        Name = "Midnight Ember",
        IsBuiltIn = true,
        LogoHue = -78,
        Colors = new Dictionary<string, string>
        {
            ["bgBase"] = "#0B0907",
            ["bgElevated"] = "#151110",
            ["bgCard"] = "#1E1917",
            ["bgHover"] = "#2A2320",
            ["borderSubtle"] = "#2E2724",
            ["borderStrong"] = "#453B36",
            ["accent"] = "#E8853F",
            ["accentHover"] = "#F2A265",
            ["accentDeep"] = "#C56526",
            ["accentBreath"] = "#FF6B3D",
            ["accentContrast"] = "#140803",
            ["textPrimary"] = "#F7F0EA",
            ["textMuted"] = "#B4A79D",
            ["textFaint"] = "#7A6C63",
            ["success"] = "#63D29B",
            ["warning"] = "#E0B252",
            ["danger"] = "#E5646B",
        },
    };

    public static IReadOnlyList<VesperTheme> BuiltIn() => [MauveBlack(), MidnightEmber()];

    public string Resolve(string token)
    {
        if (Colors.TryGetValue(token, out var value) && !string.IsNullOrWhiteSpace(value))
            return value;

        var fallback = MauveBlack();
        return fallback.Colors.TryGetValue(token, out var defaultValue) ? defaultValue : "#FF00FF";
    }

    public VesperTheme Clone(string name) => new()
    {
        Name = name,
        IsBuiltIn = false,
        LogoHue = LogoHue,
        Colors = new Dictionary<string, string>(Colors),
    };

    public static string Label(string token) => token switch
    {
        "bgBase" => "Window background",
        "bgElevated" => "Panels and title bar",
        "bgCard" => "Cards and popovers",
        "bgHover" => "Hover highlight",
        "borderSubtle" => "Dividers",
        "borderStrong" => "Popover edges",
        "accent" => "Accent",
        "accentHover" => "Accent hover",
        "accentDeep" => "Accent deep",
        "accentBreath" => "Accent glow",
        "accentContrast" => "Text on accent",
        "textPrimary" => "Main text",
        "textMuted" => "Secondary text",
        "textFaint" => "Labels and hints",
        "success" => "Success",
        "warning" => "Warning",
        "danger" => "Danger",
        _ => token,
    };

    public static string Group(string token) => token switch
    {
        "bgBase" or "bgElevated" or "bgCard" or "bgHover" => "Backgrounds",
        "borderSubtle" or "borderStrong" => "Borders",
        "accent" or "accentHover" or "accentDeep" or "accentBreath" or "accentContrast" => "Accents",
        "textPrimary" or "textMuted" or "textFaint" => "Text",
        _ => "Status",
    };

    public static IReadOnlyList<string> Groups { get; } =
        ["Backgrounds", "Accents", "Text", "Borders", "Status"];

    public static string ResourceKey(string token) =>
        char.ToUpperInvariant(token[0]) + token[1..];
}

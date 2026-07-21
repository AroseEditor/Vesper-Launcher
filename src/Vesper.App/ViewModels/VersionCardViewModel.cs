using Avalonia;
using Avalonia.Media;
using Vesper.Core.Versions;

namespace Vesper.App.ViewModels;

public sealed class VersionCardViewModel
{
    private static readonly (string From, string To)[] Palette =
    [
        ("#B57EDC", "#5E3A86"),
        ("#D14FE8", "#6B2E8A"),
        ("#9A5FC4", "#3E2560"),
        ("#C9A0DC", "#6E4A94"),
        ("#8E6BD6", "#39265F"),
        ("#E06AD8", "#5A2B72"),
    ];

    public VersionCardViewModel(VersionGroup group)
    {
        Group = group;

        var index = Math.Abs(group.Name.GetHashCode()) % Palette.Length;
        var (from, to) = Palette[index];

        Background = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.Parse(from), 0),
                new GradientStop(Color.Parse(to), 1),
            },
        };
    }

    public VersionGroup Group { get; }

    public string Name => Group.Name;

    public string Subtitle => Group.Subtitle;

    public string Newest => Group.Newest.Id;

    public IBrush Background { get; }
}

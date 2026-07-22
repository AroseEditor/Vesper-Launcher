using Avalonia.Media.Imaging;
using Vesper.App.Controls;
using Vesper.Core.Versions;

namespace Vesper.App.ViewModels;

public sealed class VersionCardViewModel
{
    private static readonly (uint Top, uint Bottom)[] Palette =
    [
        (0xFFB57EDC, 0xFF3E2560),
        (0xFFD14FE8, 0xFF4A1C63),
        (0xFF9A5FC4, 0xFF2C1A45),
        (0xFFC9A0DC, 0xFF5B3A7E),
        (0xFF8E6BD6, 0xFF261A46),
        (0xFFE06AD8, 0xFF54246B),
    ];

    private static readonly Dictionary<int, Bitmap> Cache = [];

    public VersionCardViewModel(VersionGroup group)
    {
        Group = group;

        var index = Math.Abs(StableHash(group.Name)) % Palette.Length;

        if (!Cache.TryGetValue(index, out var banner))
        {
            var (top, bottom) = Palette[index];
            banner = BannerFactory.Create(top, bottom, index * 7919);
            Cache[index] = banner;
        }

        Banner = banner;
    }

    public VersionGroup Group { get; }

    public string Name => Group.Name;

    public string Subtitle => Group.Subtitle;

    public string Newest => Group.Newest.Id;

    public Bitmap Banner { get; }

    private static int StableHash(string value)
    {
        var hash = 17;

        foreach (var c in value)
            hash = hash * 31 + c;

        return hash;
    }
}

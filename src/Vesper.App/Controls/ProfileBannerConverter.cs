using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Vesper.App.Controls;
using Vesper.Core.Versions;

namespace Vesper.App.Controls;

public sealed class ProfileBannerConverter : IValueConverter
{
    public static ProfileBannerConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string mcVersion && !string.IsNullOrWhiteSpace(mcVersion))
        {
            var groupKey = VersionCatalog.GroupKey(new MinecraftVersionInfo(mcVersion, "release", DateTimeOffset.UtcNow));
            return BannerFactory.GetBannerForGroup(groupKey);
        }

        return BannerFactory.GetBannerForGroup("1.21");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

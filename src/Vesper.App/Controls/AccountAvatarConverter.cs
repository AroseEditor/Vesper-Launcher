using System;
using System.Globalization;
using System.IO;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Vesper.Core.Accounts;
using Vesper.Core.Skins;
using Vesper.Core.Storage;

namespace Vesper.App.Controls;

public sealed class AccountAvatarConverter : IValueConverter
{
    public static AccountAvatarConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Account account)
        {
            try
            {
                var paths = VesperPaths.Resolve();
                var skins = new SkinStore(paths);
                var stored = skins.ReadSkin(account.Id);

                if (stored is not null && stored.Length > 0)
                {
                    var pixels = SkinImage.Decode(stored);
                    if (pixels is not null)
                        return SkinImage.RenderHead(pixels);

                    using var ms = new MemoryStream(stored);
                    return new Bitmap(ms);
                }

                var alexPixels = SkinStore.CreateDefaultSkin(slim: true);
                return SkinImage.RenderHead(alexPixels);
            }
            catch
            {
            }
        }

        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

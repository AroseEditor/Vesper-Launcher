using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Vesper.App.Controls;

public static class LogoTint
{
    public const string LogoUri = "avares://VesperLauncher/Assets/icon.png";

    private static readonly Dictionary<int, Bitmap> Cache = [];

    public static Bitmap Load() => new(AssetLoader.Open(new Uri(LogoUri)));

    public static Bitmap Rotate(double degrees)
    {
        var key = (int)Math.Round(degrees) % 360;

        if (Cache.TryGetValue(key, out var cached))
            return cached;

        var source = Load();

        if (key == 0)
        {
            Cache[key] = source;
            return source;
        }

        var width = source.PixelSize.Width;
        var height = source.PixelSize.Height;
        var pixels = new int[width * height];
        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);

        try
        {
            source.CopyPixels(
                new PixelRect(0, 0, width, height),
                handle.AddrOfPinnedObject(),
                pixels.Length * 4,
                width * 4);
        }
        finally
        {
            handle.Free();
        }

        for (var i = 0; i < pixels.Length; i++)
            pixels[i] = unchecked((int)ShiftHue(unchecked((uint)pixels[i]), key));

        var bitmap = new WriteableBitmap(
            new PixelSize(width, height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Unpremul);

        using (var buffer = bitmap.Lock())
        {
            for (var y = 0; y < height; y++)
                Marshal.Copy(pixels, y * width, buffer.Address + y * buffer.RowBytes, width);
        }

        source.Dispose();
        Cache[key] = bitmap;
        return bitmap;
    }

    public static uint ShiftHue(uint colour, double degrees)
    {
        var a = (colour >> 24) & 0xFF;

        if (a == 0)
            return colour;

        var r = ((colour >> 16) & 0xFF) / 255.0;
        var g = ((colour >> 8) & 0xFF) / 255.0;
        var b = (colour & 0xFF) / 255.0;

        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var lightness = (max + min) / 2.0;
        double hue = 0;
        double saturation = 0;

        if (max > min)
        {
            var delta = max - min;
            saturation = lightness > 0.5 ? delta / (2.0 - max - min) : delta / (max + min);

            if (max == r)
                hue = (g - b) / delta + (g < b ? 6 : 0);
            else if (max == g)
                hue = (b - r) / delta + 2;
            else
                hue = (r - g) / delta + 4;

            hue /= 6.0;
        }

        hue = (hue + degrees / 360.0) % 1.0;

        if (hue < 0)
            hue += 1.0;

        double nr, ng, nb;

        if (saturation == 0)
        {
            nr = ng = nb = lightness;
        }
        else
        {
            var q = lightness < 0.5 ? lightness * (1 + saturation) : lightness + saturation - lightness * saturation;
            var p = 2 * lightness - q;
            nr = HueToChannel(p, q, hue + 1.0 / 3.0);
            ng = HueToChannel(p, q, hue);
            nb = HueToChannel(p, q, hue - 1.0 / 3.0);
        }

        return (a << 24)
               | ((uint)Math.Clamp(nr * 255, 0, 255) << 16)
               | ((uint)Math.Clamp(ng * 255, 0, 255) << 8)
               | (uint)Math.Clamp(nb * 255, 0, 255);
    }

    private static double HueToChannel(double p, double q, double t)
    {
        if (t < 0)
            t += 1;

        if (t > 1)
            t -= 1;

        if (t < 1.0 / 6.0)
            return p + (q - p) * 6 * t;

        if (t < 1.0 / 2.0)
            return q;

        if (t < 2.0 / 3.0)
            return p + (q - p) * (2.0 / 3.0 - t) * 6;

        return p;
    }
}

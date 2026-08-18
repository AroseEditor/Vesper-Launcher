using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Vesper.App.Controls;

public static class BannerFactory
{
    private static readonly Dictionary<string, Bitmap> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static Bitmap GetBannerForGroup(string groupName)
    {
        lock (Cache)
        {
            if (Cache.TryGetValue(groupName, out var existing))
                return existing;

            var bitmap = LoadBannerImage(groupName);
            Cache[groupName] = bitmap;
            return bitmap;
        }
    }

    private static Bitmap LoadBannerImage(string groupName)
    {
        var fileName = groupName switch
        {
            "26.2" or "26.1" or "26.0" or "1.21" => "1_21.png",
            "1.20" => "1_20.png",
            "1.19" => "1_19.png",
            "1.18" or "1.17" => "1_18.png",
            "1.16" => "1_16.png",
            _ => "classic.png",
        };

        var candidatePaths = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Banners", fileName),
            Path.Combine(Directory.GetCurrentDirectory(), "src", "Vesper.App", "Assets", "Banners", fileName),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "src", "Vesper.App", "Assets", "Banners", fileName),
            Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Banners", fileName),
        };

        foreach (var path in candidatePaths)
        {
            if (File.Exists(path))
            {
                try
                {
                    return new Bitmap(path);
                }
                catch
                {
                }
            }
        }

        return CreateFallbackBitmap();
    }

    private static WriteableBitmap CreateFallbackBitmap()
    {
        const int width = 420;
        const int height = 250;
        var pixels = new uint[width * height];

        for (var y = 0; y < height; y++)
        {
            var t = (float)y / height;
            var r = (uint)(0x20 + 0x20 * t);
            var g = (uint)(0x12 + 0x10 * t);
            var b = (uint)(0x3A + 0x30 * t);
            var color = 0xFF000000u | (r << 16) | (g << 8) | b;

            for (var x = 0; x < width; x++)
                pixels[y * width + x] = color;
        }

        var bitmap = new WriteableBitmap(
            new PixelSize(width, height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Unpremul);

        using (var buffer = bitmap.Lock())
        {
            var line = new int[width];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                    line[x] = unchecked((int)pixels[y * width + x]);

                System.Runtime.InteropServices.Marshal.Copy(line, 0, buffer.Address + y * buffer.RowBytes, width);
            }
        }

        return bitmap;
    }
}

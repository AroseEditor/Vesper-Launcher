using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Vesper.Core.Skins;

namespace Vesper.App.Controls;

public static class SkinImage
{
    public const int Size = SkinGeometry.TextureSize;

    public static uint[]? Decode(byte[] png)
    {
        try
        {
            using var stream = new MemoryStream(png);
            using var bitmap = new Bitmap(stream);

            var width = bitmap.PixelSize.Width;
            var height = bitmap.PixelSize.Height;

            if (width != Size || (height != Size && height != Size / 2))
                return null;

            var raw = new int[width * height];
            var handle = GCHandle.Alloc(raw, GCHandleType.Pinned);

            try
            {
                bitmap.CopyPixels(
                    new PixelRect(0, 0, width, height),
                    handle.AddrOfPinnedObject(),
                    raw.Length * 4,
                    width * 4);
            }
            finally
            {
                handle.Free();
            }

            var pixels = new uint[Size * Size];

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                    pixels[y * Size + x] = unchecked((uint)raw[y * width + x]);
            }

            if (height == Size / 2)
                ExpandLegacy(pixels);

            return pixels;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static byte[] Encode(uint[] pixels)
    {
        using var bitmap = new WriteableBitmap(
            new PixelSize(Size, Size),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Unpremul);

        using (var buffer = bitmap.Lock())
        {
            var row = new int[Size];

            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                    row[x] = unchecked((int)pixels[y * Size + x]);

                Marshal.Copy(row, 0, buffer.Address + y * buffer.RowBytes, Size);
            }
        }

        using var stream = new MemoryStream();
        bitmap.Save(stream);
        return stream.ToArray();
    }

    public static WriteableBitmap RenderHead(uint[] pixels, int scale = 8)
    {
        const int face = 8;
        var side = face * scale;

        var bitmap = new WriteableBitmap(
            new PixelSize(side, side),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Unpremul);

        using (var buffer = bitmap.Lock())
        {
            var row = new int[side];

            for (var y = 0; y < side; y++)
            {
                var sy = y / scale;

                for (var x = 0; x < side; x++)
                {
                    var sx = x / scale;
                    var color = pixels[(8 + sy) * Size + 8 + sx];
                    var hat = pixels[(8 + sy) * Size + 40 + sx];

                    if (((hat >> 24) & 0xFF) > 0)
                        color = hat;

                    row[x] = unchecked((int)color);
                }

                Marshal.Copy(row, 0, buffer.Address + y * buffer.RowBytes, side);
            }
        }

        return bitmap;
    }

    private static void ExpandLegacy(uint[] pixels)
    {
        CopyMirrored(pixels, 4, 16, 4, 4, 20, 48);
        CopyMirrored(pixels, 8, 16, 4, 4, 24, 48);
        CopyMirrored(pixels, 0, 20, 4, 12, 24, 52);
        CopyMirrored(pixels, 4, 20, 4, 12, 20, 52);
        CopyMirrored(pixels, 8, 20, 4, 12, 16, 52);
        CopyMirrored(pixels, 12, 20, 4, 12, 28, 52);

        CopyMirrored(pixels, 44, 16, 4, 4, 36, 48);
        CopyMirrored(pixels, 48, 16, 4, 4, 40, 48);
        CopyMirrored(pixels, 40, 20, 4, 12, 40, 52);
        CopyMirrored(pixels, 44, 20, 4, 12, 36, 52);
        CopyMirrored(pixels, 48, 20, 4, 12, 32, 52);
        CopyMirrored(pixels, 52, 20, 4, 12, 44, 52);
    }

    private static void CopyMirrored(
        uint[] pixels, int sx, int sy, int w, int h, int dx, int dy)
    {
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
                pixels[(dy + y) * Size + dx + (w - 1 - x)] = pixels[(sy + y) * Size + sx + x];
        }
    }
}

using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Vesper.App.Controls;

public static class BannerFactory
{
    public const int Width = 420;
    public const int Height = 250;

    public static WriteableBitmap Create(uint top, uint bottom, int seed)
    {
        var pixels = new uint[Width * Height];
        var random = new Random(seed);

        for (var y = 0; y < Height; y++)
        {
            var t = (float)y / Height;
            var row = Lerp(top, bottom, t);

            for (var x = 0; x < Width; x++)
                pixels[y * Width + x] = row;
        }

        var size = 30 + random.Next(10);
        var columns = Width / size + 3;
        var rows = Height / (size / 2) + 4;

        for (var r = 0; r < rows; r++)
        {
            for (var c = 0; c < columns; c++)
            {
                if (random.NextDouble() < 0.42)
                    continue;

                var cx = c * size - (r % 2 == 0 ? 0 : size / 2) - size;
                var cy = r * (size / 2) - size;
                var lift = random.NextDouble() * 0.18;

                DrawCube(pixels, cx, cy, size, (float)lift);
            }
        }

        Vignette(pixels);

        var bitmap = new WriteableBitmap(
            new PixelSize(Width, Height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Unpremul);

        using (var buffer = bitmap.Lock())
        {
            var line = new int[Width];

            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                    line[x] = unchecked((int)pixels[y * Width + x]);

                Marshal.Copy(line, 0, buffer.Address + y * buffer.RowBytes, Width);
            }
        }

        return bitmap;
    }

    private static void DrawCube(uint[] pixels, float cx, float cy, float size, float lift)
    {
        var half = size / 2f;

        var top = new (float X, float Y)[]
        {
            (cx, cy - half), (cx + size, cy), (cx, cy + half), (cx - size, cy),
        };

        var left = new (float X, float Y)[]
        {
            (cx - size, cy), (cx, cy + half), (cx, cy + half + size), (cx - size, cy + size),
        };

        var right = new (float X, float Y)[]
        {
            (cx + size, cy), (cx, cy + half), (cx, cy + half + size), (cx + size, cy + size),
        };

        FillPolygon(pixels, top, 0.16f + lift);
        FillPolygon(pixels, left, -0.13f);
        FillPolygon(pixels, right, -0.05f);
    }

    private static void FillPolygon(uint[] pixels, (float X, float Y)[] polygon, float shift)
    {
        var minX = Math.Max(0, (int)polygon.Min(p => p.X));
        var maxX = Math.Min(Width - 1, (int)polygon.Max(p => p.X));
        var minY = Math.Max(0, (int)polygon.Min(p => p.Y));
        var maxY = Math.Min(Height - 1, (int)polygon.Max(p => p.Y));

        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                if (!Contains(polygon, x + 0.5f, y + 0.5f))
                    continue;

                var index = y * Width + x;
                pixels[index] = Adjust(pixels[index], shift);
            }
        }
    }

    private static bool Contains((float X, float Y)[] polygon, float x, float y)
    {
        var inside = false;

        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            var (xi, yi) = polygon[i];
            var (xj, yj) = polygon[j];

            if (yi > y != yj > y && x < (xj - xi) * (y - yi) / (yj - yi) + xi)
                inside = !inside;
        }

        return inside;
    }

    private static void Vignette(uint[] pixels)
    {
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var dx = (x - Width / 2f) / (Width / 2f);
                var dy = (y - Height / 2f) / (Height / 2f);
                var distance = MathF.Sqrt(dx * dx + dy * dy);
                var shade = -0.30f * Math.Clamp(distance - 0.45f, 0f, 1f);

                var index = y * Width + x;
                pixels[index] = Adjust(pixels[index], shade);
            }
        }
    }

    private static uint Lerp(uint a, uint b, float t)
    {
        var ar = (a >> 16) & 0xFF;
        var ag = (a >> 8) & 0xFF;
        var ab = a & 0xFF;
        var br = (b >> 16) & 0xFF;
        var bg = (b >> 8) & 0xFF;
        var bb = b & 0xFF;

        var r = (uint)(ar + (br - (float)ar) * t);
        var g = (uint)(ag + (bg - (float)ag) * t);
        var bl = (uint)(ab + (bb - (float)ab) * t);

        return 0xFF000000u | (r << 16) | (g << 8) | bl;
    }

    private static uint Adjust(uint color, float shift)
    {
        var r = Channel((color >> 16) & 0xFF, shift);
        var g = Channel((color >> 8) & 0xFF, shift);
        var b = Channel(color & 0xFF, shift);

        return 0xFF000000u | (r << 16) | (g << 8) | b;
    }

    private static uint Channel(uint value, float shift)
    {
        var result = shift >= 0
            ? value + (255 - value) * shift
            : value * (1 + shift);

        return (uint)Math.Clamp(result, 0, 255);
    }
}

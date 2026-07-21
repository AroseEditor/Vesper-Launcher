using Vesper.Core.Storage;

namespace Vesper.Core.Skins;

public sealed class SkinStore
{
    private readonly VesperPaths _paths;

    public SkinStore(VesperPaths paths) => _paths = paths;

    public string SkinPath(string accountId) =>
        Path.Combine(_paths.SkinDir(accountId), "skin.png");

    public string CapePath(string accountId) =>
        Path.Combine(_paths.SkinDir(accountId), "cape.png");

    public bool HasSkin(string accountId) => File.Exists(SkinPath(accountId));

    public bool HasCape(string accountId) => File.Exists(CapePath(accountId));

    public byte[]? ReadSkin(string accountId)
    {
        var path = SkinPath(accountId);
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }

    public void WriteSkin(string accountId, byte[] png)
    {
        Directory.CreateDirectory(_paths.SkinDir(accountId));
        File.WriteAllBytes(SkinPath(accountId), png);
    }

    public void WriteCape(string accountId, byte[] png)
    {
        Directory.CreateDirectory(_paths.SkinDir(accountId));
        File.WriteAllBytes(CapePath(accountId), png);
    }

    public void DeleteSkin(string accountId)
    {
        var path = SkinPath(accountId);
        if (File.Exists(path))
            File.Delete(path);
    }

    public static uint[] CreateDefaultSkin(bool slim)
    {
        const int size = SkinGeometry.TextureSize;
        var pixels = new uint[size * size];

        var skinTone = Rgb(0xC8, 0x99, 0x76);
        var skinShade = Rgb(0xB0, 0x85, 0x66);
        var hair = Rgb(0x3A, 0x2A, 0x22);
        var shirt = Rgb(0xB5, 0x7E, 0xDC);
        var shirtShade = Rgb(0x9A, 0x5F, 0xC4);
        var trousers = Rgb(0x33, 0x33, 0x40);
        var shoes = Rgb(0x24, 0x24, 0x2C);
        var eye = Rgb(0x22, 0x22, 0x2E);

        foreach (var box in SkinGeometry.Build(slim))
        {
            if (box.IsOverlay)
                continue;

            var body = box.Part switch
            {
                SkinPart.Head => skinTone,
                SkinPart.Body => shirt,
                SkinPart.RightArm or SkinPart.LeftArm => skinTone,
                _ => trousers,
            };

            var shaded = box.Part switch
            {
                SkinPart.Body => shirtShade,
                SkinPart.RightLeg or SkinPart.LeftLeg => shoes,
                _ => skinShade,
            };

            Fill(pixels, box.Up, box.Part == SkinPart.Head ? hair : body);
            Fill(pixels, box.Down, shaded);
            Fill(pixels, box.North, body);
            Fill(pixels, box.South, body);
            Fill(pixels, box.West, shaded);
            Fill(pixels, box.East, shaded);

            if (box.Part == SkinPart.Head)
            {
                Fill(pixels, box.North, hair);
                Rect(pixels, box.South.X, box.South.Y, 8, 3, hair);
                Set(pixels, box.South.X + 2, box.South.Y + 4, eye);
                Set(pixels, box.South.X + 5, box.South.Y + 4, eye);
            }

            if (box.Part is SkinPart.RightLeg or SkinPart.LeftLeg)
                Rect(pixels, box.South.X, box.South.Y + 10, box.South.Width, 2, shoes);
        }

        return pixels;
    }

    private static void Fill(uint[] pixels, FaceUv uv, uint color) =>
        Rect(pixels, uv.X, uv.Y, uv.Width, uv.Height, color);

    private static void Rect(uint[] pixels, int x, int y, int w, int h, uint color)
    {
        for (var dy = 0; dy < h; dy++)
        {
            for (var dx = 0; dx < w; dx++)
                Set(pixels, x + dx, y + dy, color);
        }
    }

    private static void Set(uint[] pixels, int x, int y, uint color)
    {
        const int size = SkinGeometry.TextureSize;

        if (x < 0 || y < 0 || x >= size || y >= size)
            return;

        pixels[y * size + x] = color;
    }

    private static uint Rgb(int r, int g, int b) =>
        0xFF000000u | ((uint)r << 16) | ((uint)g << 8) | (uint)b;
}

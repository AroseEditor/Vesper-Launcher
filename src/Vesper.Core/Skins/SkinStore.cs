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

    public static uint[] CreateDefaultSkin(bool slim = true)
    {
        const int size = SkinGeometry.TextureSize;
        var pixels = new uint[size * size];

        var alexSkin = Rgb(0xEA, 0xA0, 0x79);
        var alexShade = Rgb(0xD0, 0x88, 0x64);
        var alexHair = Rgb(0xC8, 0x5B, 0x24); // Signature Alex Orange Hair
        var alexShirt = Rgb(0x50, 0x76, 0x3C); // Signature Alex Green Shirt
        var alexShirtShade = Rgb(0x3B, 0x58, 0x2C);
        var alexPants = Rgb(0x47, 0x3F, 0x3A);
        var alexBoots = Rgb(0x2B, 0x27, 0x24);
        var alexEye = Rgb(0x2F, 0x5B, 0x39); // Green eyes

        foreach (var box in SkinGeometry.Build(slim))
        {
            if (box.IsOverlay)
                continue;

            var body = box.Part switch
            {
                SkinPart.Head => alexSkin,
                SkinPart.Body => alexShirt,
                SkinPart.RightArm or SkinPart.LeftArm => alexSkin,
                _ => alexPants,
            };

            var shaded = box.Part switch
            {
                SkinPart.Body => alexShirtShade,
                SkinPart.RightLeg or SkinPart.LeftLeg => alexBoots,
                _ => alexShade,
            };

            Fill(pixels, box.Up, box.Part == SkinPart.Head ? alexHair : body);
            Fill(pixels, box.Down, shaded);
            Fill(pixels, box.North, body);
            Fill(pixels, box.South, body);
            Fill(pixels, box.West, shaded);
            Fill(pixels, box.East, shaded);

            if (box.Part == SkinPart.Head)
            {
                Fill(pixels, box.North, alexHair);
                Rect(pixels, box.South.X, box.South.Y, 8, 3, alexHair);
                Set(pixels, box.South.X + 2, box.South.Y + 4, alexEye);
                Set(pixels, box.South.X + 5, box.South.Y + 4, alexEye);
            }

            if (box.Part is SkinPart.RightLeg or SkinPart.LeftLeg)
                Rect(pixels, box.South.X, box.South.Y + 10, box.South.Width, 2, alexBoots);
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

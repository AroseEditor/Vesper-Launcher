namespace Vesper.Core.Skins;

public sealed class SkinRenderResult
{
    public SkinRenderResult(int width, int height)
    {
        Width = width;
        Height = height;
        Pixels = new uint[width * height];
        Pick = new int[width * height];
        Array.Fill(Pick, -1);
    }

    public int Width { get; }

    public int Height { get; }

    public uint[] Pixels { get; }

    public int[] Pick { get; }

    public bool TryPick(int x, int y, out int textureX, out int textureY)
    {
        textureX = 0;
        textureY = 0;

        if (x < 0 || y < 0 || x >= Width || y >= Height)
            return false;

        var index = Pick[y * Width + x];

        if (index < 0)
            return false;

        textureX = index % SkinGeometry.TextureSize;
        textureY = index / SkinGeometry.TextureSize;
        return true;
    }
}

public static class SkinRenderer
{
    private const float TopShade = 1.0f;
    private const float BottomShade = 0.58f;
    private const float FrontShade = 0.88f;
    private const float SideShade = 0.72f;

    public static SkinRenderResult Render(
        uint[] skin,
        bool slim,
        float yawDegrees,
        float pitchDegrees,
        int width,
        int height,
        bool showOverlay = true)
    {
        var result = new SkinRenderResult(width, height);
        var depth = new float[width * height];
        Array.Fill(depth, float.MaxValue);

        var yaw = yawDegrees * MathF.PI / 180f;
        var pitch = pitchDegrees * MathF.PI / 180f;

        var scale = height / (SkinGeometry.ModelHeight * 1.22f);
        var cx = width / 2f;
        var cy = height / 2f;

        foreach (var box in SkinGeometry.Build(slim))
        {
            if (box.IsOverlay && !showOverlay)
                continue;

            foreach (var face in Faces(box))
            {
                var shade = face.Shade;
                var uv = face.Uv;

                var p0 = Project(face.C0, yaw, pitch, scale, cx, cy);
                var p1 = Project(face.C1, yaw, pitch, scale, cx, cy);
                var p2 = Project(face.C2, yaw, pitch, scale, cx, cy);
                var p3 = Project(face.C3, yaw, pitch, scale, cx, cy);

                Triangle(result, depth, skin, p0, p1, p2, (0, 0), (1, 0), (1, 1), uv, shade);
                Triangle(result, depth, skin, p0, p2, p3, (0, 0), (1, 1), (0, 1), uv, shade);
            }
        }

        return result;
    }

    private static (float X, float Y, float Z) Project(
        (float X, float Y, float Z) p,
        float yaw,
        float pitch,
        float scale,
        float cx,
        float cy)
    {
        var y = p.Y - SkinGeometry.ModelHeight / 2f;

        var sinYaw = MathF.Sin(yaw);
        var cosYaw = MathF.Cos(yaw);
        var x1 = p.X * cosYaw + p.Z * sinYaw;
        var z1 = -p.X * sinYaw + p.Z * cosYaw;

        var sinPitch = MathF.Sin(pitch);
        var cosPitch = MathF.Cos(pitch);
        var y2 = y * cosPitch - z1 * sinPitch;
        var z2 = y * sinPitch + z1 * cosPitch;

        return (cx + x1 * scale, cy - y2 * scale, z2);
    }

    private static void Triangle(
        SkinRenderResult target,
        float[] depth,
        uint[] skin,
        (float X, float Y, float Z) a,
        (float X, float Y, float Z) b,
        (float X, float Y, float Z) c,
        (float U, float V) ta,
        (float U, float V) tb,
        (float U, float V) tc,
        FaceUv uv,
        float shade)
    {
        var minX = Math.Max(0, (int)MathF.Floor(MathF.Min(a.X, MathF.Min(b.X, c.X))));
        var maxX = Math.Min(target.Width - 1, (int)MathF.Ceiling(MathF.Max(a.X, MathF.Max(b.X, c.X))));
        var minY = Math.Max(0, (int)MathF.Floor(MathF.Min(a.Y, MathF.Min(b.Y, c.Y))));
        var maxY = Math.Min(target.Height - 1, (int)MathF.Ceiling(MathF.Max(a.Y, MathF.Max(b.Y, c.Y))));

        var area = Edge(a, b, c);

        if (MathF.Abs(area) < 0.0001f)
            return;

        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                var p = (X: x + 0.5f, Y: y + 0.5f, Z: 0f);

                var w0 = Edge(b, c, p) / area;
                var w1 = Edge(c, a, p) / area;
                var w2 = Edge(a, b, p) / area;

                if (w0 < 0 || w1 < 0 || w2 < 0)
                    continue;

                var z = a.Z * w0 + b.Z * w1 + c.Z * w2;
                var index = y * target.Width + x;

                if (z >= depth[index])
                    continue;

                var u = ta.U * w0 + tb.U * w1 + tc.U * w2;
                var v = ta.V * w0 + tb.V * w1 + tc.V * w2;

                var tx = uv.X + Math.Clamp((int)(u * uv.Width), 0, uv.Width - 1);
                var ty = uv.Y + Math.Clamp((int)(v * uv.Height), 0, uv.Height - 1);

                if (tx < 0 || ty < 0 || tx >= SkinGeometry.TextureSize || ty >= SkinGeometry.TextureSize)
                    continue;

                var texel = skin[ty * SkinGeometry.TextureSize + tx];
                var alpha = (texel >> 24) & 0xFF;

                if (alpha == 0)
                    continue;

                depth[index] = z;
                target.Pixels[index] = Shade(texel, shade);
                target.Pick[index] = ty * SkinGeometry.TextureSize + tx;
            }
        }
    }

    private static float Edge(
        (float X, float Y, float Z) a,
        (float X, float Y, float Z) b,
        (float X, float Y, float Z) p) =>
        (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);

    private static uint Shade(uint color, float factor)
    {
        var a = (color >> 24) & 0xFF;
        var r = (uint)Math.Clamp(((color >> 16) & 0xFF) * factor, 0, 255);
        var g = (uint)Math.Clamp(((color >> 8) & 0xFF) * factor, 0, 255);
        var b = (uint)Math.Clamp((color & 0xFF) * factor, 0, 255);
        return (a << 24) | (r << 16) | (g << 8) | b;
    }

    private readonly record struct Face(
        (float X, float Y, float Z) C0,
        (float X, float Y, float Z) C1,
        (float X, float Y, float Z) C2,
        (float X, float Y, float Z) C3,
        FaceUv Uv,
        float Shade);

    private static IEnumerable<Face> Faces(SkinBox b)
    {
        var x0 = b.MinX;
        var y0 = b.MinY;
        var z0 = b.MinZ;
        var x1 = b.MaxX;
        var y1 = b.MaxY;
        var z1 = b.MaxZ;

        yield return new Face(
            (x0, y1, z1), (x1, y1, z1), (x1, y0, z1), (x0, y0, z1), b.South, FrontShade);

        yield return new Face(
            (x1, y1, z0), (x0, y1, z0), (x0, y0, z0), (x1, y0, z0), b.North, FrontShade);

        yield return new Face(
            (x0, y1, z0), (x0, y1, z1), (x0, y0, z1), (x0, y0, z0), b.West, SideShade);

        yield return new Face(
            (x1, y1, z1), (x1, y1, z0), (x1, y0, z0), (x1, y0, z1), b.East, SideShade);

        yield return new Face(
            (x0, y1, z0), (x1, y1, z0), (x1, y1, z1), (x0, y1, z1), b.Up, TopShade);

        yield return new Face(
            (x0, y0, z1), (x1, y0, z1), (x1, y0, z0), (x0, y0, z0), b.Down, BottomShade);
    }
}

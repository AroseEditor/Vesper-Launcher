namespace Vesper.Core.Skins;

public enum SkinPart
{
    Head,
    Body,
    RightArm,
    LeftArm,
    RightLeg,
    LeftLeg,
}

public readonly record struct FaceUv(int X, int Y, int Width, int Height);

public sealed record SkinBox(
    SkinPart Part,
    bool IsOverlay,
    float MinX,
    float MinY,
    float MinZ,
    float MaxX,
    float MaxY,
    float MaxZ,
    FaceUv Up,
    FaceUv Down,
    FaceUv North,
    FaceUv South,
    FaceUv West,
    FaceUv East);

public static class SkinGeometry
{
    public const int TextureSize = 64;
    public const float ModelHeight = 32f;
    public const float OverlayInflate = 0.5f;

    public static IReadOnlyList<SkinBox> Build(bool slim)
    {
        var armWidth = slim ? 3f : 4f;
        var boxes = new List<SkinBox>();

        boxes.Add(Box(SkinPart.Head, false, -4, 24, -4, 4, 32, 4, 8, 8, 8, 0, 0));
        boxes.Add(Box(SkinPart.Head, true, -4, 24, -4, 4, 32, 4, 8, 8, 8, 32, 0));

        boxes.Add(Box(SkinPart.Body, false, -4, 12, -2, 4, 24, 2, 8, 12, 4, 16, 16));
        boxes.Add(Box(SkinPart.Body, true, -4, 12, -2, 4, 24, 2, 8, 12, 4, 16, 32));

        var rightArmMax = -4f;
        var rightArmMin = rightArmMax - armWidth;
        boxes.Add(Box(SkinPart.RightArm, false, rightArmMin, 12, -2, rightArmMax, 24, 2,
            (int)armWidth, 12, 4, 40, 16));
        boxes.Add(Box(SkinPart.RightArm, true, rightArmMin, 12, -2, rightArmMax, 24, 2,
            (int)armWidth, 12, 4, 40, 32));

        var leftArmMin = 4f;
        var leftArmMax = leftArmMin + armWidth;
        boxes.Add(Box(SkinPart.LeftArm, false, leftArmMin, 12, -2, leftArmMax, 24, 2,
            (int)armWidth, 12, 4, 32, 48));
        boxes.Add(Box(SkinPart.LeftArm, true, leftArmMin, 12, -2, leftArmMax, 24, 2,
            (int)armWidth, 12, 4, 48, 48));

        boxes.Add(Box(SkinPart.RightLeg, false, -4, 0, -2, 0, 12, 2, 4, 12, 4, 0, 16));
        boxes.Add(Box(SkinPart.RightLeg, true, -4, 0, -2, 0, 12, 2, 4, 12, 4, 0, 32));

        boxes.Add(Box(SkinPart.LeftLeg, false, 0, 0, -2, 4, 12, 2, 4, 12, 4, 16, 48));
        boxes.Add(Box(SkinPart.LeftLeg, true, 0, 0, -2, 4, 12, 2, 4, 12, 4, 0, 48));

        return boxes;
    }

    private static SkinBox Box(
        SkinPart part,
        bool overlay,
        float minX,
        float minY,
        float minZ,
        float maxX,
        float maxY,
        float maxZ,
        int width,
        int height,
        int depth,
        int u,
        int v)
    {
        if (overlay)
        {
            minX -= OverlayInflate;
            minY -= OverlayInflate;
            minZ -= OverlayInflate;
            maxX += OverlayInflate;
            maxY += OverlayInflate;
            maxZ += OverlayInflate;
        }

        return new SkinBox(
            part,
            overlay,
            minX, minY, minZ,
            maxX, maxY, maxZ,
            Up: new FaceUv(u + depth, v, width, depth),
            Down: new FaceUv(u + depth + width, v, width, depth),
            North: new FaceUv(u + depth + width + depth, v + depth, width, height),
            South: new FaceUv(u + depth, v + depth, width, height),
            West: new FaceUv(u, v + depth, depth, height),
            East: new FaceUv(u + depth + width, v + depth, depth, height));
    }
}

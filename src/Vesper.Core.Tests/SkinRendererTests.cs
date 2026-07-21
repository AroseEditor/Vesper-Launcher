using Vesper.Core.Skins;
using Xunit;

namespace Vesper.Core.Tests;

public class SkinRendererTests
{
    private static uint[] Default() => SkinStore.CreateDefaultSkin(false);

    [Fact]
    public void DefaultSkinIsSixtyFourSquare() =>
        Assert.Equal(SkinGeometry.TextureSize * SkinGeometry.TextureSize, Default().Length);

    [Fact]
    public void DefaultSkinHasAnOpaqueBaseLayer()
    {
        var pixels = Default();

        for (var y = 8; y < 16; y++)
        {
            for (var x = 8; x < 16; x++)
                Assert.Equal(0xFFu, (pixels[y * SkinGeometry.TextureSize + x] >> 24) & 0xFF);
        }
    }

    [Fact]
    public void DefaultSkinLeavesTheOverlayLayerTransparent()
    {
        var pixels = Default();

        for (var y = 8; y < 16; y++)
        {
            for (var x = 40; x < 48; x++)
                Assert.Equal(0u, (pixels[y * SkinGeometry.TextureSize + x] >> 24) & 0xFF);
        }
    }

    [Fact]
    public void RenderProducesVisiblePixels()
    {
        var frame = SkinRenderer.Render(Default(), false, 0, 0, 120, 180);

        var drawn = frame.Pixels.Count(p => ((p >> 24) & 0xFF) > 0);

        Assert.True(drawn > 1000, "expected the model to cover a meaningful area");
    }

    [Fact]
    public void FrontViewShowsTheFace()
    {
        var frame = SkinRenderer.Render(Default(), false, 0, 0, 120, 180);

        var headY = (int)(180 * 0.22);
        Assert.True(frame.TryPick(60, headY, out var tx, out var ty));

        Assert.InRange(tx, 8, 15);
        Assert.InRange(ty, 8, 15);
    }

    [Fact]
    public void BackViewShowsTheBackOfTheHead()
    {
        var frame = SkinRenderer.Render(Default(), false, 180, 0, 120, 180);

        var headY = (int)(180 * 0.22);
        Assert.True(frame.TryPick(60, headY, out var tx, out _));

        Assert.InRange(tx, 24, 31);
    }

    [Fact]
    public void EmptyRegionsPickNothing()
    {
        var frame = SkinRenderer.Render(Default(), false, 0, 0, 120, 180);

        Assert.False(frame.TryPick(2, 2, out _, out _));
    }

    [Fact]
    public void PickIsOutOfBoundsSafe()
    {
        var frame = SkinRenderer.Render(Default(), false, 0, 0, 60, 90);

        Assert.False(frame.TryPick(-1, 10, out _, out _));
        Assert.False(frame.TryPick(10, 9999, out _, out _));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SlimAndClassicBothRender(bool slim)
    {
        var frame = SkinRenderer.Render(SkinStore.CreateDefaultSkin(slim), slim, 30, -10, 120, 180);

        Assert.Contains(frame.Pixels, p => ((p >> 24) & 0xFF) > 0);
    }

    [Fact]
    public void SlimModelIsNarrowerThanClassic()
    {
        var classic = CoveredColumns(SkinRenderer.Render(SkinStore.CreateDefaultSkin(false), false, 0, 0, 160, 200));
        var slim = CoveredColumns(SkinRenderer.Render(SkinStore.CreateDefaultSkin(true), true, 0, 0, 160, 200));

        Assert.True(slim < classic, "slim arms should occupy fewer columns");
    }

    [Fact]
    public void HidingOverlayStillRendersTheBody()
    {
        var frame = SkinRenderer.Render(Default(), false, 0, 0, 120, 180, showOverlay: false);

        Assert.Contains(frame.Pixels, p => ((p >> 24) & 0xFF) > 0);
    }

    private static int CoveredColumns(SkinRenderResult frame)
    {
        var columns = 0;

        for (var x = 0; x < frame.Width; x++)
        {
            for (var y = 0; y < frame.Height; y++)
            {
                if (((frame.Pixels[y * frame.Width + x] >> 24) & 0xFF) > 0)
                {
                    columns++;
                    break;
                }
            }
        }

        return columns;
    }
}

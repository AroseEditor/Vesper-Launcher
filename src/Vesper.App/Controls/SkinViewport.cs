using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Vesper.Core.Skins;

namespace Vesper.App.Controls;

public class SkinViewport : Control
{
    public static readonly StyledProperty<uint[]?> SkinProperty =
        AvaloniaProperty.Register<SkinViewport, uint[]?>(nameof(Skin));

    public static readonly StyledProperty<bool> SlimProperty =
        AvaloniaProperty.Register<SkinViewport, bool>(nameof(Slim));

    public static readonly StyledProperty<bool> ShowOverlayProperty =
        AvaloniaProperty.Register<SkinViewport, bool>(nameof(ShowOverlay), true);

    public static readonly StyledProperty<bool> PaintModeProperty =
        AvaloniaProperty.Register<SkinViewport, bool>(nameof(PaintMode));

    public static readonly StyledProperty<double> YawProperty =
        AvaloniaProperty.Register<SkinViewport, double>(nameof(Yaw), 28);

    public static readonly StyledProperty<double> PitchProperty =
        AvaloniaProperty.Register<SkinViewport, double>(nameof(Pitch), -10);

    private WriteableBitmap? _bitmap;
    private SkinRenderResult? _lastFrame;
    private Point? _dragOrigin;
    private double _dragYaw;
    private double _dragPitch;

    static SkinViewport()
    {
        AffectsRender<SkinViewport>(
            SkinProperty, SlimProperty, ShowOverlayProperty, YawProperty, PitchProperty);
    }

    public event EventHandler<SkinPaintEventArgs>? Painted;

    public uint[]? Skin
    {
        get => GetValue(SkinProperty);
        set => SetValue(SkinProperty, value);
    }

    public bool Slim
    {
        get => GetValue(SlimProperty);
        set => SetValue(SlimProperty, value);
    }

    public bool ShowOverlay
    {
        get => GetValue(ShowOverlayProperty);
        set => SetValue(ShowOverlayProperty, value);
    }

    public bool PaintMode
    {
        get => GetValue(PaintModeProperty);
        set => SetValue(PaintModeProperty, value);
    }

    public double Yaw
    {
        get => GetValue(YawProperty);
        set => SetValue(YawProperty, value);
    }

    public double Pitch
    {
        get => GetValue(PitchProperty);
        set => SetValue(PitchProperty, value);
    }

    public void Invalidate() => InvalidateVisual();

    public override void Render(DrawingContext context)
    {
        var width = (int)Bounds.Width;
        var height = (int)Bounds.Height;

        if (width <= 0 || height <= 0 || Skin is null)
            return;

        var frame = SkinRenderer.Render(
            Skin, Slim, (float)Yaw, (float)Pitch, width, height, ShowOverlay);

        _lastFrame = frame;

        if (_bitmap is null || _bitmap.PixelSize.Width != width || _bitmap.PixelSize.Height != height)
        {
            _bitmap?.Dispose();
            _bitmap = new WriteableBitmap(
                new PixelSize(width, height),
                new Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Unpremul);
        }

        using (var buffer = _bitmap.Lock())
        {
            for (var y = 0; y < height; y++)
            {
                var source = y * width;
                var destination = buffer.Address + y * buffer.RowBytes;
                System.Runtime.InteropServices.Marshal.Copy(
                    Array.ConvertAll(
                        frame.Pixels[source..(source + width)],
                        p => unchecked((int)p)),
                    0,
                    destination,
                    width);
            }
        }

        context.DrawImage(_bitmap, new Rect(0, 0, width, height));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        var point = e.GetPosition(this);

        if (PaintMode && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            RaisePaint(point);
            e.Pointer.Capture(this);
            return;
        }

        _dragOrigin = point;
        _dragYaw = Yaw;
        _dragPitch = Pitch;
        e.Pointer.Capture(this);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (!Equals(e.Pointer.Captured, this))
            return;

        var point = e.GetPosition(this);

        if (PaintMode && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            RaisePaint(point);
            return;
        }

        if (_dragOrigin is not { } origin)
            return;

        Yaw = _dragYaw + (point.X - origin.X) * 0.6;
        Pitch = Math.Clamp(_dragPitch + (point.Y - origin.Y) * 0.4, -60, 60);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _dragOrigin = null;
        e.Pointer.Capture(null);
    }

    private void RaisePaint(Point point)
    {
        if (_lastFrame is null)
            return;

        if (_lastFrame.TryPick((int)point.X, (int)point.Y, out var tx, out var ty))
            Painted?.Invoke(this, new SkinPaintEventArgs(tx, ty));
    }
}

public sealed class SkinPaintEventArgs : EventArgs
{
    public SkinPaintEventArgs(int x, int y)
    {
        X = x;
        Y = y;
    }

    public int X { get; }

    public int Y { get; }
}

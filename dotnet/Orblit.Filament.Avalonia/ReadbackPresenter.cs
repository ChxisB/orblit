using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Orblit.Filament.Avalonia;

/// <summary>
/// The path every system has. The renderer is headless, each frame is copied back to the CPU, and
/// the copy lands straight in a bitmap that the control draws.
/// </summary>
internal sealed unsafe class ReadbackPresenter(Control owner) : IFramePresenter
{
    private WriteableBitmap? _bitmap;

    /// <summary>The last frame shown, or null before the first.</summary>
    internal WriteableBitmap? Bitmap => _bitmap;

    public Surface Surface => Surface.Headless;

    public bool IsBusy => false;

    public Exception? Failure => null;

    public void Begin(Renderer renderer) => renderer.RequestCapture();

    public bool End(Renderer renderer, PixelSize size)
    {
        // The capture is the last one the renderer finished, which after a resize can still be the
        // old size. A call with no room only reports the size.
        renderer.TryReadCapture([], out var width, out var height);
        if (width != size.Width || height != size.Height)
        {
            return false;
        }

        if (_bitmap?.PixelSize != size)
        {
            _bitmap?.Dispose();
            _bitmap = new WriteableBitmap(size, new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Opaque);
        }

        using (var locked = _bitmap.Lock())
        {
            // shortcut: a bitmap with padded rows is refused, when one turns up copy a row at a time.
            if (locked.RowBytes != size.Width * 4)
            {
                throw new InvalidOperationException($"The bitmap pads its rows to {locked.RowBytes} bytes.");
            }

            var pixels = new Span<byte>((void*)locked.Address, locked.RowBytes * size.Height);
            // The size was checked a line ago, so this refuses only if a capture of another size landed in between.
            if (!renderer.TryReadCapture(pixels, out _, out _))
            {
                return false;
            }
        }

        owner.InvalidateVisual();
        return true;
    }

    public void Paint(DrawingContext context, Rect bounds)
    {
        if (_bitmap is not null)
        {
            context.DrawImage(_bitmap, new Rect(_bitmap.Size), bounds);
        }
    }

    public void Dispose()
    {
        _bitmap?.Dispose();
        _bitmap = null;
    }
}

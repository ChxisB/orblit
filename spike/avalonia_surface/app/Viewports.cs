using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Rendering.Composition;

namespace SurfaceSpike;

internal interface IViewport
{
    // Draws one frame and shows it. False when nothing was drawn.
    bool Frame(double seconds);

    // The size of the last frame the renderer drew, in pixels.
    PixelSize Drawn { get; }

    // What the renderer reported for the frame, or null before the first.
    Stats? Reported { get; }

    // Frames dropped because the previous one had not reached the screen.
    int Dropped { get; }

    void Close();
}

static class Pixels
{
    public static PixelSize Of(Visual visual)
    {
        var scale = TopLevel.GetTopLevel(visual)?.RenderScaling ?? 1.0;
        return new PixelSize((int)(visual.Bounds.Width * scale), (int)(visual.Bounds.Height * scale));
    }
}

// Path A. The renderer draws offscreen and the frame is copied back to the CPU
// and into a bitmap that Avalonia draws.
internal sealed unsafe class ReadbackViewport : Control, IViewport
{
    Engine? _engine;
    WriteableBitmap? _bitmap;

    public PixelSize Drawn { get; private set; }
    public Stats? Reported { get; private set; }
    public int Dropped { get; private set; }

    public bool Frame(double seconds)
    {
        var size = Pixels.Of(this);
        if (size.Width == 0 || size.Height == 0) return false;
        var w = (uint)size.Width;
        var h = (uint)size.Height;
        if (_engine == null) _engine = new Engine(Native.SurfaceHeadless, IntPtr.Zero, w, h);
        if (size != Drawn)
        {
            _engine.Resize(w, h);
            _bitmap = new WriteableBitmap(size, new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Opaque);
            Drawn = size;
        }

        Native.orblit_renderer_request_capture(_engine.Handle);
        _engine.Draw(seconds);
        Reported = _engine.Measure();

        uint gotW = 0, gotH = 0;
        var bytes = Native.orblit_renderer_read_capture(_engine.Handle, null, 0, &gotW, &gotH);
        if (bytes == 0 || gotW != w || gotH != h)
        {
            Dropped++;
            return true;
        }
        using (var frame = _bitmap!.Lock())
        {
            // The bitmap is tightly packed on every platform tried; a padded
            // row would need a row copy here.
            if (frame.RowBytes != size.Width * 4) throw new InvalidOperationException("padded rows");
            Native.orblit_renderer_read_capture(_engine.Handle, (byte*)frame.Address, bytes, &gotW, &gotH);
        }
        InvalidateVisual();
        return true;
    }

    public override void Render(DrawingContext context)
    {
        if (_bitmap != null)
        {
            context.DrawImage(_bitmap, new Rect(_bitmap.Size), new Rect(Bounds.Size));
        }
    }

    public void Close() => _engine?.Dispose();
}

// Path C. The renderer draws into IOSurface-backed pixel buffers and Avalonia's
// compositor imports the surface as a texture. No pixel is copied by the CPU.
internal sealed class SharedTextureViewport : Control, IViewport
{
    Engine? _engine;
    ICompositionGpuInterop? _interop;
    CompositionDrawingSurface? _surface;
    CompositionSurfaceVisual? _visual;
    // How many updates may be waiting on the compositor. With one, a frame is
    // released just after the next tick, ticks drop, and Avalonia falls to its
    // idle 60 Hz rate and stays there. Two is stable at 100 Hz. The renderer
    // rings four buffers, so up to three in flight cannot reuse one.
    readonly int _limit = int.Parse(Environment.GetEnvironmentVariable("SPIKE_INFLIGHT") ?? "2");
    // Metal cannot sync an imported image by itself in Avalonia 12.1.4, so the
    // frame is handed over with two timeline events: ours says the frame is
    // drawn, the compositor's says it has finished reading it.
    IntPtr _renderedEvent, _releasedEvent;
    ICompositionImportedGpuSemaphore? _rendered, _released;
    ulong _submitted;
    readonly Dictionary<uint, ICompositionImportedGpuImage> _imports = new();

    public PixelSize Drawn { get; private set; }
    public Stats? Reported { get; private set; }
    public int Dropped { get; private set; }
    public string? Failure { get; private set; }

    protected override async void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        try
        {
            var compositor = ElementComposition.GetElementVisual(this)!.Compositor;
            _surface = compositor.CreateDrawingSurface();
            _visual = compositor.CreateSurfaceVisual();
            _visual.Surface = _surface;
            ElementComposition.SetElementChildVisual(this, _visual);
            _interop = await compositor.TryGetCompositionGpuInterop();
            if (_interop == null) Failure = "the compositor has no GPU interop";
            else
            {
                Console.WriteLine($"interop: images [{string.Join(", ", _interop.SupportedImageHandleTypes)}] semaphores [{string.Join(", ", _interop.SupportedSemaphoreTypes)}] lost {_interop.IsLost}");
                Console.WriteLine($"sync for IOSurfaceRef: {_interop.GetSynchronizationCapabilities(KnownPlatformGraphicsExternalImageHandleTypes.IOSurfaceRef)}");
                _renderedEvent = Native.spike_make_shared_event();
                _releasedEvent = Native.spike_make_shared_event();
                _rendered = _interop.ImportSemaphore(new PlatformHandle(_renderedEvent, KnownPlatformGraphicsExternalSemaphoreHandleTypes.MetalSharedEvent));
                _released = _interop.ImportSemaphore(new PlatformHandle(_releasedEvent, KnownPlatformGraphicsExternalSemaphoreHandleTypes.MetalSharedEvent));
            }
        }
        catch (Exception ex)
        {
            Failure = ex.ToString();
        }
    }

    public bool Frame(double seconds)
    {
        if (_interop == null || _surface == null || _visual == null) return false;
        if (_submitted - Native.spike_event_value(_releasedEvent) >= (ulong)_limit)
        {
            Dropped++;
            return false;
        }
        var size = Pixels.Of(this);
        if (size.Width == 0 || size.Height == 0) return false;
        if (_engine == null) _engine = new Engine(Native.SurfacePlatform, IntPtr.Zero, (uint)size.Width, (uint)size.Height);
        if (size != Drawn)
        {
            _engine.Resize((uint)size.Width, (uint)size.Height);
            Drawn = size;
        }

        _engine.Draw(seconds);
        Reported = _engine.Measure();
        var buffer = Native.orblit_renderer_copy_presented(_engine.Handle);
        if (buffer == IntPtr.Zero) return true;
        try
        {
            var surface = Native.CVPixelBufferGetIOSurface(buffer);
            var id = Native.IOSurfaceGetID(surface);
            var width = (int)Native.CVPixelBufferGetWidth(buffer);
            var height = (int)Native.CVPixelBufferGetHeight(buffer);
            if (!_imports.TryGetValue(id, out var image))
            {
                image = _interop.ImportImage(
                    new PlatformHandle(surface, KnownPlatformGraphicsExternalImageHandleTypes.IOSurfaceRef),
                    new PlatformGraphicsExternalImageProperties
                    {
                        Width = width,
                        Height = height,
                        Format = PlatformGraphicsExternalImageFormat.B8G8R8A8UNorm,
                        TopLeftOrigin = Environment.GetEnvironmentVariable("SPIKE_TOPLEFT") != "0",
                    });
                _imports[id] = image;
            }
            var scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
            _visual.Size = new System.Numerics.Vector2((float)(width / scale), (float)(height / scale));
            // draw has already waited for the GPU, so the frame is complete.
            var frame = ++_submitted;
            Native.spike_event_signal(_renderedEvent, frame);
            _surface.UpdateWithTimelineSemaphoresAsync(image, _rendered!, frame, _released!, frame).ContinueWith(t =>
            {
                if (t.IsFaulted) Failure = t.Exception!.GetBaseException().ToString();
            });
        }
        finally
        {
            Native.CFRelease(buffer);
        }
        return true;
    }

    public void Close() => _engine?.Dispose();
}

// Path B. The renderer draws straight into a CAMetalLayer inside a native
// child view. Avalonia lays the view out and never sees a pixel of it.
internal sealed unsafe class NativeChildViewport : NativeControlHost, IViewport
{
    IntPtr _view;
    Engine? _engine;

    public PixelSize Drawn { get; private set; }
    public Stats? Reported { get; private set; }
    public int Dropped => 0;

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        _view = Native.spike_make_metal_view();
        return new PlatformHandle(_view, "NSView");
    }

    public bool Frame(double seconds)
    {
        uint w = 0, h = 0;
        if (_view == IntPtr.Zero || Native.spike_view_pixels(_view, &w, &h) == 0 || w == 0 || h == 0) return false;
        if (_engine == null)
        {
            _engine = new Engine(Native.SurfaceWindow, Native.spike_view_layer(_view), w, h);
        }
        var size = new PixelSize((int)w, (int)h);
        if (size != Drawn)
        {
            _engine.Resize(w, h);
            Drawn = size;
        }
        _engine.Draw(seconds);
        Reported = _engine.Measure();
        return true;
    }

    public void Close() => _engine?.Dispose();
}

using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.Composition;

namespace Orblit.Filament.Avalonia;

/// <summary>
/// The macOS path. The renderer draws into IOSurface buffers and the compositor reads each one as
/// a texture, so no pixel is copied by the CPU. Metal cannot order an imported image by itself, so
/// a frame is handed over with two shared events: ours says it is drawn, the compositor's says it
/// has finished reading it.
/// </summary>
internal sealed class SharedTexturePresenter : IFramePresenter
{
    // With one frame allowed in flight, a frame is released just after the next tick, ticks drop,
    // and Avalonia settles to its idle rate. Two is steady. The renderer rings four buffers.
    private const int FramesInFlight = 2;

    private readonly Control _owner;
    private readonly ICompositionGpuInterop _interop;
    private readonly CompositionDrawingSurface _surface;
    private readonly SharedEvent _renderedEvent = new();
    private readonly SharedEvent _releasedEvent = new();
    private readonly ICompositionImportedGpuSemaphore _rendered;
    private readonly ICompositionImportedGpuSemaphore _released;
    private readonly Dictionary<uint, ICompositionImportedGpuImage> _images = [];
    private ulong _submitted;
    private volatile Exception? _failure;

    public SharedTexturePresenter(Control owner, Compositor compositor, ICompositionGpuInterop interop)
    {
        _owner = owner;
        _interop = interop;
        _surface = compositor.CreateDrawingSurface();
        _rendered = interop.ImportSemaphore(Imported(_renderedEvent));
        _released = interop.ImportSemaphore(Imported(_releasedEvent));
        Visual = compositor.CreateSurfaceVisual();
        Visual.Surface = _surface;
        ElementComposition.SetElementChildVisual(owner, Visual);
    }

    public Surface Surface => new(SurfaceKind.Platform, 0);

    public bool IsBusy => _submitted - _releasedEvent.Value >= FramesInFlight;

    public Exception? Failure => _failure;

    private CompositionSurfaceVisual Visual { get; }

    public void Begin(Renderer renderer)
    {
    }

    public bool End(Renderer renderer, PixelSize size)
    {
        using var frame = renderer.CopyPresented();
        if (frame is null || frame.Surface == nint.Zero)
        {
            return false;
        }

        var scale = TopLevel.GetTopLevel(_owner)?.RenderScaling ?? 1.0;
        Visual.Size = new Vector2((float)(frame.Width / scale), (float)(frame.Height / scale));

        // The draw has waited for the GPU, so the frame is complete once the event says so.
        var number = ++_submitted;
        _renderedEvent.Signal(number);
        _surface.UpdateWithTimelineSemaphoresAsync(ImageOf(frame), _rendered, number, _released, number)
            .ContinueWith(
                update => _failure = update.Exception!.GetBaseException(),
                TaskContinuationOptions.OnlyOnFaulted);
        return true;
    }

    public void Paint(DrawingContext context, Rect bounds)
    {
    }

    public void Dispose()
    {
        ElementComposition.SetElementChildVisual(_owner, null);
        _ = ReleaseAsync();
    }

    private static PlatformHandle Imported(SharedEvent shared) =>
        new(shared.Handle, KnownPlatformGraphicsExternalSemaphoreHandleTypes.MetalSharedEvent);

    // Imported once per surface, because the renderer cycles through the same few.
    private ICompositionImportedGpuImage ImageOf(PresentedFrame frame)
    {
        if (!_images.TryGetValue(frame.SurfaceId, out var image))
        {
            image = _interop.ImportImage(
                new PlatformHandle(frame.Surface, KnownPlatformGraphicsExternalImageHandleTypes.IOSurfaceRef),
                new PlatformGraphicsExternalImageProperties
                {
                    Width = frame.Width,
                    Height = frame.Height,
                    Format = PlatformGraphicsExternalImageFormat.B8G8R8A8UNorm,
                    // Without this the frame is upside down.
                    TopLeftOrigin = true,
                });
            _images[frame.SurfaceId] = image;
        }

        return image;
    }

    // The compositor may still be reading, so the events outlive the control by this long.
    private async Task ReleaseAsync()
    {
        try
        {
            foreach (var image in _images.Values)
            {
                await image.DisposeAsync();
            }

            await _rendered.DisposeAsync();
            await _released.DisposeAsync();
        }
        finally
        {
            _renderedEvent.Dispose();
            _releasedEvent.Dispose();
        }
    }
}

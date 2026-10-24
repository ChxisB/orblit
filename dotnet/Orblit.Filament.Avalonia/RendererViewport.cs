using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.Composition;

namespace Orblit.Filament.Avalonia;

/// <summary>
/// Shows an Orblit renderer in an Avalonia window. The host calls <see cref="Frame"/> once a tick,
/// on the UI thread, and the control draws, sizes the renderer to itself and puts the picture on
/// the screen by the fastest path the system has. When the control leaves the window it frees the
/// renderer and everything the screen held of it.
/// </summary>
public sealed class RendererViewport : Control
{
    private readonly Presenters.Choose _choose;
    private IFramePresenter? _presenter;
    private PixelSize _drawn;

    public RendererViewport()
        : this(Presenters.ChooseAsync)
    {
    }

    internal RendererViewport(Presenters.Choose choose) => _choose = choose;

    /// <summary>The backend to start the renderer with. Read when the first frame is drawn.</summary>
    public Backend Backend { get; set; } = Backend.Default;

    /// <summary>The renderer, or null before the first frame and after the control has left its window.</summary>
    public Renderer? Renderer { get; private set; }

    /// <summary>Which path shows the frames. It is chosen once the control is in a window.</summary>
    public ViewportPath Path => (_presenter, Fallback) switch
    {
        (null, _) => ViewportPath.Pending,
        (_, null) => ViewportPath.SharedTexture,
        _ => ViewportPath.Readback,
    };

    /// <summary>Why the faster path was not taken, or null if it was or the path is not chosen yet.</summary>
    public string? Fallback { get; private set; }

    /// <summary>Frames not drawn or not shown because the screen had not caught up.</summary>
    public int DroppedFrames { get; private set; }

    internal Task Choosing { get; private set; } = Task.CompletedTask;

    /// <summary>
    /// Describes <paramref name="scene"/> to the renderer, draws it at <paramref name="seconds"/> and
    /// shows the result. False if nothing was drawn: the path is not chosen, the control has no size,
    /// or the screen is behind.
    /// </summary>
    public bool Frame(RenderScene scene, double seconds)
    {
        if (_presenter is null || PixelsOf(this) is not { Width: > 0, Height: > 0 } size)
        {
            return false;
        }

        if (_presenter.Failure is { } failure)
        {
            throw new InvalidOperationException("The screen refused a frame.", failure);
        }

        if (_presenter.IsBusy)
        {
            DroppedFrames++;
            return false;
        }

        var renderer = Renderer ??= new Renderer((uint)size.Width, (uint)size.Height, Backend, _presenter.Surface);
        if (size != _drawn)
        {
            renderer.Resize((uint)size.Width, (uint)size.Height);
            _drawn = size;
        }

        _presenter.Begin(renderer);
        renderer.Apply(scene, seconds);
        renderer.Draw(seconds);
        if (!_presenter.End(renderer, size))
        {
            DroppedFrames++;
        }

        return true;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        _presenter?.Paint(context, new Rect(Bounds.Size));
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Choosing = ChooseAsync();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Free();
    }

    private static PixelSize PixelsOf(Visual visual)
    {
        var scale = TopLevel.GetTopLevel(visual)?.RenderScaling ?? 1.0;
        return new PixelSize((int)(visual.Bounds.Width * scale), (int)(visual.Bounds.Height * scale));
    }

    private async Task ChooseAsync()
    {
        var (presenter, fallback) = await _choose(this);
        if (TopLevel.GetTopLevel(this) is null)
        {
            presenter.Dispose();
            return;
        }

        _presenter = presenter;
        Fallback = fallback;
        InvalidateVisual();
    }

    private void Free()
    {
        Renderer?.Dispose();
        Renderer = null;
        _presenter?.Dispose();
        _presenter = null;
        Fallback = null;
        _drawn = default;
    }
}

using Avalonia;
using Avalonia.Media;

namespace Orblit.Filament.Avalonia;

/// <summary>
/// One way of getting the renderer's frame onto the screen. The viewport calls it around each draw,
/// so what it asks for is only what the viewport uses.
/// </summary>
internal interface IFramePresenter : IDisposable
{
    /// <summary>The surface the renderer must be made with for this path.</summary>
    Surface Surface { get; }

    /// <summary>Whether the screen is still waiting on earlier frames, so that drawing another would only queue it.</summary>
    bool IsBusy { get; }

    /// <summary>A fault from the screen's side that the next frame must not hide, or null.</summary>
    Exception? Failure { get; }

    /// <summary>Called before the renderer draws.</summary>
    void Begin(Renderer renderer);

    /// <summary>Called after the renderer drew a frame of <paramref name="size"/>. False if there was nothing to show yet.</summary>
    bool End(Renderer renderer, PixelSize size);

    /// <summary>Draws what the control shows itself. The paths that show a visual of their own draw nothing.</summary>
    void Paint(DrawingContext context, Rect bounds);
}

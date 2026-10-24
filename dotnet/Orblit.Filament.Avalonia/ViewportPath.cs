namespace Orblit.Filament.Avalonia;

/// <summary>How a <see cref="RendererViewport"/> gets a frame from the renderer onto the screen.</summary>
public enum ViewportPath
{
    /// <summary>Not chosen yet. The control has to be in a window first, so that the compositor can be asked.</summary>
    Pending,

    /// <summary>The renderer draws into a texture the compositor reads. No pixel crosses the CPU.</summary>
    SharedTexture,

    /// <summary>The renderer draws offscreen and each frame is copied into a bitmap Avalonia draws.</summary>
    Readback,
}

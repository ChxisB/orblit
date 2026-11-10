namespace Orblit.Ui;

/// <summary>Where a <see cref="UiLayer"/> sends its pixels, for example an engine texture.</summary>
public interface IUiSurface : IDisposable
{
    /// <summary>
    /// Replaces the surface with a frame of premultiplied RGBA bytes, four per pixel, rows
    /// packed with no padding.
    /// </summary>
    void Upload(ReadOnlySpan<byte> pixels, int width, int height);
}

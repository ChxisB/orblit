using Avalonia;
using Avalonia.Media;

namespace Orblit.Filament.Avalonia.Tests;

/// <summary>A path that does nothing but write down what the viewport asked of it.</summary>
internal sealed class FakePresenter : IFramePresenter
{
    internal List<string> Calls { get; } = [];

    internal bool Busy { get; set; }

    internal bool Shows { get; set; } = true;

    internal Exception? Fault { get; set; }

    internal bool Disposed { get; private set; }

    internal ulong FramesAtBegin { get; private set; }

    internal ulong FramesAtEnd { get; private set; }

    public Surface Surface => Surface.Headless;

    public bool IsBusy => Busy;

    public Exception? Failure => Fault;

    public void Begin(Renderer renderer)
    {
        Calls.Add("begin");
        FramesAtBegin = renderer.RenderedFrames;
    }

    public bool End(Renderer renderer, PixelSize size)
    {
        Calls.Add($"end {size.Width}x{size.Height}");
        FramesAtEnd = renderer.RenderedFrames;
        return Shows;
    }

    public void Paint(DrawingContext context, Rect bounds) => Calls.Add("paint");

    public void Dispose() => Disposed = true;

    internal Presenters.Choose Choose(string? fallback = null) =>
        _ => Task.FromResult<(IFramePresenter, string?)>((this, fallback));
}

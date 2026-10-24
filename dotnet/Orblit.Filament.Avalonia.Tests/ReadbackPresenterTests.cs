using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;

namespace Orblit.Filament.Avalonia.Tests;

/// <summary>Checks the readback path against a headless renderer: what lands in the bitmap, and what the control paints.</summary>
public sealed class ReadbackPresenterTests
{
    private const int Width = 64;
    private const int Height = 48;
    private const int WaitFrames = 8;

    [RendererFact]
    public Task AFrameLandsInTheBitmapOpaque() => Headless.Run(() =>
    {
        using var renderer = new Renderer(Width, Height, Backend.Default, Surface.Headless);
        using var presenter = new ReadbackPresenter(new Control());

        Assert.True(Shown(renderer, presenter, new PixelSize(Width, Height)));

        var bitmap = presenter.Bitmap!;
        Assert.Equal(new PixelSize(Width, Height), bitmap.PixelSize);
        using var locked = bitmap.Lock();
        // An undrawn bitmap is zero in every channel, so a drawn sky shows in the alpha.
        for (var row = 0; row < Height; row++)
        {
            for (var column = 0; column < Width; column++)
            {
                var alpha = System.Runtime.InteropServices.Marshal.ReadByte(locked.Address, (row * locked.RowBytes) + (column * 4) + 3);
                Assert.Equal(255, alpha);
            }
        }

        return Task.CompletedTask;
    });

    [RendererFact]
    public Task ShowingAFrameAsksTheControlToDrawAgain() => Headless.Run(() =>
    {
        using var renderer = new Renderer(Width, Height, Backend.Default, Surface.Headless);
        var owner = new CountingControl { Width = Width, Height = Height };
        using var presenter = new ReadbackPresenter(owner);
        var window = new Window { Content = owner };
        window.Show();
        Headless.Render();
        var before = owner.Renders;

        Assert.True(Shown(renderer, presenter, new PixelSize(Width, Height)));
        Headless.Render();

        Assert.True(owner.Renders > before);
        window.Close();
        return Task.CompletedTask;
    });

    [RendererFact]
    public Task ACaptureOfAnotherSizeIsNotShown() => Headless.Run(() =>
    {
        using var renderer = new Renderer(Width, Height, Backend.Default, Surface.Headless);
        using var presenter = new ReadbackPresenter(new Control());

        Assert.False(Shown(renderer, presenter, new PixelSize(Width / 2, Height)));
        Assert.False(Shown(renderer, presenter, new PixelSize(Width, Height / 2)));

        Assert.Null(presenter.Bitmap);
        return Task.CompletedTask;
    });

    [RendererFact]
    public Task TheBitmapFollowsASizeChange() => Headless.Run(() =>
    {
        using var renderer = new Renderer(Width, Height, Backend.Default, Surface.Headless);
        using var presenter = new ReadbackPresenter(new Control());
        Assert.True(Shown(renderer, presenter, new PixelSize(Width, Height)));

        renderer.Resize(Width * 2, Height * 2);

        Assert.True(Shown(renderer, presenter, new PixelSize(Width * 2, Height * 2)));
        Assert.Equal(new PixelSize(Width * 2, Height * 2), presenter.Bitmap!.PixelSize);
        return Task.CompletedTask;
    });

    [RendererFact]
    public Task TheBitmapIsWhatTheControlPaints() => Headless.Run(() =>
    {
        using var renderer = new Renderer(Width, Height, Backend.Default, Surface.Headless);
        var owner = new PaintingControl { Width = Width, Height = Height };
        using var presenter = new ReadbackPresenter(owner);
        owner.Presenter = presenter;
        var window = new Window { Width = Width, Height = Height, Content = owner };
        window.Show();
        Headless.Render();
        var before = CentreOf(window);

        Assert.True(Shown(renderer, presenter, new PixelSize(Width, Height)));
        Headless.Render();

        Assert.NotEqual(before, CentreOf(window));
        window.Close();
        return Task.CompletedTask;
    });

    private static int CentreOf(Window window)
    {
        using var frame = window.CaptureRenderedFrame()!;
        using var locked = frame.Lock();
        return System.Runtime.InteropServices.Marshal.ReadInt32(
            locked.Address,
            (locked.Size.Height / 2 * locked.RowBytes) + (locked.Size.Width / 2 * 4));
    }

    // A capture arrives a frame or two after it is asked for, so this draws on until one is shown.
    private static bool Shown(Renderer renderer, ReadbackPresenter presenter, PixelSize size)
    {
        for (var frame = 0; frame < WaitFrames; frame++)
        {
            presenter.Begin(renderer);
            renderer.Draw(frame / 60.0);
            if (presenter.End(renderer, size))
            {
                return true;
            }
        }

        return false;
    }

    private sealed class PaintingControl : Control
    {
        internal ReadbackPresenter? Presenter { get; set; }

        public override void Render(DrawingContext context) => Presenter?.Paint(context, new Rect(Bounds.Size));
    }

    private sealed class CountingControl : Control
    {
        internal int Renders { get; private set; }

        public override void Render(DrawingContext context) => Renders++;
    }
}

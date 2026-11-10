using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;
using Orblit.Ui;
using Orblit.Ui.Sample;

namespace Orblit.Editor.Bridge.Checks;

public sealed class GameUiApp : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());

    public static AppBuilder BuildAvaloniaApp() => UiPlatform.Configure<GameUiApp>();
}

/// <summary>
/// Draws the sample game UI on a headless Avalonia thread. The engine owns the main thread, so the frame is
/// kept here as bytes and the check uploads it from the main thread.
/// </summary>
internal sealed class GameUiFrame : IUiSurface
{
    public byte[] Pixels { get; private set; } = [];

    public int Width { get; private set; }

    public int Height { get; private set; }

    public static GameUiFrame Draw(int width, int height)
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(GameUiApp));
        var frame = new GameUiFrame();
        session.Dispatch(() =>
        {
            using var layer = new UiLayer(new GameUi().Root, new PixelSize(width, height), frame);
            layer.Render();
            return 0;
        }, CancellationToken.None).Wait();
        return frame;
    }

    public void Upload(ReadOnlySpan<byte> pixels, int width, int height)
    {
        Pixels = pixels.ToArray();
        Width = width;
        Height = height;
    }

    /// <summary>The bytes of one pixel as red, green, blue, alpha.</summary>
    public (byte R, byte G, byte B, byte A) At(int x, int y)
    {
        var i = (y * Width + x) * 4;
        return (Pixels[i], Pixels[i + 1], Pixels[i + 2], Pixels[i + 3]);
    }

    public void Dispose()
    {
    }
}

using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;

namespace Orblit.Ui.Tests;

public sealed class TestApp : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());

    public static AppBuilder BuildAvaloniaApp() => UiPlatform.Configure<TestApp>();
}

/// <summary>Runs a test body on the UI thread of one shared headless Avalonia session.</summary>
internal static class UiThread
{
    static readonly HeadlessUnitTestSession s_session = HeadlessUnitTestSession.StartNew(typeof(TestApp));

    public static void Run(Action body) => s_session.Dispatch(() => { body(); return 0; }, CancellationToken.None).Wait();
}

/// <summary>A surface that keeps what it was given.</summary>
internal sealed class FakeSurface : IUiSurface
{
    public int Uploads { get; private set; }

    public int Disposals { get; private set; }

    public int Width { get; private set; }

    public int Height { get; private set; }

    public byte[] Pixels { get; private set; } = [];

    public void Upload(ReadOnlySpan<byte> pixels, int width, int height)
    {
        Uploads++;
        Width = width;
        Height = height;
        Pixels = pixels.ToArray();
    }

    public void Dispose() => Disposals++;

    /// <summary>The bytes of one pixel as red, green, blue, alpha.</summary>
    public (byte R, byte G, byte B, byte A) At(int x, int y)
    {
        var i = (y * Width + x) * 4;
        return (Pixels[i], Pixels[i + 1], Pixels[i + 2], Pixels[i + 3]);
    }
}

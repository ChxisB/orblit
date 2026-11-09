using Avalonia;
using Avalonia.Headless;

namespace Orblit.Ui;

/// <summary>Starts Avalonia with no window of its own, drawing with Skia and a bundled font.</summary>
public static class UiPlatform
{
    // UseHeadlessDrawing off is what makes the frames hold real pixels.
    public static AppBuilder Configure<TApp>() where TApp : Application, new() =>
        AppBuilder.Configure<TApp>()
            .UseSkia()
            .WithInterFont()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

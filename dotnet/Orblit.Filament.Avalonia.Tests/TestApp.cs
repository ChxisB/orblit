using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;

namespace Orblit.Filament.Avalonia.Tests;

/// <summary>
/// An application with a theme, so that a window shows its content, and no screen to draw on. It
/// draws with Skia, because the stub that headless drawing gives a bitmap forgets what was copied in.
/// </summary>
public sealed class TestApp : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<TestApp>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

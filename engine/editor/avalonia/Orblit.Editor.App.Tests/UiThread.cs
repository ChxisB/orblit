using Avalonia;
using Avalonia.Headless;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Tests;

public sealed class TestApp : Application
{
    public override void Initialize() => EditorTheme.Apply(this);

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<TestApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true });
}

/// <summary>Runs a test body on the UI thread of one shared headless Avalonia session.</summary>
internal static class UiThread
{
    static readonly HeadlessUnitTestSession s_session = HeadlessUnitTestSession.StartNew(typeof(TestApp));

    public static void Run(Action body) => s_session.Dispatch(() => { body(); return 0; }, CancellationToken.None).Wait();

    /// <summary>Runs a body that awaits, such as a read of the clipboard, to its end.</summary>
    // The session runs the UI thread until only a task with a result ends. A bare Task is taken as the result itself,
    // so the body would stop at its first await and its asserts never run.
    public static void Run(Func<Task> body) => s_session.Dispatch(async () =>
    {
        await body();
        return 0;
    }, CancellationToken.None).Wait();
}

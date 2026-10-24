using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;

namespace Orblit.Filament.Avalonia.Tests;

/// <summary>
/// One headless Avalonia for every test. The compositor here has no GPU interop, so a viewport
/// takes the readback path, which is the path Windows and Linux take too.
/// </summary>
internal static class Headless
{
    private static readonly HeadlessUnitTestSession Session = HeadlessUnitTestSession.StartNew(typeof(TestApp));

    /// <summary>
    /// Runs <paramref name="test"/> on the UI thread and finishes when it does. The session's own
    /// dispatch returns before an async body is done and drops what it throws, which would make
    /// every failed assertion after an await pass.
    /// </summary>
    internal static Task Run(Func<Task> test)
    {
        var done = new TaskCompletionSource();
        _ = Session.Dispatch(
            async () =>
            {
                try
                {
                    await test();
                    done.SetResult();
                }
                catch (Exception ex)
                {
                    done.SetException(ex);
                }
            },
            CancellationToken.None);
        return done.Task;
    }

    /// <summary>Lets the window draw what has been invalidated since it last did.</summary>
    internal static void Render()
    {
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Puts <paramref name="viewport"/> in a window of the given size and waits until it has chosen
    /// how to show frames.
    /// </summary>
    internal static async Task<Window> ShowAsync(RendererViewport viewport, int width, int height)
    {
        viewport.Width = width;
        viewport.Height = height;
        var window = new Window { Width = width, Height = height, Content = viewport };
        window.Show();
        window.UpdateLayout();
        await viewport.Choosing;
        return window;
    }
}

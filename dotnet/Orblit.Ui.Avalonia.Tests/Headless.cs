using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;

namespace Orblit.Ui.Avalonia.Tests;

/// <summary>One headless Avalonia for every test.</summary>
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

    /// <summary>Runs <paramref name="test"/> on the UI thread, for a test with nothing to await.</summary>
    internal static Task Run(Action test) =>
        Run(() =>
        {
            test();
            return Task.CompletedTask;
        });

    /// <summary>Lets the window draw what has been invalidated since it last did.</summary>
    internal static void Render()
    {
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Puts <paramref name="content"/> in a window of the given size and lays it out.</summary>
    internal static Window Show(Control content, double width = 400, double height = 300)
    {
        var window = new Window { Width = width, Height = height, Content = content };
        window.Show();
        window.UpdateLayout();
        return window;
    }
}

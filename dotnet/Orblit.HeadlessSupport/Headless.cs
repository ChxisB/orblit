using Avalonia.Headless;
using Avalonia.Threading;

namespace Orblit.HeadlessSupport;

/// <summary>One headless Avalonia for every test in a process.</summary>
public static class Headless
{
    private static readonly HeadlessUnitTestSession Session = HeadlessUnitTestSession.StartNew(typeof(TestApp));

    /// <summary>
    /// Runs <paramref name="test"/> on the UI thread and finishes when it does. The body returns a
    /// value so that the session takes it as a task to wait for. An async lambda that returns
    /// nothing binds to the overload that takes any result, so the session would call the body,
    /// close the test's application and stop running the UI thread at its first await.
    /// </summary>
    public static Task Run(Func<Task> test) =>
        Session.Dispatch(
            async () =>
            {
                await test();
                return true;
            },
            CancellationToken.None);

    /// <summary>Runs <paramref name="test"/> on the UI thread, for a test with nothing to await.</summary>
    public static Task Run(Action test) =>
        Run(() =>
        {
            test();
            return Task.CompletedTask;
        });

    /// <summary>Lets the window draw what has been invalidated since it last did.</summary>
    public static void Render()
    {
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }
}

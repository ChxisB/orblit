namespace Orblit.Asset.Tests;

// Threads have no turn of an event loop to wait on, so a test waits for what it
// expects, or for long enough to see that something does not happen.
internal static class Waits
{
    public static async Task Until(Func<bool> condition, string what)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, $"Timed out waiting for {what}.");
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }
    }

    public static Task Settle() => Task.Delay(150, TestContext.Current.CancellationToken);
}

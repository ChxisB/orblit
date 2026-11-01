namespace Orblit.Stage.Tests;

// A bake lands on any thread, so a test waits for what it expects.
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
}

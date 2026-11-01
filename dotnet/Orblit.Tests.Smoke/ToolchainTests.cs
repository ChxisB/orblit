namespace Orblit.Tests.Smoke;

public sealed class ToolchainTests
{
    [Fact]
    public void RuntimeIsNet10OrLater()
    {
        Assert.True(Environment.Version.Major >= 10);
    }
}

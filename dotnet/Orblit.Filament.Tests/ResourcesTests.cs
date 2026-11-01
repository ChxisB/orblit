namespace Orblit.Filament.Tests;

/// <summary>
/// The store is native, so these run against it and not against a
/// recording of the calls.
/// </summary>
public sealed class ResourcesTests
{
    private static readonly byte[] Bytes = [1, 2, 3, 250];

    [Fact]
    public void ANameIsThePathUnderTheRenderersOwnPrefix()
    {
        Assert.Equal("orblit:resource/models/robot.glb", RenderResources.NameFor("models/robot.glb"));
    }

    [RendererFact]
    public void ProvidingKeepsTheBytesUnderTheNameUntilTheyAreReleased()
    {
        const string name = "orblit:resource/provided-a.glb";
        RenderResources.Provide(name, Bytes);

        Assert.True(RenderResources.Release(name));
    }

    [RendererFact]
    public void ReleasingSaysWhetherThereWasAnythingToLetGoOf()
    {
        const string name = "orblit:resource/provided-b.glb";
        Assert.False(RenderResources.Release(name));

        RenderResources.Provide(name, Bytes);
        Assert.True(RenderResources.Release(name));
        Assert.False(RenderResources.Release(name));
    }

    [Fact]
    public void AResourceWithNoNameIsRefusedBeforeItIsSent()
    {
        Assert.ThrowsAny<ArgumentException>(() => RenderResources.Provide("", Bytes));
        Assert.ThrowsAny<ArgumentException>(() => RenderResources.Release(""));
    }
}

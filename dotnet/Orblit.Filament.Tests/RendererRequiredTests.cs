namespace Orblit.Filament.Tests;

/// <summary>
/// A leg that builds the renderer sets <c>ORBLIT_REQUIRE_RENDERER=1</c>. Without it a leg whose
/// build quietly produced no library would skip every draw test and stay green.
/// </summary>
public sealed class RendererRequiredTests
{
    private static readonly string[] Nowhere = ["/nowhere/liborblit_filament.so"];

    [Fact]
    public void AMissingLibraryOnALegThatRequiresItIsAFailureThatSaysWhereItLooked()
    {
        var message = RendererLibrary.UnmetRequirement(required: true, Nowhere);

        Assert.NotNull(message);
        Assert.Contains(RendererLibrary.RequireVariable, message);
        Assert.Contains(Nowhere[0], message);
    }

    [Fact]
    public void AMissingLibraryOnALegThatDoesNotRequireItIsLeftToTheSkips()
    {
        Assert.Null(RendererLibrary.UnmetRequirement(required: false, Nowhere));
    }

    [Fact]
    public void AFoundLibraryMeetsTheRequirement()
    {
        string[] found = [typeof(RendererLibrary).Assembly.Location];

        Assert.Null(RendererLibrary.UnmetRequirement(required: true, found));
    }

    [Fact]
    public void ThisLegHasTheLibraryItRequires()
    {
        // Assert.Null cuts a long value short, and the paths are the part a person needs.
        if (RendererLibrary.UnmetRequirement(RendererLibrary.Required, RendererLibrary.Searched) is { } message)
        {
            Assert.Fail(message);
        }
    }
}

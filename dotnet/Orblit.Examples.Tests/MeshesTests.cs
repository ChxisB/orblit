using Orblit.Filament;

namespace Orblit.Examples.Tests;

/// <summary>
/// Where the meshes example gets its file. The fixture ran with
/// <c>ORBLIT_MESH</c> unset and nothing typed, so every copy there is the cube.
/// </summary>
public sealed class MeshesTests
{
    private const string Typed = "Path to a .glb or .gltf";

    private static readonly RenderCamera Camera = new ViewPoint().ToRenderCamera();

    [Fact]
    public void TheFileNamedInTheEnvironmentIsOnEveryCopy()
    {
        var example = new MeshesExample(name => name == "ORBLIT_MESH" ? "crate.glb" : null);

        Assert.Equal(["crate.glb", "crate.glb", "crate.glb", null], example.Scene(Camera, 0).Objects.Select(one => one.Mesh));
        Assert.Equal("crate.glb", example.Control<TextEntry>(Typed).Value);
    }

    [Fact]
    public void ATypedPathIsTrimmedAndClearsTheLastNote()
    {
        var example = new MeshesExample(_ => null) { Note = "crate.glb could not be read" };

        example.Control<TextEntry>(Typed).Change("  /models/crate.glb ");

        Assert.Equal("/models/crate.glb", example.Scene(Camera, 0).Objects[0].Mesh);
        Assert.Null(example.Note);
    }
}

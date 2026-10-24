using System.Buffers.Binary;
using System.Text;

namespace Orblit.Filament.Tests;

public sealed class ResourcesAddedTests
{
    private const string TriangleJson =
        """{"asset":{"version":"2.0"},"scene":0,"scenes":[{"nodes":[0]}],"nodes":[{"mesh":0}],"meshes":[{"primitives":[{"attributes":{"POSITION":0}}]}],"accessors":[{"bufferView":0,"componentType":5126,"count":3,"type":"VEC3","min":[0,0,0],"max":[1,1,0]}],"bufferViews":[{"buffer":0,"byteLength":36,"target":34962}],"buffers":[{"byteLength":36}]}""";

    /// <summary>One triangle as a binary glTF, small enough to write out here and enough for the renderer to describe.</summary>
    private static byte[] Triangle()
    {
        var json = Encoding.UTF8.GetBytes(TriangleJson.PadRight((TriangleJson.Length + 3) / 4 * 4));
        var positions = new byte[36];
        float[] corners = [0, 0, 0, 1, 0, 0, 0, 1, 0];
        Buffer.BlockCopy(corners, 0, positions, 0, positions.Length);

        var file = new byte[12 + 8 + json.Length + 8 + positions.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(0), 0x46546C67);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(4), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(8), (uint)file.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(12), (uint)json.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(16), 0x4E4F534A);
        json.CopyTo(file, 20);
        var bin = 20 + json.Length;
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(bin), (uint)positions.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(bin + 4), 0x004E4942);
        positions.CopyTo(file, bin + 8);
        return file;
    }

    [RendererFact]
    public void NothingAtAllIsAcceptedAndKeptUnderItsName()
    {
        const string name = "orblit:resource/added-empty.bin";

        RenderResources.Provide(name, ReadOnlySpan<byte>.Empty);

        Assert.True(RenderResources.Release(name));
    }

    [RendererFact]
    public void ProvidingAgainReplacesWhatIsKeptAndOneReleaseLetsGoOfIt()
    {
        const string name = "orblit:resource/added-twice.bin";

        RenderResources.Provide(name, [1, 2, 3]);
        RenderResources.Provide(name, [4, 5]);

        Assert.True(RenderResources.Release(name));
        Assert.False(RenderResources.Release(name));
    }

    [RendererFact]
    public void AModelProvidedByNameIsLoadedFromTheStoreAndNotFromDisk()
    {
        var name = RenderResources.NameFor("added-triangle.glb");
        RenderResources.Provide(name, Triangle());
        try
        {
            using var renderer = new Renderer(64, 48);
            renderer.Apply(SceneBuilders.Of([SceneBuilders.Plain(1) with { Mesh = name }]));
            renderer.Draw(0);

            Assert.Contains(renderer.GetNotes(), note => note.About == AssetInfo.NotePrefix + name);
        }
        finally
        {
            RenderResources.Release(name);
        }
    }

    [Fact]
    public void ANullNameIsRefusedLikeAnEmptyOne()
    {
        Assert.Throws<ArgumentNullException>(() => RenderResources.Provide(null!, [1]));
        Assert.Throws<ArgumentNullException>(() => RenderResources.Release(null!));
    }

    [Fact]
    public void AnAssetFileKeepsTheRangeItIsGivenAndRefusesNoPath()
    {
        var file = new AssetFile("/p", 0, 0);

        Assert.Equal(("/p", 0L, 0L), (file.Path, file.Offset, file.Length));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AssetFile("/p", -1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AssetFile("/p", 0, -1));
    }
}

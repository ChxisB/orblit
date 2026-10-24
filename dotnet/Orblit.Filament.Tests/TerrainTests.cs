namespace Orblit.Filament.Tests;

/// <summary>
/// Ported from terrain_test.dart. Terrain as it crosses to the renderer: three arrays read in
/// step, and which of a terrain's pictures and regions travel at all.
/// </summary>
public sealed class TerrainTests
{
    private static RenderScene SceneWith(params RenderTerrain[] terrain) =>
        SceneBuilders.Of([], SceneBuilders.CameraAt(0, 10, 10)) with { Terrain = terrain };

    private static TerrainRegion Region(int x, int z, int revision = 1, int size = 16)
    {
        var texels = size * size;
        var heights = new float[texels];
        heights[0] = 2.5f;
        var cover = new uint[texels];
        cover[0] = 0xF8000001;
        var colour = new byte[texels * 4];
        Array.Fill(colour, (byte)200, 0, 4);
        return new TerrainRegion(x, z, heights, cover, colour) { Revision = revision };
    }

    private static byte[] Picture(int size, byte value)
    {
        var bytes = new byte[size * size * 4];
        Array.Fill(bytes, value);
        return bytes;
    }

    private static RenderTerrain Ground(
        IReadOnlyList<TerrainRegion>? regions = null,
        int regionSize = 16,
        IReadOnlyList<TerrainSet>? sets = null,
        int picturesRevision = 0) =>
        new(
            3,
            regions ?? [Region(0, 0), Region(-1, 2)],
            sets ?? [new TerrainSet { Albedo = Picture(2, 10), Normal = Picture(2, 20) }, new TerrainSet { TileSize = 8, Triplanar = true }],
            new TerrainOptions
            {
                RegionSize = regionSize,
                Spacing = 0.5,
                MeshSize = 32,
                Levels = 4,
                AutoSteep = 2,
                AutoFlat = 5,
                PicturesRevision = picturesRevision,
                CastShadows = false,
            });

    private static Held Holding(RenderTerrain terrain)
    {
        var held = new Held();
        held.Remember(SceneWith(terrain));
        return held;
    }

    [Fact]
    public void ASceneWithNoTerrainSendsNone()
    {
        var message = SceneWith().ToMessage(1);
        Assert.DoesNotContain(message.Keys, key => key.StartsWith("terrain", StringComparison.Ordinal));
    }

    [Fact]
    public void IsLaidOutAsTheRendererReadsItAndUsedUpExactly()
    {
        var read = new Reader(SceneWith(Ground()).ToMessage(1));
        Assert.Equal([1], read.Ints(1));
        Assert.Equal(
            [
                3, // key
                2, // receives shadows, casts none
                16, 32, 4, // region size, mesh size, levels
                2, 5, // automatic steep and flat
                2, 2, 1, // sets, their size, and the pictures came
                0x2, // the second set is triplanar
                2, // regions
            ],
            read.Ints(RenderTerrain.HeaderInts));
        Assert.Equal([0.5f, 0.87f, 1f, 0.1f], read.Floats(RenderTerrain.Stride));
        Assert.Equal([4f, 8f], read.Floats(2));
        Assert.Equal([0, 0, 1, -1, 2, 1], read.Ints(2 * RenderTerrain.RegionInts));

        // Albedos, then normals; a set with none is the plain pixel.
        const int layer = 2 * 2 * 4;
        Assert.All(read.Bytes(layer), one => Assert.Equal(10, one));
        Assert.Equal(Repeated(TerrainSet.PlainAlbedo, 4), read.Bytes(layer));
        Assert.All(read.Bytes(layer), one => Assert.Equal(20, one));
        Assert.Equal(Repeated(TerrainSet.PlainNormal, 4), read.Bytes(layer));

        for (var r = 0; r < 2; r++)
        {
            var maps = read.Bytes(16 * 16 * 12);
            Assert.Equal(2.5f, BitConverter.ToSingle(maps, 0));
            Assert.Equal(0xF8000001, BitConverter.ToUInt32(maps, 16 * 16 * 4));
            Assert.Equal([200, 200, 200, 200, 0], maps[(16 * 16 * 8)..((16 * 16 * 8) + 5)]);
        }

        read.Finished();
    }

    [Fact]
    public void ATerrainTheRendererHoldsSendsItsSettingsOnly()
    {
        var terrain = Ground();
        var message = SceneWith(terrain).ToMessage(1, held: Holding(terrain));
        var read = new Reader(message);
        read.Ints(1);
        Assert.Equal(0, read.Ints(RenderTerrain.HeaderInts)[9]);
        read.Floats(RenderTerrain.Stride + 2);
        Assert.Equal([0, 0, 0, -1, 2, 0], read.Ints(2 * RenderTerrain.RegionInts));
        read.Finished();
        Assert.Empty(SceneBuilders.Read<byte[]>(message, "terrainData"));
    }

    [Fact]
    public void ARegionWhoseRevisionMovedIsTheOnlyOneSent()
    {
        var before = Ground();
        var after = Ground([Region(0, 0), Region(-1, 2, revision: 2)]);
        var read = new Reader(SceneWith(after).ToMessage(1, held: Holding(before)));
        read.Ints(1 + RenderTerrain.HeaderInts);
        read.Floats(RenderTerrain.Stride + 2);
        Assert.Equal([0, 0, 0, -1, 2, 1], read.Ints(2 * RenderTerrain.RegionInts));
        read.Bytes(16 * 16 * 12);
        read.Finished();
    }

    [Fact]
    public void ANewRegionSizeSendsEveryRegionAgain()
    {
        var before = Ground();
        var after = Ground([Region(0, 0, size: 32), Region(-1, 2, size: 32)], regionSize: 32);
        var message = SceneWith(after).ToMessage(1, held: Holding(before));
        Assert.Equal(2 * 32 * 32 * 12, SceneBuilders.Read<byte[]>(message, "terrainData").Length);
    }

    [Fact]
    public void PicturesTravelAgainWhenTheirRevisionCountOrSizeMoves()
    {
        var held = Holding(Ground());

        bool Sends(RenderTerrain terrain) =>
            SceneBuilders.Read<int[]>(SceneWith(terrain).ToMessage(1, held: held), "terrainInts")[1 + 9] == 1;

        Assert.False(Sends(Ground()));
        Assert.True(Sends(Ground(picturesRevision: 1)));
        Assert.True(Sends(Ground(sets: [new TerrainSet()])));
        Assert.True(Sends(Ground(sets: [new TerrainSet { Albedo = Picture(4, 1) }])));
    }

    [Fact]
    public void ATerrainNamedTwiceIsAMistake()
    {
        Assert.ThrowsAny<ArgumentException>(() => SceneWith(Ground(), Ground()).ToMessage(1));
    }

    [Fact]
    public void ARegionSizeItCannotDraw()
    {
        foreach (var size in new[] { 8, 24, 4096 })
        {
            Assert.ThrowsAny<ArgumentException>(() => new RenderTerrain(1, options: new TerrainOptions { RegionSize = size }));
        }
    }

    [Fact]
    public void AMeshALevelCountOrAnAutomaticSetOutOfRange()
    {
        Assert.ThrowsAny<ArgumentException>(() => new RenderTerrain(1, options: new TerrainOptions { MeshSize = 33 }));
        Assert.ThrowsAny<ArgumentException>(() => new RenderTerrain(1, options: new TerrainOptions { Levels = 0 }));
        Assert.ThrowsAny<ArgumentException>(() => new RenderTerrain(1, options: new TerrainOptions { AutoFlat = 32 }));
    }

    [Fact]
    public void PicturesOfDifferentSizes()
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            new RenderTerrain(
                1,
                sets: [new TerrainSet { Albedo = Picture(4, 0) }, new TerrainSet { Normal = Picture(2, 0) }]));
    }

    [Fact]
    public void ARegionTheWrongSizeOrOneNamedTwice()
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            new RenderTerrain(1, [Region(0, 0)], options: new TerrainOptions { RegionSize = 32 }));
        Assert.ThrowsAny<ArgumentException>(() =>
            new RenderTerrain(1, [Region(0, 0), Region(0, 0)], options: new TerrainOptions { RegionSize = 16 }));
    }

    [Fact]
    public void RegionsFurtherApartThanTheRendererCanIndex()
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            new RenderTerrain(1, [Region(-64, 0), Region(64, 0)], options: new TerrainOptions { RegionSize = 16 }));
        Assert.Equal(
            2,
            new RenderTerrain(1, [Region(0, -63), Region(0, 64)], options: new TerrainOptions { RegionSize = 16 }).Regions.Count);
    }

    [Fact]
    public void MapsThatAreNotASquareOfTexels()
    {
        Assert.ThrowsAny<ArgumentException>(() => new TerrainRegion(0, 0, new float[20], new uint[20], new byte[80]));
    }

    private static byte[] Repeated(byte[] pixel, int times) => [.. Enumerable.Range(0, times).SelectMany(_ => pixel)];

    // Reads a terrain message part by part, the way the renderer does, and fails unless every part
    // is there and nothing is left over.
    private sealed class Reader(IReadOnlyDictionary<string, object> message)
    {
        private readonly int[] _ints = SceneBuilders.Read<int[]>(message, "terrainInts");
        private readonly float[] _floats = SceneBuilders.Read<float[]>(message, "terrainFloats");
        private readonly byte[] _data = SceneBuilders.Read<byte[]>(message, "terrainData");
        private int _intAt;
        private int _floatAt;
        private int _dataAt;

        public int[] Ints(int count) => Take(_ints, ref _intAt, count);

        public float[] Floats(int count) => Take(_floats, ref _floatAt, count);

        public byte[] Bytes(int count) => Take(_data, ref _dataAt, count);

        public void Finished()
        {
            Assert.True(_intAt == _ints.Length, "whole numbers left over");
            Assert.True(_floatAt == _floats.Length, "floats left over");
            Assert.True(_dataAt == _data.Length, "bytes left over");
        }

        private static T[] Take<T>(T[] from, ref int at, int count)
        {
            var taken = from[at..(at + count)];
            at += count;
            return taken;
        }
    }
}

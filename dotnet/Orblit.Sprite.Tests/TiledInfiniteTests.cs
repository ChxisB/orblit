using static Orblit.Sprite.Tests.Tree;
using static Orblit.Sprite.Tests.TiledFixture;

namespace Orblit.Sprite.Tests;

public class TiledInfiniteTests
{
    private static readonly OrderedDictionary<string, object?> Grass = SetOf("grass", 1);

    private static TileMap? Read(params object?[] layers) => TileMap.Read(MapOf(layers, [Grass], infinite: true));

    // Two 2 by 2 chunks, one up and left of the origin and one at it.
    private static OrderedDictionary<string, object?> Chunks() =>
        ChunksOf("ground", 2, (-2, -2, [1, 2, 3, 4]), (0, 0, [5, 0, 0, 6]));

    private static TileLayer Ground(TileMap? map) => Some(Some(map).Layer("ground"));

    // an infinite map

    [Fact]
    public void KeepsEachTileAtTheCellItsChunkPutsItIn()
    {
        var ground = Ground(Read(Chunks()));

        Assert.Equal(1, ground.At(-2, -2));
        Assert.Equal(2, ground.At(-1, -2));
        Assert.Equal(3, ground.At(-2, -1));
        Assert.Equal(4, ground.At(-1, -1));
        Assert.Equal(5, ground.At(0, 0));
        Assert.Equal(6, ground.At(1, 1));
    }

    [Fact]
    public void TheCellsBetweenTheChunksAreEmpty()
    {
        var ground = Ground(Read(Chunks()));

        Assert.Equal(0, ground.At(0, -1));
        Assert.Equal(0, ground.At(-1, 0));
        Assert.Equal(0, ground.At(1, 0));
        Assert.Equal(0, ground.At(0, 1));
    }

    [Fact]
    public void OutsideEveryChunkIsEmptyRatherThanAnError()
    {
        var ground = Ground(Read(Chunks()));

        Assert.Equal(0, ground.At(2, 2));
        Assert.Equal(0, ground.At(-3, -3));
        Assert.Equal(0, ground.At(100, -100));

        // Just past one edge with the other coordinate inside the layer.
        Assert.Equal(0, ground.At(2, -2));
        Assert.Equal(0, ground.At(-2, 2));
        Assert.Equal(0, ground.At(-3, 0));
        Assert.Equal(0, ground.At(0, -3));
    }

    [Fact]
    public void TheLayerStartsWhereItsFirstCellIs()
    {
        var ground = Ground(Read(Chunks()));

        Assert.Equal((-2L, -2L), (ground.Left, ground.Top));
        Assert.Equal((4L, 4L), (ground.Width, ground.Height));
    }

    [Fact]
    public void ChunksInAnyOrderGiveTheSameLayer()
    {
        var reversed = ChunksOf("ground", 2, (0, 0, [5, 0, 0, 6]), (-2, -2, [1, 2, 3, 4]));

        Assert.Equal(Ground(Read(Chunks())).Tiles, Ground(Read(reversed)).Tiles);
    }

    [Fact]
    public void AMapAsksAboutItsCellsTheSameWay()
    {
        var map = Some(Read(Chunks()));

        Assert.True(map.IsSolidAt(-2, -2));
        Assert.False(map.IsSolidAt(-1, 0));
        Assert.Equal((-1L, -2L), map.CellAt(-16, -32));
        Assert.Equal("grass", Some(map.SetFor(Ground(map).At(-1, -1))).Name);
    }

    [Fact]
    public void TheSizeALayerWritesIsNotBelieved()
    {
        // Tiled writes a size and a start for an infinite layer, and either
        // can be out of date after the chunks were edited.
        var stale = With(Chunks(), ("width", 99L), ("height", 99L), ("startx", 7L));
        var ground = Ground(Read(stale));

        Assert.Equal((-2L, 4L), (ground.Left, ground.Width));
        Assert.Equal(6, ground.At(1, 1));
    }

    [Fact]
    public void ALayerWithNoChunksIsEmptyNotUnreadable()
    {
        var map = Read(ChunksOf("ground", 2));

        Assert.NotNull(map);
        Assert.Equal(0, Ground(map).At(0, 0));
        Assert.Equal(0, Ground(map).Width);
    }

    [Fact]
    public void AChunkWithFewerTilesThanItsSizeIsNotRead()
    {
        // A map that silently lost a floor is worse than one that did not load.
        var shortOne = ChunksOf("ground", 2, (0, 0, [1, 2, 3]));

        Assert.Null(Read(shortOne));
    }

    [Fact]
    public void AChunkOfNegativeSizeIsNotRead()
    {
        var upside = With(Chunks(), ("chunks", List(Map(("x", 0L), ("y", 0L), ("width", -2L), ("height", 2L), ("data", List(1L, 2L))))));

        Assert.Null(Read(upside));
    }

    [Fact]
    public void AChunkWithNoTilesOfItsOwnIsNotRead()
    {
        var bare = With(Chunks(), ("chunks", List(Map(("x", 0L), ("y", 0L), ("width", 2L), ("height", 2L)))));

        Assert.Null(Read(bare));
    }

    [Fact]
    public void AChunkThatIsNotAnObjectIsNotRead()
    {
        Assert.Null(Read(With(Chunks(), ("chunks", List(1L)))));
    }

    [Fact]
    public void ChunksOfBase64DataAreNotReadLikeAnyOtherLayer()
    {
        var packed = With(Chunks(), ("encoding", "base64"));

        Assert.Null(Read(packed));
    }

    [Fact]
    public void ChunksTooFarApartAreNotRead()
    {
        // One stray chunk would otherwise ask for memory for all the space
        // between it and the rest.
        var far = ChunksOf("ground", 2, (0, 0, [1, 2, 3, 4]), (100000, 100000, [1, 2, 3, 4]));

        Assert.Null(Read(far));
    }
}

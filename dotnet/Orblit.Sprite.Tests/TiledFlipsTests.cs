using static Orblit.Sprite.Tests.Tree;
using static Orblit.Sprite.Tests.TiledFixture;

namespace Orblit.Sprite.Tests;

public class TiledFlipsTests
{
    // The three flip bits as Tiled writes them above a tile's number.
    private const long Horizontal = 0x80000000L;
    private const long Vertical = 0x40000000L;
    private const long Diagonal = 0x20000000L;

    // Tile 1 and tile 9 are the first tiles of the grass and stone sets.
    private static readonly object?[] Sets = [SetOf("grass", 1), SetOf("stone", 9)];

    private static TileMap Read(params long[] data) =>
        Some(TileMap.Read(MapOf([LayerOf("ground", 2, data)], Sets)));

    private static TileLayer Ground(TileMap map) => Some(map.Layer("ground"));

    // a flipped tile

    [Fact]
    public void IsLookedUpByItsNumberAlone()
    {
        var ground = Ground(Read(1 | Horizontal, 2 | Vertical, 3 | Diagonal, 9));

        Assert.Equal(1, ground.At(0, 0));
        Assert.Equal(2, ground.At(1, 0));
        Assert.Equal(3, ground.At(0, 1));
        Assert.Equal(9, ground.At(1, 1));
    }

    [Fact]
    public void StillComesFromTheSetItsNumberBelongsTo()
    {
        // A flag read as part of the number picks the last set for everything.
        var map = Read(9 | Horizontal, 1 | Horizontal | Vertical, 0, 0);
        var ground = Ground(map);

        Assert.Equal("stone", Some(map.SetFor(ground.At(0, 0))).Name);
        Assert.Equal("grass", Some(map.SetFor(ground.At(1, 0))).Name);
        Assert.Equal((0L, 0L, 16L, 16L), Some(map.SetFor(ground.At(0, 0))).RectOf(ground.At(0, 0)));
    }

    [Fact]
    public void ReportsWhichWayItIsTurned()
    {
        var ground = Ground(Read(1 | Horizontal, 2 | Vertical, 3 | Diagonal, 4 | Horizontal | Vertical | Diagonal));

        Assert.Equal(new TileFlips(true, false, false), ground.FlipsAt(0, 0));
        Assert.Equal(new TileFlips(false, true, false), ground.FlipsAt(1, 0));
        Assert.Equal(new TileFlips(false, false, true), ground.FlipsAt(0, 1));
        Assert.Equal(new TileFlips(true, true, true), ground.FlipsAt(1, 1));
    }

    [Fact]
    public void ATileThatIsNotTurnedSaysSo()
    {
        var ground = Ground(Read(1, 0, 0, 0));

        var none = new TileFlips(false, false, false);
        Assert.Equal(none, ground.FlipsAt(0, 0));
        Assert.Equal(none, ground.FlipsAt(1, 1));
        Assert.Equal(none, ground.FlipsAt(-1, 0));
        Assert.Equal(none, ground.FlipsAt(5, 5));
    }

    [Fact]
    public void IsSolidAndAFlagWithNoTileIsNot()
    {
        var map = Read(1 | Horizontal, Horizontal, 0, 0);

        Assert.True(map.IsSolidAt(0, 0));
        Assert.False(map.IsSolidAt(1, 0));
        Assert.True(Ground(map).IsEmptyAt(1, 0));
    }

    [Fact]
    public void KeepsItsFlipsInAnInfiniteMap()
    {
        var map = Some(TileMap.Read(MapOf(
            [ChunksOf("ground", 2, (-2, 0, [1 | Vertical, 0, 0, 2 | Diagonal]))],
            Sets,
            infinite: true)));
        var ground = Ground(map);

        Assert.Equal(1, ground.At(-2, 0));
        Assert.True(ground.FlipsAt(-2, 0).Vertical);
        Assert.Equal(2, ground.At(-1, 1));
        Assert.True(ground.FlipsAt(-1, 1).Diagonal);
    }
}

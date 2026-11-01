using static Orblit.Sprite.Tests.Tree;
using static Orblit.Sprite.Tests.TiledFixture;

namespace Orblit.Sprite.Tests;

public class TiledOversizedTests
{
    // A 16 pixel grid with a set of 16 pixel tiles, a set of 32 by 48 pixel
    // trees and a set of 8 pixel flowers.
    private static readonly TileMap World = Some(TileMap.Read(MapOf(
        [LayerOf("ground", 2, 1, 9, 17, 0)],
        [SetOf("grass", 1), SetOf("trees", 9, tileWidth: 32, tileHeight: 48), SetOf("flowers", 17, tileWidth: 8, tileHeight: 8)])));

    // a tile larger than the grid

    [Fact]
    public void IsCutFromItsSetAtItsOwnSize()
    {
        Assert.Equal((32L, 0L, 32L, 48L), Some(World.SetFor(9)).RectOf(10));
    }

    [Fact]
    public void IsDrawnFromTheBottomLeftOfItsCell()
    {
        // Cell (2, 3) spans 32 to 48 across and 48 to 64 down. The tree grows
        // up from 64 and out to the right.
        Assert.Equal((32L, 16L, 32L, 48L), World.PlaceOf(9, 2, 3));
    }

    [Fact]
    public void ATileTheSizeOfTheGridFillsItsCell()
    {
        Assert.Equal((32L, 48L, 16L, 16L), World.PlaceOf(1, 2, 3));
    }

    [Fact]
    public void ATileSmallerThanTheGridSitsInTheBottomLeftCorner()
    {
        Assert.Equal((32L, 56L, 8L, 8L), World.PlaceOf(17, 2, 3));
    }

    [Fact]
    public void IsDrawnAboveTheRowItOverlaps()
    {
        // The tree in the top row has its base on that row, so it starts
        // above the map.
        Assert.Equal((0L, -32L, 32L, 48L), World.PlaceOf(9, 0, 0));
    }

    [Fact]
    public void IsPlacedByCellsLeftOfAndAboveTheOrigin()
    {
        Assert.Equal((-16L, -48L, 32L, 48L), World.PlaceOf(9, -1, -1));
    }

    [Fact]
    public void HasNoPlaceWhenNoSetHoldsIt()
    {
        Assert.Null(World.PlaceOf(0, 0, 0));
        Assert.Null(World.PlaceOf(25, 0, 0));
        Assert.Null(World.PlaceOf(-1, 0, 0));
    }
}

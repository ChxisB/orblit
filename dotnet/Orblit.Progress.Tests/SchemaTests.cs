namespace Orblit.Progress.Tests;

public class SchemaTests
{
    private static ProgressSchema Build(ProgressName[] names, int version = 1) => new(version, names);

    [Fact]
    public void NumbersTheWholesInOrderAndPutsASliceInItsWholesSlot()
    {
        var schema = Build([new Whole("a"), new Whole("b"), new Slice("b.low", "b", 0, 4)]);

        Assert.Equal(2, schema.SlotCount);
        Assert.Equal(new Place(0, 0, 32), schema.PlaceOf("a"));
        Assert.Equal(new Place(1, 0, 32), schema.PlaceOf("b"));
        Assert.Equal(new Place(1, 0, 4), schema.PlaceOf("b.low"));
    }

    [Fact]
    public void ASliceMayComeBeforeTheWholeItSlices()
    {
        var schema = Build([new Slice("s", "w", 8, 8), new Whole("w")]);

        Assert.Equal(0, schema.PlaceOf("s").Slot);
    }

    [Fact]
    public void RefusesAnEmptyOrRepeatedName()
    {
        Assert.Throws<ArgumentException>(() => Build([new Whole("")]));
        Assert.Throws<ArgumentException>(() => Build([new Whole("a"), new Whole("a")]));
        Assert.Throws<ArgumentException>(() => Build([new Whole("a"), new Slice("a", "a", 0, 1)]));
    }

    [Fact]
    public void RefusesASliceOfNothingOfASliceOrPastBit32()
    {
        Assert.Throws<ArgumentException>(() => Build([new Slice("s", "w", 0, 1)]));
        Assert.Throws<ArgumentException>(() => Build(
            [new Whole("w"), new Slice("s", "w", 0, 4), new Slice("t", "s", 0, 1)]));
        foreach (var (low, bits) in new[] { (0, 0), (-1, 1), (31, 2), (0, 33) })
        {
            Assert.Throws<ArgumentException>(() => Build([new Whole("w"), new Slice("s", "w", low, bits)]));
        }
    }

    [Fact]
    public void RefusesTwoSlicesThatShareABitAndAllowsOnesThatTouch()
    {
        Assert.Throws<ArgumentException>(() => Build(
            [new Whole("w"), new Slice("a", "w", 0, 8), new Slice("b", "w", 7, 8)]));
        Assert.Equal(
            1,
            Build([new Whole("w"), new Slice("a", "w", 0, 8), new Slice("b", "w", 8, 8)]).SlotCount);
    }

    [Fact]
    public void ASliceOfTheWholeWordOverlapsEveryOtherSlice()
    {
        foreach (var order in new[] { new[] { 32, 1 }, new[] { 1, 32 } })
        {
            var names = new List<ProgressName> { new Whole("w") };
            names.AddRange(order.Select((bits, i) => new Slice($"s{i}", "w", 0, bits)));

            Assert.Throws<ArgumentException>(() => Build([.. names]));
        }
    }

    [Fact]
    public void RefusesMoreWholesThanATableCanNumber()
    {
        Assert.Throws<ArgumentException>(() => Build(Enumerable.Range(0, 0x10000).Select(i => (ProgressName)new Whole($"n{i}")).ToArray()));
        Assert.Equal(
            0xFFFF,
            Build(Enumerable.Range(0, 0xFFFF).Select(i => (ProgressName)new Whole($"n{i}")).ToArray()).SlotCount);
    }

    [Fact]
    public void TheSameBitsOfTwoWholesAreTwoDifferentPlaces()
    {
        var schema = Build(
            [new Whole("a"), new Whole("b"), new Slice("a.low", "a", 0, 8), new Slice("b.low", "b", 0, 8)]);

        Assert.Equal(0, schema.PlaceOf("a.low").Slot);
        Assert.Equal(1, schema.PlaceOf("b.low").Slot);
    }

    [Fact]
    public void RefusesAVersionOutside1To65535()
    {
        Assert.Throws<ArgumentException>(() => Build([], version: 0));
        Assert.Throws<ArgumentException>(() => Build([], version: 0x10000));
    }

    [Fact]
    public void SaysWhetherAValueFitsTheBitsOfAName()
    {
        var schema = Build([new Whole("a"), new Slice("s", "a", 4, 3), new Whole("b")]);

        Assert.True(schema.Fits("s", 7));
        Assert.False(schema.Fits("s", 8));
        Assert.False(schema.Fits("s", -1));
        Assert.True(schema.Fits("b", 0xFFFFFFFF));
        Assert.False(schema.Fits("b", 0x100000000));
        Assert.Throws<ArgumentException>(() => schema.Fits("x", 0));
    }

    [Fact]
    public void KnowsANameAndThrowsForOneItDoesNot()
    {
        var schema = Build([new Whole("a")]);

        Assert.True(schema.Has("a"));
        Assert.False(schema.Has("b"));
        Assert.Throws<ArgumentException>(() => schema.PlaceOf("b"));
    }
}

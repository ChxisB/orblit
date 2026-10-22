namespace Orblit.Progress.Tests;

public class TableTests
{
    private static readonly ProgressSchema Schema = new(
        1,
        [
            new Whole("flags"),
            new Whole("count"),
            new Slice("flag.bottom", "flags", 0, 1),
            new Slice("flag.middle", "flags", 4, 8),
            new Slice("flag.top", "flags", 31, 1),
        ]);

    private static ProgressSchema Named(int n, int version) =>
        new(version, Enumerable.Range(0, n).Select(i => (ProgressName)new Whole($"n{i}")).ToArray());

    // A value that does not fit is a range error, not an argument error.
    private static void ThrowsRange(Action action) => Assert.Throws<ArgumentOutOfRangeException>(action);

    // A table is only changed by a write, so a chain of writes reads better than nested calls.
    private static ProgressTable With(ProgressTable table, string name, long value) => table.Set(name, value).Table;

    [Fact]
    public void ASliceWritesItsBitsAndLeavesItsNeighbours()
    {
        var table = new ProgressTable(Schema);
        table = With(table, "flag.middle", 0xAB);
        table = With(table, "flag.bottom", 1);
        table = With(table, "flag.top", 1);

        Assert.Equal(0xAB, table.Get("flag.middle"));
        Assert.Equal(1, table.Get("flag.bottom"));
        Assert.Equal(1, table.Get("flag.top"));
        Assert.Equal(0x80000000L | (0xABL << 4) | 1, table.Get("flags"));

        table = With(table, "flag.middle", 0);
        Assert.Equal(1, table.Get("flag.bottom"));
        Assert.Equal(1, table.Get("flag.top"));
    }

    [Fact]
    public void ASliceReachesBit0AndTheTopBitAlone()
    {
        var bottom = With(new ProgressTable(Schema), "flag.bottom", 1);
        var top = With(new ProgressTable(Schema), "flag.top", 1);

        Assert.Equal(1, bottom.Get("flags"));
        Assert.Equal(0x80000000L, top.Get("flags"));
    }

    [Fact]
    public void ASliceRefusesAValueThatDoesNotFit()
    {
        var table = new ProgressTable(Schema);

        ThrowsRange(() => table.Set("flag.bottom", 2));
        ThrowsRange(() => table.Set("flag.middle", 256));
        ThrowsRange(() => table.Set("flag.middle", -1));
    }

    [Fact]
    public void AWholeHoldsTheFull32Bits()
    {
        var table = With(new ProgressTable(Schema), "count", 0xFFFFFFFF);

        Assert.Equal(0xFFFFFFFF, table.Get("count"));
        ThrowsRange(() => table.Set("count", 0x100000000));
        ThrowsRange(() => table.Set("count", -1));
    }

    [Fact]
    public void AWholeWritingItOverItsSlicesReplacesThem()
    {
        var table = With(With(new ProgressTable(Schema), "flag.middle", 0xFF), "flags", 0);

        Assert.Equal(0, table.Get("flag.middle"));
    }

    [Fact]
    public void SetReturnsANewTableAndLeavesTheOldOne()
    {
        var before = new ProgressTable(Schema);
        var (table, changes) = before.Set("count", 7);

        Assert.Equal(0, before.Get("count"));
        Assert.Equal(7, table.Get("count"));
        Assert.Equal([1], changes.Slots);
        Assert.Equal(7, changes.ValueOf(1));
    }

    [Fact]
    public void SetSaysNothingChangedForTheValueItAlreadyHad()
    {
        var table = With(new ProgressTable(Schema), "count", 7);

        Assert.True(table.Set("count", 7).Changes.IsEmpty);
    }

    [Fact]
    public void SetAWriteToASliceChangesItsWholesSlot()
    {
        var (table, changes) = new ProgressTable(Schema).Set("flag.top", 1);

        Assert.Equal([0], changes.Slots);
        Assert.Equal(table.Get("flags"), changes.ValueOf(0));
    }

    [Fact]
    public void ChangesAppliedToAnOlderCopyGiveTheNewerOne()
    {
        var older = With(new ProgressTable(Schema), "count", 3);
        var newer = With(With(older, "flag.middle", 9), "count", 4);

        var caught = older.Apply(newer.ChangesSince(older));

        Assert.Equal(newer.Encode(), caught.Encode());
    }

    [Fact]
    public void ChangesHoldOnlyTheSlotsThatDiffer()
    {
        var older = With(new ProgressTable(Schema), "count", 3);
        var newer = With(older, "flag.middle", 9);

        Assert.Equal([0], newer.ChangesSince(older).Slots);
        Assert.True(older.ChangesSince(older).IsEmpty);
    }

    [Fact]
    public void ChangesSurviveEncoding()
    {
        var older = new ProgressTable(Schema);
        var newer = With(With(older, "count", 5), "flag.top", 1);
        var changes = newer.ChangesSince(older);

        var read = Changes.TryDecode(Schema, changes.Encode());

        Assert.Equal(changes, read);
        Assert.Equal(changes.GetHashCode(), read!.GetHashCode());
    }

    [Fact]
    public void ChangesRefuseBytesThatAreNotChanges()
    {
        var good = new ProgressTable(Schema).Set("count", 5).Changes.Encode();
        var badSlot = (byte[])good.Clone();
        badSlot[2] = 9;
        var twice = new byte[2 + 12];
        twice[0] = 2;
        twice[2] = 1;
        twice[8] = 1;

        Assert.Null(Changes.TryDecode(Schema, []));
        Assert.Null(Changes.TryDecode(Schema, good.AsSpan(..^1)));
        Assert.Null(Changes.TryDecode(Schema, [.. good, 0]));
        Assert.Null(Changes.TryDecode(Schema, badSlot));
        Assert.Null(Changes.TryDecode(Schema, twice));
    }

    [Fact]
    public void ChangesDecodeNeverThrowsOnRandomBytes()
    {
        for (var seed = 0; seed < 500; seed++)
        {
            var bytes = new byte[seed % 40];
            for (var i = 0; i < bytes.Length; i++)
            {
                bytes[i] = (byte)(((seed * 31) + (i * 17)) & 0xFF);
            }

            _ = Changes.TryDecode(Schema, bytes);
        }
    }

    [Fact]
    public void SaveAndLoadGiveBackTheSameTable()
    {
        var table = With(With(new ProgressTable(Schema), "count", 12), "flag.middle", 200);

        var loaded = ProgressTable.Decode(Schema, table.Encode());

        Assert.Equal(12, loaded.Get("count"));
        Assert.Equal(200, loaded.Get("flag.middle"));
    }

    [Fact]
    public void ASaveOfTenNamesLoadsUnderASchemaOfEleven()
    {
        var old = new ProgressTable(Named(10, 1));
        for (var i = 0; i < 10; i++)
        {
            old = With(old, $"n{i}", i + 1);
        }

        var loaded = ProgressTable.Decode(Named(11, 2), old.Encode());

        for (var i = 0; i < 10; i++)
        {
            Assert.Equal(i + 1, loaded.Get($"n{i}"));
        }

        Assert.Equal(0, loaded.Get("n10"));
    }

    [Fact]
    public void ASaveFromALaterSchemaIsRefused()
    {
        // A later version with as many slots, and the same version with more.
        var newerVersion = new ProgressTable(Named(2, 2)).Encode();
        var moreSlots = new ProgressTable(Named(3, 1)).Encode();

        Assert.Throws<FormatException>(() => ProgressTable.Decode(Named(2, 1), newerVersion));
        Assert.Throws<FormatException>(() => ProgressTable.Decode(Named(2, 1), moreSlots));
    }

    [Fact]
    public void BytesThatAreNotATableAreRefused()
    {
        var good = new ProgressTable(Schema).Encode();
        var wrongFormat = (byte[])good.Clone();
        wrongFormat[0] = 2;

        Assert.Throws<FormatException>(() => ProgressTable.Decode(Schema, []));
        Assert.Throws<FormatException>(() => ProgressTable.Decode(Schema, [.. good, 0]));
        Assert.Throws<FormatException>(() => ProgressTable.Decode(Schema, wrongFormat));
        Assert.Throws<FormatException>(() => ProgressTable.Decode(Schema, good.AsSpan(..^1)));
    }
}

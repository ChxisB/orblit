namespace Orblit.Progress.Tests;

public class ConditionTests
{
    private static readonly ProgressSchema Schema = new(
        1,
        [new Whole("flags"), new Whole("gold"), new Slice("quest.cook.stage", "flags", 0, 4)]);

    private static ProgressTable TableWith(long stage, long gold) =>
        new ProgressTable(Schema).Set("quest.cook.stage", stage).Table.Set("gold", gold).Table;

    private static Dictionary<string, object?> Json(object? name, object? op, object? value) =>
        new Dictionary<string, object?> { ["name"] = name, ["op"] = op, ["value"] = value };

    [Fact]
    public void AComparisonHoldsForEachOperatorAtBelowAndAboveTheNumber()
    {
        var cases = new (Compare Compare, bool Below, bool At, bool Above)[]
        {
            (Compare.Equal, false, true, false),
            (Compare.NotEqual, true, false, true),
            (Compare.Less, true, false, false),
            (Compare.LessOrEqual, true, true, false),
            (Compare.Greater, false, false, true),
            (Compare.GreaterOrEqual, false, true, true),
        };
        foreach (var (compare, below, at, above) in cases)
        {
            Assert.True(compare.Holds(4, 5) == below, $"{compare.Symbol} below");
            Assert.True(compare.Holds(5, 5) == at, $"{compare.Symbol} at");
            Assert.True(compare.Holds(6, 5) == above, $"{compare.Symbol} above");
        }
    }

    [Fact]
    public void AComparisonIsFoundByItsSymbolAndByNoOtherText()
    {
        foreach (var compare in Compare.Values)
        {
            Assert.Same(compare, Compare.BySymbol(compare.Symbol));
        }

        Assert.Null(Compare.BySymbol("=>"));
        Assert.Null(Compare.BySymbol(""));
    }

    [Fact]
    public void AConditionReadsTheNamedValueFromTheTable()
    {
        var condition = new Condition("quest.cook.stage", Compare.GreaterOrEqual, 2);

        Assert.False(condition.Holds(TableWith(1, 0)));
        Assert.True(condition.Holds(TableWith(2, 0)));
        Assert.True(condition.Holds(TableWith(3, 0)));
    }

    [Fact]
    public void AConditionIsRefusedForAnEmptyNameOrAValueThatCannotBeStored()
    {
        Assert.Throws<ArgumentException>(() => new Condition("", Compare.Equal, 1));
        Assert.Throws<ArgumentException>(() => new Condition("gold", Compare.Equal, -1));
        Assert.Throws<ArgumentException>(() => new Condition("gold", Compare.Equal, 0x100000000));
        Assert.Equal(0xFFFFFFFF, new Condition("gold", Compare.Equal, 0xFFFFFFFF).Value);
    }

    [Fact]
    public void AConditionThrowsForANameTheSchemaDoesNotHave()
    {
        var condition = new Condition("nope", Compare.Equal, 0);

        Assert.Throws<ArgumentException>(() => condition.Holds(TableWith(0, 0)));
    }

    [Fact]
    public void AConditionRoundTripsThroughJson()
    {
        var condition = new Condition("gold", Compare.NotEqual, 7);

        var again = Condition.FromJson(condition.ToJson());

        Assert.Equal("gold", again.Name);
        Assert.Same(Compare.NotEqual, again.Compare);
        Assert.Equal(7, again.Value);
    }

    [Fact]
    public void AConditionIsRefusedFromJsonThatIsNotOne()
    {
        var bad = new object?[]
        {
            null,
            "gold",
            new Dictionary<string, object?>(),
            Json("gold", "==", "1"),
            Json("gold", "=>", 1),
            Json("", "==", 1),
            Json("gold", "==", -1),
            Json("gold", "==", 0x100000000),
            Json("gold", "==", 1.5),
            Json(1, "==", 1),
        };
        foreach (var json in bad)
        {
            Assert.True(
                Throws<FormatException>(() => Condition.FromJson(json)),
                $"{json}");
        }
    }

    [Fact]
    public void AListHoldsWhenEveryConditionDoesAndWhenItIsEmpty()
    {
        var both = new[]
        {
            new Condition("quest.cook.stage", Compare.GreaterOrEqual, 1),
            new Condition("gold", Compare.Greater, 10),
        };

        Assert.True(Condition.AllHold(both, TableWith(1, 11)));
        Assert.False(Condition.AllHold(both, TableWith(0, 11)));
        Assert.False(Condition.AllHold(both, TableWith(1, 10)));
        Assert.True(Condition.AllHold([], TableWith(0, 0)));
    }

    [Fact]
    public void AListIsReadFromJsonAndNullIsEmpty()
    {
        var list = Condition.ListFromJson(new object?[] { Json("gold", ">", 1) });

        Assert.Single(list);
        Assert.Empty(Condition.ListFromJson(null));
        Assert.Throws<FormatException>(() => Condition.ListFromJson(new Dictionary<string, object?> { ["a"] = 1 }));
        Assert.Throws<FormatException>(() => Condition.ListFromJson(new object?[] { 1 }));
    }

    private static bool Throws<T>(Action action)
        where T : Exception
    {
        try
        {
            action();
            return false;
        }
        catch (T)
        {
            return true;
        }
    }
}

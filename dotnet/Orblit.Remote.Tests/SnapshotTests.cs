using Orblit.Json;

namespace Orblit.Remote.Tests;

public class SnapshotTests
{
    private static object? Doc(string json) => JsonText.Decode(json);

    // A difference as plain text, because a record struct compares its maps by reference.
    private static List<string> Shown(IReadOnlyList<Difference> found) =>
        [.. found.Select(d => $"{d.Path}: {JsonText.Encode(d.From)} -> {JsonText.Encode(d.To)}")];

    private static IReadOnlyDictionary<string, object?> Map(string json) =>
        (IReadOnlyDictionary<string, object?>)Doc(json)!;

    [Fact]
    public void DiffFindsNothingBetweenEqualDocuments()
    {
        var doc = Doc("""{"a":[1,2,3],"b":{"c":"x"}}""");

        Assert.Empty(JsonDiff.Find(doc, doc));
        Assert.Empty(JsonDiff.Find(Doc("""{"n":1}"""), Doc("""{"n":1.0}""")));
    }

    [Fact]
    public void DiffNamesThePathOfANumberThatChanged()
    {
        var one = Doc("""{"entities":{"3":{"Position":{"x":1,"y":2}}}}""");
        var two = Doc("""{"entities":{"3":{"Position":{"x":1,"y":5}}}}""");

        Assert.Equal(["entities/3/Position/y: 2 -> 5"], Shown(JsonDiff.Find(one, two)));
    }

    [Fact]
    public void DiffNamesAnEntityThatWasAddedAndOneThatWasRemoved()
    {
        var one = Doc("""{"entities":{"1":{"Tag":1},"2":{"Tag":2}}}""");
        var two = Doc("""{"entities":{"2":{"Tag":2},"3":{"Tag":3}}}""");

        Assert.Equal(
            ["""entities/1: {"Tag":1} -> null""", """entities/3: null -> {"Tag":3}"""],
            Shown(JsonDiff.Find(one, two)));
    }

    [Fact]
    public void DiffComparesListsElementByElementAndByLength()
    {
        Assert.Equal(
            ["a/1: 2 -> 9", "a/2: 3 -> null"],
            Shown(JsonDiff.Find(Doc("""{"a":[1,2,3]}"""), Doc("""{"a":[1,9]}"""))));
        Assert.Equal(
            ["a/1: null -> 2"],
            Shown(JsonDiff.Find(Doc("""{"a":[1]}"""), Doc("""{"a":[1,2]}"""))));
    }

    [Fact]
    public void DiffReportsAChangeOfKindOnceAtThePlaceItHappened()
    {
        Assert.Equal(
            ["""a: {"b":1} -> 5"""],
            Shown(JsonDiff.Find(Doc("""{"a":{"b":1}}"""), Doc("""{"a":5}"""))));
    }

    [Fact]
    public void AStoreFindsASnapshotByTheIdItWasGiven()
    {
        var store = new SnapshotStore();
        store.Add(Map("""{"n":1}"""));
        var first = store.LastId;
        store.Add(Map("""{"n":2}"""));

        Assert.NotEqual(first, store.LastId);
        Assert.Equal(1L, store.Find(first)!["n"]);
        Assert.Equal(2L, store.Find(store.LastId)!["n"]);
    }

    [Fact]
    public void AStoreDropsTheOldestWhenItIsFull()
    {
        var store = new SnapshotStore(capacity: 2);
        store.Add(Map("""{"n":1}"""));
        var first = store.LastId;
        store.Add(Map("""{"n":2}"""));
        store.Add(Map("""{"n":3}"""));

        Assert.Null(store.Find(first));
        Assert.Equal(3L, store.Find(store.LastId)!["n"]);
    }

    [Fact]
    public void AStoreAnswersNullForAnIdItNeverGave() => Assert.Null(new SnapshotStore().Find("s99"));

    [Fact]
    public void ALabelIsTheIdOfItsSnapshot()
    {
        var store = new SnapshotStore();
        store.Add(Map("""{"n":1}"""), "before the jump");

        Assert.Equal("before the jump", store.LastId);
        Assert.Equal(1L, store.Find("before the jump")!["n"]);
    }

    [Fact]
    public void ALabelledSnapshotDoesNotMoveTheAutomaticCount()
    {
        var store = new SnapshotStore();
        store.Add(Map("""{"n":1}"""));
        store.Add(Map("""{"n":2}"""), "mark");
        store.Add(Map("""{"n":3}"""));

        Assert.Equal(3L, store.Find("s2")!["n"]);
        Assert.Equal(2L, store.Find("mark")!["n"]);
    }

    [Fact]
    public void ALabelTakenAgainReplacesTheOlderSnapshotAndCountsAsTheNewest()
    {
        var store = new SnapshotStore(capacity: 2);
        store.Add(Map("""{"n":1}"""), "mark");
        store.Add(Map("""{"n":2}"""));
        store.Add(Map("""{"n":3}"""), "mark");

        Assert.Equal(3L, store.Find("mark")!["n"]);
        Assert.Equal("mark", store.LastId);

        // The older automatic one goes first, so the label was not left in the oldest place.
        store.Add(Map("""{"n":4}"""));
        Assert.Null(store.Find("s1"));
        Assert.Equal(3L, store.Find("mark")!["n"]);
    }

    [Fact]
    public void ALabelledSnapshotSharesTheCapWithTheAutomaticOnes()
    {
        var store = new SnapshotStore(capacity: 2);
        store.Add(Map("""{"n":1}"""), "first");
        store.Add(Map("""{"n":2}"""));
        store.Add(Map("""{"n":3}"""));

        Assert.Null(store.Find("first"));
        Assert.Equal(2L, store.Find("s1")!["n"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("s1")]
    [InlineData("s0")]
    [InlineData("s9876")]
    [InlineData("a very long label that goes on and on and on")]
    public void ALabelThatIsBlankLongOrLooksAutomaticIsRefusedAndNothingIsKept(string label)
    {
        var store = new SnapshotStore();

        Assert.Throws<ArgumentException>(() => store.Add(Map("""{"n":1}"""), label));

        Assert.Throws<InvalidOperationException>(() => store.LastId);
        store.Add(Map("""{"n":2}"""));
        Assert.Equal("s1", store.LastId);
    }

    [Theory]
    [InlineData("s")]
    [InlineData("s1a")]
    [InlineData("S1")]
    [InlineData("start")]
    public void AWordThatOnlyStartsLikeAnAutomaticIdIsAllowed(string label)
    {
        var store = new SnapshotStore();

        store.Add(Map("""{"n":1}"""), label);

        Assert.Equal(label, store.LastId);
    }

    [Fact]
    public void AStoreHasNoLastIdBeforeTheFirstSnapshot()
    {
        Assert.Throws<InvalidOperationException>(() => new SnapshotStore().LastId);
        Assert.ThrowsAny<ArgumentException>(() => new SnapshotStore(capacity: 0));
    }
}

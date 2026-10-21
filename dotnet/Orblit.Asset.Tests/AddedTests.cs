using System.Collections;
using Orblit.Json;

namespace Orblit.Asset.Tests;

// What C# does that Dart cannot be asked: an index entry with the wrong kind of value is a miss where Dart throws a
// TypeError, a size below nothing is refused by the constructor where Dart only asserts, and a recipe is written
// for every kind of dictionary the caller can hold.
public sealed class AddedTests : IDisposable
{
    private readonly DirectoryInfo _temp = Directory.CreateTempSubdirectory("orblit_asset_added_");
    private readonly string _root;

    public AddedTests() => _root = Path.Combine(_temp.FullName, "cache");

    public void Dispose() => _temp.Delete(recursive: true);

    private string IndexPath => Path.Combine(_root, "index.json");

    private async Task<DirectoryCookCache> Stored(params string[] names)
    {
        var cache = new DirectoryCookCache(_root);
        foreach (var name in names)
        {
            await cache.StoreAsync(CookCases.Key(source: name), CookCases.Outputs("out", name));
        }

        return cache;
    }

    private async Task Rewrite(Action<OrderedDictionary<string, object?>> edit)
    {
        var index = (OrderedDictionary<string, object?>)DartJson.Decode(
            await File.ReadAllTextAsync(IndexPath, TestContext.Current.CancellationToken))!;
        edit(index);
        await File.WriteAllTextAsync(IndexPath, DartJson.Encode(index), TestContext.Current.CancellationToken);
    }

    private static OrderedDictionary<string, object?> EntryOf(OrderedDictionary<string, object?> index, string name) =>
        (OrderedDictionary<string, object?>)((OrderedDictionary<string, object?>)index["entries"]!)[
            CookCases.Key(source: name).Hash.Hex]!;

    private static OrderedDictionary<string, object?> Failures(int count)
    {
        var failures = new OrderedDictionary<string, object?>();
        var first = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        for (var i = 0; i < count; i++)
        {
            failures[FailureHex(i)] = new OrderedDictionary<string, object?>
            {
                ["reason"] = "old",
                ["at"] = DartText.Iso(first.AddSeconds(i)),
            };
        }

        return failures;
    }

    private static string FailureHex(int i) => ContentHash.Of(System.Text.Encoding.UTF8.GetBytes($"failure {i}")).Hex;

    // Dart keeps 4096 failures and drops the oldest, so a cache that kept more would grow with every broken file.
    [Fact]
    public async Task TheOldestFailureIsDroppedOnceThereAreMoreThanTheCacheKeeps()
    {
        await Stored("a");
        await Rewrite(index => index["failures"] = Failures(4096));

        await new DirectoryCookCache(_root).RecordFailureAsync(CookCases.Key(source: "bad"), "new");

        var index = (OrderedDictionary<string, object?>)DartJson.Decode(
            await File.ReadAllTextAsync(IndexPath, TestContext.Current.CancellationToken))!;
        var failures = (OrderedDictionary<string, object?>)index["failures"]!;
        Assert.Equal(4096, failures.Count);
        Assert.False(failures.ContainsKey(FailureHex(0)));
        Assert.True(failures.ContainsKey(FailureHex(1)));
        Assert.True(failures.ContainsKey(CookCases.Key(source: "bad").Hash.Hex));
    }

    // A message names the value as Dart prints it, so a whole double keeps its ".0".
    [Theory]
    [InlineData("""{"formatVersion":1.5}""", "formatVersion is 1.5,")]
    [InlineData("""{"formatVersion":2.0}""", "formatVersion is 2.0,")]
    [InlineData("""{"importer":1.5}""", "importer is 1.5,")]
    public void ADoubleInAMessageIsPrintedTheDartWay(string text, string said)
    {
        var error = Assert.Throws<AssetFormatException>(
            () => ImportSettings.Parse(text, AssetId.Parse("textures/wall.png.import.json")));
        Assert.Contains(said, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task APutThatCannotBeWrittenThrows()
    {
        byte[] bytes = [1, 2, 3];
        var store = new DirectoryContentStore(_root);
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(
            Path.Combine(_root, ContentHash.Of(bytes).Shard), "in the way", TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<IOException>(() => store.PutAsync(bytes));
    }

    [Fact]
    public void IdsAndHashesCompareWithNotEquals()
    {
        Assert.True(AssetId.Parse("a") != AssetId.Parse("b"));
        Assert.False(AssetId.Parse("a") != AssetId.Parse("a"));
        Assert.True(ContentHash.Of([1]) != ContentHash.Of([2]));
        Assert.False(ContentHash.Of([1]) != ContentHash.Of([1]));
    }

    [Fact]
    public async Task ALayeredSourceSaysWhyItFoundNothing()
    {
        var none = await Assert.ThrowsAsync<AssetNotFoundException>(
            () => new LayeredAssetSource([]).ReadAsync(AssetId.Parse("a.png")));
        Assert.Equal("there are no sources to look in", none.Reason);

        var two = await Assert.ThrowsAsync<AssetNotFoundException>(
            () => new LayeredAssetSource([new MemoryAssetSource(), new MemoryAssetSource()]).ReadAsync(AssetId.Parse("a.png")));
        Assert.Equal("none of the 2 sources has it", two.Reason);
    }

    [Fact]
    public async Task AnEntryTimedWithANumberIsAMissAndTheOthersAreKept()
    {
        await Stored("a", "b");
        await Rewrite(index => EntryOf(index, "a")["usedAt"] = 5L);

        var reopened = new DirectoryCookCache(_root);
        Assert.Null(await reopened.LookUpAsync(CookCases.Key(source: "a")));
        Assert.NotNull(await reopened.LookUpAsync(CookCases.Key(source: "b")));
    }

    [Fact]
    public async Task AnIndexThatIsNotTextIsAnEmptyCacheThatRecovers()
    {
        await Stored("a");
        await File.WriteAllBytesAsync(IndexPath, [0xFF, 0xFE, 0x00, 0xC3, 0x28], TestContext.Current.CancellationToken);

        Assert.Null(await new DirectoryCookCache(_root).LookUpAsync(CookCases.Key(source: "a")));
        await Stored("a");
        Assert.NotNull(await new DirectoryCookCache(_root).LookUpAsync(CookCases.Key(source: "a")));
    }

    // Dart's 1.0 == 1 is true, so an index a tool wrote with a point is still the same format.
    [Fact]
    public async Task AFormatVersionWrittenWithAPointIsRead()
    {
        await Stored("a");
        await Rewrite(index => index["formatVersion"] = 1.0);
        Assert.NotNull(await new DirectoryCookCache(_root).LookUpAsync(CookCases.Key(source: "a")));
    }

    [Theory]
    [InlineData("2026-10-21T10:00:00.123Z", true)]
    [InlineData("2026-10-21T10:00:00.123456Z", true)]
    [InlineData("2026-10-21T10:00:00Z", true)]
    [InlineData("2026-10-21T10:00Z", true)]
    [InlineData("2026-10-21", true)]
    [InlineData("21 Oct 2026", false)]
    [InlineData("", false)]
    public async Task ATimeIsReadInTheFormsDartWritesAndNoOthers(string text, bool hit)
    {
        await Stored("a");
        await Rewrite(index => EntryOf(index, "a")["usedAt"] = text);

        var found = await new DirectoryCookCache(_root).LookUpAsync(CookCases.Key(source: "a"));
        Assert.Equal(hit, found is not null);
    }

    [Fact]
    public async Task ATimeWithMicrosecondsIsWrittenWithSixDigits()
    {
        var cache = new DirectoryCookCache(_root, clock: () => new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero).AddTicks(1234560));
        await cache.StoreAsync(CookCases.Key(), CookCases.Outputs("out", "x"));
        Assert.Contains("2026-10-21T10:00:00.123456Z", await File.ReadAllTextAsync(IndexPath, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void AnEntryWithANegativeSizeIsRefused() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new AssetEntry(CookCases.Hash("a"), -1));

    [Fact]
    public void ARecipeIsWrittenInKeyOrderForAnyKindOfDictionary()
    {
        var plain = new Dictionary<string, object?>
        {
            ["b"] = 1L,
            ["a"] = new Dictionary<string, object?> { ["y"] = true, ["x"] = new List<object?> { 2L, null } },
        };
        var old = new Dictionary<string, object?>
        {
            ["b"] = 1L,
            ["a"] = new Hashtable { ["y"] = true, ["x"] = new ArrayList { 2L, null } },
        };

        var key = new CookKey("texture", 1, CookCases.Hash("a"), settings: plain);
        Assert.Contains("\"settings\":{\"a\":{\"x\":[2,null],\"y\":true},\"b\":1}", key.Recipe);
        Assert.Equal(key.Recipe, new CookKey("texture", 1, CookCases.Hash("a"), settings: old).Recipe);
    }
}

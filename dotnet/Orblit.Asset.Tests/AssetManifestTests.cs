using System.Text;
using Orblit.Json;

namespace Orblit.Asset.Tests;

public class AssetManifestTests
{
    private const string EmptyHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
    private const string AbcHash = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";

    private static readonly AssetId Robot = AssetId.Parse("models/robot.glb");
    private static readonly AssetId Metal = AssetId.Parse("textures/metal.png");

    // Deliberately not in id order, so the sort is what is tested.
    private static AssetManifest Sample() => new(new Dictionary<AssetId, AssetEntry>
    {
        [Metal] = new(ContentHash.Parse(EmptyHash), 0),
        [Robot] = new(ContentHash.Of(Encoding.UTF8.GetBytes("abc")), 3),
    });

    // a manifest survives a trip to text and back

    [Fact]
    public void EveryEntryComesBackTheSame()
    {
        var before = Sample();
        var load = AssetManifest.Read(before.Encode());
        Assert.False(load.HasProblems);
        Assert.Equal(before.Entries.OrderBy(p => p.Key.ToString(), StringComparer.Ordinal), load.Manifest.Entries.OrderBy(p => p.Key.ToString(), StringComparer.Ordinal));
    }

    [Fact]
    public void AnEmptyManifestIsAValidFileNotAnError()
    {
        var load = AssetManifest.Read(new AssetManifest().Encode());
        Assert.Empty(load.Manifest.Entries);
        Assert.Empty(load.Problems);
    }

    // A file that reorders itself between writes turns every commit into a diff nobody can review.
    [Fact]
    public void WritingTheSameManifestTwiceGivesTheSameBytes() => Assert.Equal(Sample().Encode(), Sample().Encode());

    [Fact]
    public void TheOrderEntriesWereAddedInMakesNoDifference()
    {
        var reversed = new AssetManifest(new Dictionary<AssetId, AssetEntry>
        {
            [Robot] = Sample().Entries[Robot],
            [Metal] = Sample().Entries[Metal],
        });
        Assert.Equal(Sample().Encode(), reversed.Encode());
    }

    [Fact]
    public void ItIsWrittenSortedIndentedByTwoSpacesAndEndingInANewline() =>
        Assert.Equal(
            $$"""
            {
              "formatVersion": 1,
              "assets": {
                "models/robot.glb": {
                  "bytes": 3,
                  "hash": "{{AbcHash}}"
                },
                "textures/metal.png": {
                  "bytes": 0,
                  "hash": "{{EmptyHash}}"
                }
              }
            }

            """.ReplaceLineEndings("\n"),
            Sample().Encode());

    [Fact]
    public void TheEntriesCannotBeChangedBehindItsBack()
    {
        var entries = (IDictionary<AssetId, AssetEntry>)Sample().Entries;
        Assert.Throws<NotSupportedException>(() => entries[AssetId.Parse("extra.bin")] = new AssetEntry(ContentHash.Parse(EmptyHash), 0));
    }

    // a manifest that cannot be read at all is refused

    private static void RefusedSaying(string text, string words)
    {
        var error = Assert.Throws<AssetFormatException>(() => AssetManifest.Read(text));
        Assert.Contains(words, error.Message);
    }

    [Fact]
    public void WhenItIsNotJson() => RefusedSaying("""{"formatVersion": 1,""", "not JSON");

    [Fact]
    public void WhenItIsNotAnObject() => RefusedSaying("[]", "JSON object");

    [Fact]
    public void WhenItDoesNotSayWhatVersionItIs()
    {
        RefusedSaying("""{"assets": {}}""", "format version");
        RefusedSaying("""{"formatVersion": "1", "assets": {}}""", "format version");
    }

    // Refused rather than half-read: a newer file may mean something different by the same keys.
    [Fact]
    public void WhenANewerOrblitWroteIt() => RefusedSaying("""{"formatVersion": 2, "assets": {}}""", "newer");

    [Fact]
    public void WhenItsVersionIsOneNothingHasWritten() =>
        Assert.Throws<AssetFormatException>(() => AssetManifest.Read("""{"formatVersion": 0, "assets": {}}"""));

    // Another Orblit file opened by mistake should say so, not look like a project with nothing in it.
    [Fact]
    public void WhenItHasNoAssetsInItRatherThanReadingAsEmpty() =>
        RefusedSaying("""{"formatVersion": 1, "objects": []}""", "\"assets\"");

    // a bad entry is dropped and named, and the rest still read

    private static readonly Dictionary<string, object?> Good = new() { ["bytes"] = 3L, ["hash"] = AbcHash };

    private static string WithEntries(Dictionary<string, object?> assets) =>
        DartJson.Encode(new Dictionary<string, object?> { ["formatVersion"] = 1L, ["assets"] = assets });

    private static AssetManifestLoad ReadWith(string key, object? entry) =>
        AssetManifest.Read(WithEntries(new() { ["models/robot.glb"] = Good, [key] = entry }));

    private static void KeepsTheGoodOne(AssetManifestLoad load)
    {
        Assert.Equal([Robot], load.Manifest.Entries.Keys);
        Assert.Equal(new AssetEntry(ContentHash.Parse(AbcHash), 3), load.Manifest.Entries[Robot]);
    }

    [Fact]
    public void AnIdThatIsNotOne()
    {
        var load = ReadWith(@"models\crate.glb", Good);
        KeepsTheGoodOne(load);
        var problem = Assert.Single(load.Problems);
        Assert.Contains(@"models\crate.glb", problem);
        Assert.Contains("left out", problem);
    }

    [Fact]
    public void AHashThatIsNotOne()
    {
        var load = ReadWith("models/crate.glb", new Dictionary<string, object?> { ["bytes"] = 3L, ["hash"] = "abc" });
        KeepsTheGoodOne(load);
        var problem = Assert.Single(load.Problems);
        Assert.Contains("\"models/crate.glb\"", problem);
        Assert.Contains("not a content hash", problem);
    }

    [Fact]
    public void NoHashAtAll()
    {
        var load = ReadWith("models/crate.glb", new Dictionary<string, object?> { ["bytes"] = 3L });
        KeepsTheGoodOne(load);
        var problem = Assert.Single(load.Problems);
        Assert.Contains("\"models/crate.glb\"", problem);
        Assert.Contains("no hash", problem);
    }

    [Fact]
    public void ANegativeSize()
    {
        var load = ReadWith("models/crate.glb", new Dictionary<string, object?> { ["bytes"] = -3L, ["hash"] = AbcHash });
        KeepsTheGoodOne(load);
        var problem = Assert.Single(load.Problems);
        Assert.Contains("\"models/crate.glb\"", problem);
        Assert.Contains("negative", problem);
    }

    [Fact]
    public void ASizeThatIsNotAWholeNumber()
    {
        var load = ReadWith("models/crate.glb", new Dictionary<string, object?> { ["bytes"] = 3.5, ["hash"] = AbcHash });
        KeepsTheGoodOne(load);
        Assert.Contains("\"models/crate.glb\"", Assert.Single(load.Problems));
    }

    [Fact]
    public void AnEntryThatIsNotAnObject()
    {
        var load = ReadWith("models/crate.glb", "crate");
        KeepsTheGoodOne(load);
        var problem = Assert.Single(load.Problems);
        Assert.Contains("\"models/crate.glb\"", problem);
        Assert.Contains("not an object", problem);
    }

    [Fact]
    public void EveryBadEntryGetsItsOwnSentence()
    {
        var load = AssetManifest.Read(WithEntries(new()
        {
            ["models/robot.glb"] = Good,
            ["/absolute.glb"] = Good,
            ["models/crate.glb"] = new Dictionary<string, object?> { ["bytes"] = -1L, ["hash"] = AbcHash },
        }));
        KeepsTheGoodOne(load);
        Assert.Equal(2, load.Problems.Count);
    }
}

using System.Text;
using System.Text.Json.Nodes;

namespace Orblit.Asset.Tests;

public class KitIndexTests
{
    private static KitManifest Manifest(string slug, string publisher = "brass-lamp") =>
        new(
            slug,
            publisher,
            $"Kit {slug}",
            "Some crates.",
            "1.0.0",
            new KitLicence("MIT", "https://example.com/l"),
            new KitEngine("0.30.0", "0.40.0"),
            false,
            ["props"]);

    private static KitRow Row(string slug, string publisher = "brass-lamp", string? thumbnail = null) =>
        new(Manifest(slug, publisher), "2026-10-19", thumbnail);

    private static PublishedRelease Published(string version) =>
        new(
            new KitRelease(
                version,
                $"https://example.com/{version}.opack",
                ContentHash.Of(Encoding.UTF8.GetBytes(version)),
                99,
                "2026-10-19",
                "Changes."),
            new KitEngine("0.30.0", "0.40.0"),
            true);

    private static byte[] Bytes(JsonNode json) => Encoding.UTF8.GetBytes(json.ToJsonString());

    private static JsonObject Json(byte[] encoded) => (JsonObject)JsonNode.Parse(Encoding.UTF8.GetString(encoded))!;

    // A row as wide as a row can be.
    private static KitRow Wide(int n) =>
        new(
            new KitManifest(
                $"k{n}",
                "brass-lamp",
                $"Kit {n}",
                new string('x', 200),
                "1.0.0",
                new KitLicence("MIT", "https://example.com/" + new string('l', 150)),
                new KitEngine("0.30.0", "0.40.0"),
                false,
                [.. Enumerable.Range(0, 8).Select(i => new string('t', 20) + i)]),
            "2026-10-19");

    [Fact]
    public void RefusesToWriteAnIndexItsReaderWouldRefuse()
    {
        var index = new KitIndex([.. Enumerable.Range(0, 30000).Select(Wide)]);

        var error = Assert.ThrowsAny<FormatException>(index.Encode);

        Assert.Contains("index.json would be", error.Message);
    }

    [Fact]
    public void SortsByPublisherAndSlugAndReadsBackTheSameBytes()
    {
        var index = new KitIndex(
        [
            Row("zebra"),
            Row("crates", thumbnail: "kits/brass-lamp/crates/preview-0.png"),
            Row("apple", "amber"),
        ]);

        Assert.Equal(
            ["amber/apple", "brass-lamp/crates", "brass-lamp/zebra"],
            index.Rows.Select(row => $"{row.Manifest.Publisher}/{row.Manifest.Slug}"));
        var read = KitIndex.Parse(index.Encode());
        Assert.Equal(index.Encode(), read.Encode());
        var crates = read.Rows[1];
        Assert.Equal("kits/brass-lamp/crates/preview-0.png", crates.Thumbnail);
        Assert.Equal(["props"], crates.Manifest.Tags);
        Assert.Equal("0.40.0", crates.Manifest.Engine.To);
        Assert.Equal("2026-10-19", crates.Updated);
        Assert.Null(read.Rows[0].Thumbnail);
    }

    [Fact]
    public void AnEmptyIndexReadsBack() =>
        Assert.Empty(KitIndex.Parse(new KitIndex([]).Encode()).Rows);

    [Fact]
    public void IgnoresAKeyItDoesNotKnowAndRefusesANewerFormat()
    {
        var file = Json(new KitIndex([Row("crates")]).Encode());
        file["kits"]![0]!["stars"] = 5;
        file["extra"] = 1;

        Assert.Single(KitIndex.Parse(Bytes(file)).Rows);
        file["formatVersion"] = 2;
        var error = Assert.ThrowsAny<FormatException>(() => KitIndex.Parse(Bytes(file)));
        Assert.Contains("newer Orblit", error.Message);
        file["formatVersion"] = 0;
        Assert.ThrowsAny<FormatException>(() => KitIndex.Parse(Bytes(file)));
    }

    [Fact]
    public void RefusesAKitTwiceAndAThumbnailOfAnotherKit()
    {
        Assert.ThrowsAny<FormatException>(() => new KitIndex([Row("crates"), Row("crates")]));
        foreach (var thumbnail in new[]
        {
            "kits/brass-lamp/other/preview-0.png",
            "kits/brass-lamp/crates/preview-6.png",
            "kits/brass-lamp/crates/preview-0.gif",
            "../x/preview-0.png",
        })
        {
            Assert.ThrowsAny<FormatException>(() => Row("crates", thumbnail: thumbnail));
        }
    }

    [Fact]
    public void RefusesARowThatAManifestWouldRefuseAndSaysWhich()
    {
        var file = Json(new KitIndex([Row("crates")]).Encode());
        var first = file["kits"]![0]!.AsObject();
        first["name"] = "Crates\u202e";

        var error = Assert.ThrowsAny<FormatException>(() => KitIndex.Parse(Bytes(file)));

        Assert.Contains("index.json, kits 1", error.Message);
        Assert.Contains("\"name\"", error.Message);
        first["name"] = "Crates";
        first.Remove("licence");
        Assert.ThrowsAny<FormatException>(() => KitIndex.Parse(Bytes(file)));
    }

    [Fact]
    public void AnIndexHoldsUpTo50000Rows()
    {
        var rows = Enumerable.Range(0, 50001).Select(i => Row($"k{i}")).ToList();

        Assert.Equal(50000, new KitIndex(rows[..50000]).Rows.Count);
        Assert.ThrowsAny<FormatException>(() => new KitIndex(rows));
    }

    [Fact]
    public void RefusesAnIndexOverTheSizeLimit()
    {
        var error = Assert.ThrowsAny<FormatException>(() => KitIndex.Parse(new byte[(16 * 1024 * 1024) + 1]));

        Assert.Contains("index.json is over", error.Message);
    }

    [Fact]
    public void ListsReleasesNewestFirstAndReadsBackTheSameBytes()
    {
        var list = new KitReleases(["preview-0.png", "preview-1.webp"], [Published("1.2.0"), Published("1.10.0")]);

        Assert.Equal(["1.10.0", "1.2.0"], list.Releases.Select(item => item.Release.Version));
        var read = KitReleases.Parse(list.Encode());
        Assert.Equal(list.Encode(), read.Encode());
        Assert.Equal(["preview-0.png", "preview-1.webp"], read.Previews);
        Assert.True(read.Releases[0].ContainsScripts);
        Assert.Equal("0.30.0", read.Releases[0].Engine.From);
        Assert.Equal(ContentHash.Of(Encoding.UTF8.GetBytes("1.10.0")), read.Releases[0].Release.Offer.Hash);
    }

    [Fact]
    public void RefusesAVersionTwiceABadPreviewNameAndABackwardRange()
    {
        Assert.ThrowsAny<FormatException>(() => new KitReleases([], []));
        Assert.ThrowsAny<FormatException>(() => new KitReleases([], [Published("1.0.0"), Published("1.0.0")]));
        foreach (var name in new[] { "../a.png", "preview-0.gif", "preview-9.png", "a/preview-0.png" })
        {
            Assert.ThrowsAny<FormatException>(() => new KitReleases([name], [Published("1.0.0")]));
        }

        Assert.ThrowsAny<FormatException>(() =>
            new PublishedRelease(Published("1.0.0").Release, new KitEngine("0.40.0", "0.30.0"), false));
    }

    [Fact]
    public void ReleasesIgnoreAKeyTheyDoNotKnowAndRefuseANewerFormat()
    {
        var file = Json(new KitReleases([], [Published("1.0.0")]).Encode());
        file["extra"] = 1;

        Assert.Single(KitReleases.Parse(Bytes(file)).Releases);
        file["formatVersion"] = 2;
        Assert.ThrowsAny<FormatException>(() => KitReleases.Parse(Bytes(file)));
    }

    [Fact]
    public void NamesTheReleaseThatIsWrong()
    {
        var file = Json(new KitReleases([], [Published("1.0.0")]).Encode());
        file["releases"]![0]!["url"] = "http://example.com/a";

        var error = Assert.ThrowsAny<FormatException>(() => KitReleases.Parse(Bytes(file)));

        Assert.Contains("releases.json, releases 1", error.Message);
    }

    [Fact]
    public void ARowRefusesALicenceAddressWithAPortPast65535()
    {
        var file = Json(new KitIndex([Row("crates")]).Encode());
        file["kits"]![0]!["licence"]!["url"] = "https://example.com:99999/l";

        Assert.ThrowsAny<FormatException>(() => KitIndex.Parse(Bytes(file)));
    }
}

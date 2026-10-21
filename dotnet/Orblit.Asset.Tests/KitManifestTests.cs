using System.Text;
using System.Text.Json.Nodes;

namespace Orblit.Asset.Tests;

public class KitManifestTests
{
    private static KitManifest Manifest(
        string slug = "crate-pack",
        string publisher = "brass-lamp",
        string name = "Crate pack",
        string summary = "Twelve crates.",
        string version = "1.2.0",
        KitLicence? licence = null,
        KitEngine? engine = null,
        bool containsScripts = false,
        IReadOnlyList<string>? tags = null,
        IReadOnlyList<AssetId>? previews = null) =>
        new(
            slug,
            publisher,
            name,
            summary,
            version,
            licence ?? new KitLicence("CC-BY-4.0", "https://example.com/licence"),
            engine ?? new KitEngine("0.30.0", "0.40.0"),
            containsScripts,
            tags ?? ["props"],
            previews);

    private static byte[] Text(JsonNode json) => Encoding.UTF8.GetBytes(json.ToJsonString());

    private static JsonObject Good() => (JsonObject)JsonNode.Parse(Encoding.UTF8.GetString(Manifest().Encode()))!;

    private static void Refuses(Action make, string? reason = null) =>
        Assert.True(Throws(make), reason);

    private static bool Throws(Action make)
    {
        try
        {
            make();
            return false;
        }
        catch (FormatException)
        {
            return true;
        }
    }

    [Fact]
    public void TheConstructorKeepsWhatItIsGiven()
    {
        var kit = Manifest(previews: [AssetId.Parse("previews/a.png")]);

        Assert.Equal("crate-pack", kit.Slug);
        Assert.Equal(new KitEngine("0.30.0", "0.40.0"), kit.Engine);
        Assert.Equal("previews/a.png", Assert.Single(kit.Previews).ToString());
    }

    [Fact]
    public void RefusesASlugOrPublisherThatIsNotLowerCaseWords()
    {
        foreach (var bad in new[] { "", "Crate", "a_b", "-a", "a-", "a--b", "a b" })
        {
            Refuses(() => Manifest(slug: bad), bad);
            Refuses(() => Manifest(publisher: bad), bad);
        }

        Refuses(() => Manifest(slug: new string('a', 41)));
    }

    [Fact]
    public void RefusesANameOrSummaryThatIsBlankLongOrNotOneLine()
    {
        Refuses(() => Manifest(name: "  "));
        Refuses(() => Manifest(name: new string('a', 61)));
        Refuses(() => Manifest(summary: "one\ntwo"));
    }

    [Fact]
    public void RefusesCharactersThatChangeHowALineIsRead()
    {
        foreach (var bad in new[] { "a\u0085b", "a\u202eb", "a\u200bb", "a\u2028b" })
        {
            Refuses(() => Manifest(name: bad), bad);
            Refuses(() => Manifest(summary: bad), bad);
        }
    }

    [Fact]
    public void AMessageShowsNoRawEscapeCharacter()
    {
        var error = Assert.Throws<FormatException>(() => Manifest(slug: "a\u001b[31m"));

        Assert.DoesNotContain('\u001b', error.Message);
        Assert.Contains(@"a\u001b[31m", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RefusesAVersionThatIsNotThreeWholeNumbers()
    {
        foreach (var bad in new[] { "1", "1.2", "1.2.3.4", "01.2.3", "v1.2.3", "1.2.x", "1.2.1234567" })
        {
            Refuses(() => Manifest(version: bad), bad);
        }
    }

    [Fact]
    public void RefusesALicenceThatSaysThereIsNone()
    {
        foreach (var bad in new[] { "", "none", "NOASSERTION", "MIT;rm", new string('a', 81) })
        {
            Refuses(() => Manifest(licence: new KitLicence(bad, "https://x.org/l")), bad);
        }

        _ = Manifest(licence: new KitLicence("MIT OR Apache-2.0", "https://x.org/l"));
    }

    [Fact]
    public void RefusesALicenceAddressThatIsNotHttpOrHttps()
    {
        foreach (var bad in new[]
        {
            "",
            "file:///etc/passwd",
            "example.com",
            "https://",
            "https://user:pw@x.org/l",
            $"https://x.org/{new string('a', 200)}",
            "https://x.org/a b",
            "https://x.org/\u202e",
        })
        {
            Refuses(() => Manifest(licence: new KitLicence("MIT", bad)), bad);
        }
    }

    // Dart reads a port of 99999 and takes the address. WebUrl refuses it, so no fixture case holds one.
    [Fact]
    public void RefusesALicenceAddressWithAPortPast65535() =>
        Refuses(() => Manifest(licence: new KitLicence("MIT", "https://x.org:99999/l")), "99999");

    [Fact]
    public void RefusesAnEngineRangeThatRunsBackwards()
    {
        Refuses(() => Manifest(engine: new KitEngine("0.40.0", "0.9.0")));
        _ = Manifest(engine: new KitEngine("0.9.0", "0.40.0"));
    }

    [Fact]
    public void RefusesTooManyTagsARepeatAndATagThatIsNotAWord()
    {
        Refuses(() => Manifest(tags: [.. Enumerable.Range(0, 9).Select(i => $"t{i}")]));
        Refuses(() => Manifest(tags: ["a", "a"]));
        Refuses(() => Manifest(tags: ["A b"]));
    }

    [Fact]
    public void RefusesAPreviewThatIsNotAnImageOrIsListedTwice()
    {
        Refuses(() => Manifest(previews: [AssetId.Parse("previews/a.gif")]));
        var one = AssetId.Parse("previews/a.png");
        Refuses(() => Manifest(previews: [one, one]));
    }

    [Fact]
    public void ReadGivesBackWhatEncodeWrote()
    {
        var kit = Manifest(containsScripts: true, previews: [AssetId.Parse("previews/a.png")]);

        var again = KitManifest.Read(kit.Encode());

        Assert.Equal(kit.Encode(), again.Encode());
        Assert.True(again.ContainsScripts);
    }

    [Fact]
    public void TheSameContentIsTheSameBytes() => Assert.Equal(Manifest().Encode(), Manifest().Encode());

    [Fact]
    public void ReadRefusesAnotherKindANewerFormatAndAMissingFlag()
    {
        var other = Good();
        other["kind"] = "orblit.pack";
        Refuses(() => KitManifest.Read(Text(other)));

        var newer = Good();
        newer["formatVersion"] = 2;
        var error = Assert.Throws<FormatException>(() => KitManifest.Read(Text(newer)));
        Assert.Contains("newer Orblit", error.Message, StringComparison.Ordinal);

        var missing = Good();
        missing.Remove("containsScripts");
        Refuses(() => KitManifest.Read(Text(missing)));
    }

    [Fact]
    public void ReadAuthoredTakesTheFlagFromTheToolNotTheFile()
    {
        var authored = new JsonObject
        {
            ["slug"] = "crate-pack",
            ["publisher"] = "brass-lamp",
            ["name"] = "Crate pack",
            ["summary"] = "Twelve crates.",
            ["version"] = "1.2.0",
            ["licence"] = new JsonObject { ["spdx"] = "MIT", ["url"] = "https://x.org/l" },
            ["engine"] = new JsonObject { ["from"] = "0.30.0", ["to"] = "0.40.0" },
            ["containsScripts"] = false,
        };

        Assert.True(KitManifest.ReadAuthored(Text(authored), containsScripts: true).ContainsScripts);
    }

    [Fact]
    public void ReadRefusesAManifestOverTheSizeLimit() =>
        Refuses(() => KitManifest.Read(Text(new JsonObject { ["name"] = new string('x', 70000) })));

    [Fact]
    public void ReadRefusesTextThatIsNotAManifest()
    {
        Refuses(() => KitManifest.Read(Encoding.UTF8.GetBytes("{")));
        Refuses(() => KitManifest.Read(Text(new JsonArray(1))));
        Refuses(() => KitManifest.Read([0xff, 0xfe]));
    }
}

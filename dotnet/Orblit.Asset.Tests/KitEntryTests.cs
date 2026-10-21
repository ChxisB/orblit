using System.Text;
using System.Text.Json.Nodes;
using Orblit.Json;

namespace Orblit.Asset.Tests;

public class KitEntryTests
{
    private static readonly string Hash = ContentHash.Of([1, 2, 3]).Hex;

    private static JsonObject Release(string version, string? key = null, JsonNode? value = null)
    {
        var release = new JsonObject
        {
            ["version"] = version,
            ["url"] = $"https://example.com/crates-{version}.opack",
            ["hash"] = Hash,
            ["bytes"] = 1234,
            ["released"] = "2026-10-19",
            ["notes"] = "Changes.",
        };
        if (key is not null)
        {
            release[key] = value;
        }

        return release;
    }

    private static JsonObject Releases(params JsonObject[] releases) =>
        new() { ["releases"] = new JsonArray([.. releases]) };

    private static byte[] Bytes(JsonNode? json) => Encoding.UTF8.GetBytes(json is null ? "null" : json.ToJsonString());

    private static KitEntry Entry(JsonNode? json) => KitEntry.Parse(Bytes(json), "brass-lamp", "crates");

    private static FormatException Refused(JsonNode? json, string what)
    {
        var error = Assert.ThrowsAny<FormatException>(() => Entry(json));
        Assert.Contains("brass-lamp/crates", error.Message);
        Assert.Contains(what, error.Message);
        return error;
    }

    public static TheoryData<string, string, string> Bad()
    {
        var data = new TheoryData<string, string, string>();

        void Add(string name, JsonNode json, string what) => data.Add(name, json.ToJsonString(), what);

        void AddRelease(string name, string key, JsonNode? value, string what) =>
            Add(name, Releases(Release("1.0.0", key, value)), what);

        var withName = Releases(Release("1.0.0"));
        withName["name"] = "x";
        Add("a key it does not know", withName, "not a key of this file");
        AddRelease("a release key it does not know", "sha", "x", "not a key of this file");
        Add("no releases", new JsonObject { ["releases"] = new JsonArray() }, "1 to 200 releases");
        Add("releases that are not a list", new JsonObject { ["releases"] = "x" }, "no \"releases\" list");
        Add("a version twice", Releases(Release("1.0.0"), Release("1.0.0")), "a version twice");
        Add("a version with a tag", Releases(Release("1.0.0-beta")), "three whole numbers");
        AddRelease("an http url", "url", "http://example.com/a.opack", "not an https address");
        AddRelease("a url with a user name", "url", "https://me@example.com/a", "not an https address");
        AddRelease("a url with a space", "url", "https://example.com/a b", "not an https address");
        AddRelease("a url with a control character", "url", "https://example.com/a\u202e", "not an https address");
        AddRelease("a url that is too long", "url", "https://example.com/" + new string('a', 300), "not an https address");
        foreach (var url in new[]
        {
            "https://localhost/a.opack",
            "https://app.localhost/a.opack",
            "https://127.0.0.1/a.opack",
            "https://127.1/a.opack",
            "https://0x7f.0.0.1/a.opack",
            "https://2130706433/a.opack",
            "https://169.254.169.254/latest",
            "https://[::1]/a.opack",
            "https://printer.local/a.opack",
            "https://build.internal/a.opack",
            "https://intranet/a.opack",
            "https://example.com./a.opack",
            "https://m\u00fcnchen.de/a.opack",
            "https://example.com:8443/a.opack",
            "https://example.com:99999/a.opack",
            "https://example.com\\@evil.com/a.opack",
            "https://example.com@evil.com/a.opack",
            "https://example.com/a.opack#evil.com",
        })
        {
            AddRelease($"the url {url}", "url", url, "not an https address");
        }

        AddRelease("a short hash", "hash", "abc", "64 hexadecimal digits");
        AddRelease("a hash that is not hex", "hash", new string('z', 64), "64 hexadecimal digits");
        AddRelease("no bytes", "bytes", 0, "A kit file is");
        AddRelease("bytes as text", "bytes", "12", "no \"bytes\" whole number");
        AddRelease("too many bytes", "bytes", 1L << 40, "A kit file is");
        AddRelease("a day that is not there", "released", "2026-02-30", "a day that exists");
        AddRelease("a day with one digit", "released", "2026-2-3", "a day that exists");
        AddRelease("a month that is not there", "released", "2026-13-01", "a day that exists");
        AddRelease("notes on two lines", "notes", "a\nb", "one line");
        AddRelease("notes that are too long", "notes", new string('a', 301), "one line");
        return data;
    }

    [Fact]
    public void ReadsReleasesNewestFirstAndOffersThePackToDownload()
    {
        var read = Entry(Releases(Release("1.2.0"), Release("1.10.0"), Release("1.9.0")));

        Assert.Equal(["1.10.0", "1.9.0", "1.2.0"], read.Releases.Select(release => release.Version));
        Assert.Equal("1.10.0", read.Latest.Version);
        Assert.Equal(WebUrl.Parse("https://example.com/crates-1.10.0.opack"), read.Latest.Offer.Url);
        Assert.Equal(Hash, read.Latest.Offer.Hash.Hex);
        Assert.Equal(Hash, read.Latest.ToJson()["hash"]);
        Assert.Equal("example.com", read.Latest.Origin.Base.Host);
        Assert.Equal(read.Latest.Bytes, read.Latest.Origin.Policy.MaxBytes);
    }

    [Fact]
    public void AUrlAtAPublicNameAQueryOrPort443IsRead()
    {
        foreach (var url in new[]
        {
            "https://github.com/o/r/releases/download/v1/a.opack",
            "https://cdn-1.example.co.uk/a.opack?raw=1",
            "https://example.com:443/a.opack",
        })
        {
            Assert.Equal(url, Entry(Releases(Release("1.0.0", "url", url))).Latest.Url);
        }
    }

    [Fact]
    public void AMissingFieldIsNamedOnce()
    {
        var bare = Release("1.0.0");
        bare.Remove("url");

        var error = Assert.ThrowsAny<FormatException>(() => Entry(Releases(bare)));

        Assert.Contains("has no \"url\" text", error.Message);
        Assert.DoesNotContain("is wrong", error.Message);
    }

    [Fact]
    public void AFileNameWithAControlCharacterIsEscapedInTheMessage()
    {
        var error = Assert.ThrowsAny<FormatException>(() => KitEntry.Parse(Bytes("nope")[..2], "am\u001bber", "rocks"));

        Assert.Contains(@"am\u001bber", error.Message);
        Assert.DoesNotContain("\u001b", error.Message);
    }

    [Fact]
    public void EncodesToTheBytesItReadsBackFrom()
    {
        var read = Entry(Releases(Release("1.2.0"), Release("1.10.0")));

        var bytes = read.Encode();

        Assert.EndsWith("}\n", Encoding.UTF8.GetString(bytes));
        var again = KitEntry.Parse(bytes, "brass-lamp", "crates");
        Assert.Equal(
            read.Releases.Select(release => DartJson.Encode(release.ToJson())),
            again.Releases.Select(release => DartJson.Encode(release.ToJson())));
        Assert.Equal(bytes, again.Encode());
    }

    [Fact]
    public void NotesMayBeLeftOut()
    {
        var bare = Release("1.0.0");
        bare.Remove("notes");

        Assert.Equal("", Entry(Releases(bare)).Latest.Notes);
    }

    [Theory]
    [MemberData(nameof(Bad))]
    public void RefusesBadEntries(string name, string json, string what)
    {
        Assert.NotEmpty(name);

        Refused(JsonNode.Parse(json), what);
    }

    [Fact]
    public void RefusesAFileThatIsNotJsonNotAnObjectOrTooBig()
    {
        Assert.ThrowsAny<FormatException>(() => KitEntry.Parse(Encoding.UTF8.GetBytes("{"), "brass-lamp", "crates"));
        Refused(new JsonArray(), "not a JSON object");
        Assert.ThrowsAny<FormatException>(() => KitEntry.Parse(new byte[300 * 1024], "brass-lamp", "crates"));
    }

    [Fact]
    public void RefusesAPublisherOrSlugThatIsNotAWord()
    {
        foreach (var name in new[] { "Brass", "a b", "../x", "" })
        {
            Assert.ThrowsAny<FormatException>(() => KitEntry.Parse(Bytes(Releases(Release("1.0.0"))), name, "crates"));
        }
    }

    [Fact]
    public void AMessageDoesNotCarryAControlCharacterRaw()
    {
        var error = Assert.ThrowsAny<FormatException>(() => Entry(Releases(Release("1.0.0", "url", "http://a\u202e"))));

        Assert.Contains(@"\u202e", error.Message);
        Assert.DoesNotContain("\u202e", error.Message);
    }
}

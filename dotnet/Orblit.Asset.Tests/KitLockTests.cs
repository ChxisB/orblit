using System.Text;
using System.Text.Json.Nodes;

namespace Orblit.Asset.Tests;

public class KitLockTests
{
    private static readonly ContentHash HashA = ContentHash.Of(Encoding.UTF8.GetBytes("a"));
    private static readonly ContentHash HashB = ContentHash.Of(Encoding.UTF8.GetBytes("b"));

    private static LockedKit Locked(
        string publisher = "brass-lamp",
        string slug = "crate-pack",
        string version = "1.0.0",
        string into = "assets/kits/crate-pack",
        Dictionary<string, ContentHash>? files = null)
    {
        files ??= new Dictionary<string, ContentHash> { ["a.png"] = HashB };
        var held = new Dictionary<AssetId, ContentHash>();
        foreach (var (name, hash) in files)
        {
            held[AssetId.Parse($"{into}/{name}".TrimStart('/'))] = hash;
        }

        return new LockedKit(publisher, slug, version, HashA, held, into);
    }

    private static JsonObject Json(KitLock kitLock) => (JsonObject)JsonNode.Parse(Encoding.UTF8.GetString(kitLock.Encode()))!;

    private static byte[] Bytes(JsonNode json) => Encoding.UTF8.GetBytes(json.ToJsonString());

    [Fact]
    public void WritesAndReadsBackTheSameLock()
    {
        var kitLock = new KitLock([Locked(), Locked(slug: "barrels")]);

        var again = KitLock.Parse(kitLock.Encode());

        Assert.Equal(["barrels", "crate-pack"], again.Kits.Select(kit => kit.Slug));
        Assert.Equal(kitLock.Encode(), again.Encode());
        var found = again.Find("brass-lamp", "crate-pack")!;
        Assert.Equal(HashA, found.Hash);
        Assert.Equal("assets/kits/crate-pack", found.Into);
        Assert.Equal(HashB, Assert.Single(found.Files.Values));
    }

    [Fact]
    public void IsTheSameBytesWhateverOrderTheFilesWereGiven()
    {
        var one = Locked(files: new() { ["a.png"] = HashA, ["b.png"] = HashB });
        var two = Locked(files: new() { ["b.png"] = HashB, ["a.png"] = HashA });

        Assert.Equal(new KitLock([one]).Encode(), new KitLock([two]).Encode());
    }

    [Fact]
    public void AddingReplacesAnEarlierInstallOfTheSameKit()
    {
        var kitLock = new KitLock([Locked(), Locked(slug: "barrels")]);

        var next = kitLock.Adding(Locked(version: "1.1.0"));

        Assert.Equal(2, next.Kits.Count);
        Assert.Equal("1.1.0", next.Find("brass-lamp", "crate-pack")!.Version);
        Assert.Equal("1.0.0", kitLock.Find("brass-lamp", "crate-pack")!.Version);
    }

    [Fact]
    public void AnEmptyLockHasNoKitsAndAddsOne()
    {
        Assert.Empty(KitLock.Empty.Kits);
        Assert.Null(KitLock.Empty.Find("a", "b"));
        Assert.Single(KitLock.Empty.Adding(Locked()).Kits);
    }

    [Fact]
    public void RefusesAKitListedTwice() =>
        Assert.ThrowsAny<FormatException>(() => new KitLock([Locked(), Locked()]));

    [Fact]
    public void RefusesABadNameVersionOrFolder()
    {
        Assert.ThrowsAny<FormatException>(() => Locked(slug: "Crate Pack"));
        Assert.ThrowsAny<FormatException>(() => Locked(version: "1.0"));
        Assert.ThrowsAny<FormatException>(() => Locked(into: ".hidden"));
    }

    [Fact]
    public void IgnoresAKeyItDoesNotKnowAndRefusesANewerFormat()
    {
        var file = Json(new KitLock([Locked()]));
        file["note"] = "hi";

        Assert.Single(KitLock.Parse(Bytes(file)).Kits);
        file["formatVersion"] = KitLock.Format + 1;
        var error = Assert.ThrowsAny<FormatException>(() => KitLock.Parse(Bytes(file)));
        Assert.Contains("newer Orblit", error.Message);
    }

    [Fact]
    public void RefusesTextThatIsNotALock()
    {
        foreach (var text in new[] { "[]", "nope", "{\"formatVersion\":1}" })
        {
            Assert.ThrowsAny<FormatException>(() => KitLock.Parse(Encoding.UTF8.GetBytes(text)));
        }

        var bad = Json(new KitLock([Locked()]));
        bad["kits"]![0]!["hash"] = "xyz";
        Assert.ThrowsAny<FormatException>(() => KitLock.Parse(Bytes(bad)));
    }

    [Fact]
    public void ALockHoldsUpTo500Kits()
    {
        var kits = Enumerable.Range(0, 501).Select(i => Locked(slug: $"kit-{i}")).ToList();

        Assert.Equal(500, new KitLock(kits[..500]).Kits.Count);
        Assert.ThrowsAny<FormatException>(() => new KitLock(kits));
    }

    [Fact]
    public void AKitHoldsUpTo5000Files()
    {
        var files = Enumerable.Range(0, 5001).ToDictionary(i => $"f{i}.bin", _ => HashB);
        var held = files.ToDictionary(file => AssetId.Parse($"assets/{file.Key}"), file => file.Value);

        Assert.Equal(5000, new LockedKit("brass-lamp", "crate-pack", "1.0.0", HashA, held.Take(5000).ToDictionary()).Files.Count);
        Assert.ThrowsAny<FormatException>(() => new LockedKit("brass-lamp", "crate-pack", "1.0.0", HashA, held));
    }
}

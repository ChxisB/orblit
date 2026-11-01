using System.Text;
using Orblit.Json;

namespace Orblit.Asset.Tests;

public class CookKeyTests
{
    private static ContentHash Hash(string text) => ContentHash.Of(Encoding.UTF8.GetBytes(text));

    // F2: six parameters, all named at each call.
    private static CookKey Key(
        string importer = "texture",
        int version = 1,
        string source = "source",
        Dictionary<AssetId, ContentHash>? dependencies = null,
        Dictionary<string, object?>? settings = null,
        Dictionary<string, object?>? target = null) =>
        new(importer, version, Hash(source), dependencies, settings, target);

    // what changes a key

    [Fact]
    public void TheSameRecipeIsTheSameKey()
    {
        Assert.Equal(Key(), Key());
        Assert.Equal(Key().Hash, Key().Hash);
    }

    [Fact]
    public void DifferentSourceBytesAreADifferentKey() => Assert.NotEqual(Key(source: "a"), Key(source: "b"));

    [Fact]
    public void ADifferentImporterIsADifferentKey() => Assert.NotEqual(Key(importer: "texture"), Key(importer: "splat"));

    // The case a cache gets wrong when nobody thinks about it: every byte on disk is the same and
    // the code that reads them is not.
    [Fact]
    public void ANewVersionOfOneImporterIsADifferentKey() => Assert.NotEqual(Key(version: 1), Key(version: 2));

    [Fact]
    public void DifferentSettingsAreADifferentKey() =>
        Assert.NotEqual(
            Key(settings: new() { ["role"] = "colour" }),
            Key(settings: new() { ["role"] = "normal" }));

    [Fact]
    public void ADifferentTargetIsADifferentKey() =>
        Assert.NotEqual(
            Key(target: new() { ["formats"] = "astc" }),
            Key(target: new() { ["formats"] = "bc" }));

    [Fact]
    public void ADependencyChangingIsADifferentKey()
    {
        var id = AssetId.Parse("textures/wall.png");
        Assert.NotEqual(
            Key(dependencies: new() { [id] = Hash("one") }),
            Key(dependencies: new() { [id] = Hash("two") }));
    }

    // Every byte in the project is the same; what the result points at is not.
    [Fact]
    public void ADependencyMovingToAnotherIdIsADifferentKey() =>
        Assert.NotEqual(
            Key(dependencies: new() { [AssetId.Parse("a/wall.png")] = Hash("x") }),
            Key(dependencies: new() { [AssetId.Parse("b/wall.png")] = Hash("x") }));

    [Fact]
    public void LosingADependencyIsADifferentKey() =>
        Assert.NotEqual(Key(dependencies: new() { [AssetId.Parse("a.bin")] = Hash("x") }), Key());

    // what does not change a key

    [Fact]
    public void TheOrderSettingsWereBuiltIn() =>
        Assert.Equal(
            Key(settings: new() { ["a"] = 1, ["b"] = 2 }),
            Key(settings: new() { ["b"] = 2, ["a"] = 1 }));

    [Fact]
    public void TheOrderNestedSettingsWereBuiltIn() =>
        Assert.Equal(
            Key(settings: new()
            {
                ["mips"] = new Dictionary<string, object?> { ["filter"] = "box", ["renormalise"] = true },
            }),
            Key(settings: new()
            {
                ["mips"] = new Dictionary<string, object?> { ["renormalise"] = true, ["filter"] = "box" },
            }));

    [Fact]
    public void TheOrderDependenciesWereBuiltIn()
    {
        var a = AssetId.Parse("a.bin");
        var b = AssetId.Parse("b.bin");
        Assert.Equal(
            Key(dependencies: new() { [a] = Hash("1"), [b] = Hash("2") }),
            Key(dependencies: new() { [b] = Hash("2"), [a] = Hash("1") }));
    }

    // the recipe

    [Fact]
    public void IsCanonicalJsonWithEveryObjectInKeyOrder()
    {
        var recipe = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(
            JsonText.Decode(Key(settings: new() { ["b"] = 1, ["a"] = 2 }).Recipe));
        var settings = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(recipe["settings"]);
        Assert.Equal(["a", "b"], settings.Keys);
        Assert.Equal([2L, 1L], settings.Values);
        Assert.Contains("\"a\":2,\"b\":1", Key(settings: new() { ["b"] = 1, ["a"] = 2 }).Recipe);
    }

    [Fact]
    public void SaysWhichOfTwoKeysDiffersWhichIsWhyItIsKept()
    {
        var colour = Key(settings: new() { ["role"] = "colour" }).Recipe;
        var normal = Key(settings: new() { ["role"] = "normal" }).Recipe;
        Assert.NotEqual(colour, normal);
        Assert.Equal(normal, colour.Replace("colour", "normal", StringComparison.Ordinal));
    }

    [Fact]
    public void IsTheTextTheKeyHashes()
    {
        var key = Key();
        Assert.Equal(ContentHash.Of(Encoding.UTF8.GetBytes(key.Recipe)), key.Hash);
    }

    // what is refused

    [Fact]
    public void AnImporterWithNoName() => Assert.Throws<ArgumentException>(() => Key(importer: ""));

    [Fact]
    public void AnImporterVersionBelowOne() => Assert.ThrowsAny<ArgumentException>(() => Key(version: 0));

    [Fact]
    public void ASettingThatIsNotJson() =>
        Assert.Throws<ArgumentException>(() => Key(settings: new() { ["when"] = TimeSpan.Zero }).Recipe);

    [Fact]
    public void ANumberJsonCannotWrite()
    {
        Assert.Throws<ArgumentException>(() => Key(settings: new() { ["scale"] = double.NaN }).Recipe);
        Assert.Throws<ArgumentException>(() => Key(settings: new() { ["scale"] = double.PositiveInfinity }).Recipe);
    }

    [Fact]
    public void AMapWhoseKeysAreNotStrings() =>
        Assert.Throws<ArgumentException>(() => Key(settings: new()
        {
            ["sizes"] = new Dictionary<object, object?> { [1L] = "one" },
        }).Recipe);
}

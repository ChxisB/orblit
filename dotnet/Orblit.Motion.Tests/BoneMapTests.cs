using Orblit.Json;
using static Orblit.Motion.Tests.ClipSamples;

namespace Orblit.Motion.Tests;

public class BoneMapTests
{
    private readonly RestSkeleton _from = Chain("root", "upper", "lower");
    private readonly RestSkeleton _to = Chain("root", "upper", "lower");

    [Fact]
    public void UnpairedLeavesOutABoneThatAMatchingNameWouldDrive()
    {
        var both = ClipRetarget.Of(Swing(), _from, _to);
        SameNames(["lower", "upper"], BonesOf(both.Clip));

        var left = ClipRetarget.Of(Swing(), _from, _to, unpaired: new HashSet<string> { "lower" });
        SameNames(["upper"], BonesOf(left.Clip));
    }

    [Fact]
    public void UnpairedLetsABoneNamedByHandDriveAnotherOne()
    {
        var paired = ClipRetarget.Of(
            Swing(),
            _from,
            _to,
            new Dictionary<string, string> { ["lower"] = "upper" },
            new HashSet<string> { "upper" });
        SameNames(["lower"], BonesOf(paired.Clip));
    }

    [Fact]
    public void UnpairedRefusesABoneTheTargetLacks() =>
        Assert.Throws<ArgumentException>(
            () => ClipRetarget.Of(Swing(), _from, _to, unpaired: new HashSet<string> { "tail" }));

    [Fact]
    public void UnpairedRefusesABoneThatIsAlsoPairedByHand() =>
        Assert.Throws<ArgumentException>(() => ClipRetarget.Of(
            Swing(),
            _from,
            _to,
            new Dictionary<string, string> { ["lower"] = "upper" },
            new HashSet<string> { "lower" }));

    [Fact]
    public void ABoneMapKeepsWhatItWasGivenAndSortsItOnTheWayOut()
    {
        var map = new BoneMap(
            fromAsset: "models/hero.glb",
            toAsset: "models/dwarf.glb",
            bones: new Dictionary<string, string> { ["wrist"] = "hand", ["ankle"] = "foot" },
            unpaired: new HashSet<string> { "tail", "cape" },
            humanoid: true,
            fixRest: true);
        var json = Assert.IsAssignableFrom<JsonMap>(JsonText.Decode(map.Encode()));

        Assert.Equal("orblit.bonemap", json["kind"]);
        Assert.Equal(new List<string> { "ankle", "wrist" }, Assert.IsAssignableFrom<JsonMap>(json["bones"]).Keys.ToList());
        Assert.Equal(new List<object?> { "cape", "tail" }, Assert.IsAssignableFrom<JsonList>(json["unpaired"]).ToList());
        Assert.EndsWith("\n", map.Encode(), StringComparison.Ordinal);
    }

    [Fact]
    public void ABoneMapReadsBackWhatItWrote()
    {
        var map = new BoneMap(
            fromAsset: "models/hero.glb",
            toAsset: "models/dwarf.glb",
            bones: new Dictionary<string, string> { ["wrist"] = "hand" },
            unpaired: new HashSet<string> { "tail" },
            humanoid: true);
        var loaded = BoneMap.Decode(map.Encode());

        Assert.Empty(loaded.Problems);
        Assert.Equal("models/hero.glb", loaded.Map.FromAsset);
        Assert.Equal("models/dwarf.glb", loaded.Map.ToAsset);
        SameMap(new Dictionary<string, string> { ["wrist"] = "hand" }, loaded.Map.Bones);
        SameNames(["tail"], loaded.Map.Unpaired);
        Assert.True(loaded.Map.Humanoid);
        Assert.False(loaded.Map.FixRest);
        Assert.Equal(map.Encode(), loaded.Map.Encode());
    }

    [Fact]
    public void ABoneMapIsTheSameAsSpellingTheArgumentsOut()
    {
        var map = new BoneMap(
            bones: new Dictionary<string, string> { ["lower"] = "upper" },
            unpaired: new HashSet<string> { "upper" },
            fixRest: true);
        var byMap = map.Apply(Swing(), _from, _to);
        var byHand = ClipRetarget.Of(
            Swing(),
            _from,
            _to,
            new Dictionary<string, string> { ["lower"] = "upper" },
            new HashSet<string> { "upper" },
            fixRest: true);
        Assert.Equal(byHand.Clip.Encode(), byMap.Clip.Encode());
        Assert.Equal(byHand.Problems, byMap.Problems);
    }

    [Fact]
    public void ABoneMapChangesOneThingWithCopyWithAndKeepsTheRest()
    {
        var map = new BoneMap(
            fromAsset: "a",
            toAsset: "b",
            bones: new Dictionary<string, string> { ["x"] = "y" },
            unpaired: new HashSet<string> { "z" });
        var other = map.CopyWith(humanoid: true);

        Assert.True(other.Humanoid);
        Assert.Equal("a", other.FromAsset);
        SameMap(new Dictionary<string, string> { ["x"] = "y" }, other.Bones);
        SameNames(["z"], other.Unpaired);
    }

    [Fact]
    public void ABoneMapPairsABoneByHandAndTakesItOffTheUnpairedOnes()
    {
        var map = new BoneMap(unpaired: new HashSet<string> { "lower" }).WithPair("lower", "upper");

        SameMap(new Dictionary<string, string> { ["lower"] = "upper" }, map.Bones);
        Assert.Empty(map.Unpaired);
    }

    [Fact]
    public void ABoneMapLeavesABoneAloneAndTakesItsPairAway()
    {
        var map = new BoneMap(bones: new Dictionary<string, string> { ["lower"] = "upper" }).WithUnpaired("lower");

        Assert.Empty(map.Bones);
        SameNames(["lower"], map.Unpaired);
    }

    [Fact]
    public void ABoneMapHandsABoneBackToNamesAndTheBody()
    {
        var map = new BoneMap(
            bones: new Dictionary<string, string> { ["lower"] = "upper" },
            unpaired: new HashSet<string> { "tail" }).WithAuto("lower").WithAuto("tail");

        Assert.Empty(map.Bones);
        Assert.Empty(map.Unpaired);
    }

    [Fact]
    public void ABoneMapKeepsTheOriginalWhenAChangeIsMade()
    {
        var map = new BoneMap(bones: new Dictionary<string, string> { ["a"] = "b" });
        map.WithUnpaired("a");
        SameMap(new Dictionary<string, string> { ["a"] = "b" }, map.Bones);
    }

    [Fact]
    public void ABoneMapNamesTheBonesASkeletonLacksAndDropsThem()
    {
        var map = new BoneMap(
            bones: new Dictionary<string, string> { ["lower"] = "upper", ["tail"] = "upper", ["upper"] = "wing" },
            unpaired: new HashSet<string> { "fin" });

        Assert.Equal(new List<string> { "fin", "tail", "upper" }, map.Stale(_from, _to).ToList());
        var fitted = map.Fitted(_from, _to);
        SameMap(new Dictionary<string, string> { ["lower"] = "upper" }, fitted.Bones);
        Assert.Empty(fitted.Unpaired);
        Assert.Empty(fitted.Stale(_from, _to));
    }

    [Fact]
    public void ABoneMapSaysWhichBoneMovesEachBoneAsAClipWouldBeMoved()
    {
        var map = new BoneMap(
            bones: new Dictionary<string, string> { ["lower"] = "upper" },
            unpaired: new HashSet<string> { "upper" });
        var pairing = map.Pairing(_from, _to);

        SameMap(new Dictionary<string, string> { ["root"] = "root", ["lower"] = "upper" }, pairing.Drivers);
        Assert.Empty(pairing.Problems);

        var moved = map.Apply(Swing(), _from, _to);
        SameNames([.. pairing.Drivers.Keys.Where(static name => name != "root")], BonesOf(moved.Clip));
    }

    [Fact]
    public void ABoneMapThrowsForANameNoSkeletonHasAsApplyDoes()
    {
        var map = new BoneMap(bones: new Dictionary<string, string> { ["tail"] = "upper" });
        Assert.Throws<ArgumentException>(() => map.Pairing(_from, _to));
    }

    [Fact]
    public void ABoneMapCannotHoldTheSameBoneAsAPairAndAsUnpaired() =>
        Assert.Throws<ArgumentException>(() => new BoneMap(
            bones: new Dictionary<string, string> { ["x"] = "y" },
            unpaired: new HashSet<string> { "x" }));

    [Fact]
    public void ABoneMapCannotBeEditedAfterItIsMade()
    {
        var map = new BoneMap(
            bones: new Dictionary<string, string> { ["x"] = "y" },
            unpaired: new HashSet<string> { "z" });
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, string>)map.Bones)["a"] = "b");
        Assert.Throws<NotSupportedException>(() => ((ISet<string>)map.Unpaired).Add("a"));
    }

    [Fact]
    public void ABoneMapRefusesAFileThatIsNotABoneMap()
    {
        Assert.Throws<BoneMapFormatException>(() => BoneMap.Decode("not json"));
        Assert.Throws<BoneMapFormatException>(() => BoneMap.Decode("{\"kind\":\"orblit.clip\"}"));
    }

    [Fact]
    public void ABoneMapRefusesAFileFromANewerOrblit() =>
        Assert.Throws<BoneMapFormatException>(
            () => BoneMap.Decode("{\"kind\":\"orblit.bonemap\",\"formatVersion\":2}"));

    [Fact]
    public void ABoneMapLeavesOutAPairThatIsNotTwoNamesAndSaysSo()
    {
        var loaded = BoneMap.Decode(JsonText.Encode(Tree.Map(
            ("kind", "orblit.bonemap"),
            ("formatVersion", 1),
            ("bones", Tree.Map(("wrist", "hand"), ("elbow", 3), ("knee", string.Empty))),
            ("unpaired", Tree.List("tail", 7)))));

        SameMap(new Dictionary<string, string> { ["wrist"] = "hand" }, loaded.Map.Bones);
        SameNames(["tail"], loaded.Map.Unpaired);
        Assert.Equal(3, loaded.Problems.Count);
        Assert.Contains("elbow", loaded.Problems[0], StringComparison.Ordinal);
    }

    [Fact]
    public void ABoneMapKeepsThePairWhenAFileAlsoLeavesItUnpaired()
    {
        var loaded = BoneMap.Decode(JsonText.Encode(Tree.Map(
            ("kind", "orblit.bonemap"),
            ("formatVersion", 1),
            ("bones", Tree.Map(("wrist", "hand"))),
            ("unpaired", Tree.List("wrist")))));

        SameMap(new Dictionary<string, string> { ["wrist"] = "hand" }, loaded.Map.Bones);
        Assert.Empty(loaded.Map.Unpaired);
        Assert.Contains("wrist", Assert.Single(loaded.Problems), StringComparison.Ordinal);
    }

    [Fact]
    public void ABoneMapReadsAFileThatSaysNothingButWhatItIs()
    {
        var loaded = BoneMap.Decode("{\"kind\":\"orblit.bonemap\",\"formatVersion\":1}");
        Assert.Empty(loaded.Map.Bones);
        Assert.False(loaded.Map.Humanoid);
        Assert.Empty(loaded.Problems);
    }

    /// <summary>Bones half a metre apart in a line, so a turn of any one of them shows further down.</summary>
    private static RestSkeleton Chain(params string[] names) => new(
        names,
        [.. Enumerable.Range(0, names.Length).Select(static i => i - 1)],
        [.. names.Select(static (_, i) => Matrix4.TranslationValues(0, i == 0 ? 0 : 0.5, 0))]);

    private static ClipChannel<Quaternion> TurnsBy(string bone, double degrees) =>
        Turns(
            bone,
            (0, Quaternion.Identity()),
            (1, Quaternion.AxisAngle(new Vector3(0, 0, 1), degrees * Math.PI / 180)));

    private static void SameMap(Dictionary<string, string> expected, IEnumerable<KeyValuePair<string, string>> actual) =>
        Assert.Equal(expected, actual.ToDictionary(static pair => pair.Key, static pair => pair.Value));

    private static void SameNames(IEnumerable<string> expected, IEnumerable<string> actual) =>
        Assert.Equal(expected.Order(StringComparer.Ordinal).ToList(), actual.Order(StringComparer.Ordinal).ToList());

    private static ClipDocument Swing() => new("Swing", 1, channels: [TurnsBy("upper", 30), TurnsBy("lower", 50)]);

    private static List<string> BonesOf(ClipDocument clip) =>
        [.. clip.Channels.Select(static channel => channel.Bone).OfType<string>().Distinct()];
}

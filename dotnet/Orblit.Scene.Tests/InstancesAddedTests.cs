using Orblit.Json;
using static Orblit.Scene.Tests.Tree;

namespace Orblit.Scene.Tests;

/// <summary>What the port checks that the ported instance tests do not.</summary>
public class InstancesAddedTests
{
    private const string LampAsset = "props/lamp.oprefab";

    private static SceneEntity Part(string id, string? parent = null) => new(
        id,
        id,
        parent,
        components: new OrderedDictionary<string, SceneComponent>
        {
            [SceneComponents.Transform] = new TransformComponent(),
        });

    private static SceneEntity Stub(
        string id,
        SceneDiff? overrides = null,
        string? parent = null,
        string asset = LampAsset) => new(
        id,
        id,
        parent,
        components: new OrderedDictionary<string, SceneComponent>
        {
            [SceneComponents.Prefab] = new PrefabComponent(asset: asset, overrides: overrides),
        });

    private static PrefabSource Lamp() => asset => asset == LampAsset
        ? new PrefabDocument(
            "Lamp",
            "lamp",
            new SceneDocument("Lamp", entities: [Part("lamp"), Part("pole", "lamp"), Part("bulb", "pole")]))
        : null;

    [Fact]
    public void EveryEditThatNeedsAnEntityThereNamesIt()
    {
        var entity = Part("lamp").ToJson();
        var ops = new (SceneOp Op, string? Target)[]
        {
            (new SetEntityName("a", "x", "y"), "a"),
            (new SetVisible("b", true, false), "b"),
            (new SetComponent("c", SceneComponents.Transform, null, null), "c"),
            (new SetField("d", SceneComponents.Transform, "position", null, null), "d"),
            (new Reparent("e", null, "f"), "e"),
            (new Reorder("g", null, "h"), "g"),
            (new AddEntity(entity), null),
            (new RemoveEntity(entity), null),
        };

        foreach (var (op, target) in ops)
        {
            Assert.Equal(target, InstanceOpener.Target(op));
        }
    }

    [Fact]
    public void PartsAlreadyBesideALinkAreKeptNotDoubled()
    {
        var written = new SceneDocument(entities: [Stub("lamp1"), Part("lamp1/pole", "lamp1")]);

        var load = Instances.Expand(written, Lamp());

        Assert.Empty(load.Problems);
        Assert.Equal(new[] { "lamp1", "lamp1/pole" }, load.Document.Entities.Select(static e => e.Id));
        Assert.Equal(PrefabState.Open, ((PrefabComponent)load.Document["lamp1"]![SceneComponents.Prefab]!).State);
    }

    [Fact]
    public void ALinkWithAPathForAnIdBelongsToTheOuterInstanceAndStaysFolded()
    {
        var written = new SceneDocument(entities: [Part("crate"), Stub("crate/extra", parent: "crate")]);

        var load = Instances.Expand(written, Lamp());

        Assert.Empty(load.Problems);
        Assert.Equal(new[] { "crate", "crate/extra" }, load.Document.Entities.Select(static e => e.Id));
        Assert.Equal(
            PrefabState.Folded,
            ((PrefabComponent)load.Document["crate/extra"]![SceneComponents.Prefab]!).State);
    }

    [Fact]
    public void AnEditThatTakesTheRootOutOfItsInstanceIsLetGo()
    {
        var overrides = new SceneDiff([
            new RemoveEntity(Part("lamp").ToJson()),
            new Reparent("lamp", null, "bulb"),
        ]);

        var load = Instances.Expand(new SceneDocument(entities: [Stub("lamp1", overrides)]), Lamp());

        Assert.Empty(load.Problems);
        Assert.Equal(
            new[] { "lamp1", "lamp1/pole", "lamp1/bulb" },
            load.Document.Entities.Select(static e => e.Id));
        Assert.Null(load.Document["lamp1"]!.Parent);
    }

    [Fact]
    public void APrefabThatHasLostItsRootSaysSo()
    {
        var broken = new PrefabDocument("Lamp", "gone", new SceneDocument(entities: [Part("lamp")]));

        var error = Assert.Throws<PrefabException>(() => broken.RootEntity);

        Assert.Contains("gone", error.Message);
    }

    [Fact]
    public void APrefabNamingAMissingRootFallsBackToTheFirstThingWithNothingAboveIt()
    {
        var text = JsonText.Encode(Map(
            ("kind", PrefabDocument.Marker),
            ("name", "Lamp"),
            ("root", "gone"),
            ("objects", List(Map(("id", "pole"), ("parent", "lamp")), Map(("id", "lamp"))))));

        var load = PrefabDocument.Decode(text);

        Assert.Equal("lamp", load.Prefab.Root);
    }

    [Fact]
    public void APrefabFromANewerOrblitOrWithNothingInItIsRefused()
    {
        var kind = ("kind", (object?)PrefabDocument.Marker);

        Assert.Throws<SceneFormatException>(
            () => PrefabDocument.Decode(JsonText.Encode(Map(kind, ("formatVersion", 2L), ("objects", List())))));
        Assert.Throws<SceneFormatException>(
            () => PrefabDocument.Decode(JsonText.Encode(Map(kind, ("sceneFormatVersion", 99L), ("objects", List())))));
        Assert.Throws<SceneFormatException>(() => PrefabDocument.Decode(JsonText.Encode(Map(kind))));
        Assert.Throws<SceneFormatException>(
            () => PrefabDocument.Decode(JsonText.Encode(Map(kind, ("objects", List())))));
    }

    [Fact]
    public void ApplyRefusesWhatIsNotAnInstanceBelongsToAnotherOrCannotBeRead()
    {
        var lamp = Lamp();
        var street = new PrefabDocument(
            "Street",
            "street",
            new SceneDocument("Street", entities: [Part("street"), Stub("lamp3", parent: "street")]));
        PrefabSource source = asset => asset == "props/street.oprefab" ? street : lamp(asset);
        var document = Instances.Expand(
            new SceneDocument(entities: [Part("crate"), Stub("lamp1"), Stub("street1", asset: "props/street.oprefab")]),
            source).Document;

        Assert.Contains("not an instance", Assert.Throws<PrefabException>(() => Instances.Apply(document, "crate", source)).Message);
        Assert.Contains("not an instance", Assert.Throws<PrefabException>(() => Instances.Apply(document, "none", source)).Message);
        Assert.Contains(
            "belongs to the prefab",
            Assert.Throws<PrefabException>(() => Instances.Apply(document, "street1/lamp3", source)).Message);
        Assert.Contains(
            LampAsset,
            Assert.Throws<PrefabException>(() => Instances.Apply(document, "lamp1", _ => null)).Message);
    }

    [Fact]
    public void RevertAndUnpackLeaveWhatIsNotAnOpenInstanceAlone()
    {
        var source = Lamp();
        var document = new SceneDocument(entities: [Part("crate"), Stub("lamp1")]);

        Assert.Same(document, Instances.Revert(document, "crate", source).Document);
        Assert.Same(document, Instances.Revert(document, "lamp1", source).Document);
        var unpacked = Instances.Unpack(document, "crate", source, () => "n1");
        Assert.Same(document, unpacked.Document);
        Assert.Empty(unpacked.Renamed);
    }

    [Fact]
    public void RefreshLeavesTheExceptionsAndAnythingThatDoesNotUseThePrefabAlone()
    {
        var source = Lamp();
        var document = Instances.Expand(new SceneDocument(entities: [Part("crate"), Stub("lamp1"), Stub("lamp2")]), source).Document;
        var changed = new PrefabDocument(
            "Lamp",
            "lamp",
            new SceneDocument("Lamp", entities: [Part("lamp"), Part("pole", "lamp"), Part("bulb", "pole"), Part("shade", "bulb")]));
        PrefabSource after = _ => changed;

        Assert.Same(document, Instances.Refresh(document, "props/other.oprefab", source, after).Document);

        var refreshed = Instances.Refresh(document, LampAsset, source, after, ["lamp2"]).Document;
        Assert.True(refreshed.Contains("lamp1/shade"));
        Assert.False(refreshed.Contains("lamp2/shade"));
    }

    [Fact]
    public void AnInstanceInsideAnotherCanBeUnpackedOnItsOwn()
    {
        var lamp = Lamp();
        var street = new PrefabDocument(
            "Street",
            "street",
            new SceneDocument("Street", entities: [Part("street"), Stub("lamp3", parent: "street")]));
        PrefabSource source = asset => asset == "props/street.oprefab" ? street : lamp(asset);
        var document = Instances.Expand(
            new SceneDocument(entities: [Stub("street1", asset: "props/street.oprefab")]),
            source).Document;

        var next = 0;
        var result = Instances.Unpack(document, "street1/lamp3", source, () => $"n{++next}");

        Assert.False(result.Document["street1/lamp3"]!.Has(SceneComponents.Prefab));
        Assert.Equal("n1", result.Renamed["street1/lamp3/pole"]);
        Assert.Equal("street1/lamp3", result.Document["n1"]!.Parent);
    }

    [Fact]
    public void RevertLeavesAnInstanceWhosePrefabCannotBeReadAsItIs()
    {
        var document = Instances.Expand(new SceneDocument(entities: [Stub("lamp1")]), Lamp()).Document;

        Assert.Same(document, Instances.Revert(document, "lamp1", _ => null).Document);
    }

    [Fact]
    public void UnpackingOneWhoseRootIsAnotherPrefabLetsGoOfTheChangesTheRootCarried()
    {
        var lamp = Lamp();
        var renamed = new SceneDiff([new SetEntityName("bulb", "bulb", "Lantern")]);
        var fancy = new PrefabDocument(
            "Fancy lamp",
            "base",
            new SceneDocument("Fancy lamp", entities: [Stub("base", renamed), Part("sign", "base")]));
        PrefabSource source = asset => asset == "fancy.oprefab" ? fancy : lamp(asset);
        var document = Instances.Expand(
            new SceneDocument(entities: [Stub("f1", asset: "fancy.oprefab")]),
            source).Document;

        var result = Instances.Unpack(document, "f1", source, () => "n1");

        var link = Instances.LinkOf(result.Document["f1"]!)!;
        Assert.Equal(PrefabState.Open, link.State);
        Assert.True(link.Overrides.IsEmpty);
        Assert.Equal("Lantern", result.Document["f1/bulb"]!.Name);
    }
}

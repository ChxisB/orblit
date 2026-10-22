using Orblit.Json;
using static Orblit.Scene.Tests.Tree;

namespace Orblit.Scene.Tests;

public class InstancesTests
{
    private const string LampAsset = "props/lamp.oprefab";
    private const string StreetAsset = "props/street.oprefab";

    private static readonly PrefabSource Source = Library((LampAsset, Lamp()), (StreetAsset, Street()));

    // F2: an entity's id, name, parent, place and mesh are separate things a test names as it needs them.
    private static SceneEntity Thing(
        string id,
        string? name = null,
        string? parent = null,
        double x = 0,
        double y = 0,
        MeshComponent? mesh = null)
    {
        var components = new OrderedDictionary<string, SceneComponent>
        {
            [SceneComponents.Transform] = new TransformComponent(new Vector3(x, y, 0)),
        };
        if (mesh is not null)
        {
            components[SceneComponents.Mesh] = mesh;
        }

        return new SceneEntity(id, name ?? id, parent, components: components);
    }

    private static SceneEntity Stub(string id, string asset, string? parent = null, SceneDiff? overrides = null) =>
        new(id, id, parent, components: new OrderedDictionary<string, SceneComponent>
        {
            [SceneComponents.Prefab] = new PrefabComponent(asset: asset, overrides: overrides),
        });

    /// <summary>A lamp post: a pole with a bulb on top.</summary>
    private static PrefabDocument Lamp(bool shade = false)
    {
        var entities = new List<SceneEntity>
        {
            Thing("lamp", name: "Lamp"),
            Thing("pole", name: "Pole", parent: "lamp", mesh: new MeshComponent(asset: "models/pole.glb")),
            Thing("bulb", name: "Bulb", parent: "pole", y: 2),
        };
        if (shade)
        {
            entities.Add(Thing("shade", name: "Shade", parent: "bulb"));
        }

        return new PrefabDocument("Lamp", "lamp", new SceneDocument("Lamp", entities: entities));
    }

    /// <summary>A street with a lamp on it, for prefabs inside prefabs.</summary>
    private static PrefabDocument Street() => new(
        "Street",
        "street",
        new SceneDocument("Street", entities: [Thing("street", name: "Street"), Stub("lamp3", LampAsset, "street")]));

    private static PrefabSource Library(params (string Asset, PrefabDocument Prefab)[] prefabs) =>
        asset => prefabs.Where(one => one.Asset == asset).Select(static one => one.Prefab).FirstOrDefault();

    private static SceneDocument Scene(params SceneEntity[] entities) => new(entities: entities);

    private static SceneDocument Opened(SceneDocument document, PrefabSource source)
    {
        var load = Instances.Expand(document, source);
        Assert.Empty(load.Problems);
        return load.Document;
    }

    private static SceneDocument MovedTo(SceneDocument document, string id, double x, double y) =>
        document.WithEntity(
            id,
            document[id]!.WithComponent(SceneComponents.Transform, new TransformComponent(new Vector3(x, y, 0))));

    private static Vector3 PositionOf(SceneDocument document, string id) =>
        ((TransformComponent)document[id]![SceneComponents.Transform]!).Position;

    private static PrefabComponent LinkOf(SceneDocument document, string id) =>
        (PrefabComponent)document[id]![SceneComponents.Prefab]!;

    private static void SameRenames(IReadOnlyDictionary<string, string> actual, params (string From, string To)[] expected) =>
        Assert.Equal(
            expected.OrderBy(static one => one.From, StringComparer.Ordinal),
            actual.OrderBy(static one => one.Key, StringComparer.Ordinal).Select(static one => (one.Key, one.Value)));

    // A path names something inside an instance.

    [Fact]
    public void OneIdForEachDocumentCrossed()
    {
        Assert.Equal("street1/lamp3", EntityPath.Join("street1", "lamp3"));
        Assert.Equal(new[] { "street1", "lamp3", "bulb" }, EntityPath.Segments("street1/lamp3/bulb"));
        Assert.Equal("street1", EntityPath.InstanceOf("street1/lamp3/bulb"));
        Assert.Null(EntityPath.InstanceOf("crate"));
    }

    [Fact]
    public void TheRootOfAnInstanceHasOneNameTheInstances()
    {
        Assert.Equal("lamp", EntityPath.LocalTo("lamp1", "lamp1", "lamp"));
        Assert.Equal("bulb", EntityPath.LocalTo("lamp1", "lamp1/bulb", "lamp"));
        Assert.Null(EntityPath.LocalTo("lamp1", "lamp10/bulb", "lamp"));
        Assert.Equal("lamp1", EntityPath.Inside("lamp1", "lamp", "lamp"));
        Assert.Equal("lamp1/bulb", EntityPath.Inside("lamp1", "bulb", "lamp"));
    }

    [Fact]
    public void WhatItIsInsideNearestFirst()
    {
        Assert.Equal(new[] { "street1/lamp3", "street1" }, EntityPath.Enclosing("street1/lamp3/bulb"));
        Assert.Empty(EntityPath.Enclosing("crate"));
        Assert.True(EntityPath.Within("lamp1", "lamp1"));
        Assert.True(EntityPath.Within("lamp1", "lamp1/bulb"));
        Assert.False(EntityPath.Within("lamp1", "lamp10"));
    }

    // A prefab file.

    [Fact]
    public void APrefabFileReadsBackWhatItWrote()
    {
        var prefab = Lamp();
        var load = PrefabDocument.Decode(prefab.Encode());

        Assert.Empty(load.Problems);
        Assert.Equal("lamp", load.Prefab.Root);
        Assert.Equal("Lamp", load.Prefab.Name);
        Assert.Equal(prefab.Encode(), load.Prefab.Encode());
    }

    [Fact]
    public void APrefabFromBeforeInstancesWereLinksStillOpens()
    {
        var load = PrefabDocument.Decode(JsonText.Encode(Map(
            ("kind", PrefabDocument.Marker),
            ("formatVersion", 1L),
            ("sceneFormatVersion", 4L),
            ("name", "Crate"),
            ("root", "c1"),
            ("objects", List(
                Map(
                    ("id", "c1"),
                    ("name", "Crate"),
                    ("components", Map(("transform", Map(("position", List(0.0, 0.0, 0.0))))))),
                Map(("id", "c2"), ("name", "Lid"), ("parent", "c1")))))));

        Assert.Equal("Crate", load.Prefab.RootEntity.Name);
        Assert.Equal("c2", Assert.Single(load.Prefab.Document.ChildrenOf("c1")).Id);
    }

    [Fact]
    public void SomethingThatIsNotAPrefabIsRefused()
    {
        Assert.Throws<SceneFormatException>(() => PrefabDocument.Decode("{\"kind\": \"orblit.scene\"}"));
        Assert.Throws<SceneFormatException>(() => PrefabDocument.Decode("not json"));
    }

    [Fact]
    public void AnInstanceOnItsRootIsALinkAndItsOverrides()
    {
        var link = new PrefabComponent(
            asset: LampAsset,
            overrides: new SceneDiff([new SetVisible("bulb", true, false)]));
        var back = SceneComponents.Read(
            SceneComponents.Prefab,
            Assert.IsAssignableFrom<JsonMap>(JsonText.Decode(JsonText.Encode(link.ToJson()))));

        Assert.Equal(JsonText.Encode(link.ToJson()), JsonText.Encode(back.ToJson()));
        Assert.False(link.ToJson().ContainsKey("state"));
    }

    // An instance is a link and a diff.

    [Fact]
    public void OpeningPutsEveryPartInUnderAPathIntoTheInstance()
    {
        var document = Opened(Scene(Stub("lamp1", LampAsset)), Source);

        Assert.Equal(new[] { "lamp1", "lamp1/pole", "lamp1/bulb" }, document.Entities.Select(static e => e.Id));
        Assert.Equal("lamp1", document["lamp1/pole"]!.Parent);
        Assert.Equal("lamp1/pole", document["lamp1/bulb"]!.Parent);
        Assert.Equal("lamp1", document["lamp1"]!.Name);
        Assert.Equal(PrefabState.Open, LinkOf(document, "lamp1").State);
        Assert.True(document["lamp1"]!.Has(SceneComponents.Transform));
    }

    [Fact]
    public void AnUntouchedInstanceFoldsBackToExactlyTheLink()
    {
        var saved = Scene(Stub("lamp1", LampAsset));
        var folded = Instances.Fold(Opened(saved, Source), Source);

        Assert.Equal(saved.Encode(), folded.Encode());
    }

    [Fact]
    public void PlacedTwiceAndEditedInOnePlaceTheEditIsInThatOneOnly()
    {
        var document = Opened(Scene(Stub("lamp1", LampAsset), Stub("lamp2", LampAsset)), Source);
        document = MovedTo(document, "lamp1/bulb", 0, 3);

        var saved = Instances.Fold(document, Source);
        var again = Opened(SceneDocument.Decode(saved.Encode()).Document, Source);

        Assert.Equal(3, PositionOf(again, "lamp1/bulb").Y);
        Assert.Equal(2, PositionOf(again, "lamp2/bulb").Y);
        Assert.True(LinkOf(saved, "lamp2").Overrides.IsEmpty);
        var overrides = LinkOf(saved, "lamp1").Overrides.Operations;
        Assert.Equal("bulb", Assert.IsType<SetField>(Assert.Single(overrides)).Id);
    }

    [Fact]
    public void ThePrefabsOwnChangeShowsInBothKeepingEachOnes()
    {
        var document = Opened(Scene(Stub("lamp1", LampAsset), Stub("lamp2", LampAsset)), Source);
        document = MovedTo(document, "lamp1/bulb", 0, 3);
        var saved = Instances.Fold(document, Source);

        var changed = Library((LampAsset, Lamp(shade: true)));
        var again = Opened(saved, changed);

        Assert.Equal("lamp1/bulb", again["lamp1/shade"]!.Parent);
        Assert.Equal("lamp2/bulb", again["lamp2/shade"]!.Parent);
        Assert.Equal(3, PositionOf(again, "lamp1/bulb").Y);
    }

    [Fact]
    public void TheFileHoldsTheDiffNotTheParts()
    {
        var document = Opened(Scene(Stub("lamp1", LampAsset), Stub("lamp2", LampAsset)), Source);
        document = MovedTo(document, "lamp1", 5, 0);
        var text = Instances.Fold(document, Source).Encode();

        Assert.DoesNotContain("lamp1/", text);
        Assert.DoesNotContain("\"Pole\"", text);
        var entities = Assert.IsAssignableFrom<JsonList>(Assert.IsAssignableFrom<JsonMap>(JsonText.Decode(text))["entities"]);
        Assert.Equal(2, entities.Count);
    }

    [Fact]
    public void WhereItStandsIsAnOverrideOnThePrefabsRoot()
    {
        var document = Opened(Scene(Stub("lamp1", LampAsset)), Source);
        document = MovedTo(document, "lamp1", 5, 0);
        var saved = Instances.Fold(document, Source);

        var op = Assert.Single(LinkOf(saved, "lamp1").Overrides.Operations);
        Assert.Equal("lamp", Assert.IsType<SetField>(op).Id);
        Assert.Equal(5, PositionOf(Opened(saved, Source), "lamp1").X);
    }

    [Fact]
    public void ARemovedPartStaysRemovedAndAnAddedOneStaysAdded()
    {
        var document = Opened(Scene(Stub("lamp1", LampAsset)), Source);
        document = document
            .WithEntity("lamp1/bulb", null)
            .WithEntity("lamp1/sign", Thing("lamp1/sign", parent: "lamp1/pole"));

        var again = Opened(Instances.Fold(document, Source), Source);

        Assert.False(again.Contains("lamp1/bulb"));
        Assert.Equal("lamp1/pole", again["lamp1/sign"]!.Parent);
    }

    [Fact]
    public void SomethingHungOnAPartBelongsToTheSceneByPath()
    {
        var document = Opened(Scene(Stub("lamp1", LampAsset)), Source);
        document = document.WithEntity("flag", Thing("flag", parent: "lamp1/bulb"));

        var saved = Instances.Fold(document, Source);
        Assert.Equal("lamp1/bulb", saved["flag"]!.Parent);
        Assert.True(LinkOf(saved, "lamp1").Overrides.IsEmpty);

        var read = SceneDocument.Decode(saved.Encode());
        Assert.Empty(read.Problems);
        Assert.Equal("lamp1/bulb", read.Document["flag"]!.Parent);
        Assert.Equal("lamp1/bulb", Opened(read.Document, Source)["flag"]!.Parent);
    }

    [Fact]
    public void HungOnAPartThePrefabNoLongerHasItMovesToTheNearestThingStillThere()
    {
        var document = Scene(Stub("lamp1", LampAsset), Thing("flag", parent: "lamp1/bulb"));
        var bare = new PrefabDocument("Lamp", "lamp", new SceneDocument(entities: [Thing("lamp")]));

        var load = Instances.Expand(document, Library((LampAsset, bare)));

        Assert.Equal("lamp1", load.Document["flag"]!.Parent);
        Assert.NotEmpty(load.Problems);
    }

    // A prefab inside a prefab.

    [Fact]
    public void OpensAllTheWayDownOneIdPerDocumentCrossed()
    {
        var document = Opened(Scene(Stub("street1", StreetAsset)), Source);

        Assert.Equal("street1", document["street1/lamp3"]!.Parent);
        Assert.Equal("street1/lamp3/pole", document["street1/lamp3/bulb"]!.Parent);
        Assert.Equal(PrefabState.Open, LinkOf(document, "street1/lamp3").State);
    }

    [Fact]
    public void AnEditDeepInsideIsKeptInTheOuterInstancesTerms()
    {
        var document = Opened(Scene(Stub("street1", StreetAsset)), Source);
        document = MovedTo(document, "street1/lamp3/bulb", 0, 4);

        var saved = Instances.Fold(document, Source);

        Assert.Single(saved.Entities);
        var op = Assert.Single(LinkOf(saved, "street1").Overrides.Operations);
        Assert.Equal("lamp3/bulb", Assert.IsType<SetField>(op).Id);
        Assert.Equal(4, PositionOf(Opened(saved, Source), "street1/lamp3/bulb").Y);
    }

    [Fact]
    public void AChangeToTheInnerPrefabReachesEveryStreet()
    {
        var saved = Scene(Stub("street1", StreetAsset));
        var again = Opened(saved, Library((LampAsset, Lamp(shade: true)), (StreetAsset, Street())));

        Assert.Equal("street1/lamp3/bulb", again["street1/lamp3/shade"]!.Parent);
    }

    [Fact]
    public void AChangeSavedOnAStreetReachesItAgainFromTheFile()
    {
        var document = Opened(Scene(Stub("street1", StreetAsset)), Source);
        document = MovedTo(document, "street1/lamp3/bulb", 0, 4);
        var saved = SceneDocument.Decode(Instances.Fold(document, Source).Encode());

        Assert.Empty(saved.Problems);
        var again = Opened(saved.Document, Source);
        Assert.Equal(4, PositionOf(again, "street1/lamp3/bulb").Y);
    }

    [Fact]
    public void AChangeToTheInstanceBeatsOneTheStreetMadeToItsLamp()
    {
        var inStreet = Opened(Street().Document, Source);
        inStreet = MovedTo(inStreet, "lamp3/bulb", 0, 3);
        var tall = Library(
            (LampAsset, Lamp()),
            (StreetAsset, new PrefabDocument("Street", "street", Instances.Fold(inStreet, Source))));
        var document = Opened(Scene(Stub("street1", StreetAsset), Stub("street2", StreetAsset)), tall);
        Assert.Equal(3, PositionOf(document, "street1/lamp3/bulb").Y);

        document = MovedTo(document, "street1/lamp3/bulb", 0, 4);
        var again = Opened(SceneDocument.Decode(Instances.Fold(document, tall).Encode()).Document, tall);

        Assert.Equal(4, PositionOf(again, "street1/lamp3/bulb").Y);
        Assert.Equal(3, PositionOf(again, "street2/lamp3/bulb").Y);
    }

    [Fact]
    public void SomethingHungInsideTheInnerPrefabOpensInEitherOrder()
    {
        var flag = Thing("flag", parent: "street1/lamp3/bulb");
        SceneEntity[][] orders = [[flag, Stub("street1", StreetAsset)], [Stub("street1", StreetAsset), flag]];

        foreach (var entities in orders)
        {
            var document = Opened(Scene(entities), Source);

            Assert.Equal("street1/lamp3/bulb", document["flag"]!.Parent);
        }
    }

    [Fact]
    public void OneThatContainsItselfStaysClosedInsteadOfLooping()
    {
        var loop = new PrefabDocument(
            "Loop",
            "loop",
            new SceneDocument(entities: [Thing("loop"), Stub("inner", "loop.oprefab", "loop")]));

        var load = Instances.Expand(Scene(Stub("x", "loop.oprefab")), Library(("loop.oprefab", loop)));

        Assert.NotNull(load.Document["x/inner"]);
        Assert.Equal(PrefabState.Folded, LinkOf(load.Document, "x/inner").State);
        Assert.Contains("itself", Assert.Single(load.Problems));
    }

    // A prefab that cannot be read.

    [Fact]
    public void AnUnreadablePrefabLeavesTheLinkAsItWasOverridesAndAll()
    {
        var overrides = new SceneDiff([new SetVisible("bulb", true, false)]);
        var document = Scene(Stub("lamp1", LampAsset, overrides: overrides), Thing("flag", parent: "lamp1/bulb"));

        var load = Instances.Expand(document, Library());

        Assert.Contains(LampAsset, Assert.Single(load.Problems));
        Assert.Equal(document.Encode(), load.Document.Encode());
        Assert.Equal(document.Encode(), Instances.Fold(load.Document, Library()).Encode());
    }

    [Fact]
    public void AChangeToAPartItNoLongerHasIsLetGoAndSaid()
    {
        var load = Instances.Expand(
            Scene(Stub("lamp1", LampAsset, overrides: new SceneDiff([new SetVisible("gone", true, false)]))),
            Library((LampAsset, Lamp())));

        Assert.True(load.Document.Contains("lamp1/pole"));
        Assert.Contains("no longer has", Assert.Single(load.Problems));
    }

    // A copy from before instances were links.

    private static OrderedDictionary<string, object?> Copied(string id, string name, string? parent = null, double y = 0)
    {
        var components = Map(("transform", Map(("position", List(0.0, y, 0.0)))));
        if (name == "Pole")
        {
            components["mesh"] = new MeshComponent(asset: "models/pole.glb").ToJson();
        }

        components["prefab"] = Map(("asset", LampAsset));
        var entity = Map(("id", id), ("name", name));
        if (parent is not null)
        {
            entity["parent"] = parent;
        }

        entity["components"] = components;
        return entity;
    }

    private static SceneDocument Legacy() => SceneDocument.Decode(JsonText.Encode(Map(
        ("formatVersion", 4L),
        ("entities", List(
            Copied("o1", "Lamp"),
            Copied("o2", "Pole", "o1"),
            // Somebody raised this one's bulb.
            Copied("o3", "Bulb", "o2", 5),
            // And hung a sign on it.
            Map(("id", "o4"), ("name", "Sign"), ("parent", "o1"))))))).Document;

    [Fact]
    public void ACopyIsRelinkedKeepingWhatWasChangedAboutIt()
    {
        var load = Instances.Expand(Legacy(), Library((LampAsset, Lamp())));
        var document = load.Document;

        Assert.Equal(PrefabState.Open, LinkOf(document, "o1").State);
        Assert.Equal("o1", document["o1/pole"]!.Parent);
        Assert.Equal("o1/pole", document["o1/bulb"]!.Parent);
        Assert.False(document["o1/pole"]!.Has(SceneComponents.Prefab));
        Assert.Equal(5, PositionOf(document, "o1/bulb").Y);
        Assert.Equal("o1", document["o4"]!.Parent);
        Assert.Contains("now linked", Assert.Single(load.Problems));
    }

    [Fact]
    public void ARelinkedCopySavesAsALinkWithOnlyThatChangeInIt()
    {
        var source = Library((LampAsset, Lamp()));
        var saved = Instances.Fold(Instances.Expand(Legacy(), source).Document, source);

        Assert.Equal(new[] { "o1", "o4" }, saved.Entities.Select(static e => e.Id));
        var op = Assert.Single(LinkOf(saved, "o1").Overrides.Operations);
        Assert.Equal("bulb", Assert.IsType<SetField>(op).Id);
    }

    [Fact]
    public void ACopyStaysACopyWhileItsPrefabCannotBeRead()
    {
        var document = Legacy();
        var load = Instances.Expand(document, Library());

        Assert.Equal(document.Encode(), load.Document.Encode());
        Assert.Equal(PrefabState.Stamped, LinkOf(load.Document, "o2").State);
    }

    // Making a prefab.

    private static SceneDocument Post() => Scene(
        Thing("post", name: "Post", x: 5, mesh: new MeshComponent(asset: "models/pole.glb")),
        Thing("light", name: "Light", parent: "post", y: 3));

    [Fact]
    public void MakesAThingNotAThingAtAPlaceAndLinksIt()
    {
        var made = Instances.MakePrefab(Post(), "post", "props/post.oprefab", Library());

        Assert.Equal("post", made.Prefab.Root);
        Assert.Equal(0, PositionOf(made.Prefab.Document, "post").X);
        SameRenames(made.Renamed, ("light", "post/light"));

        var document = made.Document;
        Assert.Equal(PrefabState.Open, LinkOf(document, "post").State);
        Assert.Equal("post", document["post/light"]!.Parent);
        Assert.Equal(5, PositionOf(document, "post").X);
        Assert.Empty(made.Problems);

        var saved = Instances.Fold(document, Library(("props/post.oprefab", made.Prefab)));
        Assert.Single(saved.Entities);
        var op = Assert.Single(LinkOf(saved, "post").Overrides.Operations);
        Assert.Equal("position", Assert.IsType<SetField>(op).Field);
    }

    [Fact]
    public void AnInstanceInsideStaysAnInstanceWrittenFolded()
    {
        var source = Library((LampAsset, Lamp()));
        var document = Opened(Scene(Thing("corner"), Stub("lamp1", LampAsset, "corner")), source);

        var made = Instances.MakePrefab(document, "corner", "props/corner.oprefab", source);

        Assert.Equal(new[] { "corner", "lamp1" }, made.Prefab.Document.Entities.Select(static e => e.Id));
        Assert.NotNull(made.Document["corner/lamp1/bulb"]);
        Assert.Equal("corner/lamp1/bulb", made.Renamed["lamp1/bulb"]);
    }

    [Fact]
    public void IsRefusedForSomethingInsideAnInstanceOrThatWouldContainItself()
    {
        var source = Library((LampAsset, Lamp()));
        var document = Opened(Scene(Thing("corner"), Stub("lamp1", LampAsset, "corner")), source);

        Assert.Throws<PrefabException>(
            () => Instances.MakePrefab(document, "lamp1/pole", "props/pole.oprefab", source));
        Assert.Throws<PrefabException>(() => Instances.MakePrefab(document, "corner", LampAsset, source));
    }

    // Applying, reverting and unpacking.

    private static SceneDocument TwoLamps()
    {
        var document = Opened(Scene(Stub("lamp1", LampAsset), Stub("lamp2", LampAsset)), Source);
        document = MovedTo(document, "lamp1", 7, 0);
        document = MovedTo(document, "lamp1/bulb", 0, 3);
        document = document.WithEntity("shade", Thing("shade", name: "Shade", parent: "lamp1/bulb"));
        return document.WithEntity("lamp2/pole", document["lamp2/pole"]!.CopyWith(name: "Tall pole"));
    }

    [Fact]
    public void ApplyWritesThisInstanceIntoThePrefabAndEveryOtherInstanceKeepsItsOwnChanges()
    {
        var applied = Instances.Apply(TwoLamps(), "lamp1", Source);
        var prefab = applied.Prefab;

        Assert.Equal("lamp", prefab.Root);
        Assert.Equal("bulb", prefab.Document["shade"]!.Parent);
        Assert.Equal(3, PositionOf(prefab.Document, "bulb").Y);
        Assert.Equal(0, PositionOf(prefab.Document, "lamp").X);
        SameRenames(applied.Renamed, ("shade", "lamp1/shade"));

        var document = applied.Document;
        Assert.False(document.Contains("shade"));
        Assert.Equal("lamp1/bulb", document["lamp1/shade"]!.Parent);
        Assert.Equal(7, PositionOf(document, "lamp1").X);
        Assert.Equal("lamp2/bulb", document["lamp2/shade"]!.Parent);
        Assert.Equal(3, PositionOf(document, "lamp2/bulb").Y);
        Assert.Equal("Tall pole", document["lamp2/pole"]!.Name);

        var saved = Instances.Fold(document, Library((LampAsset, prefab)));
        var ops = LinkOf(saved, "lamp1").Overrides.Operations;
        Assert.Equal("lamp", Assert.IsType<SetField>(Assert.Single(ops)).Id);
    }

    [Fact]
    public void ApplyReachesTheLampsInsideEveryStreet()
    {
        var document = Opened(Scene(Stub("lamp1", LampAsset), Stub("street1", StreetAsset)), Source);
        document = MovedTo(document, "lamp1/bulb", 0, 6);

        var applied = Instances.Apply(document, "lamp1", Source);

        Assert.Equal(6, PositionOf(applied.Document, "street1/lamp3/bulb").Y);
    }

    [Fact]
    public void RevertDropsTheChangesAndKeepsWhereItStands()
    {
        var document = TwoLamps().WithEntity("flag", Thing("flag", parent: "lamp1/pole"));

        var reverted = Instances.Revert(document, "lamp1", Source).Document;

        Assert.Equal(2, PositionOf(reverted, "lamp1/bulb").Y);
        Assert.Equal(7, PositionOf(reverted, "lamp1").X);
        Assert.Equal("lamp1/pole", reverted["flag"]!.Parent);
        Assert.Equal("Tall pole", reverted["lamp2/pole"]!.Name);
        // A thing hung on the bulb from outside is the scene's, and stays.
        Assert.Equal("lamp1/bulb", reverted["shade"]!.Parent);
    }

    [Fact]
    public void UnpackGivesThePartsIdsOfTheirOwn()
    {
        var next = 0;
        var result = Instances.Unpack(TwoLamps(), "lamp1", Source, () => $"n{++next}");
        var document = result.Document;

        Assert.False(document["lamp1"]!.Has(SceneComponents.Prefab));
        SameRenames(result.Renamed, ("lamp1/pole", "n1"), ("lamp1/bulb", "n2"));
        Assert.Equal("lamp1", document["n1"]!.Parent);
        Assert.Equal("n1", document["n2"]!.Parent);
        Assert.Equal("n2", document["shade"]!.Parent);
        Assert.Equal(3, PositionOf(document, "n2").Y);

        var saved = Instances.Fold(document, Source);
        Assert.True(saved.Contains("n2"));
    }

    [Fact]
    public void UnpackingAStreetLeavesItsLampsAsInstances()
    {
        var document = Opened(Scene(Stub("street1", StreetAsset)), Source);

        var result = Instances.Unpack(document, "street1", Source, () => "n1");

        Assert.Equal(PrefabState.Open, LinkOf(result.Document, "n1").State);
        Assert.Equal("n1/pole", result.Document["n1/bulb"]!.Parent);
        var saved = Instances.Fold(result.Document, Source);
        Assert.Equal(new[] { "street1", "n1" }, saved.Entities.Select(static e => e.Id));
    }

    [Fact]
    public void UnpackingOneWhoseRootIsAnotherPrefabLeavesItThatOne()
    {
        var fancy = new PrefabDocument(
            "Fancy lamp",
            "base",
            new SceneDocument(entities: [Stub("base", LampAsset), Thing("sign", parent: "base")]));
        var both = Library((LampAsset, Lamp()), ("fancy.oprefab", fancy));
        var document = Opened(Scene(Stub("f1", "fancy.oprefab")), both);
        Assert.NotNull(document["f1/base/bulb"]);
        Assert.Equal("f1", document["f1/sign"]!.Parent);

        var result = Instances.Unpack(document, "f1", both, () => "n1");

        Assert.Equal(LampAsset, LinkOf(result.Document, "f1").Asset);
        Assert.Equal("f1/pole", result.Document["f1/bulb"]!.Parent);
        Assert.Equal("f1", result.Document["n1"]!.Parent);
        var saved = Instances.Fold(result.Document, both);
        Assert.Equal(new[] { "f1", "n1" }, saved.Entities.Select(static e => e.Id));
    }

    [Fact]
    public void UnpackingAgainstAPrefabThatHasLostItsRootStillUnpacks()
    {
        var document = TwoLamps();
        var broken = Library((LampAsset, new PrefabDocument("Lamp", "gone", Lamp().Document)));
        var next = 0;

        var result = Instances.Unpack(document, "lamp1", broken, () => $"n{++next}");

        Assert.False(result.Document["lamp1"]!.Has(SceneComponents.Prefab));
        Assert.Equal("n1", result.Document["n2"]!.Parent);
        Assert.Throws<PrefabException>(() => Instances.Apply(document, "lamp1", broken));
    }

    [Fact]
    public void APartMovedOutOfItsInstanceFoldsBackUnderItsRoot()
    {
        var document = TwoLamps();
        var moved = document.WithEntity("lamp1/bulb", document["lamp1/bulb"]!.CopyWith(parent: "lamp2/bulb"));

        var saved = Instances.Fold(moved, Source);

        Assert.False(saved.Contains("lamp1/bulb"));
        var back = Opened(saved, Source);
        Assert.Equal("lamp1", back["lamp1/bulb"]!.Parent);
    }
}

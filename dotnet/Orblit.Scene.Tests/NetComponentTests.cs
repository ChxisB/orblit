using static Orblit.Scene.Tests.Tree;

namespace Orblit.Scene.Tests;

public class NetComponentTests
{
    private const string CrateAsset = "props/crate.oprefab";

    // reading a tag

    [Fact]
    public void IsHeldByTheServerAndSyncsTheTransformWhenTheFileSaysNothing()
    {
        var net = NetComponent.FromJson(Map());

        Assert.Equal(NetAuthority.Server, net.Authority);
        Assert.Equal(new[] { "transform" }, net.Sync);
    }

    [Fact]
    public void IsTheTagItWasWrittenAs()
    {
        var net = new NetComponent(NetAuthority.Owner, ["transform", "health"]);
        var read = NetComponent.FromJson(net.ToJson());

        Same(net.ToJson(), read.ToJson());
        Assert.Equal(NetAuthority.Owner, read.Authority);
        Assert.Equal(new[] { "transform", "health" }, read.Sync);
    }

    [Fact]
    public void WritesTheAuthorityAsAWord() =>
        Same(
            Map(("authority", "owner"), ("sync", List("transform"))),
            new NetComponent(NetAuthority.Owner).ToJson());

    [Fact]
    public void KeepsAnEmptySyncListWhichSharesTheEntityAndNoState()
    {
        var read = NetComponent.FromJson(Map(("sync", List())));

        Assert.Empty(read.Sync);
    }

    [Fact]
    public void ReadsAnAuthorityItDoesNotKnowAsTheServersSoATypoNeverHandsAClientTheEntity()
    {
        Assert.Equal(NetAuthority.Server, NetComponent.FromJson(Map(("authority", "client"))).Authority);
        Assert.Equal(NetAuthority.Server, NetComponent.FromJson(Map(("authority", 3L))).Authority);
    }

    [Fact]
    public void DropsAnEmptyAndARepeatedSyncNameWhenReadingSoTheSceneStillOpens()
    {
        var read = NetComponent.FromJson(Map(("sync", List("transform", "", "health", "transform", 7L))));

        Assert.Equal(new[] { "transform", "health" }, read.Sync);
    }

    [Fact]
    public void UsesTheDefaultSyncWhenTheFileGivesNoList() =>
        Assert.Equal(new[] { "transform" }, NetComponent.FromJson(Map(("sync", "transform"))).Sync);

    // making a tag

    [Fact]
    public void RefusesAnEmptySyncName()
    {
        var error = Assert.Throws<ArgumentException>(() => new NetComponent(sync: ["transform", " "]));

        Assert.Contains("empty", error.Message);
    }

    [Fact]
    public void RefusesARepeatedSyncName()
    {
        var error = Assert.Throws<ArgumentException>(() => new NetComponent(sync: ["health", "transform", "health"]));

        Assert.Contains("\"health\"", error.Message);
    }

    [Fact]
    public void RefusesAnAuthorityThatIsNotOneOfTheTwo()
    {
        var error = Assert.Throws<ArgumentException>(() => new NetComponent((NetAuthority)7));

        Assert.Contains("authority", error.Message);
    }

    [Fact]
    public void IsNotChangedByTheListItWasMadeFrom()
    {
        var names = new List<string> { "transform" };
        var net = new NetComponent(sync: names);

        names.Add("health");

        Assert.Equal(new[] { "transform" }, net.Sync);
    }

    // where it sits in a scene

    [Fact]
    public void IsReadBackAsATagNotAnUnknownComponent()
    {
        var read = SceneComponents.Read(SceneComponents.Net, Map(("authority", "owner")));

        Assert.Equal("net", read.Type);
        Assert.Equal(NetAuthority.Owner, Assert.IsType<NetComponent>(read).Authority);
    }

    [Fact]
    public void IsWrittenAfterTheSoundAndBeforeWhatTheEntityCarries()
    {
        var entity = new SceneEntity("crate", "Crate", components: new OrderedDictionary<string, SceneComponent>
        {
            [SceneComponents.Data] = new DataComponent(["rules.json"]),
            [SceneComponents.Net] = new NetComponent(),
            [SceneComponents.Transform] = new TransformComponent(new Vector3(0, 0, 0)),
        });

        var written = SceneDocument.Decode(new SceneDocument(entities: [entity]).Encode()).Document["crate"]!;

        Assert.Equal(new[] { "transform", "net", "data" }, written.Components.Keys);
    }

    [Fact]
    public void NeedsNoNewFormatVersionSoAnOlderEditorStillOpensTheScene()
    {
        var scene = new SceneDocument(entities: [Tagged("crate")]);

        Assert.Equal((long)SceneDocument.PlainVersion, scene.ToJson()["formatVersion"]);
    }

    [Fact]
    public void ARoundTripThroughAFileKeepsTheTag()
    {
        var scene = new SceneDocument(entities: [Tagged("crate", NetAuthority.Owner)]);
        var load = SceneDocument.Decode(scene.Encode());

        Assert.Empty(load.Problems);
        Assert.Equal(NetAuthority.Owner, Net(load.Document, "crate").Authority);
    }

    // prefabs

    [Fact]
    public void EveryInstanceOfAPrefabWhoseRootIsTaggedIsTagged()
    {
        var prefab = new PrefabDocument("Crate", "crate", new SceneDocument("Crate", entities: [Tagged("crate")]));
        PrefabSource source = asset => asset == CrateAsset ? prefab : null;
        var scene = new SceneDocument(entities: [Instance("one"), Instance("two")]);

        var load = Instances.Expand(scene, source);

        Assert.Empty(load.Problems);
        Assert.Equal(NetAuthority.Server, Net(load.Document, "one").Authority);
        Assert.Equal(NetAuthority.Server, Net(load.Document, "two").Authority);
    }

    [Fact]
    public void ATagOnThePrefabsRootStaysOutOfTheSceneThatLinksIt()
    {
        var prefab = new PrefabDocument("Crate", "crate", new SceneDocument("Crate", entities: [Tagged("crate")]));
        PrefabSource source = asset => asset == CrateAsset ? prefab : null;
        var scene = new SceneDocument(entities: [Instance("one")]);

        var folded = Instances.Fold(Instances.Expand(scene, source).Document, source);

        Assert.Equal(scene.Encode(), folded.Encode());
    }

    private static NetComponent Net(SceneDocument document, string id) =>
        Assert.IsType<NetComponent>(document[id]![SceneComponents.Net]);

    private static SceneEntity Tagged(string id, NetAuthority authority = NetAuthority.Server) =>
        new(id, id, components: new OrderedDictionary<string, SceneComponent>
        {
            [SceneComponents.Net] = new NetComponent(authority),
        });

    private static SceneEntity Instance(string id) =>
        new(id, id, components: new OrderedDictionary<string, SceneComponent>
        {
            [SceneComponents.Prefab] = new PrefabComponent(asset: CrateAsset),
        });
}

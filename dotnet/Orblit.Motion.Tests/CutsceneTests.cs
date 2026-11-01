using Orblit.Json;
using Orblit.Scene;
using Orblit.Sequence;

namespace Orblit.Motion.Tests;

public class CutsceneTests
{
    /// <summary>The cube slides along x for four seconds, seen first from the front and then from the side, with a second of the two blended between.</summary>
    internal static CutsceneDocument Slide() => new(
        new ClipDocument(
            "Cube slides",
            4,
            channels:
            [
                new ClipChannel<Vector3>(
                    "cube",
                    "transform.position",
                    ChannelKind.Vector,
                    [new Key<Vector3>(0, Vector3.Zero()), new Key<Vector3>(4, new Vector3(4, 0, 0))]),
                new ClipChannel<double>(
                    "street1/lamp3/bulb",
                    "light.power",
                    ChannelKind.Number,
                    [new Key<double>(0, 10), new Key<double>(2, 80)]),
            ],
            marks: [new Mark(3, "door opens", Tree.Map(("door", "front")))]),
        shots: [new CutsceneShot("side", 2, 2), new CutsceneShot("front", 0, 3)],
        sounds: [new CutsceneSound("sounds/whoosh.ogg", 1, 2)]);

    private static Dictionary<string, double> Weights(CutsceneDocument cutscene, double at) =>
        cutscene.ShotsAt(at).ToDictionary(static shot => shot.Camera, static shot => shot.Weight);

    private static string File(params (string Key, object? Value)[] body) =>
        JsonText.Encode(Tree.With(
            Tree.Map(("kind", CutsceneDocument.Marker), ("formatVersion", CutsceneDocument.FormatVersion)),
            body));

    private static Func<string, bool> Has(string part) => line => line.Contains(part, StringComparison.Ordinal);

    private static OrderedDictionary<string, object?> Shot(string camera, long start, long duration) =>
        Tree.Map(("camera", camera), ("start", start), ("duration", duration));

    [Fact]
    public void ReadsBackAsTheCutsceneItWasWrittenFrom()
    {
        var written = Slide();
        var load = CutsceneDocument.Decode(written.Encode());
        Assert.Empty(load.Problems);
        Tree.Same(written.ToJson(), load.Cutscene.ToJson());
        Assert.Equal("Cube slides", load.Cutscene.Name);
        Assert.Equal(["front", "side"], load.Cutscene.Shots.Select(static shot => shot.Camera));
        Assert.Equal("sounds/whoosh.ogg", Assert.Single(load.Cutscene.Sounds).Sound);
        Tree.Same(Tree.Map(("door", "front")), Assert.Single(load.Cutscene.Motion.Marks).Payload);
    }

    [Fact]
    public void WritesOneKeyMarkShotAndSoundToALine()
    {
        var lines = Slide().Encode().Split('\n');
        Assert.Equal(2, lines.Count(Has("\"camera\"")));
        Assert.Contains("\"duration\":3.0", lines.Single(Has("\"camera\":\"front\"")), StringComparison.Ordinal);
        Assert.Contains("\"start\":1.0", lines.Single(Has("\"sound\"")), StringComparison.Ordinal);
        Assert.Contains("\"at\":3.0", lines.Single(Has("\"door opens\"")), StringComparison.Ordinal);
        Assert.Equal(5, lines.Count(Has("\"at\"")));
    }

    [Fact]
    public void CarriesItsOwnKindAndExtension()
    {
        Assert.Equal("orblit.cutscene", Slide().ToJson()["kind"]);
        Assert.Equal(".ocutscene", CutsceneDocument.Extension);
        Assert.False(Slide().ToJson().ContainsKey("rootMotion"));
    }

    [Fact]
    public void DropsAShotItCannotReadAndSaysSo()
    {
        var load = CutsceneDocument.Decode(File((
            "shots",
            Tree.List(Shot("front", 0, 2), Shot(string.Empty, 0, 2), Shot("side", 1, 0)))));
        Assert.Equal("front", Assert.Single(load.Cutscene.Shots).Camera);
        Assert.Equal(2, load.Problems.Count);
    }

    [Fact]
    public void DropsKeysOnBonesAndRootMotionAndSaysSo()
    {
        var load = CutsceneDocument.Decode(File(
            ("rootMotion", Tree.Map(("bone", "hips"))),
            (
                "channels",
                Tree.List(
                    Tree.Map(
                        ("target", "hero"),
                        ("bone", "hips"),
                        ("property", "rotation"),
                        ("kind", "rotation"),
                        ("keys", Tree.List(Tree.Map(("at", 0L), ("value", Tree.List(0L, 0L, 0L, 1L)))))),
                    Tree.Map(
                        ("target", "cube"),
                        ("property", "mesh.visible"),
                        ("kind", "flag"),
                        ("keys", Tree.List(Tree.Map(("at", 0L), ("value", true)))))))));
        var motion = load.Cutscene.Motion;
        Assert.Equal("cube", Assert.Single(motion.Channels).Target);
        Assert.Null(motion.RootMotion);
        Assert.Contains("bones", Assert.Single(load.Problems), StringComparison.Ordinal);
    }

    [Fact]
    public void CallsACutsceneWithNoNameACutscene() =>
        Assert.Equal("Cutscene", CutsceneDocument.Decode(File()).Cutscene.Name);

    [Fact]
    public void TakesTheLastShotOrSoundAsTheLengthWhenNoneIsGiven()
    {
        var load = CutsceneDocument.Decode(File(
            ("shots", Tree.List(Shot("front", 0, 2))),
            ("sounds", Tree.List(Tree.Map(("sound", "a.ogg"), ("start", 1L), ("duration", 4L))))));
        Assert.Equal(5, load.Cutscene.Duration);
    }

    [Fact]
    public void KeepsAStatedLengthShorterThanItsShots()
    {
        var load = CutsceneDocument.Decode(File(("duration", 1L), ("shots", Tree.List(Shot("front", 0, 2)))));
        Assert.Equal(1, load.Cutscene.Duration);
    }

    [Fact]
    public void RefusesAFileThatIsNotACutscene()
    {
        Assert.Throws<CutsceneFormatException>(() => CutsceneDocument.Decode(Slide().Motion.Encode()));
        Assert.Throws<CutsceneFormatException>(() => CutsceneDocument.Decode("not json"));
    }

    [Fact]
    public void RefusesACutsceneFromANewerOrblit()
    {
        var text = JsonText.Encode(Tree.Map(
            ("kind", CutsceneDocument.Marker),
            ("formatVersion", CutsceneDocument.FormatVersion + 1)));
        var error = Assert.Throws<CutsceneFormatException>(() => CutsceneDocument.Decode(text));
        Assert.Contains("newer", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RefusesKeysOnABone()
    {
        var wave = new ClipDocument(
            "Wave",
            1,
            channels: [new ClipChannel<double>("hero", "scale", ChannelKind.Number, [new Key<double>(0, 1)], bone: "hand")]);
        Assert.ThrowsAny<ArgumentException>(() => new CutsceneDocument(wave));
    }

    [Fact]
    public void RefusesAShotWithNoCameraNoLengthOrAStartBeforeTheStart()
    {
        Assert.ThrowsAny<ArgumentException>(() => new CutsceneShot(string.Empty, 0, 1));
        Assert.ThrowsAny<ArgumentException>(() => new CutsceneShot("front", 0, 0));
        Assert.ThrowsAny<ArgumentException>(() => new CutsceneShot("front", -1, 1));
        Assert.ThrowsAny<ArgumentException>(() => new CutsceneSound("a.ogg", 0, 0));
    }

    [Fact]
    public void KeepsTheShotsInTheOrderTheyStart() =>
        Assert.Equal([0.0, 2.0], Slide().Shots.Select(static shot => shot.Start));

    [Fact]
    public void LooksThroughOneCameraWhereOnlyOneRuns()
    {
        Assert.Equal(new Dictionary<string, double> { ["front"] = 1 }, Weights(Slide(), 1));
        Assert.Equal(new Dictionary<string, double> { ["side"] = 1 }, Weights(Slide(), 3.5));
    }

    [Fact]
    public void BlendsOverTheTimeTwoOverlapAddingUpToOne()
    {
        var cutscene = Slide();
        foreach (var at in new[] { 2.1, 2.25, 2.5, 2.75, 2.9 })
        {
            var both = Weights(cutscene, at);
            Assert.Equal(["front", "side"], both.Keys.Order());
            Assert.Equal(1, both.Values.Sum(), 1e-9);
        }

        Assert.True(Weights(cutscene, 2.25)["side"] < 0.5);
        Assert.Equal(0.5, Weights(cutscene, 2.5)["side"], 1e-9);
        Assert.True(Weights(cutscene, 2.75)["side"] > 0.5);
    }

    [Fact]
    public void CutsFromOneCameraToTheNextWhenShotsTouch()
    {
        var cutscene = new CutsceneDocument(
            new ClipDocument("Cut", 2),
            shots: [new CutsceneShot("front", 0, 1), new CutsceneShot("side", 1, 1)]);
        Assert.Equal(new Dictionary<string, double> { ["front"] = 1 }, Weights(cutscene, 0.999));
        Assert.Equal(new Dictionary<string, double> { ["side"] = 1 }, Weights(cutscene, 1));
    }

    [Fact]
    public void LooksThroughNothingBeforeTheFirstInAGapOrAtTheEnd()
    {
        var cutscene = new CutsceneDocument(
            new ClipDocument("Gaps", 4),
            shots: [new CutsceneShot("front", 1, 1), new CutsceneShot("side", 3, 1)]);
        Assert.Empty(cutscene.ShotsAt(0.5));
        Assert.Empty(cutscene.ShotsAt(2.5));
        Assert.Empty(cutscene.ShotsAt(4));
    }

    [Fact]
    public void HoldsOnItsLastFrameByDefaultFiringItsMarksOnce()
    {
        var director = new Director(Slide().Sequence);
        director.Play();
        var marks = new List<string>();
        for (var i = 0; i < 50; i++)
        {
            marks.AddRange(director.Advance(0.1).Marks.Select(static mark => mark.Name));
        }

        Assert.Equal(["door opens"], marks);
        Assert.True(director.Finished);
        Assert.Equal(4, director.At);
    }

    [Fact]
    public void PlaysItsSoundsFromWhereTheCutsceneHasGot()
    {
        var frame = Slide().Sequence.SampleAt(1.5);
        var sound = Assert.Single(frame.Sounds);
        Assert.Equal("sounds/whoosh.ogg", sound.Sound);
        Assert.Equal(0.5, sound.At, 1e-9);
    }

    [Fact]
    public void KeepsItsLastKeysValuesAtTheEnd()
    {
        var frame = Slide().Motion.SampleAt(4);
        Assert.Equal(new Vector3(4, 0, 0), frame.Values["cube"]["transform.position"]);
    }

    private static SceneDocument Scene() => new(
        entities:
        [
            Entity("cube"),
            Entity("street1"),
            Entity("street1/lamp3", "street1"),
            new SceneEntity(
                "street1/lamp3/bulb",
                "bulb",
                parent: "street1/lamp3",
                components: new OrderedDictionary<string, SceneComponent>
                {
                    [SceneComponents.Transform] = new TransformComponent(),
                    [SceneComponents.Light] = new LightComponent(),
                }),
        ]);

    private static SceneEntity Entity(string id, string? parent = null) => new(
        id,
        id,
        parent: parent,
        components: new OrderedDictionary<string, SceneComponent> { [SceneComponents.Transform] = new TransformComponent() });

    [Fact]
    public void NamesThingsByTheScenesOwnIds()
    {
        Assert.Equal("cube", ClipScope.WholeScene.Resolve("cube"));
        Assert.Equal("street1/lamp3/bulb", ClipScope.WholeScene.Resolve("street1/lamp3/bulb"));
        Assert.Equal("street1/lamp3", ClipScope.WholeScene.TargetOf("street1/lamp3"));
    }

    [Fact]
    public void MovesTheCubeAndAPartOfAPlacedPrefab()
    {
        var ops = ClipScene.OpsFor(Slide().Motion.SampleAt(2), Scene(), ClipScope.WholeScene);
        Assert.Equal(
            [("cube", "transform", "position"), ("street1/lamp3/bulb", "light", "power")],
            ops.Select(static op => (op.Id, op.Type, op.Field)).Order());
    }
}

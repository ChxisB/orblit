using Orblit.Audio;
using Orblit.Motion;
using Orblit.Scene;
using Orblit.Sequence;
using Orblit.VectorMath;

namespace Orblit.Stage.Tests;

public class SoundViewTests
{
    private static SceneEntity Thing(string id, Vector3 at, bool visible = true, params (string Type, SceneComponent Component)[] parts) =>
        Documents.Thing(
            id,
            visible: visible,
            components: Documents.Parts([(SceneComponents.Transform, new TransformComponent(position: at)), .. parts]));

    private static SceneEntity Fire(Vector3 at, SoundSourceComponent? source = null, bool visible = true) =>
        Thing(
            "fire",
            at,
            visible,
            (SceneComponents.SoundSource, source ?? new SoundSourceComponent(clip: "fire.ogg", bus: "sfx")));

    private static SceneEntity Ear(Vector3 at) =>
        Thing("ear", at, true, (SceneComponents.SoundListener, new SoundListenerComponent()));

    /// <summary>A camera at (0, 0, 10) looking down -Z, with <paramref name="others"/> in the scene.</summary>
    private static SceneDocument World(params SceneEntity[] others) => Documents.Of(
        [
            Thing("game", new Vector3(0, 0, 10), true, (SceneComponents.Camera, new CameraComponent())),
            .. others,
        ]);

    private static CutsceneDocument Swell() => new(
        new ClipDocument("Swell", 1, channels: []),
        sounds: [new CutsceneSound("swell.ogg", 0, 1)]);

    private sealed class Room
    {
        public Room(params SceneEntity[] others)
        {
            Backend = new RecordedAudio();
            Audio = new AudioPlayer(Backend);
            Audio.Mixer.Add("sfx");
            View = new DocumentView(World(others));
            Sound = new SoundView(View, Audio);
        }

        public RecordedAudio Backend { get; }

        public AudioPlayer Audio { get; }

        public DocumentView View { get; }

        public SoundView Sound { get; }

        public void Frame(double seconds = 1.0 / 60, CutsceneStep? step = null)
        {
            Sound.Advance(seconds, step);
            Audio.Advance(seconds);
        }

        public List<AudioStarted> Starts() => [.. Backend.Calls.OfType<AudioStarted>()];

        public Level LastLevel() => Backend.Calls[^1] is AudioUpdated updated ? updated.Level : Starts()[^1].Level;
    }

    [Fact]
    public void ASourcePlaysFromItsEntityWhileItIsShown()
    {
        var room = new Room(Fire(new Vector3(10, 0, 10)));
        room.Frame();
        var start = Assert.Single(room.Starts());
        Assert.Equal("fire.ogg", start.Clip);
        Assert.True(start.Loop);
        Assert.Equal(1.0, start.Level.Pan, 1e-9);
        Assert.InRange(start.Level.Gain, double.Epsilon, 1 - double.Epsilon);

        room.View.Replace(World(Fire(new Vector3(10, 0, 10), visible: false)));
        room.Frame(0.1);
        Assert.IsType<AudioStopped>(room.Backend.Calls[^1]);

        room.View.Replace(World(Fire(new Vector3(10, 0, 10))));
        room.Frame();
        Assert.Equal(2, room.Starts().Count);
    }

    [Fact]
    public void ASourceIsSilentWithNoClip()
    {
        var room = new Room(Fire(Vector3.Zero(), new SoundSourceComponent()));
        room.Frame();

        Assert.Empty(room.Backend.Calls);
    }

    [Fact]
    public void ASourceFollowsAChangeOfVolumeAndStartsOverForANewClip()
    {
        var room = new Room(Fire(new Vector3(0, 0, 5)));
        room.Frame();
        var full = Assert.Single(room.Starts()).Level.Gain;

        room.View.Replace(World(Fire(new Vector3(0, 0, 5), new SoundSourceComponent(clip: "fire.ogg", bus: "sfx", volume: 0.5))));
        room.Frame();
        Assert.Single(room.Starts());
        Assert.Equal(full / 2, room.LastLevel().Gain, 1e-9);

        room.View.Replace(World(Fire(new Vector3(0, 0, 5), new SoundSourceComponent(clip: "blaze.ogg", bus: "sfx"))));
        room.Frame(0.1);
        Assert.Equal(["fire.ogg", "blaze.ogg"], room.Starts().Select(static start => start.Clip));
        Assert.Single(room.Backend.Calls.OfType<AudioStopped>());
    }

    [Fact]
    public void ASourceThatDoesNotLoopPlaysOnceAndAgainWhenAsked()
    {
        var room = new Room(Fire(new Vector3(0, 0, 5), new SoundSourceComponent(clip: "bang.ogg", bus: "sfx", loop: false)));
        room.Frame();
        room.Frame();
        var start = Assert.Single(room.Starts());
        Assert.False(start.Loop);

        room.Backend.Finish(start.Voice);
        room.Sound.Play("fire");
        room.Frame();
        Assert.Equal(2, room.Starts().Count);
    }

    [Fact]
    public void ASourceOnABusTheMixerLacksIsRefusedByName()
    {
        var room = new Room(Fire(Vector3.Zero(), new SoundSourceComponent(clip: "fire.ogg", bus: "nowhere")));

        var refused = Assert.Throws<ArgumentException>(() => room.Frame());

        Assert.Contains("\"fire\"", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MovingSlowlyIsHeardAsASpeedAndACutIsNot()
    {
        var room = new Room(Fire(new Vector3(10, 0, 10)));
        room.Frame();

        room.View.Replace(World(Fire(new Vector3(9, 0, 10))));
        room.Frame(1);
        Assert.True(room.LastLevel().Pitch > 1);

        room.View.Replace(World(Fire(new Vector3(109, 0, 10))));
        room.Frame(1.0 / 60);
        Assert.Equal(1, room.LastLevel().Pitch);
    }

    [Fact]
    public void ASourceThatCannotStartIsRefusedEveryFrameUntilItCan()
    {
        var room = new Room(Fire(Vector3.Zero(), new SoundSourceComponent(clip: "fire.ogg", bus: "nowhere")));

        Assert.Throws<ArgumentException>(() => room.Frame());
        Assert.Throws<ArgumentException>(() => room.Frame());
    }

    [Fact]
    public void ASourcePlayedBeforeAnyFrameIsHeardFromWhereTheListenerIs()
    {
        var played = new Room(Fire(new Vector3(0, 0, 4)));
        played.Sound.Play("fire");

        var framed = new Room(Fire(new Vector3(0, 0, 4)));
        framed.Frame();

        Assert.Equal(framed.LastLevel().Gain, played.LastLevel().Gain);
    }

    [Fact]
    public void ASourceIsSilencedByStop()
    {
        var room = new Room(Fire(new Vector3(0, 0, 5)));
        room.Frame();
        room.Sound.Stop();
        room.Audio.Advance(0.1);

        Assert.IsType<AudioStopped>(room.Backend.Calls[^1]);
    }

    [Fact]
    public void ASceneThatChangesAfterStopStartsItsSourcesAgain()
    {
        var room = new Room(Fire(new Vector3(0, 0, 5)));
        room.Frame();
        room.Sound.Stop();
        room.Audio.Advance(0.1);

        room.View.Replace(World(Fire(new Vector3(0, 0, 6))));
        room.Frame();

        Assert.Equal(2, room.Starts().Count);
    }

    [Fact]
    public void TheListenerIsTheCameraWhenNoEntitySaysOtherwise()
    {
        var room = new Room(Fire(new Vector3(10, 0, 10)));
        room.Frame();

        Assert.Equal(1.0, Assert.Single(room.Starts()).Level.Pan, 1e-9);
    }

    [Fact]
    public void MovingTowardsASoundIsHeardAsASpeed()
    {
        static SceneDocument Approach(double z) => World(Fire(new Vector3(0, 0, -20)), Ear(new Vector3(0, 0, z)));
        var room = new Room();
        room.View.Replace(Approach(10));
        room.Frame();

        room.View.Replace(Approach(9));
        room.Frame(1);

        Assert.True(room.LastLevel().Pitch > 1);
    }

    [Fact]
    public void TheListenerIsTheFirstEntityThatHasOneFacingDownItsOwnMinusZ()
    {
        var room = new Room(Fire(new Vector3(10, 0, 10)), Ear(new Vector3(10, 0, 20)));
        room.Frame();

        Assert.Equal(0.0, Assert.Single(room.Starts()).Level.Pan, 1e-9);
    }

    [Fact]
    public void ACutscenePlaysItsSoundAndLetsGoAtTheEnd()
    {
        var room = new Room();
        var cutscenes = new Cutscenes(room.View, [Swell()]);
        cutscenes.Start("Swell");
        room.Frame(0.25, cutscenes.Advance(0.25));
        var start = Assert.Single(room.Starts());
        Assert.Equal("swell.ogg", start.Clip);
        Assert.Equal(0.25, start.Offset, 1e-9);

        room.Frame(0.25, cutscenes.Advance(0.25));
        Assert.Single(room.Starts());

        room.Frame(1, cutscenes.Advance(1));
        Assert.IsType<AudioStopped>(room.Backend.Calls[^1]);
    }

    [Fact]
    public void ACutsceneHearsABoundCueFromItsEntityAndAnUnboundOneFromNowhere()
    {
        var room = new Room(Thing("cube", new Vector3(4, 0, 0)));
        room.Frame(
            1.0 / 60,
            new CutsceneStep(sounds: [new SoundAt("thud.ogg", 0, 1, binding: "cube"), new SoundAt("thud.ogg", 0, 1, binding: "nobody")]));

        var starts = room.Starts();
        Assert.Equal(2, starts.Count);
        Assert.True(starts[0].Level.Pan > 0.3);
        Assert.Equal(0, starts[1].Level.Pan);
    }

    [Fact]
    public void ASurfaceNamesWhatAnEntityIsMadeOf()
    {
        var room = new Room(
            Thing("crate", Vector3.Zero(), true, (SceneComponents.SoundSurface, new SoundSurfaceComponent("wood"))),
            Thing("rock", Vector3.Zero(), true, (SceneComponents.SoundSurface, new SoundSurfaceComponent())));

        Assert.Equal("wood", room.Sound.SurfaceOf("crate"));
        Assert.Null(room.Sound.SurfaceOf("rock"));
        Assert.Null(room.Sound.SurfaceOf("game"));
        Assert.Null(room.Sound.SurfaceOf("nobody"));
    }
}

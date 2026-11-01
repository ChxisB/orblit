using Orblit.VectorMath;

namespace Orblit.Audio.Tests;

public class AudioTests
{
    private readonly RecordedAudio _backend = new();
    private readonly AudioPlayer _audio;

    public AudioTests() => _audio = new AudioPlayer(_backend);

    [Fact]
    public void AVoiceStartsAtTheGainOfItsVolumeAndEveryBusAboveIt()
    {
        var music = _audio.Mixer.Add("music");
        music.Volume = 0.5;
        _audio.Mixer.Master.Volume = 0.8;

        var voice = _audio.Play("theme", bus: music, volume: 0.5, loop: true);

        Assert.Single(_backend.Calls);
        var started = Assert.IsType<AudioStarted>(_backend.Calls.Single());
        Assert.Equal(voice.Id, started.Voice);
        Assert.Equal("theme", started.Clip);
        Assert.True(started.Loop);
        Assert.Equal(0.2, started.Level.Gain, 1e-9);
    }

    [Fact]
    public void EachVoiceHasItsOwnId()
    {
        var a = _audio.Play("a");
        var b = _audio.Play("b");

        Assert.NotEqual(b.Id, a.Id);
    }

    [Fact]
    public void ABusChangeReachesTheBackendOnTheNextAdvanceOnce()
    {
        var sfx = _audio.Mixer.Add("sfx");
        var voice = _audio.Play("hit", bus: sfx, loop: true);
        _backend.Calls.Clear();

        sfx.Volume = 0.25;
        Assert.Empty(_backend.Calls);
        _audio.Advance(0);
        _audio.Advance(0);

        Assert.Single(_backend.Calls);
        var update = Assert.IsType<AudioUpdated>(_backend.Calls.Single());
        Assert.Equal(voice.Id, update.Voice);
        Assert.Equal(0.25, update.Level.Gain);
    }

    [Fact]
    public void MutingABusSilencesItsVoicesAndUnmutingBringsThemBack()
    {
        var sfx = _audio.Mixer.Add("sfx");
        var voice = _audio.Play("hit", bus: sfx, loop: true);

        sfx.Muted = true;
        _audio.Advance(0);
        Assert.Equal(0, _backend.LevelOf(voice.Id)!.Gain);

        sfx.Muted = false;
        _audio.Advance(0);
        Assert.Equal(1, _backend.LevelOf(voice.Id)!.Gain);
    }

    // fades

    [Fact]
    public void AFadeMovesTheGainInAStraightLine()
    {
        var voice = _audio.Play("hum", loop: true);

        voice.FadeTo(0.2, seconds: 4);
        _audio.Advance(1);
        Assert.Equal(0.8, _backend.LevelOf(voice.Id)!.Gain, 1e-9);
        _audio.Advance(2);
        Assert.Equal(0.4, _backend.LevelOf(voice.Id)!.Gain, 1e-9);
        _audio.Advance(1);

        Assert.Equal(0.2, _backend.LevelOf(voice.Id)!.Gain);
        Assert.Equal(0.2, voice.Volume);
        Assert.True(voice.IsPlaying);
    }

    [Fact]
    public void AFadePastItsEndHoldsAtTheTarget()
    {
        var voice = _audio.Play("hum", loop: true);

        voice.FadeTo(0.5, seconds: 1);
        _audio.Advance(10);

        Assert.Equal(0.5, voice.Volume);
    }

    [Fact]
    public void AFadeOfNoTimeIsAJump()
    {
        var voice = _audio.Play("hum", loop: true);

        voice.FadeTo(0.5, seconds: 0);

        Assert.Equal(0.5, voice.Volume);
    }

    [Fact]
    public void ASecondFadeStartsFromWhereTheFirstHadGotTo()
    {
        var voice = _audio.Play("hum", loop: true);
        voice.FadeTo(0, seconds: 2);
        _audio.Advance(1);

        voice.FadeTo(1, seconds: 1);
        _audio.Advance(0.5);

        Assert.Equal(0.75, voice.Volume, 1e-9);
    }

    [Fact]
    public void SettingTheVolumeCancelsAFade()
    {
        var voice = _audio.Play("hum", loop: true);
        voice.FadeTo(0, seconds: 2);

        voice.Volume = 0.6;
        _audio.Advance(5);

        Assert.Equal(0.6, voice.Volume);
    }

    [Fact]
    public void AFadeToANegativeVolumeIsRefused()
    {
        var voice = _audio.Play("hum");

        Assert.ThrowsAny<ArgumentException>(() => voice.FadeTo(-1, seconds: 1));
    }

    // stop

    [Fact]
    public void StopsTheBackendAtOnceAndLeavesTheListOfVoices()
    {
        var voice = _audio.Play("hum", loop: true);

        voice.Stop();

        Assert.IsType<AudioStopped>(_backend.Calls.Last());
        Assert.False(voice.IsPlaying);
        Assert.Empty(_audio.Voices);
    }

    [Fact]
    public void WithAFadeItFallsToSilenceThenStopsOnce()
    {
        var voice = _audio.Play("hum", loop: true);

        voice.Stop(fadeSeconds: 2);
        _audio.Advance(1);
        Assert.True(voice.IsPlaying);
        Assert.Equal(0.5, _backend.LevelOf(voice.Id)!.Gain, 1e-9);
        _audio.Advance(1);
        _audio.Advance(1);

        Assert.False(voice.IsPlaying);
        Assert.Single(_backend.Calls.OfType<AudioStopped>());
        Assert.IsType<AudioStopped>(_backend.Calls.Last());
    }

    [Fact]
    public void AStoppedVoiceIgnoresALaterStopOrFade()
    {
        var voice = _audio.Play("hum", loop: true);
        voice.Stop();

        voice.Stop();
        voice.FadeTo(1, seconds: 1);
        _audio.Advance(1);

        Assert.Single(_backend.Calls.OfType<AudioStopped>());
        Assert.Empty(_backend.Calls.OfType<AudioUpdated>());
    }

    [Fact]
    public void AClipThatPlaysToItsEndDropsOutOfTheVoices()
    {
        var voice = _audio.Play("ping");
        Assert.Equal([voice], _audio.Voices);

        _backend.Finish(voice.Id);
        _audio.Advance(0.1);

        Assert.False(voice.IsPlaying);
        Assert.Empty(_audio.Voices);
        Assert.Empty(_backend.Calls.OfType<AudioStopped>());
    }

    [Fact]
    public void PitchChangesReachTheBackendAndMustBeAboveZero()
    {
        var voice = _audio.Play("engine", loop: true);

        voice.Pitch = 1.5;
        _audio.Advance(0);

        Assert.Equal(1.5, _backend.LevelOf(voice.Id)!.Pitch);
        Assert.ThrowsAny<ArgumentException>(() => { voice.Pitch = 0; });
        Assert.ThrowsAny<ArgumentException>(() => _audio.Play("x", pitch: -1));
    }

    // a positioned voice

    [Fact]
    public void IsHeardByDistanceFromTheListenerAndMovesWithIt()
    {
        var emitter = new Emitter(
            new Vector3(0, 0, -6),
            falloff: new Falloff(near: 1, far: 11, curve: FalloffCurve.Linear));
        var voice = _audio.Play("fire", at: emitter, loop: true);
        Assert.Equal(0.5, _backend.LevelOf(voice.Id)!.Gain, 1e-9);

        _audio.Listener.Teleport(new Vector3(0, 0, -6.5));
        _audio.Advance(0);
        Assert.Equal(1, _backend.LevelOf(voice.Id)!.Gain, 1e-9);

        _audio.Listener.Teleport(new Vector3(0, 0, 40));
        _audio.Advance(0);
        Assert.Equal(0, _backend.LevelOf(voice.Id)!.Gain);
    }

    [Fact]
    public void PansToTheSideItIsOn()
    {
        var voice = _audio.Play("fire", at: new Emitter(new Vector3(5, 0, 0)), loop: true);

        Assert.Equal(1, _backend.LevelOf(voice.Id)!.Pan, 1e-9);
    }

    [Fact]
    public void ShiftsInPitchAsTheEmitterDrivesTowardsTheListener()
    {
        var emitter = new Emitter(new Vector3(0, 0, -100), falloff: new Falloff(near: 1, far: 500));
        var voice = _audio.Play("car", at: emitter, loop: true);

        emitter.MoveTo(new Vector3(0, 0, -70), 1);
        _audio.Advance(1);

        Assert.Equal(343.0 / (343 - 30), _backend.LevelOf(voice.Id)!.Pitch, 1e-9);
    }

    [Fact]
    public void IsScaledByItsBusOnTopOfTheDistance()
    {
        var sfx = _audio.Mixer.Add("sfx");
        sfx.Volume = 0.5;
        var emitter = new Emitter(
            new Vector3(0, 0, -6),
            falloff: new Falloff(near: 1, far: 11, curve: FalloffCurve.Linear));

        var voice = _audio.Play("fire", at: emitter, bus: sfx, volume: 0.5);

        Assert.Equal(0.125, _backend.LevelOf(voice.Id)!.Gain, 1e-9);
    }

    [Fact]
    public void AVoiceWithNoEmitterIsTheSameWhereverTheListenerIs()
    {
        var voice = _audio.Play("theme", loop: true);

        _audio.Listener.Teleport(new Vector3(900, 0, 0));
        _audio.Advance(0);

        Assert.Equal(new Level(), _backend.LevelOf(voice.Id));
        Assert.Empty(_backend.Calls.OfType<AudioUpdated>());
    }

    [Fact]
    public void TheCallsArriveInTheOrderTheyWereMade()
    {
        var a = _audio.Play("a", loop: true);
        var b = _audio.Play("b", loop: true);
        a.Stop();
        b.Volume = 0.5;
        _audio.Advance(0);

        Assert.Equal<(Type, long)>(
            [
                (typeof(AudioStarted), a.Id),
                (typeof(AudioStarted), b.Id),
                (typeof(AudioStopped), a.Id),
                (typeof(AudioUpdated), b.Id),
            ],
            _backend.Calls.Select(call => (call.GetType(), call.Voice)));
    }

    [Fact]
    public void AFadeToTheEndOfAVoiceHoldsWhenTheVolumeIsSetMeanwhile()
    {
        var a = _audio.Play("a", loop: true);
        a.Stop(fadeSeconds: 2);
        var b = _audio.Play("b", loop: true);
        b.Stop(fadeSeconds: 2);
        a.Volume = 0.5;
        b.FadeTo(1, seconds: 10);

        _audio.Advance(1);
        Assert.True(a.IsPlaying && b.IsPlaying);
        _audio.Advance(1);

        Assert.False(a.IsPlaying);
        Assert.False(b.IsPlaying);
    }

    [Fact]
    public void AStopIsNotSentForAClipThatAlreadyPlayedToItsEnd()
    {
        var voice = _audio.Play("hit");
        _backend.Finish(voice.Id);

        voice.Stop();

        Assert.False(voice.IsPlaying);
        Assert.Empty(_backend.Calls.OfType<AudioStopped>());
    }

    [Fact]
    public void ALengthOfTimeIsZeroOrMoreAndSaidEvenForAnEndedVoice()
    {
        var voice = _audio.Play("hit");
        voice.Stop();

        Assert.ThrowsAny<ArgumentException>(() => voice.FadeTo(1, seconds: -1));
        Assert.ThrowsAny<ArgumentException>(() => voice.FadeTo(1, seconds: double.NaN));
        Assert.ThrowsAny<ArgumentException>(() => voice.FadeTo(-1, seconds: 1));
        Assert.ThrowsAny<ArgumentException>(() => voice.Stop(fadeSeconds: -1));
    }

    [Fact]
    public void AFadeOrAStopOnAnEndedVoiceChangesNothing()
    {
        var voice = _audio.Play("hit");
        voice.Volume = 0.3;
        voice.Stop();

        voice.FadeTo(1, seconds: 0);
        voice.Stop(fadeSeconds: 1);

        Assert.Equal(0.3, voice.Volume);
        Assert.Single(_backend.Calls.OfType<AudioStopped>());
    }

    [Fact]
    public void AFadeOfNoTimeReplacesAFadeUnderWay()
    {
        var voice = _audio.Play("a", loop: true);
        voice.FadeTo(0, seconds: 10);

        voice.FadeTo(0.5, seconds: 0);
        _audio.Advance(1);

        Assert.Equal(0.5, voice.Volume);
    }

    [Fact]
    public void ABackendThatCannotStartAClipLeavesNoVoiceBehind()
    {
        var refusing = new AudioPlayer(new Refusing());

        Assert.Throws<InvalidOperationException>(() => refusing.Play("missing"));

        Assert.Empty(refusing.Voices);
    }

    [Fact]
    public void ABusOfAnotherMixerIsRefused()
    {
        var foreign = new Mixer().Add("sfx");

        Assert.ThrowsAny<ArgumentException>(() => _audio.Play("hit", bus: foreign));
        Assert.Empty(_audio.Voices);
    }

    [Fact]
    public void AMovedEmitterIsHeardMovingForThatFrameOnly()
    {
        var emitter = new Emitter(new Vector3(0, 0, -10), doppler: 1);
        var a = _audio.Play("a", loop: true, at: emitter);
        var b = _audio.Play("b", loop: true, at: emitter);

        emitter.MoveTo(new Vector3(0, 0, -20), 1);
        _audio.Advance(1);
        Assert.True(_backend.LevelOf(a.Id)!.Pitch < 1);
        Assert.Equal(_backend.LevelOf(a.Id)!.Pitch, _backend.LevelOf(b.Id)!.Pitch);

        _audio.Advance(1);
        Assert.Equal(1, _backend.LevelOf(a.Id)!.Pitch);
        Assert.Equal(1, _backend.LevelOf(b.Id)!.Pitch);
    }

    [Fact]
    public void AMovedListenerIsHeardMovingForThatFrameOnly()
    {
        var voice = _audio.Play("a", loop: true, at: new Emitter(new Vector3(0, 0, -10)));

        _audio.Listener.MoveTo(new Vector3(0, 0, -5), 1);
        _audio.Advance(1);
        Assert.True(_backend.LevelOf(voice.Id)!.Pitch > 1);

        _audio.Advance(1);
        Assert.Equal(1, _backend.LevelOf(voice.Id)!.Pitch);
    }

    [Fact]
    public void AVoiceBeginsWhereItIsAskedToAndNotBeforeZero()
    {
        _audio.Play("theme", offset: 12.5);

        Assert.Equal(12.5, Assert.IsType<AudioStarted>(Assert.Single(_backend.Calls)).Offset);
        Assert.ThrowsAny<ArgumentException>(() => _audio.Play("theme", offset: -1));
    }

    [Fact]
    public void TimeCannotRunBackwards()
    {
        Assert.ThrowsAny<ArgumentException>(() => _audio.Advance(-1));
        Assert.ThrowsAny<ArgumentException>(() => _audio.Advance(double.NaN));
    }

    private sealed class Refusing : IAudioBackend
    {
        public void Start(long voice, string clip, bool loop, Level level, double offset = 0) =>
            throw new InvalidOperationException($"no such clip: {clip}");

        public void Update(long voice, Level level)
        {
        }

        public void Stop(long voice)
        {
        }

        public bool IsPlaying(long voice) => false;
    }
}

namespace Orblit.Audio.Tests;

public class MusicTests
{
    private readonly RecordedAudio _backend = new();
    private readonly AudioPlayer _audio;

    public MusicTests() => _audio = new AudioPlayer(_backend);

    // 120 bpm in 4/4: a beat is half a second and a bar is two.
    private Music Make(IReadOnlyList<MusicMove>? moves = null) =>
        new(
            _audio,
            new Dictionary<string, MusicTrack>
            {
                ["calm"] = new("calm.ogg"),
                ["fight"] = new("fight.ogg", bpm: 60, beatsPerBar: 3),
            },
            moves: moves);

    private List<string> Starts() => [.. _backend.Calls.OfType<AudioStarted>().Select(call => call.Clip)];

    [Fact]
    public void TheFirstTrackStartsAtOnceLooping()
    {
        var music = Make();
        music.Go("calm");

        var started = Assert.IsType<AudioStarted>(Assert.Single(_backend.Calls));
        Assert.Equal("calm.ogg", started.Clip);
        Assert.True(started.Loop);
        Assert.Equal("calm", music.Playing);
    }

    [Fact]
    public void AChangeOnTheNextBeatWaitsForTheBeatAndStartsOnIt()
    {
        var music = Make([new MusicMove(to: "fight", when: MusicWhen.NextBeat)]);
        music.Go("calm");
        music.Advance(0.2);

        music.Go("fight");
        music.Advance(0.2);
        Assert.Equal("calm", music.Playing);
        Assert.Equal("fight", music.WaitingFor);

        music.Advance(0.2);

        Assert.Equal("fight", music.Playing);
        Assert.Null(music.WaitingFor);
        Assert.Equal(["calm.ogg", "fight.ogg"], Starts());
    }

    [Fact]
    public void TheNewTrackStartsAsFarInAsTheStepWentPastTheBeat()
    {
        var music = Make([new MusicMove(to: "fight", when: MusicWhen.NextBeat)]);
        music.Go("calm");
        music.Advance(0.4);
        music.Go("fight");

        music.Advance(0.35);

        var started = _backend.Calls.OfType<AudioStarted>().Last();
        Assert.Equal(0.25, started.Offset, 1e-9);
    }

    [Fact]
    public void AChangeOnTheNextBarWaitsOutTheBar()
    {
        var music = Make([new MusicMove(to: "fight", when: MusicWhen.NextBar)]);
        music.Go("calm");
        music.Advance(0.1);
        music.Go("fight");

        music.Advance(1.8);
        Assert.Equal("calm", music.Playing);
        music.Advance(0.2);

        Assert.Equal("fight", music.Playing);
    }

    [Fact]
    public void ABeatLandedOnExactlyIsNotTheNextBeat()
    {
        var music = Make([new MusicMove(to: "fight", when: MusicWhen.NextBeat)]);
        music.Go("calm");
        music.Advance(0.5);
        music.Go("fight");

        music.Advance(0.4);
        Assert.Equal("calm", music.Playing);
        music.Advance(0.1);

        Assert.Equal("fight", music.Playing);
    }

    [Fact]
    public void StoppingFadesTheTrackOutDropsAWaitingChangeAndStartsAnew()
    {
        var music = Make([new MusicMove(to: "fight", when: MusicWhen.NextBar)]);
        music.Go("calm");
        music.Go("fight");

        music.Stop(fade: 1);
        _audio.Advance(0.5);
        Assert.Equal(0.5, _backend.LevelOf(0)!.Gain, 1e-9);
        _audio.Advance(0.5);
        Assert.Null(_backend.LevelOf(0));
        Assert.Null(music.Playing);
        Assert.Null(music.WaitingFor);

        music.Advance(5);
        Assert.Equal(["calm.ogg"], Starts());
        music.Go("fight");
        Assert.Equal(["calm.ogg", "fight.ogg"], Starts());
    }

    [Fact]
    public void AFadeBringsTheNewTrackInWhileTheOldOneGoesOut()
    {
        var music = Make([new MusicMove(to: "fight", fade: 1)]);
        music.Go("calm");

        music.Go("fight");
        _audio.Advance(0.5);

        Assert.Equal(0, _backend.Calls.OfType<AudioStarted>().Last().Level.Gain);
        Assert.Equal(0.5, _backend.LevelOf(0)!.Gain, 1e-9);
        Assert.Equal(0.5, _backend.LevelOf(1)!.Gain, 1e-9);
        _audio.Advance(0.5);
        Assert.Null(_backend.LevelOf(0));
        Assert.Equal(1, _backend.LevelOf(1)!.Gain);
    }

    [Fact]
    public void ARowFromOneTrackBeatsARowFromAnyAndNoRowIsACut()
    {
        var music = Make([new MusicMove(to: "fight", fade: 2), new MusicMove(from: "calm", to: "fight", fade: 1)]);
        music.Go("calm");

        music.Go("fight");
        _audio.Advance(1);

        Assert.Equal(1, _backend.LevelOf(1)!.Gain);
        Assert.Null(_backend.LevelOf(0));
    }

    [Fact]
    public void GoingBackToTheTrackThatPlaysCancelsAChangeThatWaits()
    {
        var music = Make([new MusicMove(to: "fight", when: MusicWhen.NextBeat)]);
        music.Go("calm");
        music.Go("fight");

        music.Go("calm");
        music.Advance(5);

        Assert.Equal("calm", music.Playing);
        Assert.Equal(["calm.ogg"], Starts());
    }

    [Fact]
    public void AStingerDucksTheTrackAndGivesItBackWhenItEnds()
    {
        var music = Make();
        music.Go("calm");

        music.Stinger("sting.ogg");
        _audio.Advance(0.1);
        Assert.Equal(0.3, _backend.LevelOf(0)!.Gain, 1e-9);

        _backend.Finish(1);
        _audio.Advance(0);
        music.Advance(0.1);
        _audio.Advance(0.5);

        Assert.Equal(1, _backend.LevelOf(0)!.Gain);
    }

    [Fact]
    public void ATrackThatStartsUnderAStingerStartsDucked()
    {
        var music = Make();
        music.Go("calm");
        music.Stinger("sting.ogg");

        music.Go("fight");

        Assert.Equal(0.3, _backend.Calls.OfType<AudioStarted>().Last().Level.Gain);
    }

    [Fact]
    public void ATrackTheTableDoesNotKnowIsAnError()
    {
        Assert.ThrowsAny<ArgumentException>(() => Make().Go("dance"));
    }

    [Fact]
    public void ATrackNeedsABeatThatExists()
    {
        Assert.ThrowsAny<ArgumentException>(() => new MusicTrack("a.ogg", bpm: 0));
        Assert.ThrowsAny<ArgumentException>(() => new MusicTrack("a.ogg", beatsPerBar: 0));
    }
}

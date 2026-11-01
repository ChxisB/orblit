using Orblit.VectorMath;

namespace Orblit.Audio.Tests;

public class CuesTests
{
    private readonly RecordedAudio _backend = new();
    private readonly AudioPlayer _audio;

    public CuesTests() => _audio = new AudioPlayer(_backend);

    private List<string> Log() =>
    [
        .. _backend.Calls.Select(call => call switch
        {
            AudioStarted started => $"start {started.Clip}",
            AudioStopped stopped => $"stop {stopped.Voice}",
            AudioUpdated => "update",
            _ => throw new InvalidDataException("unknown call"),
        }),
    ];

    [Fact]
    public void CuesStartInTheOrderThePlayheadReachesThem()
    {
        var board = new CueBoard(_audio);

        board.Update([new Cue("door", 0, 1)]);
        board.Update([new Cue("door", 0.1, 1), new Cue("voice", 0, 1)]);

        Assert.Equal(["start door", "start voice"], Log());
    }

    [Fact]
    public void ACueThatStaysFollowsItsGainAndStartsNothingAgain()
    {
        var board = new CueBoard(_audio);
        board.Update([new Cue("door", 0, 1)]);

        board.Update([new Cue("door", 0.1, 0.5)]);
        _audio.Advance(0);

        Assert.Equal(["start door", "update"], Log());
        Assert.Equal(0.5, _backend.LevelOf(0)!.Gain);
    }

    [Fact]
    public void AClipThatPlayedToItsEndIsNotStartedAgain()
    {
        var board = new CueBoard(_audio);
        board.Update([new Cue("door", 0, 1)]);
        _backend.Finish(0);
        _audio.Advance(0);

        board.Update([new Cue("door", 0.3, 1)]);

        Assert.Equal(["start door"], Log());
    }

    [Fact]
    public void ACueTheSequenceDropsFadesOutAndMayStartAgainLater()
    {
        var board = new CueBoard(_audio, release: 0.1);
        board.Update([new Cue("door", 0, 1)]);
        board.Update([]);
        _audio.Advance(0.2);

        board.Update([new Cue("door", 0, 1)]);

        Assert.Equal(["start door", "stop 0", "start door"], Log());
    }

    [Fact]
    public void ASeekIntoTheMiddleOfAOneShotStartsNothingThenOrLater()
    {
        var board = new CueBoard(_audio);

        board.Update([new Cue("door", 2, 1)], seeked: true);
        board.Update([new Cue("door", 2.1, 1)]);

        Assert.Empty(Log());
    }

    [Fact]
    public void ASeekIntoABedStartsItPartWayIn()
    {
        var board = new CueBoard(_audio, beds: new HashSet<string> { "theme" });

        board.Update([new Cue("theme", 12.5, 1)], seeked: true);

        var started = Assert.IsType<AudioStarted>(Assert.Single(_backend.Calls));
        Assert.Equal("theme", started.Clip);
        Assert.Equal(12.5, started.Offset);
    }

    [Fact]
    public void ASeekPutsAPlayingBedWhereThePlayheadNowIs()
    {
        var board = new CueBoard(_audio, beds: new HashSet<string> { "theme" });
        board.Update([new Cue("theme", 1, 1)]);

        board.Update([new Cue("theme", 30, 1)], seeked: true);
        _audio.Advance(0.1);

        Assert.Equal(["start theme", "start theme", "stop 0"], Log());
        Assert.Equal(30, ((AudioStarted)_backend.Calls[1]).Offset);
    }

    [Fact]
    public void ACueWithABindingIsHeardFromWhereItsEntityIs()
    {
        var place = new Emitter(new Vector3(4, 0, 0));
        var board = new CueBoard(_audio, emitterFor: binding => binding == "bell" ? place : null);

        board.Update([new Cue("ring", 0, 1, binding: "bell")]);

        Assert.Same(place, _audio.Voices.Single().Emitter);
    }

    [Fact]
    public void ACueWithNoBindingHasNoPlace()
    {
        var board = new CueBoard(_audio, emitterFor: _ => new Emitter(Vector3.Zero()));

        board.Update([new Cue("narration", 0, 1)]);

        Assert.Null(_audio.Voices.Single().Emitter);
    }

    [Fact]
    public void TwoCuesOfOneClipAreOneVoiceAtTheLouderGain()
    {
        var board = new CueBoard(_audio);

        board.Update([new Cue("wind", 0, 0.2), new Cue("wind", 0, 0.9)]);

        var started = Assert.IsType<AudioStarted>(Assert.Single(_backend.Calls));
        Assert.Equal(0.9, started.Level.Gain);
    }

    [Fact]
    public void StopFadesOutEverything()
    {
        var board = new CueBoard(_audio, release: 0);
        board.Update([new Cue("a", 0, 1), new Cue("b", 0, 1)]);

        board.Stop();

        Assert.Equal(["start a", "start b", "stop 0", "stop 1"], Log());
    }
}

namespace Orblit.Audio.Tests;

public class RecordedTests
{
    [Fact]
    public void TheRecordingFollowsAVoiceFromStartToStop()
    {
        var backend = new RecordedAudio();

        backend.Start(1, "a", loop: false, level: new Level(gain: 0.5));
        Assert.True(backend.IsPlaying(1));
        Assert.Equal(new Level(gain: 0.5), backend.LevelOf(1));

        backend.Update(1, new Level(gain: 0.25));
        Assert.Equal(new Level(gain: 0.25), backend.LevelOf(1));

        backend.Stop(1);
        Assert.False(backend.IsPlaying(1));
        Assert.Null(backend.LevelOf(1));
    }

    [Fact]
    public void ACallTheContractRulesOutIsAFailure()
    {
        var backend = new RecordedAudio();

        Assert.Throws<InvalidOperationException>(() => backend.Update(1, new Level()));
        Assert.Throws<InvalidOperationException>(() => backend.Stop(1));

        backend.Start(1, "a", loop: false, level: new Level());
        backend.Stop(1);

        Assert.Throws<InvalidOperationException>(() => backend.Update(1, new Level()));
        Assert.Throws<InvalidOperationException>(() => backend.Stop(1));
        Assert.Throws<InvalidOperationException>(() => backend.Start(1, "a", loop: false, level: new Level()));
    }

    [Fact]
    public void FinishEndsAVoiceWithoutAStopCall()
    {
        var backend = new RecordedAudio();
        backend.Start(1, "a", loop: false, level: new Level());

        backend.Finish(1);

        Assert.False(backend.IsPlaying(1));
        Assert.Single(backend.Calls);
    }

    [Fact]
    public void LevelsAreEqualWhenTheirNumbersAre()
    {
        Assert.Equal(new Level(gain: 0.5), new Level(gain: 0.5));
        Assert.NotEqual(new Level(gain: 0.5), new Level(gain: 0.5, pan: 0.1));
        Assert.NotEqual(new Level(pitch: 2), new Level());
    }
}

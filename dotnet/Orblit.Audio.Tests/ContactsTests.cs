namespace Orblit.Audio.Tests;

public class ContactsTests
{
    private readonly RecordedAudio _backend = new();
    private readonly AudioPlayer _audio;

    public ContactsTests() => _audio = new AudioPlayer(_backend);

    private ContactSounds Make(long limit = 16) =>
        new(
            _audio,
            [
                new ContactRule("wood", "stone", "wood_on_stone"),
                new ContactRule("glass", null, "glass_any"),
                new ContactRule("wood", null, "wood_any"),
            ],
            quiet: 1,
            loud: 5,
            limit: limit);

    private string ClipOf(int index) => ((AudioStarted)_backend.Calls[index]).Clip;

    private double GainOf(int index) => ((AudioStarted)_backend.Calls[index]).Level.Gain;

    [Fact]
    public void APairRuleMatchesInEitherOrder()
    {
        var sounds = Make();

        sounds.Hit("wood", "stone", strength: 3);
        sounds.Hit("stone", "wood", strength: 3);

        Assert.Equal(["wood_on_stone", "wood_on_stone"], [ClipOf(0), ClipOf(1)]);
    }

    [Fact]
    public void ARuleForTwoSurfacesBeatsARuleForOne()
    {
        Make().Hit("wood", "stone", strength: 3);

        Assert.Equal("wood_on_stone", ClipOf(0));
    }

    [Fact]
    public void ARuleForOneSurfaceCoversTheRest()
    {
        var sounds = Make();

        sounds.Hit("wood", "grass", strength: 3);
        sounds.Hit("grass", "glass", strength: 3);

        Assert.Equal(["wood_any", "glass_any"], [ClipOf(0), ClipOf(1)]);
    }

    [Fact]
    public void VolumeClimbsFromQuietToLoudAndStopsThere()
    {
        var sounds = Make();

        sounds.Hit("wood", "grass", strength: 3);
        sounds.Hit("wood", "grass", strength: 50);

        Assert.Equal(0.5, GainOf(0), 1e-9);
        Assert.Equal(1, GainOf(1));
    }

    [Fact]
    public void ATouchBelowQuietOrAPairNothingCoversIsSilent()
    {
        var sounds = Make();

        sounds.Hit("wood", "grass", strength: 0.9);
        sounds.Hit("wood", "grass", strength: double.NaN);
        sounds.Hit("rubber", "grass", strength: 9);

        Assert.Empty(_backend.Calls);
    }

    [Fact]
    public void NoMoreThanTheLimitPlayAtOnceAndAFreedSlotIsReused()
    {
        var sounds = Make(limit: 2);
        for (var i = 0; i < 5; i++)
        {
            sounds.Hit("wood", "grass", strength: 3);
        }

        Assert.Equal(2, _backend.Calls.Count);

        _backend.Finish(0);
        _audio.Advance(0);
        sounds.Hit("wood", "grass", strength: 3);

        Assert.Equal(3, _backend.Calls.Count);
    }

    [Fact]
    public void TheLevelsHaveToMakeSense()
    {
        Assert.ThrowsAny<ArgumentException>(() => new ContactSounds(_audio, [], quiet: 5, loud: 5));
    }
}

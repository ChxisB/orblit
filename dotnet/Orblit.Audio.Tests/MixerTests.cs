namespace Orblit.Audio.Tests;

public class MixerTests
{
    [Fact]
    public void ABusScalesAVoiceByEveryBusAbove()
    {
        var mixer = new Mixer();
        var music = mixer.Add("music");
        music.Volume = 0.5;
        var menu = mixer.Add("menu", under: music);
        menu.Volume = 0.5;
        mixer.Master.Volume = 0.8;

        Assert.Equal(0.4, music.Gain, 1e-9);
        Assert.Equal(0.2, menu.Gain, 1e-9);
    }

    [Fact]
    public void AMuteAnywhereAboveSilencesABusAndAnUnmuteRestoresIt()
    {
        var mixer = new Mixer();
        var music = mixer.Add("music");
        var menu = mixer.Add("menu", under: music);

        music.Muted = true;
        Assert.Equal(0, menu.Gain);

        music.Muted = false;
        Assert.Equal(1, menu.Gain);
    }

    [Fact]
    public void ABusIsFoundByNameAndTheMasterBusIsOneOfThem()
    {
        var mixer = new Mixer();
        var sfx = mixer.Add("sfx");

        Assert.Same(sfx, mixer.Bus("sfx"));
        Assert.Same(mixer.Master, mixer.Bus("master"));
        Assert.ThrowsAny<ArgumentException>(() => mixer.Bus("nope"));
    }

    [Fact]
    public void ANameIsUsedOnceAndAParentMustBeInTheSameMixer()
    {
        var mixer = new Mixer();
        mixer.Add("sfx");
        var stranger = new Mixer().Add("sfx");

        Assert.ThrowsAny<ArgumentException>(() => mixer.Add("sfx"));
        Assert.ThrowsAny<ArgumentException>(() => mixer.Add("ui", under: stranger));
    }

    [Fact]
    public void AVolumeMustBeAFiniteNumberOfZeroOrMore()
    {
        var bus = new Mixer().Add("sfx");

        Assert.ThrowsAny<ArgumentException>(() => { bus.Volume = -0.1; });
        Assert.ThrowsAny<ArgumentException>(() => { bus.Volume = double.NaN; });
        Assert.ThrowsAny<ArgumentException>(() => { bus.Volume = double.PositiveInfinity; });
        bus.Volume = 2;
        Assert.Equal(2, bus.Volume);
    }
}

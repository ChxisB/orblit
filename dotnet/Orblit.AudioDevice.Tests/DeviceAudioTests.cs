using Orblit.Audio;

namespace Orblit.AudioDevice.Tests;

public sealed class DeviceAudioTests : IClassFixture<Tones>, IDisposable
{
    private const int Rate = 48000;

    private readonly Tones _tones;
    private readonly DeviceAudio _device;

    public DeviceAudioTests(Tones tones)
    {
        _tones = tones;
        _device = Offline(tones.Folder.FullName);
    }

    public void Dispose() => _device.Dispose();

    private static DeviceAudio Offline(string folder, long streamAbove = DeviceAudioOptions.DefaultStreamAbove) =>
        DeviceAudio.Offline(options: new DeviceAudioOptions { Directory = folder, StreamAbove = streamAbove });

    private static double Peak(float[] samples, int channel) => Wav.PeakOf(samples, channel);

    private float[] Heard(int frames) => _device.Render(frames);

    [Fact]
    public void AClipIsHeardOnBothSidesAtItsOwnLevel()
    {
        _device.Start(1, "tone.wav", loop: false, level: new Level());

        var heard = Heard(Rate / 10);

        Assert.Equal(Rate / 10 * 2, heard.Length);
        Assert.Equal(_tones.Peak, Peak(heard, 0), 0.01);
        Assert.Equal(_tones.Peak, Peak(heard, 1), 0.01);
        Assert.True(_device.IsPlaying(1));
    }

    [Fact]
    public void GainScalesWhatIsHeard()
    {
        _device.Start(1, "tone.wav", loop: false, level: new Level(gain: 0.5));

        Assert.Equal(_tones.Peak / 2, Peak(Heard(Rate / 10), 0), 0.01);
    }

    [Fact]
    public void PanMovesAClipToOneSide()
    {
        _device.Start(1, "tone.wav", loop: true, level: new Level(pan: -1));
        var left = Heard(Rate / 10);
        _device.Update(1, new Level(pan: 1));
        var right = Heard(Rate / 10);

        Assert.True(Peak(left, 0) > 0.2);
        Assert.Equal(0, Peak(left, 1));
        Assert.Equal(0, Peak(right, 0));
        Assert.True(Peak(right, 1) > 0.2);
    }

    [Fact]
    public void AClipEndsWhenItHasPlayedThrough()
    {
        _device.Start(1, "tone.wav", loop: false, level: new Level());

        Heard(Rate / 4);
        Assert.True(_device.IsPlaying(1));
        Heard(Rate / 2);
        Assert.False(_device.IsPlaying(1));
    }

    [Fact]
    public void AClipBeginsWhereItIsAskedTo()
    {
        _device.Start(1, "tone.wav", loop: false, level: new Level(), offset: 0.4);

        var first = Heard(Rate / 20);
        Assert.Equal(_tones.Peak, Peak(first, 0), 0.05);
        Heard(Rate / 10);
        Assert.False(_device.IsPlaying(1));
    }

    [Fact]
    public void AClipAskedToBeginPastItsEndIsOverAtOnce()
    {
        _device.Start(1, "tone.wav", loop: false, level: new Level(), offset: 100);

        Assert.Equal(0, Peak(Heard(Rate / 20), 0));
        Assert.False(_device.IsPlaying(1));
    }

    [Fact]
    public void ALoopAskedToBeginPastItsEndWrapsAndKeepsPlaying()
    {
        _device.Start(1, "tone.wav", loop: true, level: new Level(), offset: 100);

        Assert.True(Peak(Heard(Rate / 20), 0) > 0);
        Assert.True(_device.IsPlaying(1));
    }

    [Fact]
    public void AHigherPitchPlaysThroughSooner()
    {
        _device.Start(1, "tone.wav", loop: false, level: new Level(pitch: 2));

        Heard((int)Math.Round(Rate * 0.3));

        Assert.False(_device.IsPlaying(1));
    }

    [Fact]
    public void ALoopKeepsPlayingPastItsLength()
    {
        _device.Start(1, "tone.wav", loop: true, level: new Level());

        Heard(Rate * 2);

        Assert.True(_device.IsPlaying(1));
    }

    [Fact]
    public void AStoppedVoiceIsSilentAndNoLongerPlaying()
    {
        _device.Start(1, "tone.wav", loop: true, level: new Level());
        Heard(Rate / 10);

        _device.Stop(1);

        Assert.False(_device.IsPlaying(1));
        Assert.Equal(0, Peak(Heard(Rate / 10), 0));
    }

    [Fact]
    public void TwoVoicesOfOneClipAreMixed()
    {
        _device.Start(1, "tone.wav", loop: true, level: new Level());
        _device.Start(2, "tone.wav", loop: true, level: new Level());

        Assert.Equal(_tones.Peak * 2, Peak(Heard(Rate / 10), 0), 0.02);
    }

    [Fact]
    public void AClipAtAnotherSampleRateKeepsItsLength()
    {
        File.WriteAllBytes(Path.Combine(_tones.Folder.FullName, "slow.wav"), Wav.Sine(rate: 22050, seconds: 1).Bytes);
        _device.Start(1, "slow.wav", loop: false, level: new Level());

        Heard((int)Math.Round(Rate * 0.9));
        Assert.True(_device.IsPlaying(1));
        Heard((int)Math.Round(Rate * 0.2));
        Assert.False(_device.IsPlaying(1));
    }

    [Fact]
    public void AFileOverTheStreamSizeIsStreamedAndHeardTheSame()
    {
        using var streamed = Offline(_tones.Folder.FullName, streamAbove: 0);
        streamed.Start(1, "tone.wav", loop: false, level: new Level());

        Assert.Equal(_tones.Peak, Peak(streamed.Render(Rate / 10), 0), 0.01);
    }

    [Fact]
    public void AnOggVorbisFileIsDecoded()
    {
        using var device = Offline(Path.Combine(AppContext.BaseDirectory, "support"));
        device.Start(1, "sine.ogg", loop: false, level: new Level());

        Assert.True(Peak(device.Render(Rate / 10), 0) > 0.05);
    }

    [Fact]
    public void AMissingFileThrowsWhenNothingHandlesIt()
    {
        Assert.Throws<AudioDeviceException>(() => _device.Start(1, "nope.wav", loop: false, level: new Level()));
        Assert.False(_device.IsPlaying(1));
    }

    [Fact]
    public void AMissingFileGoesToTheHandlerAndTheVoiceNeverPlays()
    {
        var failures = new List<string>();
        using var device = DeviceAudio.Offline(options: new DeviceAudioOptions
        {
            Directory = _tones.Folder.FullName,
            OnError = (clip, _) => failures.Add(clip),
        });

        device.Start(1, "nope.wav", loop: false, level: new Level());

        Assert.Equal(["nope.wav"], failures);
        Assert.False(device.IsPlaying(1));
    }

    [Fact]
    public void AFileThatIsNotAudioIsRefused()
    {
        File.WriteAllText(Path.Combine(_tones.Folder.FullName, "text.wav"), "not a sound at all");

        Assert.Throws<AudioDeviceException>(() => _device.Start(1, "text.wav", loop: false, level: new Level()));
    }

    [Fact]
    public void AGameDrivesItThroughAudioPlayer()
    {
        var audio = new AudioPlayer(_device);
        var voice = audio.Play("tone.wav");

        Heard(Rate / 10);
        audio.Advance(0.1);
        Assert.True(voice.IsPlaying);
        Heard(Rate);
        audio.Advance(0.1);

        Assert.False(voice.IsPlaying);
        Assert.Empty(audio.Voices);
    }

    [Fact]
    public void ClosingWithVoicesStillPlayingIsSafe()
    {
        _device.Start(1, "tone.wav", loop: true, level: new Level());
        _device.Start(2, "tone.wav", loop: true, level: new Level());
        Heard(Rate / 10);

        _device.Dispose();
    }

    [Fact]
    public void DisposingTwiceIsSafeAndEveryVoiceIsThenOver()
    {
        _device.Start(1, "tone.wav", loop: true, level: new Level());

        _device.Dispose();
        _device.Dispose();

        Assert.False(_device.IsPlaying(1));
        Assert.Throws<ObjectDisposedException>(() => _device.Start(2, "tone.wav", loop: false, level: new Level()));
        Assert.Throws<ObjectDisposedException>(() => _device.Render(10));
    }

    [Fact]
    public void AnAbsoluteClipPathIgnoresTheDirectory()
    {
        using var device = Offline(Path.Combine(AppContext.BaseDirectory, "support"));

        device.Start(1, Path.Combine(_tones.Folder.FullName, "tone.wav"), loop: false, level: new Level());

        Assert.Equal(_tones.Peak, Peak(device.Render(Rate / 10), 0), 0.01);
    }
}

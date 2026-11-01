namespace Orblit.Audio;

/// <summary>A change of volume over time.</summary>
internal sealed class Fade(double from, double to, double length)
{
    public double From { get; } = from;

    public double To { get; } = to;

    public double Length { get; } = length;

    public double Elapsed { get; set; }

    public bool IsDone => Elapsed >= Length;

    public double Volume => IsDone ? To : From + ((To - From) * (Elapsed / Length));
}

/// <summary>One clip playing on one bus. <see cref="AudioPlayer.Play"/> makes it.</summary>
public sealed class Voice
{
    private readonly IAudioBackend _backend;
    private double _volume;
    private double _pitch;
    private bool _playing = true;
    private Fade? _fade;

    /// <summary>
    /// Seconds left before the voice ends, once <see cref="Stop"/> has been given a
    /// fade. A fade of the volume cannot cancel it.
    /// </summary>
    private double? _stopIn;
    private Level _sent = new();

    // F2: nine parameters, and AudioPlayer.Play calls every one by name.
    internal Voice(
        long id,
        string clip,
        Bus bus,
        bool loop,
        Emitter? emitter,
        double offset,
        double volume,
        double pitch,
        IAudioBackend backend)
    {
        Id = id;
        Clip = clip;
        Bus = bus;
        Loop = loop;
        Emitter = emitter;
        Offset = offset;
        _volume = Checks.Gain(volume, nameof(volume));
        _pitch = CheckedPitch(pitch);
        _backend = backend;
    }

    public long Id { get; }

    public string Clip { get; }

    public Bus Bus { get; }

    public bool Loop { get; }

    /// <summary>Where in the clip the voice began, in seconds.</summary>
    public double Offset { get; }

    /// <summary>Null for a sound with no place, such as music or an interface click.</summary>
    public Emitter? Emitter { get; }

    public bool IsPlaying => _playing;

    /// <summary>The voice's own gain, before its buses and its distance.</summary>
    public double Volume
    {
        get => _volume;
        set
        {
            _volume = Checks.Gain(value, nameof(Volume));
            _fade = null;
        }
    }

    /// <summary>A playback rate. 1 is the clip as recorded.</summary>
    public double Pitch
    {
        get => _pitch;
        set => _pitch = CheckedPitch(value);
    }

    /// <summary>Moves the volume to <paramref name="volume"/> in a straight line over <paramref name="seconds"/>.</summary>
    public void FadeTo(double volume, double seconds)
    {
        var target = Checks.Gain(volume, nameof(volume));
        var length = Checks.Seconds(seconds, nameof(seconds));
        if (_playing)
        {
            StartFade(target, length);
        }
    }

    /// <summary>
    /// Ends the voice now, or after fading to silence over <paramref name="fadeSeconds"/>.
    /// The end holds even if the volume is set or faded meanwhile.
    /// </summary>
    public void Stop(double fadeSeconds = 0)
    {
        var length = Checks.Seconds(fadeSeconds, nameof(fadeSeconds));
        if (length == 0)
        {
            End();
            return;
        }

        _stopIn = length;
        StartFade(0, length);
    }

    /// <summary>What this voice sounds like to <paramref name="listener"/>.</summary>
    public Level LevelFor(Listener listener)
    {
        var heard = Emitter?.HeardBy(listener) ?? new Level();
        return new Level(gain: _volume * Bus.Gain * heard.Gain, pitch: _pitch * heard.Pitch, pan: heard.Pan);
    }

    internal void Begin(Listener listener)
    {
        _sent = LevelFor(listener);
        _backend.Start(Id, Clip, loop: Loop, level: _sent, offset: Offset);
    }

    internal void Advance(double seconds, Listener listener)
    {
        StepFade(seconds);
        if (_playing && !_backend.IsPlaying(Id))
        {
            _playing = false;
        }

        if (!_playing)
        {
            return;
        }

        var level = LevelFor(listener);
        if (level.Equals(_sent))
        {
            return;
        }

        _sent = level;
        _backend.Update(Id, level);
    }

    private static double CheckedPitch(double value)
    {
        if (!double.IsFinite(value) || value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "must be above zero");
        }

        return value;
    }

    private void StartFade(double target, double seconds)
    {
        if (seconds == 0)
        {
            _volume = target;
            _fade = null;
        }
        else
        {
            _fade = new Fade(_volume, target, seconds);
        }
    }

    private void StepFade(double seconds)
    {
        if (_fade is { } fade)
        {
            fade.Elapsed += seconds;
            _volume = fade.Volume;
            if (fade.IsDone)
            {
                _fade = null;
            }
        }

        if (_stopIn is not { } left)
        {
            return;
        }

        _stopIn = left - seconds;
        if (left <= seconds)
        {
            End();
        }
    }

    /// <summary>A clip that already played to its end is not stopped a second time.</summary>
    private void End()
    {
        if (_backend.IsPlaying(Id))
        {
            _backend.Stop(Id);
        }

        _playing = false;
    }
}

/// <summary>
/// The sound calls a game makes. Nothing is heard until a backend is behind it,
/// and nothing changes in the backend between two calls to <see cref="Advance"/>
/// except a start or a stop.
/// </summary>
public sealed class AudioPlayer(IAudioBackend backend)
{
    private readonly List<Voice> _voices = [];
    private long _nextId;

    public Mixer Mixer { get; } = new();

    public Listener Listener { get; } = new();

    /// <summary>The voices that are playing.</summary>
    public IReadOnlyList<Voice> Voices => _voices.Where(voice => voice.IsPlaying).ToList().AsReadOnly();

    /// <summary>
    /// Plays <paramref name="clip"/> on <paramref name="bus"/>, or on the master bus,
    /// from <paramref name="offset"/> seconds in. A voice with an <paramref name="at"/>
    /// is heard from where that emitter is.
    /// </summary>
    public Voice Play(
        string clip,
        Bus? bus = null,
        bool loop = false,
        double volume = 1,
        double pitch = 1,
        double offset = 0,
        Emitter? at = null)
    {
        if (bus is not null && !Mixer.Owns(bus))
        {
            throw new ArgumentOutOfRangeException(nameof(bus), bus, "is not a bus of this mixer");
        }

        var voice = new Voice(
            id: _nextId++,
            clip: clip,
            bus: bus ?? Mixer.Master,
            loop: loop,
            emitter: at,
            offset: Checks.Seconds(offset, nameof(offset)),
            volume: volume,
            pitch: pitch,
            backend: backend);
        voice.Begin(Listener);
        _voices.Add(voice);
        return voice;
    }

    /// <summary>
    /// Moves every fade on by <paramref name="seconds"/>, then tells the backend what
    /// changed since the last call: a bus, the listener, an emitter or a fade.
    /// </summary>
    public void Advance(double seconds)
    {
        if (!(seconds >= 0))
        {
            throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "must be zero or more");
        }

        foreach (var voice in _voices)
        {
            voice.Advance(seconds, Listener);
        }

        // After every voice has used them, since an emitter can be shared.
        Listener.Settle();
        foreach (var voice in _voices)
        {
            voice.Emitter?.Settle();
        }

        _voices.RemoveAll(voice => !voice.IsPlaying);
    }
}

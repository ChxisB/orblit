namespace Orblit.Audio;

/// <summary>One call a backend received.</summary>
public abstract class AudioCall
{
    private protected AudioCall(long voice) => Voice = voice;

    public long Voice { get; }
}

public sealed class AudioStarted : AudioCall
{
    // F2: five parameters, and RecordedAudio.Start calls the last four by name.
    public AudioStarted(long voice, string clip, bool loop, Level level, double offset)
        : base(voice)
    {
        Clip = clip;
        Loop = loop;
        Level = level;
        Offset = offset;
    }

    public string Clip { get; }

    public bool Loop { get; }

    public Level Level { get; }

    public double Offset { get; }
}

public sealed class AudioUpdated(long voice, Level level) : AudioCall(voice)
{
    public Level Level { get; } = level;
}

public sealed class AudioStopped(long voice) : AudioCall(voice);

/// <summary>
/// A backend that writes every call down and plays nothing. Every test of
/// sound in this repository runs against it. It throws
/// <see cref="InvalidOperationException"/> for a call the <see cref="IAudioBackend"/>
/// contract rules out: a start of an id used before, or an update or a stop of a
/// voice that is not playing.
/// </summary>
public sealed class RecordedAudio : IAudioBackend
{
    private readonly Dictionary<long, Level> _playing = [];
    private readonly HashSet<long> _everStarted = [];

    /// <summary>Every call, in the order it arrived.</summary>
    public List<AudioCall> Calls { get; } = [];

    /// <summary>How <paramref name="voice"/> last sounded, or null when it is not playing.</summary>
    public Level? LevelOf(long voice) => _playing.GetValueOrDefault(voice);

    /// <summary>Ends <paramref name="voice"/> as a clip does when it plays to its end.</summary>
    public void Finish(long voice) => _playing.Remove(voice);

    public void Start(long voice, string clip, bool loop, Level level, double offset = 0)
    {
        if (!_everStarted.Add(voice))
        {
            throw new InvalidOperationException($"Voice {voice} was started before.");
        }

        Calls.Add(new AudioStarted(voice, clip: clip, loop: loop, level: level, offset: offset));
        _playing[voice] = level;
    }

    public void Update(long voice, Level level)
    {
        MustBePlaying(voice);
        Calls.Add(new AudioUpdated(voice, level));
        _playing[voice] = level;
    }

    public void Stop(long voice)
    {
        MustBePlaying(voice);
        Calls.Add(new AudioStopped(voice));
        _playing.Remove(voice);
    }

    public bool IsPlaying(long voice) => _playing.ContainsKey(voice);

    /// <summary>
    /// The contract says a call on a voice that is not playing never happens, so
    /// the fake makes one a failure rather than a note in the log.
    /// </summary>
    private void MustBePlaying(long voice)
    {
        if (!IsPlaying(voice))
        {
            throw new InvalidOperationException($"Voice {voice} is not playing.");
        }
    }
}

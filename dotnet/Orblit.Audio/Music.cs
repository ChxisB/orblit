using Orblit.VectorMath;

namespace Orblit.Audio;

/// <summary>A piece of music that loops, and the beat it keeps.</summary>
public sealed class MusicTrack
{
    public MusicTrack(string clip, double bpm = 120, long beatsPerBar = 4)
    {
        if (!(double.IsFinite(bpm) && bpm > 0))
        {
            throw new ArgumentOutOfRangeException(nameof(bpm), bpm, "must be above zero");
        }

        if (beatsPerBar < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(beatsPerBar), beatsPerBar, "must be 1 or more");
        }

        Clip = clip;
        Bpm = bpm;
        BeatsPerBar = beatsPerBar;
    }

    public string Clip { get; }

    public double Bpm { get; }

    public long BeatsPerBar { get; }

    public double Beat => 60 / Bpm;

    public double Bar => Beat * BeatsPerBar;
}

/// <summary>When a change of music happens.</summary>
public enum MusicWhen
{
    Now,
    NextBeat,
    NextBar,
}

/// <summary>
/// One row of the transition table: going <see cref="To"/> a track, from
/// <see cref="From"/> or, when that is null, from any track.
/// </summary>
public sealed class MusicMove(string to, string? from = null, MusicWhen when = MusicWhen.Now, double fade = 0)
{
    public string? From { get; } = from;

    public string To { get; } = to;

    public MusicWhen When { get; } = when;

    /// <summary>Seconds the old track fades out over while the new one fades in.</summary>
    public double Fade { get; } = fade;
}

/// <summary>
/// The music of a game: one track looping at a time, changed by a table, with
/// stingers over the top.
/// Time is the sampled kind. <see cref="Advance"/> says how long has passed, so a
/// change that waits for the beat fires on the first call that crosses it, and a
/// test drives it with whatever steps it likes.
/// </summary>
public sealed class Music
{
    // shortcut: the duck and the recovery fade for a fixed time, make them
    // fields when a game wants to tune them.
    private const double DuckFade = 0.1;
    private const double RecoverFade = 0.5;

    private readonly List<Voice> _stingers = [];
    private string? _playing;
    private Voice? _bed;

    /// <summary>Seconds the current track has been playing.</summary>
    private double _position;
    private MusicMove? _pending;

    public Music(
        AudioPlayer audio,
        IReadOnlyDictionary<string, MusicTrack> tracks,
        IReadOnlyList<MusicMove>? moves = null,
        Bus? bus = null,
        double duck = 0.3)
    {
        Audio = audio;
        Tracks = tracks;
        Moves = moves ?? [];
        Bus = bus;
        Duck = duck;
    }

    public AudioPlayer Audio { get; }

    public IReadOnlyDictionary<string, MusicTrack> Tracks { get; }

    public IReadOnlyList<MusicMove> Moves { get; }

    public Bus? Bus { get; }

    /// <summary>What a track is turned down to while a stinger plays.</summary>
    public double Duck { get; }

    /// <summary>The track playing now, or null before the first.</summary>
    public string? Playing => _playing;

    /// <summary>The track a change is waiting to start, or null.</summary>
    public string? WaitingFor => _pending?.To;

    /// <summary>
    /// Goes to <paramref name="track"/> by the first row that fits, else a cut. Going to the
    /// track that is already waited for does nothing, and going to the one that is
    /// playing cancels a change that was waiting.
    /// </summary>
    public void Go(string track)
    {
        if (!Tracks.ContainsKey(track))
        {
            throw new ArgumentOutOfRangeException(nameof(track), track, "there is no such track");
        }

        if (track == _playing)
        {
            _pending = null;
            return;
        }

        if (track == _pending?.To)
        {
            return;
        }

        var move = MoveTo(track);
        if (move.When == MusicWhen.Now || _playing is null)
        {
            Begin(move, late: 0);
        }
        else
        {
            _pending = move;
        }
    }

    /// <summary>
    /// Fades the track out over <paramref name="fade"/> seconds and leaves nothing playing. A
    /// change that was waiting is dropped, and the next <see cref="Go"/> starts at once.
    /// </summary>
    public void Stop(double fade = 0)
    {
        _pending = null;
        _bed?.Stop(fadeSeconds: fade);
        _bed = null;
        _playing = null;
    }

    /// <summary>Plays <paramref name="clip"/> once over the music and turns the track down until it ends.</summary>
    public void Stinger(string clip)
    {
        _stingers.Add(Audio.Play(clip, bus: Bus));
        _bed?.FadeTo(Duck, seconds: DuckFade);
    }

    /// <summary>
    /// Moves on by <paramref name="seconds"/>: the beat, a change waiting for it, and a
    /// stinger that has ended.
    /// </summary>
    public void Advance(double seconds)
    {
        var before = _position;
        _position += seconds;
        if (_pending is { } move)
        {
            var unit = Unit(move.When);
            var boundary = (Doubles.Floor(before / unit) + 1) * unit;
            if (_position >= boundary)
            {
                Begin(move, late: _position - boundary);
            }
        }

        var wasDucked = _stingers.Count > 0;
        _stingers.RemoveAll(voice => !voice.IsPlaying);
        if (wasDucked && _stingers.Count == 0)
        {
            _bed?.FadeTo(1, seconds: RecoverFade);
        }
    }

    private MusicMove MoveTo(string track)
    {
        foreach (var move in Moves)
        {
            if (move.To == track && move.From == _playing)
            {
                return move;
            }
        }

        foreach (var move in Moves)
        {
            if (move.To == track && move.From is null)
            {
                return move;
            }
        }

        return new MusicMove(to: track);
    }

    private double Unit(MusicWhen when)
    {
        var track = Tracks[_playing!]; // Go only waits when a track plays.
        return when == MusicWhen.NextBar ? track.Bar : track.Beat;
    }

    /// <summary>
    /// Starts <paramref name="move"/>'s track. <paramref name="late"/> is how far past its beat
    /// this is, so the new track begins where it would have if it had started on time.
    /// </summary>
    private void Begin(MusicMove move, double late)
    {
        _pending = null;
        _bed?.Stop(fadeSeconds: move.Fade);
        _playing = move.To;
        _position = late;
        var level = _stingers.Count == 0 ? 1.0 : Duck;
        var bed = Audio.Play(
            Tracks[move.To].Clip,
            bus: Bus,
            loop: true,
            volume: move.Fade > 0 ? 0 : level,
            offset: late);
        if (move.Fade > 0)
        {
            bed.FadeTo(level, seconds: move.Fade);
        }

        _bed = bed;
    }
}

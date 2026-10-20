namespace Orblit.Audio;

/// <summary>
/// A sound a sequence wants playing: which clip, how far into it the playhead
/// is, how loud, and where it comes from. Shaped like <c>orblit_sequence</c>'s
/// <c>SoundAt</c>, which this package does not import.
/// </summary>
public sealed class Cue(string clip, double at, double gain, string? binding = null)
{
    public string Clip { get; } = clip;

    /// <summary>Seconds into the clip.</summary>
    public double At { get; } = at;

    public double Gain { get; } = gain;

    /// <summary>What the sound comes from, or null for a sound that has no place.</summary>
    public string? Binding { get; } = binding;
}

/// <summary>
/// Plays the cues of a sequence as the playhead passes them.
/// A sequence states what should be sounding at each moment. This turns that
/// into starts and stops: a cue that appears starts a voice, one that goes
/// stops it, and one that stays follows its gain. A cue is started once, so a
/// clip that plays to its end before the sequence does is not started again.
/// </summary>
public sealed class CueBoard
{
    /// <summary>A cue seen and not yet gone. The voice is null for one a seek skipped.</summary>
    private readonly OrderedDictionary<(string?, string), Voice?> _voices = [];

    public CueBoard(
        AudioPlayer audio,
        Bus? bus = null,
        IReadOnlySet<string>? beds = null,
        Func<string, Emitter?>? emitterFor = null,
        double release = 0.05)
    {
        Audio = audio;
        Bus = bus;
        Beds = beds ?? new HashSet<string>();
        EmitterFor = emitterFor;
        Release = release;
    }

    public AudioPlayer Audio { get; }

    public Bus? Bus { get; }

    /// <summary>
    /// Clips that are meant to be there whenever the playhead is, such as music
    /// or narration. After a seek they start part-way in. Any other clip is a
    /// one-shot, and a seek into the middle of it starts nothing.
    /// </summary>
    public IReadOnlySet<string> Beds { get; }

    /// <summary>
    /// Where a bound cue is heard from, or null to hear it from nowhere. Asked
    /// once, when the cue starts.
    /// </summary>
    public Func<string, Emitter?>? EmitterFor { get; }

    /// <summary>
    /// Seconds to fade a cue out over when the sequence drops it, so a cut does
    /// not click.
    /// </summary>
    public double Release { get; }

    /// <summary>
    /// Moves on to what the sequence says now. Pass <paramref name="seeked"/> when the
    /// playhead jumped rather than ran. Two cues of one clip and binding are one
    /// voice, at the louder gain.
    /// </summary>
    // shortcut: a clip twice under one binding is a single voice, split them by
    // binding if a sequence needs two at once.
    public void Update(IEnumerable<Cue> cues, bool seeked = false)
    {
        var now = Loudest(cues);
        foreach (var key in _voices.Keys.Where(key => !now.ContainsKey(key)).ToList())
        {
            FadeOut(key);
        }

        foreach (var (key, cue) in now)
        {
            Follow(key, cue, seeked);
        }
    }

    /// <summary>Fades out every cue, as when the sequence ends.</summary>
    public void Stop() => Update([]);

    private static OrderedDictionary<(string?, string), Cue> Loudest(IEnumerable<Cue> cues)
    {
        var now = new OrderedDictionary<(string?, string), Cue>();
        foreach (var cue in cues)
        {
            var key = (cue.Binding, cue.Clip);
            if (!now.TryGetValue(key, out var other) || cue.Gain > other.Gain)
            {
                now[key] = cue;
            }
        }

        return now;
    }

    private void FadeOut((string?, string) key)
    {
        if (_voices.Remove(key, out var voice))
        {
            voice?.Stop(fadeSeconds: Release);
        }
    }

    private void Follow((string?, string) key, Cue cue, bool seeked)
    {
        var bed = Beds.Contains(cue.Clip);
        if (_voices.TryGetValue(key, out var voice) && !(seeked && bed))
        {
            voice?.Volume = cue.Gain;
            return;
        }

        // A seek into a bed starts it again, and removing the key first puts it last.
        FadeOut(key);
        _voices[key] = seeked && !bed ? null : Start(cue);
    }

    private Voice Start(Cue cue) =>
        Audio.Play(
            cue.Clip,
            bus: Bus,
            volume: cue.Gain,
            offset: cue.At,
            at: cue.Binding is null ? null : EmitterFor?.Invoke(cue.Binding));
}

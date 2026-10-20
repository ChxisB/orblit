using Orblit.VectorMath;

namespace Orblit.Audio;

/// <summary>
/// The clip two surfaces make when they meet. A null <see cref="B"/> is any
/// surface, so ("glass", null) covers glass against everything the table does
/// not name.
/// </summary>
public sealed class ContactRule(string a, string? b, string clip)
{
    public string A { get; } = a;

    public string? B { get; } = b;

    public string Clip { get; } = clip;

    internal bool Fits(string x, string y) =>
        (A == x && (B is null || B == y)) || (A == y && (B is null || B == x));
}

/// <summary>
/// The sound of two things touching: a clip chosen by what each is made of, as
/// loud as the touch was hard.
/// </summary>
public sealed class ContactSounds
{
    private readonly List<Voice> _playing = [];

    // F2: six parameters, and the four after the rules are optional and called by name.
    public ContactSounds(
        AudioPlayer audio,
        IReadOnlyList<ContactRule> rules,
        Bus? bus = null,
        double quiet = 0.5,
        double loud = 10,
        long limit = 16)
    {
        if (!(quiet >= 0 && loud > quiet && double.IsFinite(loud)))
        {
            throw new ArgumentException("Need 0 <= quiet < loud, finite.");
        }

        Audio = audio;
        Rules = rules;
        Bus = bus;
        Quiet = quiet;
        Loud = loud;
        Limit = limit;
    }

    public AudioPlayer Audio { get; }

    /// <summary>
    /// Tried in order, a rule naming both surfaces before one naming a single
    /// surface, whatever the order in the list.
    /// </summary>
    public IReadOnlyList<ContactRule> Rules { get; }

    public Bus? Bus { get; }

    /// <summary>
    /// The strength below which a touch is silent, and the one at and above which
    /// it is full volume. The unit is the caller's, such as the newton-seconds a
    /// physics touch event carries.
    /// </summary>
    public double Quiet { get; }

    public double Loud { get; }

    /// <summary>
    /// How many contact sounds may play at once. A pile of crates landing in one
    /// frame is a handful of sounds and not a hundred.
    /// </summary>
    public long Limit { get; }

    /// <summary>
    /// Plays what <paramref name="a"/> and <paramref name="b"/> sound like meeting at
    /// <paramref name="strength"/>, from <paramref name="at"/>. Nothing plays for a pair no
    /// rule covers, a touch below <see cref="Quiet"/>, or when <see cref="Limit"/> sounds
    /// are already going.
    /// </summary>
    // shortcut: volume is linear in strength, give it a curve if a hard hit and
    // a harder one sound alike.
    public void Hit(string a, string b, double strength, Emitter? at = null)
    {
        _playing.RemoveAll(voice => !voice.IsPlaying);
        if (!(strength >= Quiet) || _playing.Count >= Limit)
        {
            return;
        }

        var clip = ClipFor(a, b);
        if (clip is null)
        {
            return;
        }

        var volume = Doubles.Clamp((strength - Quiet) / (Loud - Quiet), 0.0, 1.0);
        _playing.Add(Audio.Play(clip, bus: Bus, volume: volume, at: at));
    }

    private string? ClipFor(string a, string b)
    {
        foreach (var rule in Rules)
        {
            if (rule.B is not null && rule.Fits(a, b))
            {
                return rule.Clip;
            }
        }

        foreach (var rule in Rules)
        {
            if (rule.B is null && rule.Fits(a, b))
            {
                return rule.Clip;
            }
        }

        return null;
    }
}

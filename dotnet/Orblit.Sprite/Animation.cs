using Orblit.VectorMath;

namespace Orblit.Sprite;

/// <summary>
/// One frame of an animation, and how long it is held. The time is per frame rather than one
/// rate for the whole animation, because an animator's timing is not even: a punch holds on the
/// wind-up and flies through the strike, and an even rate flattens exactly the part that carries
/// the weight.
/// </summary>
public sealed record Frame(Region Region, double Seconds);

/// <summary>
/// A sequence of frames, sampled by time. Sampled rather than stepped, like everything else
/// here that has a clock: asked what it looks like at a moment rather than advanced by a
/// frame's worth. The same animation at the same moment is the same frame however the frames
/// fell, which is what makes a replay match and lets a test ask about the middle without running
/// the beginning.
/// </summary>
public sealed class SpriteAnimation(IReadOnlyList<Frame> frames, bool loop = true, bool pingPong = false)
{
    public IReadOnlyList<Frame> Frames { get; } = frames;

    /// <summary>Whether it starts again at the end.</summary>
    public bool Loop { get; } = loop;

    /// <summary>
    /// Whether it runs back down instead of jumping to the start. For anything that has to end
    /// where it began, a breath, a hover, a blink, where a plain loop shows a jump on every
    /// repeat.
    /// </summary>
    public bool PingPong { get; } = pingPong;

    public bool IsEmpty => Frames.Count == 0;

    public int Length => Frames.Count;

    /// <summary>How long one run takes. Added in order.</summary>
    public double Duration => Frames.Aggregate(0.0, (total, frame) => total + frame.Seconds);

    /// <summary>How long a whole cycle takes, the way back included.</summary>
    public double Cycle => PingPong && Frames.Count > 1 ? Duration * 2 : Duration;

    /// <summary>Every frame at one rate.</summary>
    public static SpriteAnimation At(
        IReadOnlyList<Region> regions,
        double fps = 12,
        bool loop = true,
        bool pingPong = false)
    {
        var seconds = fps <= 0 ? 0.0 : 1 / fps;
        return new SpriteAnimation([.. regions.Select(region => new Frame(region, seconds))], loop, pingPong);
    }

    /// <summary>Every frame of an atlas whose name starts with <paramref name="prefix"/>, in order.</summary>
    public static SpriteAnimation From(
        Atlas atlas,
        string prefix,
        double fps = 12,
        bool loop = true,
        bool pingPong = false) => At(atlas.Sequence(prefix), fps, loop, pingPong);

    /// <summary>Which frame is showing at <paramref name="seconds"/>.</summary>
    public int IndexAt(double seconds)
    {
        if (Frames.Count == 0)
        {
            return 0;
        }

        var duration = Duration;
        if (duration <= 0)
        {
            return 0;
        }

        var at = seconds < 0 ? 0 : seconds;
        if (Loop)
        {
            at = Doubles.Mod(at, Cycle);
            if (PingPong && at >= duration)
            {
                // Coming back down. The far end is not repeated, or the last frame would be held
                // for twice as long as every other.
                at = (duration * 2) - at;
            }
        }
        else if (at >= duration)
        {
            return Frames.Count - 1;
        }

        var spent = 0.0;
        for (var i = 0; i < Frames.Count; i++)
        {
            spent += Frames[i].Seconds;
            if (at < spent)
            {
                return i;
            }
        }

        return Frames.Count - 1;
    }

    /// <summary>Which region is showing at <paramref name="seconds"/>, or null for an empty animation.</summary>
    public Region? At(double seconds) => Frames.Count == 0 ? null : Frames[IndexAt(seconds)].Region;

    /// <summary>Whether a non-looping animation has run out.</summary>
    public bool DoneBy(double seconds) => !Loop && seconds >= Duration;
}

/// <summary>
/// A named set of animations and which one is playing. The small amount of state a sprite
/// actually has. Kept apart from the animations themselves so that one set of clips serves every
/// copy of a character, the same reason a behaviour tree is stateless.
/// </summary>
public sealed class Flipbook(IReadOnlyDictionary<string, SpriteAnimation> clips, string? playing = null)
{
    public IReadOnlyDictionary<string, SpriteAnimation> Clips { get; } = clips;

    /// <summary>
    /// The clip playing. With none named it is the first key the clips enumerate, so a caller
    /// that cares builds them in an order that keeps one.
    /// </summary>
    public string Playing { get; private set; } = playing ?? clips.Keys.FirstOrDefault() ?? string.Empty;

    public double Since { get; private set; }

    public SpriteAnimation? Current => Clips.GetValueOrDefault(Playing);

    /// <summary>What to draw now.</summary>
    public Region? Frame => Current?.At(Since);

    /// <summary>Whether the clip playing has finished. Always false for a loop.</summary>
    public bool Done => Current?.DoneBy(Since) ?? true;

    /// <summary>
    /// Switches to <paramref name="name"/>, starting it from the beginning. Playing the same clip
    /// again is not a restart. A game says "walk" on every frame the stick is held, and restarting
    /// on each of those is a character stuck on frame one.
    /// </summary>
    public void Play(string name, bool restart = false)
    {
        if ((name == Playing && !restart) || !Clips.ContainsKey(name))
        {
            return;
        }

        Playing = name;
        Since = 0;
    }

    public void Advance(double seconds) => Since += seconds;
}

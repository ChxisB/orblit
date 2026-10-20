using System.Diagnostics.CodeAnalysis;

namespace Orblit.Sequence;

/// <summary>
/// What a sequence has to say at one moment. It carries values rather than
/// effects: nothing here has touched an object, so the same frame can be
/// applied to a live scene, compared against another take, or thrown away.
/// </summary>
public sealed class SequenceFrame(double at)
{
    /// <summary>The moment it describes, in sequence time.</summary>
    public double At { get; } = at;

    /// <summary>
    /// Binding, then property, then the value, already mixed across every clip
    /// that had something to say about it.
    /// </summary>
    public OrderedDictionary<string, OrderedDictionary<string, object?>> Values { get; } = [];

    /// <summary>
    /// Bindings a sequence says should exist right now. Absent means not
    /// mentioned, which is not the same as false.
    /// </summary>
    public OrderedDictionary<string, bool> Active { get; } = [];

    /// <summary>Sounds that should be playing, and how far into each one the sequence has reached.</summary>
    public List<SoundAt> Sounds { get; } = [];

    /// <summary>
    /// Camera shots and how much say each has. Adjacent clips give one at a
    /// time, which is a cut. Overlapping clips give two, which is a blend.
    /// </summary>
    public List<ShotAt> Shots { get; } = [];

    /// <summary>Sub-sequences running inside this one, already resolved into this frame.</summary>
    public List<string> Running { get; } = [];

    public void Put(string binding, string property, object? value)
    {
        if (!Values.TryGetValue(binding, out var properties))
        {
            properties = [];
            Values[binding] = properties;
        }

        properties[property] = value;
    }

    public object? Get(string binding, string property) =>
        Values.TryGetValue(binding, out var properties) && properties.TryGetValue(property, out var value)
            ? value
            : null;
}

/// <summary>A sound the sequence wants playing, and where in it the playhead is.</summary>
public sealed class SoundAt(string sound, double at, double gain, string? binding = null)
{
    public string Sound { get; } = sound;

    public double At { get; } = at;

    public double Gain { get; } = gain;

    /// <summary>Where it is coming from, or null for a sound that is simply playing.</summary>
    public string? Binding { get; } = binding;
}

/// <summary>A camera the sequence is looking through, and how much.</summary>
public sealed class ShotAt(string camera, double weight)
{
    public string Camera { get; } = camera;

    public double Weight { get; } = weight;
}

/// <summary>Something that happened at a moment rather than over a span.</summary>
public sealed class Mark(double at, string name, object? payload = null)
{
    public double At { get; } = at;

    public string Name { get; } = name;

    /// <summary>
    /// Whatever the receiver needs. Untyped, because a sequence should not have
    /// to know what a door opening takes as an argument.
    /// </summary>
    public object? Payload { get; } = payload;
}

/// <summary>One row of a sequence.</summary>
public abstract class Track
{
    protected Track(string? name, string? binding, bool muted)
    {
        Name = name;
        Binding = binding;
        Muted = muted;
    }

    /// <summary>What it is called in an editor.</summary>
    public string? Name { get; }

    /// <summary>
    /// What it drives, resolved by whoever is applying the frame. A name rather
    /// than an object, because a sequence is an asset and the same cutscene
    /// plays on this scene's crate and on the next scene's.
    /// </summary>
    public string? Binding { get; }

    /// <summary>Off, but still in the sequence. What an editor toggles while working.</summary>
    public bool Muted { get; }

    /// <summary>When it stops having anything to say.</summary>
    public abstract double End { get; }

    /// <summary>Adds what it has to say at <paramref name="at"/> to <paramref name="frame"/>.</summary>
    public abstract void Contribute(double at, SequenceFrame frame);

    /// <summary>
    /// Every mark after <paramref name="from"/> and up to <paramref name="to"/>,
    /// in order. Only event tracks have any.
    /// </summary>
    public virtual IEnumerable<Mark> MarksIn(double from, double to) => [];

    /// <summary>The latest of the ends, or 0 for none.</summary>
    private protected static double Latest(IEnumerable<double> ends) =>
        ends.Aggregate(0.0, (most, end) => end > most ? end : most);
}

/// <summary>A clip that carries a curve.</summary>
public sealed class Keyed<T>(Clip clip, Channel<T> channel)
{
    public Clip Clip { get; } = clip;

    /// <summary>
    /// Sampled in the clip's own time, so trimming the front of a clip does not
    /// shift its keys and changing its speed does not rewrite them.
    /// </summary>
    public Channel<T> Channel { get; } = channel;
}

/// <summary>
/// A track that drives one named property. Generic so that a rotation is mixed
/// as a rotation. The frame it writes into is not, because whoever applies it
/// knows what a rotation is.
/// </summary>
public sealed class PropertyTrack<T> : Track
{
    // F2: six parameters, called by name as in Dart, and the last three are optional.
    public PropertyTrack(
        string property,
        IReadOnlyList<Keyed<T>> clips,
        IMixer<T> mixer,
        string? name = null,
        string? binding = null,
        bool muted = false)
        : base(name, binding, muted)
    {
        Property = property;
        Clips = clips;
        Mixer = mixer;
    }

    public string Property { get; }

    public IReadOnlyList<Keyed<T>> Clips { get; }

    public IMixer<T> Mixer { get; }

    public override double End => Latest(Clips.Select(one => one.Clip.End));

    /// <summary>
    /// The value at <paramref name="at"/>, or false where no clip covers it.
    /// Dart answers with null. Here it is a bool, because "this track says
    /// nothing here" and "this track says zero here" have to be different, and a
    /// <c>double</c> has no null.
    /// </summary>
    public bool TryValueAt(double at, [MaybeNullWhen(false)] out T value)
    {
        List<T>? values = null;
        List<double>? weights = null;
        foreach (var one in Clips)
        {
            var weight = one.Clip.WeightAt(at);
            if (weight <= 0)
            {
                continue;
            }

            (values ??= []).Add(one.Channel.At(one.Clip.LocalAt(at)));
            (weights ??= []).Add(weight);
        }

        if (values is null)
        {
            value = default;
            return false;
        }

        value = values.Count == 1 ? values[0] : Mixer.Mix(values, weights!);
        return true;
    }

    public override void Contribute(double at, SequenceFrame frame)
    {
        if (Muted || Binding is null || !TryValueAt(at, out var value))
        {
            return;
        }

        frame.Put(Binding, Property, value);
    }
}

/// <summary>A track that says whether the thing it is bound to is there.</summary>
public sealed class ActivationTrack(
    IReadOnlyList<Clip> clips,
    string? name = null,
    string? binding = null,
    bool muted = false) : Track(name, binding, muted)
{
    public IReadOnlyList<Clip> Clips { get; } = clips;

    public override double End => Latest(Clips.Select(one => one.End));

    /// <summary>
    /// Whether the thing should be there at <paramref name="at"/>, or null where
    /// the track has no opinion. Before the first clip and after the last, a
    /// finished sequence leaves the scene as it found it. The gaps between clips
    /// are the part where it does say off.
    /// </summary>
    public bool? ContributionAt(double at)
    {
        if (Clips.Count == 0)
        {
            return null;
        }

        var from = double.PositiveInfinity;
        var to = double.NegativeInfinity;
        var covered = false;
        foreach (var clip in Clips)
        {
            if (clip.Start < from)
            {
                from = clip.Start;
            }

            if (clip.End > to)
            {
                to = clip.End;
            }

            covered = covered || clip.Covers(at);
        }

        if (at < from || at >= to)
        {
            return null;
        }

        return covered;
    }

    public override void Contribute(double at, SequenceFrame frame)
    {
        if (Muted || Binding is null || ContributionAt(at) is not { } says)
        {
            return;
        }

        frame.Active[Binding] = says;
    }
}

/// <summary>
/// A track of things that happen rather than things that last. Marks are the
/// only part of a sequence that is not a function of the moment, because "it
/// happened" is about an interval. They come out of <see cref="MarksIn"/> rather
/// than out of a frame, which keeps a scrub backwards from firing a door open.
/// </summary>
public sealed class MarkTrack(
    IReadOnlyList<Mark> marks,
    string? name = null,
    string? binding = null,
    bool muted = false) : Track(name, binding, muted)
{
    /// <summary>In ascending order of <see cref="Mark.At"/>.</summary>
    public IReadOnlyList<Mark> Marks { get; } = marks;

    public override double End => Marks.Count == 0 ? 0 : Marks[^1].At;

    public override void Contribute(double at, SequenceFrame frame)
    {
    }

    public override IEnumerable<Mark> MarksIn(double from, double to)
    {
        if (Muted || to <= from)
        {
            yield break;
        }

        foreach (var mark in Marks)
        {
            if (mark.At > from && mark.At <= to)
            {
                yield return mark;
            }
        }
    }
}

/// <summary>A track of sounds.</summary>
public sealed class SoundTrack(
    IReadOnlyList<(Clip Clip, string Sound)> clips,
    double gain = 1.0,
    string? name = null,
    string? binding = null,
    bool muted = false) : Track(name, binding, muted)
{
    /// <summary>
    /// The clip's own time is where in the sound the playhead is, so trimming a
    /// clip's front starts the sound part-way in and slowing it down is a slowed sound.
    /// </summary>
    public IReadOnlyList<(Clip Clip, string Sound)> Clips { get; } = clips;

    public double Gain { get; } = gain;

    public override double End => Latest(Clips.Select(one => one.Clip.End));

    public override void Contribute(double at, SequenceFrame frame)
    {
        if (Muted)
        {
            return;
        }

        foreach (var (clip, sound) in Clips)
        {
            if (!clip.Covers(at))
            {
                continue;
            }

            frame.Sounds.Add(new SoundAt(sound, clip.LocalAt(at), Gain * clip.WeightAt(at), Binding));
        }
    }
}

/// <summary>
/// A track of camera shots. Two clips that touch are a cut and two that
/// overlap are a blend, which is the whole of the grammar. Nothing here decides
/// where a camera goes, only which one is looked through and how much.
/// </summary>
public sealed class ShotTrack(
    IReadOnlyList<(Clip Clip, string Camera)> shots,
    string? name = null,
    bool muted = false) : Track(name, null, muted)
{
    public IReadOnlyList<(Clip Clip, string Camera)> Shots { get; } = shots;

    public override double End => Latest(Shots.Select(one => one.Clip.End));

    public override void Contribute(double at, SequenceFrame frame)
    {
        if (Muted)
        {
            return;
        }

        foreach (var (clip, camera) in Shots)
        {
            var weight = clip.WeightAt(at);
            if (weight <= 0)
            {
                continue;
            }

            frame.Shots.Add(new ShotAt(camera, weight));
        }
    }
}

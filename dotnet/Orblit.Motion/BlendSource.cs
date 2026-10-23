using Orblit.Scene;

namespace Orblit.Motion;

/// <summary>
/// What a state of a blend plays: one clip, or clips mixed by the blend's inputs.
/// </summary>
/// <remarks>
/// A clip is named, never held, the way a scene's motion component names the clips it has: whoever plays the
/// blend hands over the clips by the same names. So one blend serves every character with the same set of
/// moves, and the file says which clips it wants without carrying them.
/// <para>
/// The mixes nest. A point on a line or a plane plays anything a state can, so running can be a plane of
/// directions at each of three speeds.
/// </para>
/// </remarks>
public abstract class BlendSource
{
    // Internal so the sources in this assembly are the only ones there are.
    internal BlendSource()
    {
    }

    /// <summary>Every clip this names, whether or not it has a say right now.</summary>
    public abstract IEnumerable<string> Clips { get; }

    /// <summary>Every input this reads.</summary>
    public abstract IEnumerable<string> Inputs { get; }

    /// <summary>
    /// What a state or a point in a file plays, or null with a note when it names nothing that can be played.
    /// <paramref name="subject"/> says whose it is, for the note.
    /// </summary>
    public static BlendSource? FromJson(JsonMap raw, string subject, List<string> problems)
    {
        if (Values.Text(raw, "clip") is { Length: > 0 } clip)
        {
            return new BlendClip(clip);
        }

        return raw.GetValueOrDefault("line") is JsonMap line ? BlendLine.Read(line, subject, problems)
            : raw.GetValueOrDefault("plane") is JsonMap plane ? BlendPlane.Read(plane, subject, problems)
            : raw.GetValueOrDefault("graph") is JsonMap graph ? ReadGraph(graph, subject, problems)
            : Nothing(subject, problems);
    }

    /// <summary>
    /// Every clip this plays and how much say each has, with <paramref name="read"/> giving an input's value,
    /// in the order they are written. Clips with no say are left out, and a clip named twice has the two says
    /// added together.
    /// </summary>
    public OrderedDictionary<string, double> Weigh(Func<string, double> read)
    {
        var weights = new OrderedDictionary<string, double>();
        AddWeights(read, 1, weights);
        return weights;
    }

    /// <summary>Decoded JSON, as a state or a point writes it: one of <c>clip</c>, <c>line</c>, <c>plane</c> or <c>graph</c>.</summary>
    public abstract OrderedDictionary<string, object?> ToJson();

    internal abstract void AddWeights(Func<string, double> read, double scale, OrderedDictionary<string, double> into);

    private static BlendGraph? ReadGraph(JsonMap graph, string subject, List<string> problems)
    {
        try
        {
            var loaded = BlendDocument.FromJson(graph);
            problems.AddRange(loaded.Problems);
            return new BlendGraph(loaded.Blend);
        }
        catch (BlendFormatException error)
        {
            problems.Add($"The graph in {subject} could not be read: {error.Message}");
            return null;
        }
    }

    private static BlendSource? Nothing(string subject, List<string> problems)
    {
        var capital = subject.Length == 0 ? subject : char.ToUpperInvariant(subject[0]) + subject[1..];
        problems.Add($"{capital} plays nothing: it names no clip, line or plane.");
        return null;
    }
}

/// <summary>
/// A graph inside a state. Its leaf states are addressed as <c>state/leaf</c>.
/// </summary>
/// <remarks>
/// Nested graphs are expanded when their owner is constructed. Playback still has one explicit place, and
/// entering the parent enters its start.
/// </remarks>
public sealed class BlendGraph(BlendDocument graph) : BlendSource
{
    public BlendDocument Graph { get; } = graph;

    public override IEnumerable<string> Clips => Graph.ClipNames;

    public override IEnumerable<string> Inputs => Graph.InputsRead;

    public override OrderedDictionary<string, object?> ToJson() => new() { ["graph"] = Graph.ToJson() };

    internal override void AddWeights(Func<string, double> read, double scale, OrderedDictionary<string, double> into)
    {
        // The document refuses a start that is not one of its states.
        Graph.StateNamed(Graph.Start)!.Plays.AddWeights(read, scale, into);
    }
}

/// <summary>One clip, played as it is.</summary>
public sealed class BlendClip(string clip) : BlendSource
{
    /// <summary>The clip's name, as whoever plays the blend knows it.</summary>
    public string Clip { get; } = clip;

    public override IEnumerable<string> Clips => [Clip];

    public override IEnumerable<string> Inputs => [];

    public override OrderedDictionary<string, object?> ToJson() => new() { ["clip"] = Clip };

    internal override void AddWeights(Func<string, double> read, double scale, OrderedDictionary<string, double> into)
    {
        if (scale > 0)
        {
            into[Clip] = (into.TryGetValue(Clip, out var before) ? before : 0) + scale;
        }
    }
}

using Orblit.Scene;
using Orblit.Sequence;

namespace Orblit.Motion;

/// <summary>
/// A way from one state to another: when it is taken, and how long the fade between the two takes.
/// </summary>
/// <remarks>
/// A change with no <see cref="From"/> is from anywhere, which is how an interruption is written: a jump
/// from whatever the character was doing. It is never taken into the state already being played, so it
/// does not restart it every frame its condition holds; a change from a state to itself, named, is how a
/// state restarts on purpose.
/// </remarks>
public sealed class BlendChange
{
    // F2: seven parameters, all but the first optional and called by name.
    public BlendChange(
        string to,
        string? from = null,
        BlendCondition? when = null,
        double fade = 0,
        Easing shape = Easing.Smooth,
        bool inStep = false,
        bool fromPose = false)
    {
        To = to;
        From = from;
        When = when ?? BlendCondition.Always;
        Fade = fade;
        Shape = shape;
        InStep = inStep;
        FromPose = fromPose;
    }

    /// <summary>The state it leaves, or null for any.</summary>
    public string? From { get; }

    /// <summary>The state it enters.</summary>
    public string To { get; }

    public BlendCondition When { get; }

    /// <summary>Seconds the fade takes. Nought is a cut.</summary>
    public double Fade { get; }

    /// <summary>How the fade eases. Smoothly unless told otherwise, because a fade is there not to be noticed.</summary>
    public Easing Shape { get; }

    /// <summary>
    /// Whether the state entered starts as far through its lap as the one left is through its own, so a
    /// walk turning into a run keeps its feet.
    /// </summary>
    public bool InStep { get; }

    /// <summary>Freeze the outgoing pose while the incoming clip keeps playing.</summary>
    public bool FromPose { get; }

    /// <summary>
    /// This change with whatever is given in place of what it has. A <paramref name="clearFrom"/> makes it
    /// a change from anywhere.
    /// </summary>
    // F2: eight parameters, all optional and called by name.
    public BlendChange CopyWith(
        string? from = null,
        bool clearFrom = false,
        string? to = null,
        BlendCondition? when = null,
        double? fade = null,
        Easing? shape = null,
        bool? inStep = null,
        bool? fromPose = null) => new(
        to ?? To,
        clearFrom ? null : from ?? From,
        when ?? When,
        fade ?? Fade,
        shape ?? Shape,
        inStep ?? InStep,
        fromPose ?? FromPose);

    public OrderedDictionary<string, object?> ToJson()
    {
        var condition = When.ToJson();
        return Values.Pruned(new OrderedDictionary<string, object?>
        {
            ["from"] = From,
            ["to"] = To,
            ["when"] = condition.Count == 0 ? null : condition,
            ["fade"] = Fade == 0 ? null : Fade,
            ["shape"] = Shape == Easing.Smooth ? null : FileNames.Of(Shape),
            ["inStep"] = InStep ? true : null,
            ["fromPose"] = FromPose ? true : null,
        });
    }
}

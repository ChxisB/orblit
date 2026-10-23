using Orblit.Sequence;

namespace Orblit.Motion;

/// <summary>How much say one clip has at a place, and how far through it is.</summary>
/// <param name="State">The state playing it.</param>
/// <param name="Clip">The clip's name.</param>
/// <param name="Weight">Its share of the whole. Every share at a place adds to one.</param>
/// <param name="Lap">How far through its state is.</param>
public sealed record ClipWeight(string State, string Clip, double Weight, double Lap);

/// <summary>One step of a blend: where it now is, and what happened getting there.</summary>
public sealed class BlendStep
{
    // F2: five parameters, all but the first two optional and called by name.
    public BlendStep(
        BlendPlace place,
        ClipFrame frame,
        IReadOnlyList<Mark>? marks = null,
        RootStep? moved = null,
        BlendChange? change = null)
    {
        Place = place;
        Frame = frame;
        Marks = marks ?? [];
        Moved = moved ?? new RootStep();
        Change = change;
    }

    public BlendPlace Place { get; }

    /// <summary>The clips where the blend now is, mixed, with root motion held in place.</summary>
    public ClipFrame Frame { get; }

    /// <summary>Every mark the state being played passed, in the order it passed them.</summary>
    public IReadOnlyList<Mark> Marks { get; }

    /// <summary>How far root motion carried the character, mixed as the clips are.</summary>
    public RootStep Moved { get; }

    /// <summary>The change taken at the end of the step, or null.</summary>
    public BlendChange? Change { get; }
}

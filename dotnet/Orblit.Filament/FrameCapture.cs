using Orblit.VectorMath;

namespace Orblit.Filament;

/// <summary>What one pass cost the last time the frame was drawn.</summary>
/// <param name="Name">The pass, as the graph named it.</param>
/// <param name="Milliseconds">What it cost.</param>
/// <param name="Draws">How many draw calls it submitted.</param>
public sealed record NamedPassTiming(string Name, double Milliseconds, int Draws)
{
    /// <summary>The pass as a row of its name, time and draws.</summary>
    public OrderedDictionary<string, object?> ToJson() => new()
    {
        ["name"] = Name,
        ["milliseconds"] = Milliseconds,
        ["draws"] = Draws,
    };
}

/// <summary>
/// What actually happened in a frame: which passes ran, in what order and what each cost. That is
/// the reason to declare passes and not hard code them, because a frame that says what it did can
/// be looked at.
/// </summary>
public sealed record FrameCapture
{
    public IReadOnlyList<NamedPassTiming> Passes { get; init; } = [];

    /// <summary>What the whole frame cost.</summary>
    public double Milliseconds { get; init; }

    /// <summary>
    /// How many objects the last publish put into a group big enough to merge. Nought with
    /// batching off, and nought with it on in a scene where nothing is repeated.
    /// </summary>
    public int BatchedObjects { get; init; }

    /// <summary>
    /// How many instanced renderables those objects came to once built. A group of up to
    /// sixty-four members is one, so the batched objects minus this is exactly the number of draws
    /// saved. A batched group is built as one renderable from the start, so unlike automatic
    /// instancing there is nothing left for a sort order to get in the way of.
    /// </summary>
    public int BatchGroups { get; init; }

    /// <summary>The pass that cost the most, or null if nothing ran. The first of equals wins.</summary>
    public NamedPassTiming? Slowest =>
        Passes.Count == 0 ? null : Passes.Aggregate((worst, pass) => pass.Milliseconds > worst.Milliseconds ? pass : worst);

    public int Draws => Passes.Sum(pass => pass.Draws);

    /// <summary>
    /// Reads a capture out of what the renderer sent back: two floats a pass, what it cost and how
    /// many draws it made, in the order the passes were scheduled. The names come from this side,
    /// so they never cross as strings on every frame.
    /// </summary>
    public static FrameCapture From(ReadOnlySpan<float> packed, IReadOnlyList<string> names, int batchedObjects = 0, int batchGroups = 0)
    {
        var passes = new List<NamedPassTiming>();
        for (var i = 0; i < names.Count && (i * 2) + 1 < packed.Length; i++)
        {
            passes.Add(new NamedPassTiming(names[i], packed[i * 2], (int)Doubles.Round(packed[(i * 2) + 1])));
        }

        return new FrameCapture
        {
            Passes = passes,
            Milliseconds = passes.Sum(pass => pass.Milliseconds),
            BatchedObjects = batchedObjects,
            BatchGroups = batchGroups,
        };
    }
}

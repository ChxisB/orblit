using Orblit.VectorMath;

namespace Orblit.Ui;

/// <summary>
/// The widths where a layout changes. A name such as <c>md</c> is active from its start width upward.
/// </summary>
public sealed record UiBreakpoints(double Sm = 640, double Md = 900, double Lg = 1280, double Xl = 1680)
{
    /// <summary>The names, smallest first when the starts keep their default order.</summary>
    public static IReadOnlyList<string> Names { get; } = ["sm", "md", "lg", "xl"];

    /// <summary>Where a name starts, or null for a name that is not a breakpoint.</summary>
    public double? StartOf(string name) => name switch
    {
        "sm" => Sm,
        "md" => Md,
        "lg" => Lg,
        "xl" => Xl,
        _ => null,
    };

    /// <summary>True for <c>sm</c>, <c>md</c>, <c>lg</c> and <c>xl</c>.</summary>
    public bool Knows(string name) => StartOf(name) is not null;

    /// <summary>
    /// The names that are active at a width, in the order they start. Two that start at the same width
    /// keep their default order. No width reaches a start that is not a number.
    /// </summary>
    public IReadOnlyList<string> ActiveAt(double width) =>
    [
        .. Names
            .Where(name => width >= StartOf(name))
            .OrderBy(name => StartOf(name)!.Value, Comparer<double>.Create(Doubles.Compare)),
    ];

    /// <summary>The last active name, or <c>base</c> when none is.</summary>
    public string LabelAt(double width) => ActiveAt(width) is [.., var last] ? last : "base";
}

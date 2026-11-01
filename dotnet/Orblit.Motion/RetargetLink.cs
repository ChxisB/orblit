namespace Orblit.Motion;

/// <summary>How one bone of the target is worked out from the source's bones.</summary>
/// <remarks>
/// The turn is <c>Left * Down⁻¹ * Up * Right</c>. <see cref="Up"/> is the source bones from below the shared
/// ancestor of the driver and the pivot down to the driver, outermost first, and <see cref="Down"/> the same
/// down to the pivot. The pivot is the source bone driving the nearest target ancestor that has one. Their
/// poses above the shared ancestor cancel, so nothing there is looked at.
/// </remarks>
// F2: five parameters, all of a record's parts, called by name.
internal sealed record RetargetLink(int Source, Quaternion Left, Quaternion Right, IReadOnlyList<int> Up, IReadOnlyList<int> Down)
{
    /// <summary>Every source bone whose turn changes this bone's.</summary>
    public IEnumerable<int> Involved => Up.Concat(Down).Distinct();
}

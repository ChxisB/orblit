namespace Orblit.Save;

/// <summary>
/// What <see cref="SaveTools.RestorePlanAsync"/> found: the versions it would write, and
/// the keys it left alone because a player is using them.
/// </summary>
public sealed record RestorePlan(IReadOnlyList<HistoryEntry> Changes, IReadOnlyList<string> Live);

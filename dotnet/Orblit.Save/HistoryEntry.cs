namespace Orblit.Save;

/// <summary>One kept version of a save. The bytes are in the history under <paramref name="Hash"/>.</summary>
public sealed record HistoryEntry(string Key, DateTimeOffset At, SaveReason Reason, string Hash);

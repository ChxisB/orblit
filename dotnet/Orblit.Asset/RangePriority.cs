namespace Orblit.Asset;

/// <summary>How soon a want is needed.</summary>
public enum RangePriority
{
    /// <summary>Someone is waiting on it. Sent before anything that can wait.</summary>
    Now,

    /// <summary>Wanted soon, with nobody waiting. Sent when no urgent want is queued.</summary>
    Later,
}

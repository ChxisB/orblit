namespace Orblit.Asset;

/// <summary>
/// How badly something is wanted. The queue takes <see cref="OnScreen"/> work before <see cref="Soon"/> and
/// <see cref="Soon"/> before <see cref="Eventually"/>, and within one level it takes what was asked for first.
/// That last part is not a detail: without it, a level with more work than slots starves whatever asked
/// earliest, and the thing nobody is waiting for finishes before the thing somebody is.
/// </summary>
public enum FetchUrgency
{
    /// <summary>Something being looked at now. A frame is worse for the want of it.</summary>
    OnScreen,

    /// <summary>The next room, the next level, the thing behind the door.</summary>
    Soon,

    /// <summary>Warming the cache. Should never delay either of the others.</summary>
    Eventually,
}

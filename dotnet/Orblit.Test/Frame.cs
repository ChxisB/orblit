namespace Orblit.Test;

/// <summary>One step of a game, as it hands it to its frame function.</summary>
/// <param name="Dt">Seconds since the last frame. The same every time.</param>
/// <param name="Now">Time since the stage began, as frames count it.</param>
public readonly record struct Frame(double Dt, TimeSpan Now);

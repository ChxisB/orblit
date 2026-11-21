namespace Orblit.Editor.Bridge;

/// <summary>
/// One signal of an entity: the class that declares it, or an empty string for the entity's script, its name, the
/// names of its arguments and the connections a save keeps.
/// </summary>
public sealed record EntitySignal(string Class, string Name, IReadOnlyList<string> Arguments, IReadOnlyList<SignalConnection> Links);

/// <summary>A saved connection of a signal: the object it calls and the method.</summary>
public sealed record SignalConnection(nint Target, string Method);

namespace Orblit.Editor.Bridge;

/// <summary>One metadata value of an entity. The value is a string, a bool, a double or a long.</summary>
public readonly record struct MetaEntry(string Name, object Value);

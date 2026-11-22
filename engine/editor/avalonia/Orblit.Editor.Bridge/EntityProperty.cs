namespace Orblit.Editor.Bridge;

/// <summary>
/// One property the inspector lists: the class that declares it, its inspector group or an empty string, its
/// engine name, its Variant type number and its value. The hint and its string are the engine's edit hints, such
/// as an enum's choices or a range and its unit.
/// </summary>
public sealed record EntityProperty(string Class, string Group, string Name, int Type, object? Value)
{
    public int Hint { get; init; }

    public string HintString { get; init; } = "";
}

namespace Orblit.Filament;

/// <summary>An enum member as the rows spell it: PascalCase becomes camelCase.</summary>
// F1: Orblit.Scene and Orblit.Mesh carry the same helper, and a shared home is a package of its own.
internal static class EnumNames
{
    internal static string Of<T>(T value)
        where T : struct, Enum
    {
        var name = value.ToString();
        return char.ToLowerInvariant(name[0]) + name[1..];
    }
}

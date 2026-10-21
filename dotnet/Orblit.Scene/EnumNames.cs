namespace Orblit.Scene;

/// <summary>An enum member as Dart's <c>name</c> spells it: PascalCase becomes camelCase.</summary>
internal static class EnumNames
{
    internal static string Of<T>(T value)
        where T : struct, Enum
    {
        var name = value.ToString();
        return char.ToLowerInvariant(name[0]) + name[1..];
    }
}

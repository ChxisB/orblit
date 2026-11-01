namespace Orblit.Mesh;

/// <summary>An enum member as a file spells it: PascalCase becomes camelCase.</summary>
internal static class EnumNames
{
    internal static string Of<T>(T value)
        where T : struct, Enum
    {
        var name = value.ToString();
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    /// <summary>The member whose name is <paramref name="text"/>, or <paramref name="fallback"/> when none is.</summary>
    internal static T Parse<T>(object? text, T fallback)
        where T : struct, Enum
    {
        foreach (var member in Enum.GetValues<T>())
        {
            if (text is string name && Of(member) == name)
            {
                return member;
            }
        }

        return fallback;
    }
}

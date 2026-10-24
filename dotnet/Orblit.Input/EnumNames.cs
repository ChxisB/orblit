namespace Orblit.Input;

/// <summary>An enum member as a profile file spells it: PascalCase becomes camelCase.</summary>
internal static class EnumNames
{
    internal static string Of<T>(T value)
        where T : struct, Enum
    {
        var name = value.ToString();
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    /// <summary>The member whose name is <paramref name="name"/>, or null when none is.</summary>
    internal static T? Find<T>(string name)
        where T : struct, Enum
    {
        foreach (var member in Enum.GetValues<T>())
        {
            if (Of(member) == name)
            {
                return member;
            }
        }

        return null;
    }
}

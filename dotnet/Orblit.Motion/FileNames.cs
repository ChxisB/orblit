namespace Orblit.Motion;

/// <summary>An enum member as a clip or cutscene file spells it: PascalCase becomes camelCase.</summary>
internal static class FileNames
{
    internal static string Of<T>(T value)
        where T : struct, Enum
    {
        var name = value.ToString();
        var spelt = char.ToLowerInvariant(name[0]) + name[1..];

        // Every file written so far spells the easing In as in_, so it stays that way.
        return spelt == "in" ? "in_" : spelt;
    }

    /// <summary>
    /// One of an enum's members by its name in a file, or null when the name is not one. Null rather
    /// than a default, so a caller can tell a name it does not know from no name at all.
    /// </summary>
    internal static T? Named<T>(object? name)
        where T : struct, Enum
    {
        if (name is not string text)
        {
            return null;
        }

        foreach (var value in Enum.GetValues<T>())
        {
            if (Of(value) == text)
            {
                return value;
            }
        }

        return null;
    }
}

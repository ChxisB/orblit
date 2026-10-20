using System.Text.RegularExpressions;

namespace Orblit.Rig;

/// <summary>
/// What a bone is for. A generated rig holds far more bones than a skeleton does, and they are told
/// apart by a prefix rather than by a flag. That looks like a hack until you have to debug one:
/// every bone announces its job in the outliner, in an error message, and in a file diff, without
/// anything having to be loaded.
/// </summary>
public enum BoneRole
{
    /// <summary>What an animator grabs. No prefix.</summary>
    Control,

    /// <summary>
    /// The original skeleton, copied from the source rig. Drives nothing on its own. It exists so a
    /// generated rig can be regenerated against what it came from.
    /// </summary>
    Original,

    /// <summary>What the mesh is bound to. The only bones skinning ever sees.</summary>
    Deform,

    /// <summary>Hidden plumbing: the bones that wire a control to a deform without either knowing about the other.</summary>
    Mechanism,
}

public static class BoneRoles
{
    /// <summary>The prefix a bone of this role carries.</summary>
    public static string Prefix(this BoneRole role) => role switch
    {
        BoneRole.Control => string.Empty,
        BoneRole.Original => "ORG-",
        BoneRole.Deform => "DEF-",
        BoneRole.Mechanism => "MCH-",
        _ => throw new InvalidOperationException($"{role} is not a bone role."),
    };
}

/// <summary>Which side of the body a bone is on.</summary>
public enum Side
{
    Left,
    Right,
}

public static class Sides
{
    public static string Suffix(this Side side) => side switch
    {
        Side.Left => ".L",
        Side.Right => ".R",
        _ => throw new InvalidOperationException($"{side} is not a side."),
    };

    public static Side Opposite(this Side side) => side == Side.Left ? Side.Right : Side.Left;
}

/// <summary>
/// Reading and writing the names a generated rig uses. Conventions rather than data, so two tools
/// that never speak can still agree, which is the point of a convention.
/// </summary>
public static partial class BoneNaming
{
    // The tail of a name Blender gave a duplicate. \z and [0-9] because .NET's $ also matches
    // before a final newline and its \d matches digits outside ASCII, where Dart's do not.
    [GeneratedRegex(@"\.[0-9]{3}\z")]
    private static partial Regex Numeric();

    /// <summary>
    /// <paramref name="names"/>, made fit to be bone names: none empty and no two alike.
    /// A skeleton read from a file is free to leave a joint unnamed or to name two alike, and an
    /// armature is not: bones are found by name, so two answering to one would be a rig where
    /// posing one moves the other. An unnamed joint is called <c>joint</c> and its position in the
    /// list. A name already taken gets <c>.001</c>, <c>.002</c> and so on, which is what an
    /// artist's tools do to a duplicated bone and so what they will recognise. The first with a
    /// name keeps it, whatever comes after, so a renamed joint never takes a name from another.
    /// One function for everything that names joints, so an imported clip and the model it plays on
    /// agree about which bone is which.
    /// </summary>
    public static List<string> Unique(IReadOnlyList<string> names)
    {
        var taken = new HashSet<string>(StringComparer.Ordinal);
        var result = new string?[names.Count];

        // Every name given is claimed before anything is renamed, so a renamed joint cannot take
        // the name of one that comes after it.
        for (var at = 0; at < names.Count; at++)
        {
            var name = names[at];
            if (name.Length > 0 && taken.Add(name))
            {
                result[at] = name;
            }
        }

        for (var at = 0; at < names.Count; at++)
        {
            if (result[at] is not null)
            {
                continue;
            }

            var baseName = names[at].Length == 0 ? $"joint {at}" : names[at];
            var name = baseName;
            for (var copy = 1; !taken.Add(name); copy++)
            {
                name = $"{baseName}.{copy.ToString(System.Globalization.CultureInfo.InvariantCulture).PadLeft(3, '0')}";
            }

            result[at] = name;
        }

        // An entry is filled by one of the two passes above, so none is null here.
        return [.. result.Select(name => name!)];
    }

    /// <summary>What a bone is for, from its prefix.</summary>
    public static BoneRole RoleOf(string name)
    {
        foreach (var role in Enum.GetValues<BoneRole>())
        {
            var prefix = role.Prefix();
            if (prefix.Length > 0 && name.StartsWith(prefix, StringComparison.Ordinal))
            {
                return role;
            }
        }

        return BoneRole.Control;
    }

    /// <summary>
    /// Which side a bone is on, or null if it is on the midline. A duplicate suffix is stripped
    /// first, so <c>hand.L.001</c> is still a left hand: Blender appends the number after the side,
    /// and something that only checked the end of the string would call it sideless.
    /// </summary>
    public static Side? SideOf(string name)
    {
        var trimmed = Numeric().Replace(name, string.Empty);
        foreach (var side in Enum.GetValues<Side>())
        {
            if (trimmed.EndsWith(side.Suffix(), StringComparison.Ordinal))
            {
                return side;
            }
        }

        return null;
    }

    /// <summary>The name with its role prefix and side suffix taken off.</summary>
    public static string BaseOf(string name)
    {
        var result = Numeric().Replace(name, string.Empty);

        var prefix = RoleOf(result).Prefix();
        if (prefix.Length > 0)
        {
            result = result[prefix.Length..];
        }

        var side = SideOf(result);
        if (side is not null)
        {
            result = result[..^side.Value.Suffix().Length];
        }

        return result;
    }

    /// <summary>Builds a name from its parts.</summary>
    public static string Compose(string baseName, BoneRole role = BoneRole.Control, Side? side = null) =>
        $"{role.Prefix()}{baseName}{side?.Suffix() ?? string.Empty}";

    /// <summary>The same bone, in the other role.</summary>
    public static string AsRole(string name, BoneRole role) => Compose(BaseOf(name), role, SideOf(name));

    /// <summary>
    /// The bone on the other side of the body, or null for one on the midline. What every mirroring
    /// tool needs, and the reason sides are a suffix rather than a naming habit.
    /// </summary>
    public static string? Mirror(string name)
    {
        var side = SideOf(name);
        return side is null ? null : Compose(BaseOf(name), RoleOf(name), side.Value.Opposite());
    }

    /// <summary>Whether two names describe the same bone on opposite sides.</summary>
    public static bool AreMirrors(string a, string b) => a != b && Mirror(a) == b;
}

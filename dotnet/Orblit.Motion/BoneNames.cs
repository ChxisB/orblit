using System.Text.RegularExpressions;
using Orblit.Rig;

namespace Orblit.Motion;

/// <summary>Pairing the bones of two skeletons by what their names say.</summary>
public static partial class BoneNames
{
    private static readonly char[] Namespaces = [':', '|'];

    // Full case mapping turns this into an i and a combining dot, and the dot goes with the punctuation. The
    // invariant culture leaves it alone, so it is mapped by hand.
    private const char CapitalDottedI = (char)0x130;

    // \z and not $, which in .NET also matches before a final newline.
    [GeneratedRegex(@"^(left|right)[_ -]?|[_ -]?(left|right)\z|[_ -]([lr])\z")]
    private static partial Regex SideWord();

    [GeneratedRegex("[^a-z0-9]")]
    private static partial Regex Punctuation();

    /// <summary>
    /// What is left of a bone's name once the things that vary between tools are taken off, and which side it
    /// is on.
    /// </summary>
    /// <remarks>
    /// Off go a namespace such as <c>mixamorig:</c>, case, punctuation, and the side however it is written:
    /// <c>.L</c>, <c>_L</c> or <c>Left</c>. The side is <c>L</c>, <c>R</c> or null.
    /// </remarks>
    public static (string Base, string? Side) CleanName(string name)
    {
        var bare = name[(name.LastIndexOfAny(Namespaces) + 1)..];
        var side = BoneNaming.SideOf(bare)?.Suffix()[1..];
        var stem = BoneNaming.BaseOf(bare).Replace(CapitalDottedI, 'i').ToLowerInvariant();
        var word = SideWord().Match(stem);
        if (word.Success)
        {
            side ??= SideLetter(word);
            stem = stem.Remove(word.Index, word.Length);
        }

        return (Punctuation().Replace(stem, string.Empty), side);
    }

    /// <summary>Which bone of <paramref name="from"/> moves each bone of <paramref name="to"/>, by name.</summary>
    /// <remarks>
    /// <para>
    /// A bone with the same name in both is matched first. The rest are matched by what is left of the name
    /// once the things that vary between tools are taken off: a namespace such as <c>mixamorig:</c>, a
    /// <c>DEF-</c> or <c>ORG-</c> prefix, case, punctuation, and which side it is on, whether written
    /// <c>.L</c>, <c>_L</c> or <c>Left</c>. So <c>mixamorig:LeftHand</c> and <c>hand.L</c> are the same bone.
    /// </para>
    /// <para>
    /// A name that two bones of one skeleton share once cleaned matches nothing, since guessing would drive a
    /// bone from the wrong side of the body. The bones this leaves out are the ones to name by hand when a clip
    /// is retargeted, as target bone to source bone.
    /// </para>
    /// </remarks>
    public static OrderedDictionary<string, string> Match(RestSkeleton from, RestSkeleton to)
    {
        var matched = new OrderedDictionary<string, string>();
        foreach (var name in to.Names.Where(name => from.IndexOf(name) is not null))
        {
            matched[name] = name;
        }

        var taken = matched.Values.ToHashSet();
        var sources = ByKey(from.Names.Where(name => !taken.Contains(name)));
        var targets = ByKey(to.Names.Where(name => !matched.ContainsKey(name)));
        foreach (var (key, target) in targets)
        {
            if (sources.TryGetValue(key, out var source))
            {
                matched[target] = source;
            }
        }

        return matched;
    }

    private static string SideLetter(Match word) =>
        (word.Groups[1].Success ? word.Groups[1] : word.Groups[2].Success ? word.Groups[2] : word.Groups[3]).Value[..1]
            .ToUpperInvariant();

    /// <summary><paramref name="names"/> by their cleaned name, without any that share one.</summary>
    private static OrderedDictionary<string, string> ByKey(IEnumerable<string> names)
    {
        var seen = new OrderedDictionary<string, string>();
        var shared = new HashSet<string>();
        foreach (var name in names)
        {
            var key = KeyOf(name);
            if (seen.ContainsKey(key))
            {
                shared.Add(key);
            }

            seen[key] = name;
        }

        return new OrderedDictionary<string, string>(seen.Where(pair => !shared.Contains(pair.Key)));
    }

    private static string KeyOf(string name)
    {
        var (stem, side) = CleanName(name);
        return $"{stem}/{side}";
    }
}

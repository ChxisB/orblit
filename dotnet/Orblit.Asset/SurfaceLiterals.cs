using System.Text.RegularExpressions;
using static Orblit.Asset.DartPattern;

namespace Orblit.Asset;

/// <summary>The hints and defaults an author writes after a uniform's name.</summary>
internal static partial class SurfaceLiterals
{
    private static readonly HashSet<string> KnownHints =
    [
        "source_color",
        "hint_range",
        "hint_enum",
        "hint_normal",
        "hint_default_white",
        "hint_default_black",
        "hint_default_transparent",
        "filter_nearest",
        "filter_linear",
        "filter_nearest_mipmap",
        "filter_linear_mipmap",
        "repeat_enable",
        "repeat_disable",
    ];

    [GeneratedRegex($"^({Word}+){Space}*(?:\\(({Any}*)\\))?\\z")]
    private static partial Regex HintShape();

    /// <summary><c>hint_range(0, 1), source_color</c> to its hints, splitting only on commas outside brackets.</summary>
    internal static OrderedDictionary<string, IReadOnlyList<string>> ParseHints(string text, SurfaceOrigin at)
    {
        var hints = new OrderedDictionary<string, IReadOnlyList<string>>();
        foreach (var piece in SplitTopLevel(text))
        {
            var match = HintShape().Match(DartText.Trim(piece));
            if (!match.Success)
            {
                throw new SurfaceException(at, $"cannot read hint `{piece}`");
            }

            var name = match.Groups[1].Value;
            if (!KnownHints.Contains(name))
            {
                throw new SurfaceException(at, $"unknown hint `{name}`");
            }

            hints[name] = match.Groups[2].Success ? SplitTopLevel(match.Groups[2].Value) : [];
        }

        return hints;
    }

    /// <summary>Splits on commas that are not inside brackets, trimmed.</summary>
    internal static List<string> SplitTopLevel(string text)
    {
        var parts = new List<string>();
        var depth = 0;
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '(')
            {
                depth++;
            }

            if (c == ')')
            {
                depth--;
            }

            if (c == ',' && depth == 0)
            {
                parts.Add(DartText.Trim(text[start..i]));
                start = i + 1;
            }
        }

        parts.Add(DartText.Trim(text[start..]));
        return parts;
    }

    /// <summary>
    /// A default written as a literal: <c>1.5</c>, <c>true</c>, <c>vec3(0.5)</c> or <c>vec4(1.0, 0.5, 0.2, 1.0)</c>.
    /// A single argument fills every component. A whole number stays a <see cref="long"/> and a fraction is a
    /// <see cref="double"/>, as Dart's <c>num</c> keeps them.
    /// </summary>
    internal static object ParseDefault(SurfaceType type, string text, SurfaceOrigin at)
    {
        var value = DartText.Trim(text);
        if (type == SurfaceType.Boolean)
        {
            return ParseBoolean(value, at);
        }

        var width = type.Width();
        var inner = width == 1 ? value : ConstructorArgs(type, value, at);
        var numbers = SplitTopLevel(inner).Select(part => ParseNumber(part, at)).ToList();
        if (width == 1 && numbers.Count == 1)
        {
            return numbers[0];
        }

        if (numbers.Count == 1)
        {
            return Enumerable.Repeat(numbers[0], width).ToList();
        }

        if (numbers.Count != width)
        {
            throw new SurfaceException(at, $"{type.Word()} needs {width} numbers");
        }

        return numbers;
    }

    private static bool ParseBoolean(string value, SurfaceOrigin at) => value switch
    {
        "true" => true,
        "false" => false,
        _ => throw new SurfaceException(at, "a bool default is `true` or `false`"),
    };

    private static string ConstructorArgs(SurfaceType type, string value, SurfaceOrigin at)
    {
        var prefix = type.Word() + "(";
        if (!value.StartsWith(prefix, StringComparison.Ordinal) || !value.EndsWith(')'))
        {
            throw new SurfaceException(at, $"a {type.Word()} default is `{prefix}...)`");
        }

        return value[prefix.Length..^1];
    }

    // A trailing f is GLSL's float suffix, and Dart's number reader does not know it.
    private static object ParseNumber(string text, SurfaceOrigin at) =>
        DartText.ParseNum(text.EndsWith('f') || text.EndsWith('F') ? text[..^1] : text)
        ?? throw new SurfaceException(at, $"cannot read `{text}` as a number");
}

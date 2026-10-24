using System.Text.RegularExpressions;

namespace Orblit.Filament.Tests;

/// <summary>
/// <c>orblit_renderer.h</c> read as data: the functions with their parameters
/// and the enums with their values. The C# declarations are checked against
/// this, so a function added to the header or a parameter moved fails a test.
/// </summary>
internal static partial class RendererHeader
{
    internal sealed record Parameter(string Name, bool IsPointer);

    internal sealed record Function(string Entry, bool ReturnsNothing, IReadOnlyList<Parameter> Parameters);

    internal sealed record Member(string Name, long Value);

    private static readonly string Text = BlockComment().Replace(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "orblit_renderer.h")),
        string.Empty);

    internal static IReadOnlyList<Function> Functions() =>
        [.. FunctionDeclaration().Matches(Text).Select(match => new Function(
            match.Groups["name"].Value,
            match.Groups["return"].Value.Trim() == "void",
            [.. ParameterList(match.Groups["parameters"].Value)]))];

    /// <summary>The members of the enum <paramref name="cName"/>, with the values C gives them.</summary>
    internal static IReadOnlyList<Member> Enum(string cName)
    {
        var body = EnumDeclaration().Matches(Text).Single(match => match.Groups["name"].Value == cName).Groups["body"].Value;
        var members = new List<Member>();
        var next = 0L;
        foreach (var item in body.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = item.Split('=', StringSplitOptions.TrimEntries);
            var value = parts.Length == 2 ? Evaluate(parts[1]) : next;
            members.Add(new Member(parts[0], value));
            next = value + 1;
        }

        return members;
    }

    // The header writes a value as a number or as one shifted left.
    private static long Evaluate(string expression)
    {
        var shift = expression.Split("<<", StringSplitOptions.TrimEntries);
        return shift.Length == 2 ? long.Parse(shift[0]) << int.Parse(shift[1]) : long.Parse(expression);
    }

    private static IEnumerable<Parameter> ParameterList(string text)
    {
        foreach (var declaration in text.Split(',', StringSplitOptions.TrimEntries))
        {
            var name = ParameterName().Match(declaration).Groups["name"].Value;
            yield return new Parameter(name, declaration.Contains('*') || declaration.Contains('['));
        }
    }

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex BlockComment();

    [GeneratedRegex(@"^(?<return>[A-Za-z_][\w\s\*]*?)\b(?<name>orblit_renderer_\w+)\s*\((?<parameters>[^)]*)\)\s*;", RegexOptions.Multiline)]
    private static partial Regex FunctionDeclaration();

    [GeneratedRegex(@"typedef enum \w+ \{(?<body>[^}]*)\} (?<name>\w+);")]
    private static partial Regex EnumDeclaration();

    [GeneratedRegex(@"(?<name>\w+)\s*(\[\d*\])?$")]
    private static partial Regex ParameterName();
}

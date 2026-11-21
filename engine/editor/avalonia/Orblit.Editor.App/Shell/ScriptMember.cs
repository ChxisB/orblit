using System.Text.RegularExpressions;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// A member a script declares, as its outline lists it: the name, the word beside it, the icon, and where the name
/// starts in the text.
/// </summary>
internal sealed partial record ScriptMember(string Name, string Detail, string Icon, int Index)
{
    // The words a type may be although they are keywords.
    static readonly HashSet<string> s_builtInTypes =
        ["bool", "byte", "char", "decimal", "double", "float", "int", "long", "object", "sbyte", "short", "string", "uint", "ulong", "ushort", "void"];

    /// <summary>
    /// The methods, properties and fields of the text, in order. A field needs a word such as public in front, so a
    /// local variable stays out. It reads a line at a time, so a declaration split over lines may be missed.
    /// </summary>
    public static List<ScriptMember> Read(string text) =>
        [.. Declaration().Matches(text).Where(IsMember).Select(Of)];

    static bool IsMember(Match match)
    {
        var type = match.Groups["type"].Value;
        var name = match.Groups["name"].Value;
        var field = match.Groups["end"].Value is not ("(" or "{" or "=>");
        return (!CodeColours.IsKeyword(type) || s_builtInTypes.Contains(type))
            && !CodeColours.IsKeyword(name)
            && !(field && match.Groups["mods"].Value == "");
    }

    static ScriptMember Of(Match match)
    {
        var mods = match.Groups["mods"].Value.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        var type = match.Groups["type"].Value;
        var name = match.Groups["name"];
        var (icon, detail) = match.Groups["end"].Value switch
        {
            "(" when mods.Contains("override") => ("MethodOverride", "override"),
            "(" => ("MemberMethod", type),
            _ when mods.Contains("const") => ("MemberConstant", type),
            _ => ("MemberProperty", type),
        };
        return new ScriptMember(name.Value, detail, icon, name.Index);
    }

    // Attributes in square brackets, words such as public, a type, a name, then a bracket, a brace, an arrow, an equals sign, a semicolon or the end
    // of the line.
    [GeneratedRegex(@"^[ \t]*(?:\[[^\]\n]*\][ \t]*)*(?<mods>(?:(?:public|private|protected|internal|static|readonly|const|override|virtual|abstract|sealed|async|partial|new|extern|unsafe|volatile|required)[ \t]+)*)(?<type>[A-Za-z_][\w<>\[\],.?]*)[ \t]+(?<name>[A-Za-z_]\w*)[ \t]*(?<end>\(|\{|=>|=|;|\r?$)", RegexOptions.Multiline)]
    private static partial Regex Declaration();
}

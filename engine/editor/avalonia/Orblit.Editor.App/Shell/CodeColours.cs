namespace Orblit.Editor.App.Shell;

/// <summary>What a piece of C# text is, which sets its colour.</summary>
internal enum CodeKind
{
    Plain,
    Keyword,
    Type,
    String,
    Number,
    Comment,
    Function,
    Attribute,
}

/// <summary>A stretch of text of one kind, by where it starts and how long it is.</summary>
internal readonly record struct CodeSpan(int Start, int Length, CodeKind Kind);

/// <summary>
/// Splits C# text into stretches to colour. It reads words and their neighbors, not the grammar. A type is a
/// capital word in a place a type stands, and a word before a bracket is a function.
/// </summary>
internal static class CodeColours
{
    // The keywords of C# and the contextual ones a script uses. A list of cases, so it is data.
    static readonly HashSet<string> s_keywords =
    [
        "abstract", "as", "async", "await", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
        "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else", "enum", "event",
        "explicit", "extern", "false", "finally", "fixed", "float", "for", "foreach", "get", "goto", "if",
        "implicit", "in", "init", "int", "interface", "internal", "is", "lock", "long", "namespace", "new", "not",
        "null", "object", "operator", "or", "and", "out", "override", "params", "partial", "private", "protected",
        "public", "readonly", "record", "ref", "required", "return", "sbyte", "sealed", "set", "short", "sizeof",
        "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true", "try", "typeof", "uint",
        "ulong", "unchecked", "unsafe", "ushort", "using", "var", "virtual", "void", "volatile", "when", "where",
        "while", "with", "yield",
    ];

    // The words after which a capital word names a type.
    static readonly HashSet<string> s_typeAfter = ["class", "struct", "interface", "enum", "record", "new", "is", "as"];

    public static bool IsKeyword(string word) => s_keywords.Contains(word);

    /// <summary>The stretches of the text in order, with no gaps. Neighbors of one kind are joined.</summary>
    public static List<CodeSpan> Read(string text)
    {
        var spans = new List<CodeSpan>();
        var at = 0;
        while (at < text.Length)
        {
            var (length, kind) = Next(text, at);
            if (spans.Count > 0 && spans[^1].Kind == kind)
            {
                spans[^1] = spans[^1] with { Length = spans[^1].Length + length };
            }
            else
            {
                spans.Add(new CodeSpan(at, length, kind));
            }

            at += length;
        }

        return spans;
    }

    static (int Length, CodeKind Kind) Next(string text, int at)
    {
        var c = text[at];
        if (c == '/' && At(text, at + 1) is '/' or '*')
        {
            return (CommentEnd(text, at) - at, CodeKind.Comment);
        }

        if (IsStringStart(text, at))
        {
            return (StringEnd(text, at) - at, CodeKind.String);
        }

        if (char.IsAsciiDigit(c))
        {
            return (WordEnd(text, at) - at, CodeKind.Number);
        }

        if (char.IsLetter(c) || c == '_')
        {
            var end = WordEnd(text, at);
            return (end - at, WordKind(text, at, end));
        }

        return (1, CodeKind.Plain);
    }

    // The character at a place, or a null character past either end.
    static char At(string text, int at) => at >= 0 && at < text.Length ? text[at] : '\0';

    static int CommentEnd(string text, int at)
    {
        var line = At(text, at + 1) == '/';
        var end = text.IndexOf(line ? "\n" : "*/", at + 2, StringComparison.Ordinal);
        return end < 0 ? text.Length : line ? end : end + 2;
    }

    // A quote, or one or two of @ and $ before a double quote.
    static bool IsStringStart(string text, int at)
    {
        var quote = at;
        while (At(text, quote) is '@' or '$' && quote - at < 2)
        {
            quote++;
        }

        return At(text, quote) == '"' || (quote == at && text[at] == '\'');
    }

    // A plain string ends at its quote or at the end of the line. A verbatim one runs on, and two quotes are one.
    static int StringEnd(string text, int at)
    {
        var verbatim = false;
        while (text[at] is '@' or '$')
        {
            verbatim |= text[at] == '@';
            at++;
        }

        var quote = text[at];
        var i = at + 1;
        while (i < text.Length && !(text[i] == '\n' && !verbatim))
        {
            if (text[i] == quote && !(verbatim && At(text, i + 1) == quote))
            {
                return i + 1;
            }

            // A backslash takes the next character with it, and so does a doubled quote.
            i += (text[i] == '\\' && !verbatim) || text[i] == quote ? 2 : 1;
        }

        return Math.Min(i, text.Length);
    }

    static int WordEnd(string text, int at)
    {
        var end = at;
        while (end < text.Length && (char.IsLetterOrDigit(text[end]) || text[end] == '_' || (text[end] == '.' && char.IsAsciiDigit(text[at]))))
        {
            end++;
        }

        return end;
    }

    static CodeKind WordKind(string text, int at, int end)
    {
        if (s_keywords.Contains(text[at..end]))
        {
            return CodeKind.Keyword;
        }

        var before = Before(text, at);
        var after = After(text, end);
        if (before == '[' && IsLineStart(text, Back(text, at)))
        {
            return CodeKind.Attribute;
        }

        if (char.IsUpper(text[at]) && IsTypePlace(text, at, before, after))
        {
            return CodeKind.Type;
        }

        return after == '(' ? CodeKind.Function : CodeKind.Plain;
    }

    // After class or new, after a colon or an angle bracket, or before a name, a dot or an angle bracket.
    static bool IsTypePlace(string text, int at, char before, char after) =>
        before is ':' or '<' || after is '.' or '<' || char.IsLetter(after) || after == '_' || s_typeAfter.Contains(WordBefore(text, at));

    // The place of the last character before this one that is not a space.
    static int Back(string text, int at)
    {
        var back = at - 1;
        while (back >= 0 && text[back] is ' ' or '\t')
        {
            back--;
        }

        return back;
    }

    static char Before(string text, int at) => At(text, Back(text, at));

    static char After(string text, int end)
    {
        while (At(text, end) is ' ' or '\t')
        {
            end++;
        }

        return At(text, end);
    }

    static string WordBefore(string text, int at)
    {
        var end = Back(text, at) + 1;
        var start = end;
        while (start > 0 && char.IsLetter(text[start - 1]))
        {
            start--;
        }

        return text[start..end];
    }

    // Whether only spaces stand between the start of the line and this place.
    static bool IsLineStart(string text, int at) => Back(text, at) < 0 || At(text, Back(text, at)) is '\n' or '\r';
}

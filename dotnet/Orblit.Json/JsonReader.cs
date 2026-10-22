using System.Globalization;
using System.Text;

namespace Orblit.Json;

internal sealed class JsonReader(string text)
{
    private int _at;

    internal object? ReadAll()
    {
        var value = ReadValue(0);
        SkipSpace();
        return _at < text.Length ? throw Fail("Unexpected character") : value;
    }

    private FormatException Fail(string reason) => new($"{reason} at {_at}.");

    // The format's whitespace is these four and nothing else, so a byte-order mark or a vertical tab is refused.
    private void SkipSpace()
    {
        while (_at < text.Length && text[_at] is ' ' or '\t' or '\r' or '\n')
        {
            _at++;
        }
    }

    private object? ReadValue(int depth)
    {
        SkipSpace();
        if (_at >= text.Length)
        {
            throw Fail("Unexpected end of input");
        }

        return text[_at] switch
        {
            '{' => ReadObject(depth),
            '[' => ReadArray(depth),
            '"' => ReadString(),
            't' => ReadWord("true", true),
            'f' => ReadWord("false", false),
            'n' => ReadWord("null", null),
            '-' or (>= '0' and <= '9') => ReadNumber(),
            _ => throw Fail("Unexpected character"),
        };
    }

    private object? ReadWord(string word, object? value)
    {
        if (string.CompareOrdinal(text, _at, word, 0, word.Length) != 0)
        {
            throw Fail(_at + word.Length > text.Length ? "Unexpected end of input" : "Unexpected character");
        }

        _at += word.Length;
        return value;
    }

    private void Enter(int depth)
    {
        if (depth >= JsonText.MaxDepth)
        {
            throw Fail("Nested too deep");
        }
    }

    private OrderedDictionary<string, object?> ReadObject(int depth)
    {
        Enter(depth);
        var map = new OrderedDictionary<string, object?>();
        _at++;
        SkipSpace();
        if (Peek() == '}')
        {
            _at++;
            return map;
        }

        while (true)
        {
            SkipSpace();
            if (Peek() != '"')
            {
                throw Fail("Unexpected character");
            }

            var key = ReadString();
            SkipSpace();
            Expect(':');
            map[key] = ReadValue(depth + 1);
            SkipSpace();
            if (!Continues('}'))
            {
                return map;
            }
        }
    }

    private List<object?> ReadArray(int depth)
    {
        Enter(depth);
        var list = new List<object?>();
        _at++;
        SkipSpace();
        if (Peek() == ']')
        {
            _at++;
            return list;
        }

        while (true)
        {
            list.Add(ReadValue(depth + 1));
            SkipSpace();
            if (!Continues(']'))
            {
                return list;
            }
        }
    }

    // After an item: a comma means another follows, the closer ends the collection, anything else is an error.
    private bool Continues(char closer)
    {
        var c = Peek();
        if (c == ',')
        {
            _at++;
            return true;
        }

        Expect(closer);
        return false;
    }

    private char Peek() => _at < text.Length ? text[_at] : '\0';

    private void Expect(char c)
    {
        if (_at >= text.Length)
        {
            throw Fail("Unexpected end of input");
        }

        if (text[_at] != c)
        {
            throw Fail("Unexpected character");
        }

        _at++;
    }

    private string ReadString()
    {
        var builder = new StringBuilder();
        _at++;
        while (true)
        {
            if (_at >= text.Length)
            {
                throw Fail("Unterminated string");
            }

            var c = text[_at++];
            if (c == '"')
            {
                return builder.ToString();
            }

            builder.Append(c == '\\' ? ReadEscape() : c < ' ' ? throw Fail("Control character in string") : c);
        }
    }

    private char ReadEscape()
    {
        if (_at >= text.Length)
        {
            throw Fail("Unterminated string");
        }

        var c = text[_at++];
        return c switch
        {
            '"' or '\\' or '/' => c,
            'b' => '\b',
            'f' => '\f',
            'n' => '\n',
            'r' => '\r',
            't' => '\t',
            'u' => ReadUnit(),
            _ => throw Fail("Unrecognized string escape"),
        };
    }

    private char ReadUnit()
    {
        if (_at + 4 > text.Length
            || !int.TryParse(text.AsSpan(_at, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var unit))
        {
            throw Fail("Invalid unicode escape");
        }

        _at += 4;
        return (char)unit;
    }

    private object ReadNumber()
    {
        var start = _at;
        if (Peek() == '-')
        {
            _at++;
        }

        ReadDigits(leadingZeroEndsIt: true);
        var whole = true;
        if (Peek() == '.')
        {
            _at++;
            ReadDigits(leadingZeroEndsIt: false);
            whole = false;
        }

        if (Peek() is 'e' or 'E')
        {
            _at++;
            if (Peek() is '+' or '-')
            {
                _at++;
            }

            ReadDigits(leadingZeroEndsIt: false);
            whole = false;
        }

        var span = text.AsSpan(start, _at - start);
        return whole && long.TryParse(span, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var integer)
            ? (object)integer
            : double.Parse(span, NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    // A whole part that starts with 0 is that 0 alone, so 01 reads as 0 followed by a stray 1.
    private void ReadDigits(bool leadingZeroEndsIt)
    {
        if (!char.IsAsciiDigit(Peek()))
        {
            throw Fail("Missing expected digit");
        }

        if (leadingZeroEndsIt && Peek() == '0')
        {
            _at++;
            return;
        }

        while (char.IsAsciiDigit(Peek()))
        {
            _at++;
        }
    }
}

using System.Diagnostics.CodeAnalysis;

namespace Orblit.Save;

/// <summary>
/// Why a save was made. It is stored beside the bytes, so an operator reading
/// history can tell a routine save from one made as a player fell off the
/// network.
/// </summary>
public abstract record SaveReason
{
    private const string CustomPrefix = "custom:";

    private protected SaveReason()
    {
    }

    /// <summary>A short name that <see cref="Parse"/> reads back.</summary>
    public abstract string Label { get; }

    /// <summary>Reads what <see cref="Label"/> wrote. Throws <see cref="FormatException"/> for anything else.</summary>
    [SuppressMessage("Maintainability", "CA1502:Avoid excessive complexity", Justification = "F2: a list of cases is data.")]
    public static SaveReason Parse(string label) => label switch
    {
        "autosave" => new Autosave(),
        "leave" => new Leave(),
        "disconnect" => new Disconnect(),
        "shutdown" => new Shutdown(),
        "error" => new Failed(),
        _ when label.StartsWith(CustomPrefix, StringComparison.Ordinal) => new Custom(label[CustomPrefix.Length..]),
        _ => throw new FormatException($"Not a save reason: {label}"),
    };

    /// <inheritdoc />
    public sealed override string ToString() => Label;
}

/// <summary>A routine save of a player who is still playing.</summary>
public sealed record Autosave : SaveReason
{
    /// <inheritdoc />
    public override string Label => "autosave";
}

/// <summary>A player left on purpose.</summary>
public sealed record Leave : SaveReason
{
    /// <inheritdoc />
    public override string Label => "leave";
}

/// <summary>A player's connection ended and the game gave up waiting.</summary>
public sealed record Disconnect : SaveReason
{
    /// <inheritdoc />
    public override string Label => "disconnect";
}

/// <summary>The server is stopping.</summary>
public sealed record Shutdown : SaveReason
{
    /// <inheritdoc />
    public override string Label => "shutdown";
}

/// <summary>The game saved while something was wrong.</summary>
public sealed record Failed : SaveReason
{
    /// <inheritdoc />
    public override string Label => "error";
}

/// <summary>A reason the game names itself, such as <c>pre-restore</c>.</summary>
public sealed record Custom : SaveReason
{
    /// <summary>The longest text a <see cref="Custom"/> reason holds.</summary>
    private const int MaxCustomReason = 64;

    /// <summary>Refuses an empty text, one over 64 characters, and one with a new line.</summary>
    public Custom(string text)
    {
        if (text.Length == 0 || text.Length > MaxCustomReason || text.Contains('\n', StringComparison.Ordinal))
        {
            throw new ArgumentOutOfRangeException(
                nameof(text),
                text,
                $"is empty, over {MaxCustomReason} characters, or has a new line");
        }

        Text = text;
    }

    /// <summary>The text after <c>custom:</c>.</summary>
    public string Text { get; }

    /// <inheritdoc />
    public override string Label => $"custom:{Text}";
}

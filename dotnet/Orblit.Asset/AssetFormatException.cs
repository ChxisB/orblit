namespace Orblit.Asset;

/// <summary>
/// Text that is not what it was read as: Dart's <c>FormatException</c> with the text and the
/// place in it that a reader can show.
/// </summary>
public sealed class AssetFormatException(string message, string? text = null, int? offset = null)
    : FormatException(message)
{
    /// <summary>The text that was refused, when the reader has it.</summary>
    public string? Text { get; } = text;

    /// <summary>Where in <see cref="Text"/> the problem is.</summary>
    public int? Offset { get; } = offset;
}

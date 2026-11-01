namespace Orblit.Json;

/// <summary>The text is not JSON. <see cref="Reason"/> says why, without the place.</summary>
public sealed class JsonFormatException(string reason, int at) : FormatException($"{reason} at {at}.")
{
    /// <summary>The refusal as the reader words it, such as <c>Unexpected end of input</c>.</summary>
    public string Reason { get; } = reason;
}

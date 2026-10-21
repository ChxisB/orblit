namespace Orblit.Asset;

/// <summary>What the three command lines share when they print.</summary>
internal static class CliOutput
{
    /// <summary>Writes <paramref name="text"/> and a line feed, whatever the platform's own line ending is.</summary>
    internal static void Line(TextWriter writer, string text) => writer.Write(text + "\n");
}

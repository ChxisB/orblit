namespace Orblit.Mesh;

/// <summary>
/// What <see cref="ObjFiles.ReadObj"/>, <see cref="StlFiles.ReadStl"/> and <see cref="PlyFiles.ReadPly"/>
/// take before they refuse. A model file is bytes from anywhere, and a line or a header in it can
/// ask for as much memory as it has digits. The defaults are far above any model somebody blocks
/// out by hand and far below what would stall an editor.
/// </summary>
public sealed record ReadLimits
{
    /// <summary>The most bytes the file, or one <c>.mtl</c>, may hold.</summary>
    public int Bytes { get; init; } = 128 << 20;

    /// <summary>The most points, texture coordinates and other rows read.</summary>
    public int Vertices { get; init; } = 4000000;

    /// <summary>The most faces read.</summary>
    public int Faces { get; init; } = 4000000;

    /// <summary>
    /// The most corners one face may have. An OBJ drops a longer face, and the other readers
    /// refuse the file.
    /// </summary>
    /// <remarks>
    /// shortcut: triangulating a face costs the square of its corners, so a file of the largest
    /// faces takes seconds. Read an untrusted file off the drawing thread, or lower this, until a
    /// convex face is cut as a fan.
    /// </remarks>
    public int Corners { get; init; } = 1024;

    /// <summary>
    /// The most corners in all the faces together. <see cref="Faces"/> and <see cref="Corners"/>
    /// each allow a file that holds their product.
    /// </summary>
    public int Indices { get; init; } = 16000000;

    /// <summary>The most groups and materials an OBJ names.</summary>
    public int Names { get; init; } = 100000;

    /// <summary>The most characters in a line of text, once a backslash has joined it to the next.</summary>
    public int Line { get; init; } = 1 << 20;

    /// <summary>
    /// The lines of <paramref name="text"/>, each cut out as it is asked for, so a file that is
    /// all line ends costs no more than the file. Throws <see cref="FormatException"/> for a line
    /// over <see cref="Line"/>.
    /// </summary>
    public IEnumerable<string> LinesOf(string text)
    {
        var from = 0;
        while (from <= text.Length)
        {
            var end = text.IndexOf('\n', from);
            if (end < 0)
            {
                end = text.Length;
            }

            if (end - from > Line)
            {
                throw new FormatException($"A line over {Line} characters is not read.");
            }

            yield return text.Substring(from, end - from);
            from = end + 1;
        }
    }
}

using System.Buffers.Binary;
using Orblit.VectorMath;

namespace Orblit.Mesh;

public static class StlFiles
{
    private const int HeaderSize = 84;
    private const int TriangleSize = 50;

    /// <summary>
    /// Reads the STL in <paramref name="bytes"/>, binary or text, as a mesh of triangles.
    /// </summary>
    /// <remarks>
    /// STL repeats every corner for each triangle that uses it. Corners at the same place are
    /// joined, so the mesh comes back as a solid and not as a pile of loose triangles. A triangle
    /// with two equal corners has no area and is dropped. The normals in the file are not kept,
    /// because a mesh works them out from the winding.
    /// <para>
    /// Throws <see cref="FormatException"/> when the file is not an STL, is cut short, holds a
    /// number that is not finite, or passes <paramref name="limits"/>.
    /// </para>
    /// <para>
    /// shortcut: a binary file is told by its length being exactly 84 plus 50 for each triangle,
    /// so one with bytes after the last triangle is read as text and refused. Loosen the check
    /// when a writer that pads turns up.
    /// </para>
    /// </remarks>
    public static Mesh ReadStl(byte[] bytes, ReadLimits? limits = null)
    {
        limits ??= new ReadLimits();
        if (bytes.Length > limits.Bytes)
        {
            throw new FormatException($"An STL over {limits.Bytes} bytes is not read.");
        }

        var welder = new Welder(limits);
        if (IsBinary(bytes))
        {
            ReadBinary(bytes, welder, limits);
        }
        else
        {
            ReadText(bytes, welder, limits);
        }

        if (welder.Mesh.Faces.Count == 0 || !welder.IsBetweenTriangles)
        {
            throw new FormatException("This STL has no whole triangle.");
        }

        return welder.Mesh;
    }

    private static bool IsBinary(byte[] bytes)
    {
        if (bytes.Length < HeaderSize)
        {
            return false;
        }

        var count = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(80));
        return bytes.Length == HeaderSize + (TriangleSize * (long)count);
    }

    private static void ReadBinary(byte[] bytes, Welder welder, ReadLimits limits)
    {
        var count = (bytes.Length - HeaderSize) / TriangleSize;
        if (count > limits.Faces)
        {
            throw new FormatException($"More than {limits.Faces} triangles.");
        }

        for (var i = 0; i < count; i++)
        {
            // Twelve bytes of normal, then the three corners.
            var at = HeaderSize + (i * TriangleSize) + 12;
            for (var corner = 0; corner < 3; corner++)
            {
                var p = at + (corner * 12);
                welder.Add(FloatAt(bytes, p), FloatAt(bytes, p + 4), FloatAt(bytes, p + 8));
            }
        }
    }

    private static double FloatAt(byte[] bytes, int at) => BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(at));

    private static void ReadText(byte[] bytes, Welder welder, ReadLimits limits)
    {
        var text = TextRules.DecodeLenient(bytes);
        if (!StartsWithSolid(text))
        {
            throw new FormatException("This is not an STL.");
        }

        foreach (var line in limits.LinesOf(text))
        {
            var trimmed = TextRules.Trim(line);
            var words = TextRules.SplitSpace(trimmed);
            if (TextRules.ToLower(words[0]) != "vertex")
            {
                continue;
            }

            var xyz = words.Count < 4 ? null : words.Skip(1).Take(3).Select(word => TextRules.ParseDouble(word)).ToArray();
            if (xyz is null || xyz.Any(value => value is null))
            {
                throw new FormatException($"\"{trimmed}\" is not a point.");
            }

            welder.Add(xyz[0]!.Value, xyz[1]!.Value, xyz[2]!.Value);
        }
    }

    /// <summary>The pattern <c>^\s*solid</c>.</summary>
    private static bool StartsWithSolid(string text)
    {
        var at = 0;
        while (at < text.Length && TextRules.IsPatternSpace(text[at]))
        {
            at++;
        }

        return text.AsSpan(at).StartsWith("solid", StringComparison.Ordinal);
    }

    /// <summary>Joins corners that sit at one place, and makes a face of every three.</summary>
    private sealed class Welder(ReadLimits limits)
    {
        private readonly Dictionary<(double, double, double), int> _seen = [];
        private readonly List<int> _pending = [];

        internal Mesh Mesh { get; } = new();

        internal bool IsBetweenTriangles => _pending.Count == 0;

        internal void Add(double x, double y, double z)
        {
            if (!(double.IsFinite(x) && double.IsFinite(y) && double.IsFinite(z)))
            {
                throw new FormatException("An STL point is not a finite number.");
            }

            if (_pending.Count == 0 && Mesh.FaceCount >= limits.Faces)
            {
                throw new FormatException($"More than {limits.Faces} triangles.");
            }

            _pending.Add(PointAt(x, y, z));
            if (_pending.Count < 3)
            {
                return;
            }

            var (a, b, c) = (_pending[0], _pending[1], _pending[2]);
            if (a != b && b != c && a != c)
            {
                Mesh.Faces.Add(new Face([a, b, c]));
            }

            _pending.Clear();
        }

        // F5: a lookup that adds on a miss, as a dictionary's putIfAbsent does.
        private int PointAt(double x, double y, double z)
        {
            // Adding zero turns -0 into 0. The two are equal, and a key that holds one must find
            // the other.
            var key = (x + 0.0, y + 0.0, z + 0.0);
            if (_seen.TryGetValue(key, out var at))
            {
                return at;
            }

            if (Mesh.VertexCount >= limits.Vertices)
            {
                throw new FormatException($"More than {limits.Vertices} points.");
            }

            Mesh.Positions.Add(new Vector3(x, y, z));
            at = Mesh.VertexCount - 1;
            _seen[key] = at;
            return at;
        }
    }
}

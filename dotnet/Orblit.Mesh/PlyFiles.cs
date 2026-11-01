using System.Buffers.Binary;
using System.Text;
using Orblit.Json;
using Orblit.VectorMath;

namespace Orblit.Mesh;

public static class PlyFiles
{
    /// <summary>
    /// Reads the PLY in <paramref name="bytes"/>, text or binary of either byte order, as a mesh.
    /// </summary>
    /// <remarks>
    /// Takes the <c>x</c>, <c>y</c> and <c>z</c> of each <c>vertex</c>, the coordinates when they
    /// are named <c>s</c> and <c>t</c>, <c>u</c> and <c>v</c>, or <c>texture_u</c> and
    /// <c>texture_v</c>, and the <c>vertex_indices</c> or <c>vertex_index</c> list of each
    /// <c>face</c>, kept as the polygon it is. Everything else in the file is read past by the
    /// types its header declares: colours, normals, edges and any other element.
    /// <para>
    /// Throws <see cref="FormatException"/> when the file is not a PLY, is cut short, holds a
    /// number that is not finite, names a point that is not there, has no face, or passes
    /// <paramref name="limits"/>. A file of splats is refused by name, because the splat loader
    /// reads those.
    /// </para>
    /// <para>
    /// shortcut: the vertices must come before the faces, as every writer puts them, and a file in
    /// the other order is refused as naming points that are not there. Read the faces first and
    /// check them after when one turns up.
    /// </para>
    /// </remarks>
    public static Mesh ReadPly(byte[] bytes, ReadLimits? limits = null)
    {
        limits ??= new ReadLimits();
        if (bytes.Length > limits.Bytes)
        {
            throw new FormatException($"A PLY over {limits.Bytes} bytes is not read.");
        }

        var header = PlyHeader.Parse(bytes, limits);
        PlyValues values = header.Format == "ascii"
            ? new PlyWords(bytes, header.BodyStart)
            : new PlyBinary(bytes, header.BodyStart, header.Format == "binary_little_endian");
        return new PlyBody(header, values, limits).Read();
    }
}

internal enum PlyKind
{
    Int8,
    UInt8,
    Int16,
    UInt16,
    Int32,
    UInt32,
    Float32,
    Float64,
}

/// <summary>A type a header may name, and the bytes it takes in a binary file.</summary>
internal readonly record struct PlyType(PlyKind Kind, int Size)
{
    /// <summary>Whether a list's length may be written in it.</summary>
    internal bool IsWhole => Kind is not (PlyKind.Float32 or PlyKind.Float64);
}

internal static class PlyTypes
{
    private static readonly Dictionary<string, PlyType> Named = new()
    {
        ["char"] = new(PlyKind.Int8, 1),
        ["int8"] = new(PlyKind.Int8, 1),
        ["uchar"] = new(PlyKind.UInt8, 1),
        ["uint8"] = new(PlyKind.UInt8, 1),
        ["short"] = new(PlyKind.Int16, 2),
        ["int16"] = new(PlyKind.Int16, 2),
        ["ushort"] = new(PlyKind.UInt16, 2),
        ["uint16"] = new(PlyKind.UInt16, 2),
        ["int"] = new(PlyKind.Int32, 4),
        ["int32"] = new(PlyKind.Int32, 4),
        ["uint"] = new(PlyKind.UInt32, 4),
        ["uint32"] = new(PlyKind.UInt32, 4),
        ["float"] = new(PlyKind.Float32, 4),
        ["float32"] = new(PlyKind.Float32, 4),
        ["double"] = new(PlyKind.Float64, 8),
        ["float64"] = new(PlyKind.Float64, 8),
    };

    internal static PlyType? Find(string name) => Named.TryGetValue(name, out var type) ? type : null;
}

/// <param name="Name">What the header calls it.</param>
/// <param name="Type">The type of the value, or of each item when this is a list.</param>
/// <param name="Length">The type the length of a list is written in, or null for one value.</param>
internal sealed record PlyProperty(string Name, PlyType Type, PlyType? Length)
{
    internal bool IsList => Length is not null;
}

internal sealed record PlyElement(string Name, long Count)
{
    internal List<PlyProperty> Properties { get; } = [];
}

internal sealed record PlyHeader(string Format, List<PlyElement> Elements, int BodyStart)
{
    /// <summary>The most a header may take, which is far more than any real one does.</summary>
    private const int HeaderBytes = 64 << 10;

    internal static PlyHeader Parse(byte[] bytes, ReadLimits limits)
    {
        var window = Encoding.Latin1.GetString(bytes, 0, Math.Min(bytes.Length, HeaderBytes));
        var end = window.IndexOf("end_header", StringComparison.Ordinal);
        if (end < 0 || !window.StartsWith("ply", StringComparison.Ordinal))
        {
            throw new FormatException("This is not a PLY.");
        }

        var body = end + "end_header".Length;
        if (body < bytes.Length && bytes[body] == 13)
        {
            body++;
        }

        if (body >= bytes.Length || bytes[body] != 10)
        {
            throw new FormatException("The PLY header does not end in a line.");
        }

        return FromLines(window[..end].Split('\n'), limits, body + 1);
    }

    private static PlyHeader FromLines(string[] lines, ReadLimits limits, int body)
    {
        string? format = null;
        var elements = new List<PlyElement>();
        foreach (var line in lines)
        {
            var words = PlainText.SplitSpace(PlainText.Trim(line));
            switch (words[0])
            {
                case "format":
                    format = words.Count > 1 ? words[1] : null;
                    break;
                case "element":
                    elements.Add(ElementOf(words, elements, limits));
                    break;
                case "property":
                    if (elements.Count == 0)
                    {
                        throw new FormatException("A property of nothing.");
                    }

                    elements[^1].Properties.Add(PropertyOf(words));
                    break;
            }
        }

        if (format is not ("ascii" or "binary_little_endian" or "binary_big_endian"))
        {
            throw new FormatException($"The PLY format \"{format ?? "null"}\" is not one of three.");
        }

        return new PlyHeader(format, elements, body);
    }

    private static PlyElement ElementOf(List<string> words, List<PlyElement> before, ReadLimits limits)
    {
        var count = words.Count == 3 ? PlainText.ParseInt(words[2]) : null;
        var most = words.Count > 1 && words[1] == "face" ? limits.Faces : limits.Vertices;
        if (count is not { } size || size < 0 || size > most)
        {
            throw new FormatException($"\"{string.Join(' ', words)}\" is not a size to read.");
        }

        if (before.Any(element => element.Name == words[1]))
        {
            throw new FormatException($"The PLY has two {words[1]} elements.");
        }

        return new PlyElement(words[1], size);
    }

    private static PlyProperty PropertyOf(List<string> words)
    {
        var list = words.Count == 5 && words[1] == "list";
        var type = list ? words[3] : (words.Count == 3 ? words[1] : string.Empty);
        var item = PlyTypes.Find(type);
        var length = list ? PlyTypes.Find(words[2]) : null;
        if (item is not { } found || (list && length is not { IsWhole: true }))
        {
            throw new FormatException($"\"{string.Join(' ', words)}\" is not a property.");
        }

        return new PlyProperty(words[^1], found, length);
    }
}

/// <summary>The values of a PLY body, one type at a time.</summary>
internal abstract class PlyValues
{
    internal abstract WholeOrFraction Read(PlyType type);

    /// <summary>Takes <paramref name="count"/> values of <paramref name="type"/> and drops them.</summary>
    internal virtual void Skip(PlyType type, long count)
    {
        for (long i = 0; i < count; i++)
        {
            Read(type);
        }
    }
}

internal sealed class PlyWords(byte[] bytes, int at) : PlyValues
{
    private int _at = at;

    internal override WholeOrFraction Read(PlyType type)
    {
        while (_at < bytes.Length && bytes[_at] <= 32)
        {
            _at++;
        }

        var start = _at;
        while (_at < bytes.Length && bytes[_at] > 32)
        {
            _at++;
        }

        // No number is this long, and a longer word is a binary file read as text.
        if (start == _at || _at - start > 40)
        {
            throw new FormatException("The PLY data ends or is not numbers.");
        }

        var word = Encoding.Latin1.GetString(bytes, start, _at - start);
        return TextRules.ParseNum(word) ?? throw new FormatException($"\"{word}\" is not a number.");
    }
}

internal sealed class PlyBinary(byte[] bytes, int at, bool little) : PlyValues
{
    private long _at = at;

    internal override WholeOrFraction Read(PlyType type)
    {
        var span = bytes.AsSpan((int)Take(type.Size));
        return type.Kind switch
        {
            PlyKind.Int8 => WholeOrFraction.Of((long)(sbyte)span[0]),
            PlyKind.UInt8 => WholeOrFraction.Of((long)span[0]),
            PlyKind.Int16 => WholeOrFraction.Of((long)(little ? BinaryPrimitives.ReadInt16LittleEndian(span) : BinaryPrimitives.ReadInt16BigEndian(span))),
            PlyKind.UInt16 => WholeOrFraction.Of((long)(little ? BinaryPrimitives.ReadUInt16LittleEndian(span) : BinaryPrimitives.ReadUInt16BigEndian(span))),
            PlyKind.Int32 => WholeOrFraction.Of((long)(little ? BinaryPrimitives.ReadInt32LittleEndian(span) : BinaryPrimitives.ReadInt32BigEndian(span))),
            PlyKind.UInt32 => WholeOrFraction.Of((long)(little ? BinaryPrimitives.ReadUInt32LittleEndian(span) : BinaryPrimitives.ReadUInt32BigEndian(span))),
            PlyKind.Float32 => WholeOrFraction.Of((double)(little ? BinaryPrimitives.ReadSingleLittleEndian(span) : BinaryPrimitives.ReadSingleBigEndian(span))),
            _ => WholeOrFraction.Of(little ? BinaryPrimitives.ReadDoubleLittleEndian(span) : BinaryPrimitives.ReadDoubleBigEndian(span)),
        };
    }

    internal override void Skip(PlyType type, long count) => Take(type.Size * count);

    /// <summary>Moves past <paramref name="size"/> bytes and answers where they began.</summary>
    // F5: moves and answers, as the reader's cursor does.
    private long Take(long size)
    {
        if (_at + size > bytes.Length)
        {
            throw new FormatException("The PLY data ends too soon.");
        }

        _at += size;
        return _at - size;
    }
}

internal sealed class PlyBody(PlyHeader header, PlyValues values, ReadLimits limits)
{
    private static readonly string[] SplatFields = ["f_dc_0", "opacity", "scale_0", "rot_0"];

    /// <summary>Where in the row of five a property goes: x, y, z, then the two coordinates.</summary>
    private static readonly Dictionary<string, int> Slots = new()
    {
        ["x"] = 0,
        ["y"] = 1,
        ["z"] = 2,
        ["s"] = 3,
        ["u"] = 3,
        ["texture_u"] = 3,
        ["texture_s"] = 3,
        ["t"] = 4,
        ["v"] = 4,
        ["texture_v"] = 4,
        ["texture_t"] = 4,
    };

    private readonly Mesh _mesh = new();
    private readonly List<Vector2> _uvs = [];
    private long _corners;

    internal Mesh Read()
    {
        foreach (var element in header.Elements)
        {
            switch (element.Name)
            {
                case "vertex":
                    ReadVertices(element);
                    break;
                case "face":
                    ReadFaces(element);
                    break;
                default:
                    SkipElement(element);
                    break;
            }

            if (_mesh.VertexCount > 0 && _mesh.Faces.Count > 0)
            {
                break;
            }
        }

        if (_mesh.Faces.Count == 0)
        {
            throw new FormatException("This PLY has no faces, so no model.");
        }

        return _mesh;
    }

    /// <summary>The place of each property in the row, minus one for anything else.</summary>
    private static int[] SlotsOf(PlyElement element)
    {
        if (element.Properties.Any(p => SplatFields.Contains(p.Name)))
        {
            throw new FormatException("This PLY holds splats, which the splat loader reads and the model readers do not.");
        }

        var slots = element.Properties
            .Select(p => !p.IsList && Slots.TryGetValue(p.Name, out var slot) ? slot : -1)
            .ToArray();
        if (!(slots.Contains(0) && slots.Contains(1) && slots.Contains(2)))
        {
            throw new FormatException("The PLY vertices have no x, y and z.");
        }

        return slots;
    }

    private void ReadVertices(PlyElement element)
    {
        var slots = SlotsOf(element);
        var hasUv = slots.Contains(3) && slots.Contains(4);
        var row = new double[5];
        for (long n = 0; n < element.Count; n++)
        {
            for (var i = 0; i < slots.Length; i++)
            {
                var value = ReadValue(element.Properties[i]);
                if (slots[i] >= 0)
                {
                    row[slots[i]] = value.ToDouble();
                }
            }

            if (!row.All(double.IsFinite))
            {
                throw new FormatException("A PLY vertex is not a finite number.");
            }

            _mesh.Positions.Add(new Vector3(row[0], row[1], row[2]));
            if (hasUv)
            {
                _uvs.Add(new Vector2(row[3], row[4]));
            }
        }
    }

    /// <summary>The value of <paramref name="p"/>, or for a list the number of items it dropped.</summary>
    // F5: reads and answers, as the cursor under it does.
    private WholeOrFraction ReadValue(PlyProperty p)
    {
        if (!p.IsList)
        {
            return values.Read(p.Type);
        }

        var count = Length(p);
        values.Skip(p.Type, count);
        return WholeOrFraction.Of(count);
    }

    private long Length(PlyProperty p)
    {
        var length = WholeNumber(values.Read(p.Length!.Value));
        if (length < 0)
        {
            throw new FormatException("A PLY list is of negative length.");
        }

        return length;
    }

    private static long WholeNumber(WholeOrFraction value)
    {
        if (value.IsWhole)
        {
            return value.Whole;
        }

        var fraction = value.Fraction;
        if (double.IsFinite(fraction) && fraction == Math.Truncate(fraction))
        {
            // The result stops at the ends of the 64-bit range, where a C# cast does not.
            return fraction >= 9.2233720368547758E18 ? long.MaxValue : fraction <= -9.2233720368547758E18 ? long.MinValue : (long)fraction;
        }

        throw new FormatException($"{Doubles.ToFileString(fraction)} is not a whole number.");
    }

    private void ReadFaces(PlyElement element)
    {
        var corners = element.Properties.FirstOrDefault(p => p.IsList && p.Name is "vertex_indices" or "vertex_index")
            ?? throw new FormatException("The PLY faces have no vertex_indices.");
        for (long n = 0; n < element.Count; n++)
        {
            List<int> points = [];
            foreach (var p in element.Properties)
            {
                if (ReferenceEquals(p, corners))
                {
                    points = ReadPoints(p);
                }
                else
                {
                    ReadValue(p);
                }
            }

            var uv = _uvs.Count == 0 ? null : new FaceUv(manual: [.. points.Select(i => _uvs[i].Clone())]);
            _mesh.Faces.Add(new Face(points, uv: uv));
        }
    }

    private List<int> ReadPoints(PlyProperty p)
    {
        var count = Length(p);
        if (count < 3 || count > limits.Corners)
        {
            throw new FormatException($"A PLY face has {count} corners.");
        }

        _corners += count;
        if (_corners > limits.Indices)
        {
            throw new FormatException($"More than {limits.Indices} face corners.");
        }

        var points = new List<int>((int)count);
        for (var i = 0; i < count; i++)
        {
            points.Add(PointAt(WholeNumber(values.Read(p.Type))));
        }

        return points;
    }

    private int PointAt(long index)
    {
        if (index < 0 || index >= _mesh.VertexCount)
        {
            throw new FormatException($"A PLY face names point {index}, which is not there.");
        }

        return (int)index;
    }

    private void SkipElement(PlyElement element)
    {
        if (element.Properties.Count == 0)
        {
            return;
        }

        for (long n = 0; n < element.Count; n++)
        {
            element.Properties.ForEach(p => ReadValue(p));
        }
    }
}

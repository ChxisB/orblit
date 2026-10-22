using System.Text;
using Orblit.VectorMath;

namespace Orblit.Mesh;

/// <summary>The faces an <c>o</c> or <c>g</c> line named.</summary>
/// <param name="Name">What the line called them.</param>
/// <param name="Faces">Indices into <see cref="ObjModel.Mesh"/>'s faces, in the order they were read.</param>
public sealed record ObjGroup(string Name, IReadOnlyList<int> Faces);

/// <summary>An OBJ file, read.</summary>
/// <param name="Mesh">
/// Every point and face in the file, n-gons as they were written. A face's
/// <see cref="Face.Material"/> indexes <paramref name="Materials"/>.
/// </param>
/// <param name="Groups">The named parts of the file, in the order each first had a face.</param>
/// <param name="Materials">
/// The materials the file's libraries defined, and any a face named that no library defined, as a
/// default one with that name.
/// </param>
/// <param name="Problems">What could not be read, in the order it was met.</param>
public sealed record ObjModel(Mesh Mesh, IReadOnlyList<ObjGroup> Groups, IReadOnlyList<GlbMaterial> Materials, List<string> Problems)
{
    /// <summary>
    /// <paramref name="group"/> as a mesh of its own, holding only the points its faces use. The
    /// material indices still index <see cref="Materials"/>.
    /// </summary>
    public Mesh MeshOf(ObjGroup group)
    {
        var taken = new Dictionary<int, int>();
        var output = new Mesh();
        foreach (var at in group.Faces)
        {
            var face = Mesh.Faces[at].Copy();
            for (var i = 0; i < face.Vertices.Count; i++)
            {
                var old = face.Vertices[i];
                if (!taken.TryGetValue(old, out var index))
                {
                    index = output.Positions.Count;
                    output.Positions.Add(Mesh.Positions[old].Clone());
                    taken[old] = index;
                }

                face.Vertices[i] = index;
            }

            output.Faces.Add(face);
        }

        return output;
    }
}

public static class ObjFiles
{
    /// <summary>
    /// Reads the Wavefront OBJ in <paramref name="bytes"/>.
    /// </summary>
    /// <remarks>
    /// <paramref name="files"/> holds the <c>.mtl</c> the file names, by the name it gives. The
    /// folder in a name is ignored when the whole name is not there.
    /// <para>
    /// Reads <c>v</c>, <c>vt</c>, <c>f</c>, <c>o</c>, <c>g</c>, <c>s</c>, <c>usemtl</c> and
    /// <c>mtllib</c>. Normals, lines, points and curves are skipped without a note, and so are the
    /// pictures an <c>.mtl</c> names, with one note for each material that has any.
    /// </para>
    /// <para>
    /// Lenient, like the glTF readers: a line that cannot be read is a note in
    /// <see cref="ObjModel.Problems"/> and the rest of the file is still read. Throws
    /// <see cref="FormatException"/> when a limit in <paramref name="limits"/> is passed, or when
    /// the file holds no face.
    /// </para>
    /// </remarks>
    public static ObjModel ReadObj(byte[] bytes, IReadOnlyDictionary<string, byte[]>? files = null, ReadLimits? limits = null)
    {
        limits ??= new ReadLimits();
        if (bytes.Length > limits.Bytes)
        {
            throw new FormatException($"An OBJ over {limits.Bytes} bytes is not read.");
        }

        return new ObjReader(files ?? new Dictionary<string, byte[]>(), limits).Read(TextRules.DecodeLenient(bytes));
    }

    /// <summary>
    /// The box an OBJ's points sit in, or null when it cannot be read. The third of the readers
    /// beside <see cref="MeshExport.BoundsOfGlb"/> and <see cref="MeshExport.BoundsOfGltf"/>. An
    /// OBJ has no minimum and maximum written down, so this reads the points.
    /// </summary>
    public static (Vector3 Min, Vector3 Max)? BoundsOfObj(byte[] obj)
    {
        try
        {
            return ReadObj(obj).Mesh.Bounds;
        }
        catch (FormatException)
        {
            return null;
        }
    }
}

internal sealed class ObjReader(IReadOnlyDictionary<string, byte[]> files, ReadLimits limits)
{
    /// <summary>
    /// The most notes kept. A file of bad lines should not fill memory with sentences about them.
    /// </summary>
    private const int Listed = 50;

    private readonly Mesh _mesh = new();
    private readonly List<double> _uvs = [];
    private readonly OrderedDictionary<string, List<int>> _groups = [];
    private readonly List<GlbMaterial> _materials = [];
    private readonly Dictionary<string, int> _materialAt = [];
    private readonly List<string> _problems = [];
    private readonly HashSet<string> _loaded = [];
    private readonly HashSet<int> _unread = [];
    private long _corners;
    private int _unlisted;
    private int _line;
    private string _group = "default";

    // shortcut: a face before any usemtl wears material 0, whichever material that is. Give such
    // faces a material of their own when a file mixes the two.
    private int _material;
    private bool _smooth;

    internal ObjModel Read(string text)
    {
        foreach (var (number, line) in Lines(text))
        {
            _line = number;
            Take(line);
        }

        if (_mesh.Faces.Count == 0)
        {
            throw new FormatException("This OBJ has no faces, so no model.");
        }

        if (_unlisted > 0)
        {
            _problems.Add($"{_unlisted} more problems were not listed.");
        }

        return new ObjModel(
            _mesh,
            [.. _groups.Select(entry => new ObjGroup(entry.Key, entry.Value))],
            _materials.AsReadOnly(),
            _problems);
    }

    /// <summary>
    /// The file's lines, each with the number of the line it began on. A line ending in a
    /// backslash goes on to the next, which is how long faces are wrapped.
    /// </summary>
    private IEnumerable<(int Number, string Line)> Lines(string text)
    {
        var joined = new StringBuilder();
        var start = 0;
        var number = 0;
        foreach (var raw in limits.LinesOf(text))
        {
            number++;
            var line = TextRules.TrimEnd(raw);
            if (joined.Length == 0)
            {
                start = number;
            }

            if (joined.Length + line.Length > limits.Line)
            {
                throw new FormatException($"A line over {limits.Line} characters.");
            }

            if (line.EndsWith('\\'))
            {
                joined.Append(line, 0, line.Length - 1).Append(' ');
                continue;
            }

            joined.Append(line);
            yield return (start, joined.ToString());
            joined.Clear();
        }

        if (joined.Length > 0)
        {
            yield return (start, joined.ToString());
        }
    }

    private void Take(string line)
    {
        var hash = line.IndexOf('#', StringComparison.Ordinal);
        var words = TextRules.SplitSpace(TextRules.Trim(hash < 0 ? line : line[..hash]));
        var args = words.GetRange(1, words.Count - 1);
        switch (words[0])
        {
            case "v":
                TakeVertex(args);
                break;
            case "vt":
                TakeCoordinate(args);
                break;
            case "f":
                TakeFace(args);
                break;
            case "o" or "g":
                _group = args.Count == 0 ? "default" : string.Join(' ', args);
                break;
            case "usemtl":
                _material = MaterialNamed(string.Join(' ', args));
                break;
            case "mtllib":
                args.ForEach(Library);
                break;
            case "s":
                _smooth = IsSmooth(args);
                break;
        }
    }

    private static bool IsSmooth(List<string> args) => args.Count > 0 && args[0] != "off" && args[0] != "0";

    private void TakeVertex(List<string> args)
    {
        if (_mesh.VertexCount >= limits.Vertices)
        {
            throw new FormatException($"More than {limits.Vertices} points.");
        }

        if (Numbers(args, 3) is not { } xyz)
        {
            // The origin holds the place, so the points after it keep their numbers. A face that
            // names it is dropped.
            _unread.Add(_mesh.VertexCount);
            _mesh.Positions.Add(Vector3.Zero());
            Note("a point needs three finite numbers.");
            return;
        }

        _mesh.Positions.Add(new Vector3(xyz[0], xyz[1], xyz[2]));
    }

    private void TakeCoordinate(List<string> args)
    {
        if (_uvs.Count / 2 >= limits.Vertices)
        {
            throw new FormatException($"More than {limits.Vertices} coordinates.");
        }

        var uv = Numbers(args.Count == 1 ? [args[0], "0"] : args, 2);

        // A coordinate that cannot be read still takes its number.
        _uvs.AddRange(uv ?? [0, 0]);
        if (uv is null)
        {
            Note("a coordinate needs finite numbers.");
        }
    }

    private void TakeFace(List<string> args)
    {
        if (_mesh.FaceCount >= limits.Faces)
        {
            throw new FormatException($"More than {limits.Faces} faces.");
        }

        if (args.Count < 3)
        {
            Note("a face needs three corners.");
            return;
        }

        if (args.Count > limits.Corners)
        {
            Note($"a face has {args.Count} corners, over {limits.Corners}.");
            return;
        }

        var points = new List<int>();
        var uvs = new List<Vector2>();
        foreach (var word in args)
        {
            if (!TakeCorner(word, points, uvs))
            {
                Note($"\"{word}\" is not a point read so far.");
                return;
            }
        }

        _corners += points.Count;
        if (_corners > limits.Indices)
        {
            throw new FormatException($"More than {limits.Indices} face corners.");
        }

        GroupFaces().Add(_mesh.FaceCount);

        // Coordinates are kept only when every corner had one, because a face with some is no more
        // drawn than one with none.
        _mesh.Faces.Add(new Face(points, _material, _smooth, uvs.Count == points.Count ? new FaceUv(manual: uvs) : null));
    }

    /// <summary>One <c>point/uv/normal</c> word. False when its point was not read.</summary>
    private bool TakeCorner(string word, List<int> points, List<Vector2> uvs)
    {
        var parts = word.Split('/');
        if (Resolve(parts[0], _mesh.VertexCount) is not { } point || _unread.Contains(point))
        {
            return false;
        }

        points.Add(point);
        if (parts.Length > 1 && Resolve(parts[1], _uvs.Count / 2) is { } uv)
        {
            uvs.Add(new Vector2(_uvs[uv * 2], _uvs[(uv * 2) + 1]));
        }

        return true;
    }

    // F5: a lookup that adds on a miss, as a dictionary's putIfAbsent does.
    private List<int> GroupFaces()
    {
        if (_groups.TryGetValue(_group, out var faces))
        {
            return faces;
        }

        if (_groups.Count >= limits.Names)
        {
            throw TooMany();
        }

        faces = [];
        _groups[_group] = faces;
        return faces;
    }

    private FormatException TooMany() => new($"More than {limits.Names} names.");

    /// <summary>
    /// The zero-based index <paramref name="word"/> names among <paramref name="count"/> items, or
    /// null when it is not a number or is outside them. A negative one counts back from the last
    /// item read.
    /// </summary>
    private static int? Resolve(string word, int count)
    {
        // A parse alone reads 0x10 and +1, which other readers refuse.
        if (!IsInteger(word) || TextRules.ParseInt(word) is not { } number || number == 0)
        {
            return null;
        }

        var at = number > 0 ? number - 1 : count + number;
        return at >= 0 && at < count ? (int)at : null;
    }

    private static bool IsInteger(string word)
    {
        var digits = word.StartsWith('-') ? word.AsSpan(1) : word.AsSpan();
        return digits.Length > 0 && digits.IndexOfAnyExceptInRange('0', '9') < 0;
    }

    internal static double[]? Numbers(List<string> words, int count)
    {
        if (words.Count < count)
        {
            return null;
        }

        var output = new double[count];
        for (var i = 0; i < count; i++)
        {
            if (TextRules.ParseDouble(words[i]) is not { } value || !double.IsFinite(value))
            {
                return null;
            }

            output[i] = value;
        }

        return output;
    }

    // F5: a lookup that adds on a miss.
    private int MaterialNamed(string name) =>
        _materialAt.TryGetValue(name, out var at) ? at : AddMaterial(new GlbMaterial { Name = name });

    private void Define(GlbMaterial material)
    {
        if (_materialAt.TryGetValue(material.Name, out var at))
        {
            _materials[at] = material;
        }
        else
        {
            AddMaterial(material);
        }
    }

    // F5: says where the material went, as List.add does on a mesh.
    private int AddMaterial(GlbMaterial material)
    {
        if (_materials.Count >= limits.Names)
        {
            throw TooMany();
        }

        _materialAt[material.Name] = _materials.Count;
        _materials.Add(material);
        return _materials.Count - 1;
    }

    private void Library(string name)
    {
        var key = files.ContainsKey(name) ? name : name[(name.LastIndexOfAny(['\\', '/']) + 1)..];
        if (!files.TryGetValue(key, out var bytes))
        {
            Note($"the library {name} was not given.");
            return;
        }

        if (bytes.Length > limits.Bytes)
        {
            Note($"the library {name} is over {limits.Bytes} bytes.");
            return;
        }

        // A file that names one library a thousand times reads it once.
        if (_loaded.Add(key))
        {
            ReadMaterials(TextRules.DecodeLenient(bytes));
        }
    }

    private void ReadMaterials(string text)
    {
        MaterialDraft? draft = null;
        foreach (var raw in limits.LinesOf(text))
        {
            var hash = raw.IndexOf('#', StringComparison.Ordinal);
            var line = TextRules.Trim(hash < 0 ? raw : raw[..hash]);
            if (line.Length == 0)
            {
                continue;
            }

            var words = TextRules.SplitSpace(line);
            if (TextRules.ToLower(words[0]) == "newmtl")
            {
                Finish(draft);
                draft = new MaterialDraft(string.Join(' ', words.Skip(1)));
            }
            else if (draft is not null && !draft.Read(words))
            {
                Remark($"Material {draft.Name}: \"{line}\" was not read.");
            }
        }

        Finish(draft);
    }

    private void Finish(MaterialDraft? draft)
    {
        if (draft is null)
        {
            return;
        }

        Define(draft.Build());
        if (draft.Pictures)
        {
            Remark($"Material {draft.Name} has pictures, which were not read.");
        }
    }

    private void Note(string text) => Remark($"Line {_line}: {text}");

    private void Remark(string text)
    {
        if (_problems.Count < Listed)
        {
            _problems.Add(text);
        }
        else
        {
            _unlisted++;
        }
    }
}

/// <summary>A material being read out of an <c>.mtl</c>.</summary>
internal sealed class MaterialDraft(string name)
{
    private readonly double[] _colour = [0.8, 0.8, 0.8, 1.0];
    private readonly double[] _emissive = [0, 0, 0];
    private double? _roughness;
    private double? _exponent;
    private double _metallic;

    internal string Name { get; } = name;

    internal bool Pictures { get; private set; }

    /// <summary>Takes one line. False when its numbers are not numbers.</summary>
    internal bool Read(List<string> words)
    {
        var args = words.GetRange(1, words.Count - 1);
        switch (TextRules.ToLower(words[0]))
        {
            case "kd":
                return Into(_colour, args);
            case "ke":
                return Into(_emissive, args);
            case "d":
                return One(args, value => _colour[3] = value);
            case "tr":
                return One(args, value => _colour[3] = 1 - value);
            case "ns":
                return One(args, value => _exponent = value);
            case "pr":
                return One(args, value => _roughness = value);
            case "pm":
                return One(args, value => _metallic = value);
            case var other when IsPicture(other):
                Pictures = true;
                break;
        }

        return true;
    }

    private static bool IsPicture(string key) =>
        key.StartsWith("map_", StringComparison.Ordinal) || key is "bump" or "disp" or "decal";

    /// <summary>A colour. One value is a grey, which the format allows.</summary>
    private static bool Into(double[] output, List<string> args)
    {
        if (ObjReader.Numbers(args.Count == 1 ? [args[0], args[0], args[0]] : args, 3) is not { } values)
        {
            return false;
        }

        values.CopyTo(output, 0);
        return true;
    }

    private static bool One(List<string> args, Action<double> set)
    {
        if (ObjReader.Numbers(args, 1) is not { } values)
        {
            return false;
        }

        set(values[0]);
        return true;
    }

    /// <summary>
    /// A material from what was read. Roughness is <c>Pr</c> when the file has it. Otherwise it is
    /// the specular exponent run backwards through the mapping <c>ToObj</c> writes. <c>Ks</c> is
    /// not read for metalness, because OBJ files fill it with a half by default and every material
    /// would come out half metal; <c>Pm</c> is the one that says.
    /// </summary>
    internal GlbMaterial Build()
    {
        var roughness = _roughness
            ?? (_exponent is { } exponent ? 1 - Math.Sqrt(Math.Max(0, exponent - 1) / 900) : 0.5);
        return new GlbMaterial
        {
            Name = Name,
            Colour = Array.AsReadOnly(_colour.ToArray()),
            Metallic = Doubles.Clamp(_metallic, 0.0, 1.0),
            Roughness = Doubles.Clamp(roughness, 0.0, 1.0),
            Emissive = Array.AsReadOnly(_emissive.ToArray()),
        };
    }
}

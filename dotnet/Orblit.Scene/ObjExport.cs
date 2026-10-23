using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Orblit.Light;
using Orblit.Mesh;

namespace Orblit.Scene;

/// <summary>A scene as OBJ, with the material library that goes beside it.</summary>
/// <param name="Obj">The geometry.</param>
/// <param name="Mtl">The materials the geometry names.</param>
/// <param name="Problems">What the format could not carry, in the order it was met.</param>
public sealed record ObjScene(string Obj, string Mtl, IReadOnlyList<string> Problems);

/// <summary>
/// Writes a scene as one flattened OBJ and its material library.
/// </summary>
/// <remarks>
/// OBJ has no tree, no animation, no lights and no cameras, so this is a photograph of the scene
/// rather than the scene: every mesh is baked into world space and given a group of its own, which is
/// as much of the structure as the format can hold. What it is good for is handing a shape to
/// something that reads nothing else, such as a CNC path, a physics tool or an ancient importer, and
/// for that it is the best format there is.
/// <para>
/// Everything the format cannot carry is listed in <see cref="ObjScene.Problems"/> rather than
/// dropped in silence, so an export names what it left behind.
/// </para>
/// </remarks>
internal sealed partial class ObjWriter(SceneDocument scene, MaterialLibrary? materials, string library)
{
    // The space a name is trimmed of, and the space that splits it. They differ by one character.
    private static readonly char[] TrimSpace =
    [
        .. new[] { 0x09, 0x0a, 0x0b, 0x0c, 0x0d, 0x20, 0x85, 0xa0, 0x1680, 0x2028, 0x2029, 0x202f, 0x205f, 0x3000, 0xfeff }
            .Concat(Enumerable.Range(0x2000, 0x0b))
            .Select(static code => (char)code),
    ];

    private static readonly (string Key, string Slot)[] MapKeys =
    [
        ("map_Kd", "baseColour"),
        ("norm", "normal"),
        ("map_Pr", "metallicRoughness"),
        ("map_Ka", "occlusion"),
        ("map_Ke", "emissive"),
    ];

    private readonly StringBuilder _obj = new();
    private readonly List<string> _problems = [];
    private readonly OrderedDictionary<string, OrderedDictionary<string, string>> _named = [];
    private readonly Dictionary<string, string> _nameOf = [];
    private readonly Dictionary<string, Matrix4> _worlds = [];
    private readonly HashSet<string> _taken = [];

    // OBJ counts vertices from one and counts them across the whole file, not per object, so the
    // running totals below are the format's own bookkeeping rather than an optimisation.
    private int _vertices = 1;
    private int _normals = 1;
    private int _coordinates = 1;

    public ObjScene Write()
    {
        Line("# Written by Orblit");
        Line($"mtllib {library}");
        foreach (var entity in scene.Entities)
        {
            Add(entity);
        }

        var mtl = new StringBuilder();
        mtl.Append("# Written by Orblit\n");
        foreach (var (name, lines) in _named)
        {
            mtl.Append("\nnewmtl ").Append(name).Append('\n');
            foreach (var (key, value) in lines)
            {
                mtl.Append(key).Append(' ').Append(value).Append('\n');
            }
        }

        return new ObjScene(_obj.ToString(), mtl.ToString(), _problems);
    }

    private void Add(SceneEntity entity)
    {
        if (!entity.Visible)
        {
            return;
        }

        if (entity[SceneComponents.Mesh] is not MeshComponent mesh)
        {
            NoteUnmeshed(entity);
            return;
        }

        if (mesh.Asset is { } file && mesh.Geometry is null)
        {
            _problems.Add($"{entity.Name} draws {file} from a file, and an OBJ cannot point at another model. Export as glTF to keep it.");
            return;
        }

        var triangles = (mesh.Geometry ?? mesh.Shape?.Build())?.Triangulate();
        if (triangles is { Indices.Length: > 0 })
        {
            Object(entity, mesh, triangles);
        }
    }

    private void NoteUnmeshed(SceneEntity entity)
    {
        if (entity.Has(SceneComponents.Light) || entity.Has(SceneComponents.Camera))
        {
            _problems.Add($"{entity.Name} is a light or a camera, and OBJ has no word for either. Export as glTF to keep it.");
        }
    }

    private void Object(SceneEntity entity, MeshComponent mesh, Triangles triangles)
    {
        var world = WorldOf(entity);

        // A normal is not a direction the way a corner is a place: scale a box along one axis and its
        // sloped faces tilt the other way. The inverse transpose is what turns the corners' transform
        // into the normals'.
        var direction = world.GetRotation();
        direction.CopyInverse(direction);
        direction.Transpose();

        var asset = (entity[SceneComponents.Material] as MaterialComponent)?.Asset;

        // Two entities that end up saying the same thing are one material, and they are compared by
        // what they say rather than by where it came from: a colour a material overrides is not a
        // difference. A file that restated it per object would be four hundred identical blocks.
        var written = MtlOf(asset, mesh.Colour);
        var key = string.Join('\n', written.Select(static line => $"{line.Key} {line.Value}"));
        if (!_nameOf.TryGetValue(key, out var name))
        {
            name = _nameOf[key] = NameFor(entity, asset);
        }

        _named[name] = written;

        Line(string.Empty);
        Line($"o {Clean(entity.Name)}");
        Line($"g {Clean(entity.Name)}");

        var count = triangles.Positions.Length / 3;
        Corners(triangles.Positions, "v", count, p => world.Transformed3(p));
        var hasNormals = triangles.Normals.Length == triangles.Positions.Length;
        if (hasNormals)
        {
            Corners(triangles.Normals, "vn", count, n =>
            {
                var turned = direction.Transformed(n);
                turned.Normalize();
                return turned;
            });
        }

        var hasUvs = triangles.Uvs.Length == count * 2;
        if (hasUvs)
        {
            Coordinates(triangles.Uvs, count);
        }

        Line($"usemtl {name}");
        Faces(triangles.Indices, hasUvs ? _coordinates : null, hasNormals ? _normals : null);
        if (triangles.Groups.Count > 1)
        {
            _problems.Add(
                $"{entity.Name} paints {triangles.Groups.Count} materials onto its faces, and an OBJ group wears one. It is written in its first.");
        }

        _vertices += count;
        _normals += hasNormals ? count : 0;
        _coordinates += hasUvs ? count : 0;
    }

    private void Corners(float[] values, string tag, int count, Func<Vector3, Vector3> turn)
    {
        for (var i = 0; i < count; i++)
        {
            var at = turn(new Vector3(values[i * 3], values[(i * 3) + 1], values[(i * 3) + 2]));
            Line($"{tag} {Short(at.X)} {Short(at.Y)} {Short(at.Z)}");
        }
    }

    private void Coordinates(float[] uvs, int count)
    {
        for (var i = 0; i < count; i++)
        {
            // OBJ's v runs up from the bottom of the picture and glTF's runs down from the top, so one
            // of them has to be turned over and it is this one.
            Line($"vt {Short(uvs[i * 2])} {Short(1 - (double)uvs[(i * 2) + 1])}");
        }
    }

    private void Faces(uint[] indices, int? coordinates, int? normals)
    {
        for (var i = 0; i + 2 < indices.Length; i += 3)
        {
            var face = string.Join(' ', Enumerable.Range(0, 3).Select(c => Corner(indices[i + c], _vertices, coordinates, normals)));
            Line($"f {face}");
        }
    }

    private static string Corner(uint index, int vertices, int? coordinates, int? normals)
    {
        var v = index + (long)vertices;
        return (coordinates, normals) switch
        {
            (null, null) => Whole(v),
            (null, { } n) => $"{Whole(v)}//{Whole(index + (long)n)}",
            ({ } t, null) => $"{Whole(v)}/{Whole(index + (long)t)}",
            ({ } t, { } n) => $"{Whole(v)}/{Whole(index + (long)t)}/{Whole(index + (long)n)}",
        };
    }

    private static string Whole(long value) => value.ToString(CultureInfo.InvariantCulture);

    private Matrix4 WorldOf(SceneEntity entity)
    {
        if (_worlds.TryGetValue(entity.Id, out var cached))
        {
            return cached;
        }

        var local = entity[SceneComponents.Transform] is TransformComponent transform ? MatrixOf(transform) : Matrix4.Identity();
        var parent = entity.Parent is null ? null : scene[entity.Parent];
        return _worlds[entity.Id] = parent is null ? local : WorldOf(parent).Multiplied(local);
    }

    private static Matrix4 MatrixOf(TransformComponent transform)
    {
        var p = transform.Position;
        var s = transform.Scale;
        var matrix = Matrix4.TranslationValues(p.X, p.Y, p.Z);
        matrix.Multiply(Matrix4.RotationZ(Doubles.Radians(transform.Rotation.Z)));
        matrix.Multiply(Matrix4.RotationY(Doubles.Radians(transform.Rotation.Y)));
        matrix.Multiply(Matrix4.RotationX(Doubles.Radians(transform.Rotation.X)));
        matrix.Multiply(Matrix4.Diagonal3Values(s.X, s.Y, s.Z));
        return matrix;
    }

    /// <summary>A name no other material in this file has.</summary>
    private string NameFor(SceneEntity entity, string? asset)
    {
        var stem = Clean(asset is null ? entity.Name : asset.Split('/')[^1].Split('.')[0]);
        if (_taken.Add(stem))
        {
            return stem;
        }

        var n = 2;
        while (!_taken.Add($"{stem}.{n}"))
        {
            n++;
        }

        return $"{stem}.{n}";
    }

    /// <summary>OBJ splits its lines on spaces, so a name with one in it is two names.</summary>
    private static string Clean(string name)
    {
        var trimmed = Spaces().Replace(name.Trim(TrimSpace), "_");
        return trimmed.Length == 0 ? "unnamed" : trimmed;
    }

    [GeneratedRegex("[\\t\\n\\v\\f\\r \\u00a0\\u1680\\u2000-\\u200a\\u2028\\u2029\\u202f\\u205f\\u3000\\ufeff]+")]
    private static partial Regex Spaces();

    /// <summary>
    /// One material, as the lines an MTL file states it in.
    /// </summary>
    /// <remarks>
    /// The <c>P</c> keys are the de-facto PBR extension every modern tool that still reads OBJ
    /// understands; the <c>K</c> keys beside them are what everything older reads, so the file says
    /// the same thing twice in two vocabularies rather than choosing which half of the world to work
    /// with.
    /// </remarks>
    private OrderedDictionary<string, string> MtlOf(string? asset, Tint colour)
    {
        var resolved = asset is null ? null : materials?.Resolve(asset);
        var baseColour = resolved?.Numbers("baseColour");
        var linear = colour.Linear;
        double[] rgb = baseColour is { Count: >= 3 } ? [baseColour[0], baseColour[1], baseColour[2]] : [linear.X, linear.Y, linear.Z];
        var roughness = resolved?.Number("roughness") ?? 0.8;
        var metallic = resolved?.Number("metallic") ?? 0.0;

        var mtl = new OrderedDictionary<string, string>
        {
            ["Kd"] = string.Join(' ', rgb.Select(Short)),

            // Shininess from roughness by the usual inversion: mirror-smooth is a thousand, fully
            // rough is nothing.
            ["Ns"] = Short((1 - roughness) * (1 - roughness) * 1000),

            // Metals reflect their own colour; everything else reflects about four per cent of
            // white, which is the number the whole of PBR is built on.
            ["Ks"] = string.Join(' ', rgb.Select(c => Short((c * metallic) + (0.04 * (1 - metallic))))),
            ["Pr"] = Short(roughness),
            ["Pm"] = Short(metallic),
        };

        Finish(mtl, resolved, baseColour is { Count: 4 } ? baseColour[3] : 1.0);
        return mtl;
    }

    private static void Finish(OrderedDictionary<string, string> mtl, ResolvedMaterial? resolved, double alpha)
    {
        if (resolved?.Numbers("emissive") is { Count: >= 3 } emissive)
        {
            mtl["Ke"] = string.Join(' ', emissive.Take(3).Select(Short));
        }

        mtl["d"] = Short(alpha);
        mtl["illum"] = resolved?.Choice("shading") == "unlit" ? "0" : "2";
        foreach (var (key, slot) in MapKeys)
        {
            if (resolved?.Maps.GetValueOrDefault(slot) is { } path)
            {
                mtl[key] = path;
            }
        }
    }

    /// <summary>A number short enough to read and exact enough to reopen.</summary>
    internal static string Short(double value)
    {
        if (!double.IsFinite(value))
        {
            return "0";
        }

        if (value == Math.Floor(value) && Math.Abs(value) < 1e9)
        {
            return Whole((long)value);
        }

        var text = Doubles.ToStringAsFixed(value, 6).TrimEnd('0');
        return text.EndsWith('.') ? text[..^1] : text;
    }

    private void Line(string text) => _obj.Append(text).Append('\n');
}

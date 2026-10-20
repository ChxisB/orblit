using System.Text.Json;
using System.Text.Json.Nodes;
using Orblit.Json;
using Orblit.Parity;
using static Orblit.Parity.Dump;

namespace Orblit.Mesh.Tests;

/// <summary>Reads a mesh spec and writes results in the shape the Dart exporter wrote.</summary>
internal static class MeshDump
{
    public static JsonNode Vec(Vector3 v) => new JsonArray(Num(v.X), Num(v.Y), Num(v.Z));

    public static JsonNode Vec2(Vector2 v) => new JsonArray(Num(v.X), Num(v.Y));

    public static JsonNode Floats(IEnumerable<float> values) => Arr(values, x => Num((double)x));

    public static JsonNode Whole<T>(IEnumerable<T> values)
        where T : struct, IConvertible =>
        Arr(values, x => JsonValue.Create(x.ToInt64(null)));

    public static JsonNode? Box((Vector3 Min, Vector3 Max)? box) =>
        box is { } b ? new JsonObject { ["min"] = Vec(b.Min), ["max"] = Vec(b.Max) } : null;

    public static JsonNode? Box2((Vector2 Min, Vector2 Max)? box) =>
        box is { } b ? new JsonObject { ["min"] = Vec2(b.Min), ["max"] = Vec2(b.Max) } : null;

    public static JsonNode Edges(IEnumerable<MeshEdge> edges) =>
        Arr(edges, e => new JsonArray(JsonValue.Create(e.Item1), JsonValue.Create(e.Item2)));

    /// <summary>Positions as one flat list and each face as its corners, material, smoothing and coordinates.</summary>
    public static JsonNode Spec(Mesh mesh) => new JsonObject
    {
        ["p"] = new JsonArray([.. mesh.Positions.SelectMany(at => new[] { Num(at.X), Num(at.Y), Num(at.Z) })]),
        ["f"] = Arr(mesh.Faces, FaceSpec),
    };

    private static JsonNode FaceSpec(Face face)
    {
        var spec = new JsonObject
        {
            ["v"] = Arr(face.Vertices, v => JsonValue.Create(v)),
            ["m"] = face.Material,
            ["s"] = face.Smooth,
        };
        var uv = face.Uv.ToJson();
        if (uv.Count > 0)
        {
            spec["uv"] = Tree(uv);
        }

        return spec;
    }

    public static JsonNode Ids(Mesh mesh, IEnumerable<Face> faces) =>
        Arr(faces, f => JsonValue.Create(mesh.Faces.IndexOf(f)));

    public static JsonNode? IdOrNull(Mesh mesh, Face? face) =>
        face is null ? null : JsonValue.Create(mesh.Faces.IndexOf(face));

    public static JsonNode Size(Mesh mesh)
    {
        var area = 0.0;
        foreach (var face in mesh.Faces)
        {
            area += mesh.AreaOf(face);
        }

        return new JsonObject
        {
            ["faces"] = mesh.Faces.Count,
            ["positions"] = mesh.Positions.Count,
            ["box"] = Box(mesh.Bounds),
            ["area"] = Num(area),
        };
    }

    // reading a spec

    public static double D(JsonElement value) => Fixture.Number(value);

    public static JsonElement? Opt(JsonElement spec, string name) =>
        spec.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null ? value : null;

    public static double Or(JsonElement spec, string name, double fallback) =>
        Opt(spec, name) is { } value ? D(value) : fallback;

    public static int OrInt(JsonElement spec, string name, int fallback) =>
        Opt(spec, name) is { } value ? value.GetInt32() : fallback;

    public static bool Flag(JsonElement spec, string name) =>
        Opt(spec, name) is { ValueKind: JsonValueKind.True };

    public static bool OrFlag(JsonElement spec, string name, bool fallback) =>
        Opt(spec, name) is { } value ? value.GetBoolean() : fallback;

    public static Vector3 V3(JsonElement json) => new(D(json[0]), D(json[1]), D(json[2]));

    public static Vector2 V2(JsonElement json) => new(D(json[0]), D(json[1]));

    public static List<Vector3> Points(JsonElement json) => [.. json.EnumerateArray().Select(V3)];

    public static List<double> Doubles(JsonElement json) => [.. json.EnumerateArray().Select(D)];

    public static List<int> Ints(JsonElement json) => [.. json.EnumerateArray().Select(i => i.GetInt32())];

    public static byte[] Bytes(JsonElement json) => Convert.FromBase64String(json.GetString()!);

    /// <summary>A value the way the Dart reader sees it after JSON, for the readers that take any value.</summary>
    public static object? Decoded(JsonElement json) => DartJson.Decode(json.GetRawText());

    public static Mesh MeshOf(JsonElement spec)
    {
        var p = Doubles(spec.GetProperty("p"));
        var positions = new List<Vector3>();
        for (var i = 0; i + 2 < p.Count; i += 3)
        {
            positions.Add(new Vector3(p[i], p[i + 1], p[i + 2]));
        }

        var faces = spec.GetProperty("f").EnumerateArray().Select(FaceOf).ToList();
        return new Mesh(positions, faces);
    }

    private static Face FaceOf(JsonElement raw) => new(
        Ints(raw.GetProperty("v")),
        raw.GetProperty("m").GetInt32(),
        raw.GetProperty("s").GetBoolean(),
        Opt(raw, "uv") is { } uv ? FaceUv.FromJson(Decoded(uv)) : null);

    public static GlbMaterial MaterialOf(JsonElement spec) => new()
    {
        Name = spec.GetProperty("name").GetString()!,
        Colour = Doubles(spec.GetProperty("colour")),
        Metallic = D(spec.GetProperty("metallic")),
        Roughness = D(spec.GetProperty("roughness")),
        Emissive = Doubles(spec.GetProperty("emissive")),
        DoubleSided = spec.GetProperty("doubleSided").GetBoolean(),
        Cutout = spec.GetProperty("cutout").GetBoolean(),
    };

    public static ReadLimits LimitsOf(JsonElement spec) => Opt(spec, "limits") is { } l
        ? new ReadLimits
        {
            Bytes = OrInt(l, "bytes", 128 << 20),
            Vertices = OrInt(l, "vertices", 4000000),
            Faces = OrInt(l, "faces", 4000000),
            Corners = OrInt(l, "corners", 1024),
            Indices = OrInt(l, "indices", 16000000),
            Names = OrInt(l, "names", 100000),
            Line = OrInt(l, "line", 1 << 20),
        }
        : new ReadLimits();

    public static Dictionary<string, byte[]> FilesOf(JsonElement spec) =>
        Opt(spec, "files") is { } files
            ? files.EnumerateObject().ToDictionary(e => e.Name, e => Bytes(e.Value))
            : [];

    public static Shape ShapeOf(JsonElement s)
    {
        var shape = Shape.Of(Enum.Parse<ShapeKind>(s.GetProperty("kind").GetString()!, ignoreCase: true));
        return shape with
        {
            Width = Or(s, "width", shape.Width),
            Height = Or(s, "height", shape.Height),
            Depth = Or(s, "depth", shape.Depth),
            Sides = OrInt(s, "sides", shape.Sides),
            Rings = OrInt(s, "rings", shape.Rings),
            Columns = OrInt(s, "columns", shape.Columns),
            Steps = OrInt(s, "steps", shape.Steps),
            Subdivisions = OrInt(s, "subdivisions", shape.Subdivisions),
            WidthCuts = OrInt(s, "widthCuts", shape.WidthCuts),
            HeightCuts = OrInt(s, "heightCuts", shape.HeightCuts),
            Thickness = Or(s, "thickness", shape.Thickness),
            TubeRadius = Or(s, "tubeRadius", shape.TubeRadius),
            Circumference = Or(s, "circumference", shape.Circumference),
            PedimentHeight = Or(s, "pedimentHeight", shape.PedimentHeight),
            SideWidth = Or(s, "sideWidth", shape.SideWidth),
            StepHeight = Or(s, "stepHeight", shape.StepHeight),
            Capped = OrFlag(s, "capped", shape.Capped),
            Smooth = OrFlag(s, "smooth", shape.Smooth),
            ByCount = OrFlag(s, "byCount", shape.ByCount),
        };
    }
}

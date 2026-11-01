using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using Orblit.Parity;
using static Orblit.Mesh.Tests.MeshDump;
using static Orblit.Parity.Dump;

namespace Orblit.Mesh.Tests;

/// <summary>
/// Runs the cases the fixture exporter wrote for orblit_mesh and compares what the port gives with what
/// the fixture holds, including the cases that were refused.
/// </summary>
public class MeshParityTests
{
    private static void Check(string op, Func<JsonElement, JsonNode?> run) =>
        Fixture.Load("orblit_mesh", "mesh").Check(op, s => Attempt(() => run(s)));

    // the shapes

    [Fact]
    public void ShapesBuildTheSameMeshes() => Check("shape", s => Spec(ShapeOf(s).Build()));

    [Fact]
    public void ShapesHaveTheSameSizeAtAnyCount() => Check("shapeSize", s => Size(ShapeOf(s).Build()));

    // editing

    [Fact]
    public void EditsGiveTheSameMesh() => Check("edit", MeshSteps.Run);

    [Fact]
    public void ActionsGiveTheSameMesh() => Check("action", MeshSteps.Run);

    [Fact]
    public void UvEditsGiveTheSameMesh() => Check("uvEdit", MeshSteps.Run);

    [Fact]
    public void CutsGiveTheSameMesh() => Check("cut", MeshSteps.Run);

    // what a renderer takes

    [Fact]
    public void TrianglesAreTheSame() => Check("triangulate", s =>
    {
        var t = MeshOf(s.GetProperty("mesh")).Triangulate();
        return new JsonObject
        {
            ["positions"] = Floats(t.Positions),
            ["normals"] = Floats(t.Normals),
            ["uvs"] = Floats(t.Uvs),
            ["indices"] = Whole(t.Indices),
            ["groups"] = Arr(t.Groups, g => new JsonArray(g.Material, g.Start, g.Count)),
            ["tangents"] = Floats(MeshTangents.TangentsOf(t)),
        };
    });

    // the plain functions

    [Fact]
    [SuppressMessage("Maintainability", "CA1502:Avoid excessive complexity", Justification = "F2: a list of cases is data.")]
    public void FunctionsGiveTheSameAnswers() => Check("fn", s =>
    {
        switch (s.GetProperty("f").GetString())
        {
            case "cutUp":
                return Whole(MeshTriangles.CutUp(Points(s.GetProperty("points")), V3(s.GetProperty("normal"))));
            case "signedLoopArea":
                return Num(MeshCut.SignedLoopArea(Points(s.GetProperty("points")), V3(s.GetProperty("normal"))));
            case "signedAreaOf":
                return Num(Drawing.SignedAreaOf(Points(s.GetProperty("points")), V3(s.GetProperty("normal"))));
            case "outlineCrosses":
                return Drawing.OutlineCrosses(Points(s.GetProperty("points")), V3(s.GetProperty("normal")));
            case "flattenFace":
                return Flatten(s);
            case "closesOutline":
                return Opt(s, "reach") is { } reach
                    ? Drawing.ClosesOutline(Points(s.GetProperty("points")), V3(s.GetProperty("at")), D(reach))
                    : Drawing.ClosesOutline(Points(s.GetProperty("points")), V3(s.GetProperty("at")));
            case "isNewPoint":
                return Drawing.IsNewPoint(Points(s.GetProperty("points")), V3(s.GetProperty("at")));
            case "pointInsideOutline":
                return MeshCut.PointInsideOutline([.. s.GetProperty("outline").EnumerateArray().Select(V2)], V2(s.GetProperty("at")));
            case "distanceToSegment":
                return Num(Drawing.DistanceToSegment(V2(s.GetProperty("at")), V2(s.GetProperty("a")), V2(s.GetProperty("b"))));
            case "turnBetween":
                return Num(Drawing.TurnBetween(V2(s.GetProperty("a")), V2(s.GetProperty("b"))));
            case "angleBetween":
                return Num(MeshTriangles.AngleBetween(V3(s.GetProperty("a")), V3(s.GetProperty("b"))));
            case "onPlane":
                return OnPlane(s);
            case "edgeOf":
                var pair = MeshActions.EdgeOf(s.GetProperty("a").GetInt32(), s.GetProperty("b").GetInt32());
                return new JsonArray(pair.Item1, pair.Item2);
            default:
                throw new InvalidOperationException($"no function {s.GetProperty("f")}");
        }
    });

    private static JsonObject Flatten(JsonElement s)
    {
        var flat = MeshCut.FlattenFace(Points(s.GetProperty("points")), V3(s.GetProperty("normal")));
        return new JsonObject
        {
            ["flat"] = Arr(flat.Flat, Vec2),
            ["u"] = Vec(flat.U),
            ["v"] = Vec(flat.V),
        };
    }

    private static JsonNode? OnPlane(JsonElement s)
    {
        var hit = PolyShape.OnPlane(
            V3(s.GetProperty("origin")),
            V3(s.GetProperty("direction")),
            V3(s.GetProperty("planePoint")),
            V3(s.GetProperty("planeNormal")));
        return hit is { } at ? Vec(at) : null;
    }

    [Fact]
    public void DrawnShapesBuildTheSameMeshes() => Check("poly", s =>
    {
        var shape = new PolyShape(Points(s.GetProperty("points")), D(s.GetProperty("height")), s.GetProperty("flipped").GetBoolean());
        var plane = shape.Plane;
        return new JsonObject
        {
            ["drawable"] = shape.IsDrawable,
            ["centre"] = Vec(plane.Centre),
            ["normal"] = Vec(plane.Normal),
            ["mesh"] = Spec(shape.Build()),
        };
    });

    [Fact]
    public void FaceCoordinatesAreTheSame() => Check("faceUv", s =>
    {
        var uv = FaceUv.FromJson(Decoded(s.GetProperty("uv")))!;
        var normal = V3(s.GetProperty("normal"));
        var axes = FaceUv.AxesFor(normal);
        return new JsonObject
        {
            ["coordinates"] = Arr(uv.ForFace(Points(s.GetProperty("points")), normal), Vec2),
            ["again"] = Tree(uv.ToJson()),
            ["u"] = Vec(axes.U),
            ["v"] = Vec(axes.V),
        };
    });

    [Fact]
    public void AnyJsonValueReadsTheSameWay() => Check("readJson", s =>
    {
        var value = Decoded(s.GetProperty("value"));
        switch (s.GetProperty("what").GetString())
        {
            case "uv":
                return FaceUv.FromJson(value) is { } uv ? Tree(uv.ToJson()) : null;
            case "mesh":
                return Mesh.FromJson(value) is { } mesh ? Spec(mesh) : null;
            case "shape":
                return Shape.FromJson(value) is { } shape ? Tree(shape.ToJson()) : null;
            default:
                return PolyShape.FromJson(value) is { } poly ? Tree(poly.ToJson()) : null;
        }
    });

    // writing

    [Fact]
    public void FilesAreWrittenByteForByte() => Check("export", s =>
    {
        var mesh = MeshOf(s.GetProperty("mesh"));
        var materials = s.GetProperty("materials").EnumerateArray().Select(MaterialOf).ToList();
        var written = mesh.WriteAs(
            Enum.Parse<MeshFormat>(s.GetProperty("format").GetString()!, ignoreCase: true),
            s.GetProperty("name").GetString()!,
            materials,
            s.GetProperty("binary").GetBoolean());
        return Arr(written, w => new JsonObject
        {
            ["name"] = w.Name,
            ["bytes"] = Convert.ToBase64String(w.Bytes),
        });
    });

    // reading

    [Fact]
    public void ObjFilesReadTheSameWay() => Check("readObj", s =>
    {
        var model = ObjFiles.ReadObj(Bytes(s.GetProperty("bytes")), FilesOf(s), LimitsOf(s));
        return new JsonObject
        {
            ["mesh"] = Spec(model.Mesh),
            ["groups"] = Arr(model.Groups, g => new JsonArray(g.Name, Arr(g.Faces, i => JsonValue.Create(i)))),
            ["materials"] = Arr(model.Materials, m => Tree(m.ToGltf())),
            ["problems"] = Arr(model.Problems, Text),
        };
    });

    [Fact]
    public void StlFilesReadTheSameWay() =>
        Check("readStl", s => Spec(StlFiles.ReadStl(Bytes(s.GetProperty("bytes")), LimitsOf(s))));

    [Fact]
    public void PlyFilesReadTheSameWay() =>
        Check("readPly", s => Spec(PlyFiles.ReadPly(Bytes(s.GetProperty("bytes")), LimitsOf(s))));

    // the glTF container

    [Fact]
    public void GlbChunksSplitTheSameWay() => Check("glbChunks", s =>
    {
        if (GltfFiles.GlbChunks(Bytes(s.GetProperty("bytes"))) is not { } chunks)
        {
            return null;
        }

        return new JsonObject
        {
            ["json"] = Tree(chunks.Json),
            ["binary"] = Convert.ToBase64String(chunks.Binary),
        };
    });

    [Fact]
    public void GltfPartsSplitTheSameWay() => Check("gltfParts", s =>
    {
        var parts = GltfFiles.GltfParts(Bytes(s.GetProperty("bytes")), FilesOf(s));
        return new JsonObject
        {
            ["json"] = Tree(parts.Json),
            ["binary"] = parts.Binary is { } binary ? Convert.ToBase64String(binary) : null,
        };
    });

    [Fact]
    public void BoundsAreTheSame() => Check("bounds", s =>
    {
        var bytes = Bytes(s.GetProperty("bytes"));
        return Box(s.GetProperty("kind").GetString() switch
        {
            "glb" => MeshExport.BoundsOfGlb(bytes),
            "obj" => ObjFiles.BoundsOfObj(bytes),
            _ => MeshExport.BoundsOfGltf(TextRules.Decode(bytes)),
        });
    });

    [Fact]
    public void AccessorsReadTheSameWay() => Check("accessors", s =>
    {
        var json = (IReadOnlyDictionary<string, object?>)Decoded(s.GetProperty("json"))!;
        var binary = Opt(s, "binary") is { } b ? Bytes(b) : null;
        var reader = new GltfAccessors(json, binary);
        var results = s.GetProperty("calls").EnumerateArray().Select(call => Call(reader, call)).ToList();
        return new JsonObject
        {
            ["results"] = new JsonArray([.. results]),
            ["problems"] = Arr(reader.Problems, Text),
        };
    });

    private static JsonNode? Call(GltfAccessors reader, JsonElement call)
    {
        int? at = Opt(call, "at") is { } index ? index.GetInt32() : null;
        return call.GetProperty("f").GetString() switch
        {
            "count" => reader.Count(at),
            "floats" => Arr(reader.Floats(at, call.GetProperty("n").GetInt32()), x => Num((double)x)),
            _ => Whole(reader.Indices(at!.Value)),
        };
    }
}

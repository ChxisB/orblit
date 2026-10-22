using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using static Orblit.Mesh.Tests.MeshDump;
using static Orblit.Parity.Dump;

namespace Orblit.Mesh.Tests;

/// <summary>Runs one step of an edit sequence the way the fixture exporter ran it.</summary>
internal static class MeshSteps
{
    private static List<Face> Which(Mesh mesh, JsonElement ids) =>
        ids.ValueKind == JsonValueKind.String
            ? [.. mesh.Faces]
            : [.. ids.EnumerateArray().Select(i => mesh.Faces[i.GetInt32()])];

    private static MeshEdge EdgeOf(JsonElement e) => (e[0].GetInt32(), e[1].GetInt32());

    private static List<MeshEdge> EdgeList(JsonElement es) => [.. es.EnumerateArray().Select(EdgeOf)];

    private static Face FaceAt(Mesh mesh, JsonElement spec) => mesh.Faces[spec.GetProperty("face").GetInt32()];

    private static Matrix3 M9(JsonElement json)
    {
        var matrix = new Matrix3();
        for (var index = 0; index < 9; index++)
        {
            matrix[index] = D(json[index]);
        }

        return matrix;
    }

    private static Matrix4 M16(JsonElement json)
    {
        var matrix = Matrix4.Zero();
        for (var index = 0; index < 16; index++)
        {
            matrix[index] = D(json[index]);
        }

        return matrix;
    }

    public static JsonNode Run(JsonElement spec)
    {
        var mesh = MeshOf(spec.GetProperty("mesh"));
        var results = spec.GetProperty("steps").EnumerateArray().Select(step => Step(mesh, step)).ToList();
        return new JsonObject { ["results"] = new JsonArray([.. results]), ["mesh"] = Spec(mesh) };
    }

    private static JsonNode? Step(Mesh mesh, JsonElement s) => s.GetProperty("do").GetString() switch
    {
        "whereOn" or "cutFace" => CutStep(mesh, s),
        "freezeUvs" or "releaseUvs" or "nudgeUvs" or "scaleUvs" or "turnUvs" or "projectPlanar"
            or "projectBox" or "fitUvs" or "flipUvs" or "uvsOf" or "uvBoundsOf" => UvStep(mesh, s),
        "extrude" or "inset" or "subdivide" or "deleteFaces" or "flipFaces" or "weld" or "compact"
            or "moveVertices" or "verticesOf" or "edgesOf" or "openFaces" or "movePoints" or "turnPoints"
            or "scalePoints" or "grown" or "measure" or "flip" or "merge" or "transform" => EditStep(mesh, s),
        _ => ActionStep(mesh, s),
    };

    [SuppressMessage("Maintainability", "CA1502:Avoid excessive complexity", Justification = "F2: a list of cases is data.")]
    private static JsonNode? EditStep(Mesh mesh, JsonElement s)
    {
        switch (s.GetProperty("do").GetString())
        {
            case "extrude":
                return Ids(mesh, mesh.Extrude(Which(mesh, s.GetProperty("faces")), D(s.GetProperty("n"))));
            case "inset":
                return Ids(mesh, mesh.Inset(Which(mesh, s.GetProperty("faces")), D(s.GetProperty("n"))));
            case "subdivide":
                return Ids(mesh, mesh.Subdivide(Which(mesh, s.GetProperty("faces"))));
            case "deleteFaces":
                mesh.DeleteFaces(Which(mesh, s.GetProperty("faces")));
                return null;
            case "flipFaces":
                mesh.FlipFaces(Which(mesh, s.GetProperty("faces")));
                return null;
            case "weld":
                return Opt(s, "within") is { } within ? mesh.Weld(D(within)) : mesh.Weld();
            case "compact":
                return mesh.Compact();
            case "moveVertices":
                mesh.MoveVertices(Ints(s.GetProperty("verts")), V3(s.GetProperty("by")));
                return null;
            case "verticesOf":
                return Arr(MeshEdits.VerticesOf(Which(mesh, s.GetProperty("faces"))).Order(), v => JsonValue.Create(v));
            case "edgesOf":
                return Edges(MeshEdits.EdgesOf(Which(mesh, s.GetProperty("faces"))));
            case "openFaces":
                return Ids(mesh, mesh.OpenFaces);
            case "movePoints":
                mesh.MovePoints(Ints(s.GetProperty("verts")), V3(s.GetProperty("by")));
                return null;
            case "turnPoints":
                mesh.TurnPoints(Ints(s.GetProperty("verts")), M9(s.GetProperty("matrix")), V3(s.GetProperty("about")));
                return null;
            case "scalePoints":
                mesh.ScalePoints(Ints(s.GetProperty("verts")), V3(s.GetProperty("by")), V3(s.GetProperty("about")));
                return null;
            case "grown":
                return Spec(mesh.Grown(D(s.GetProperty("n"))));
            case "measure":
                return Measure(mesh);
            case "flip":
                mesh.Flip();
                return null;
            case "merge":
                mesh.Merge(MeshOf(s.GetProperty("other")));
                return null;
            default:
                mesh.Transform(M16(s.GetProperty("matrix")));
                return null;
        }
    }

    private static JsonObject Measure(Mesh mesh) => new JsonObject
    {
        ["faces"] = Arr(mesh.Faces, f => new JsonObject
        {
            ["n"] = Vec(mesh.NormalOf(f)),
            ["c"] = Vec(mesh.CentreOf(f)),
            ["a"] = Num(mesh.AreaOf(f)),
        }),
        ["box"] = Box(mesh.Bounds),
    };

    [SuppressMessage("Maintainability", "CA1502:Avoid excessive complexity", Justification = "F2: a list of cases is data.")]
    private static JsonNode? ActionStep(Mesh mesh, JsonElement s)
    {
        switch (s.GetProperty("do").GetString())
        {
            case "facesOn":
                return Ids(mesh, mesh.FacesOn(EdgeOf(s.GetProperty("edge"))));
            case "allEdges":
                return Edges(mesh.AllEdges);
            case "openEdges":
                return Edges(mesh.OpenEdges);
            case "facesAt":
                return Ids(mesh, mesh.FacesAt(s.GetProperty("v").GetInt32()));
            case "grow":
                return Ids(mesh, mesh.Grow(Which(mesh, s.GetProperty("faces")), Opt(s, "angle") is { } a ? D(a) : null));
            case "shrink":
                return Ids(mesh, MeshActions.Shrink(Which(mesh, s.GetProperty("faces"))));
            case "edgeLoop":
                return Edges(mesh.EdgeLoop(EdgeOf(s.GetProperty("edge"))));
            case "edgeRing":
                return Edges(mesh.EdgeRing(EdgeOf(s.GetProperty("edge"))));
            case "faceLoop":
                return Ids(mesh, mesh.FaceLoop(FaceAt(mesh, s)));
            case "collapse":
                return mesh.Collapse(Ints(s.GetProperty("verts")), toFirst: Flag(s, "toFirst"));
            case "split":
                return mesh.Split(Ints(s.GetProperty("verts")));
            case "fillHole":
                return Ids(mesh, mesh.FillHole(Ints(s.GetProperty("verts"))));
            case "bevel":
                return Ids(mesh, mesh.Bevel(EdgeList(s.GetProperty("edges")), D(s.GetProperty("n"))));
            case "bridge":
                return IdOrNull(mesh, mesh.Bridge(EdgeOf(s.GetProperty("a")), EdgeOf(s.GetProperty("b"))));
            case "subdivideEdges":
                return Whole(mesh.SubdivideEdges(EdgeList(s.GetProperty("edges")), OrInt(s, "into", 1)));
            case "detach":
                return Spec(mesh.Detach(Which(mesh, s.GetProperty("faces"))));
            case "duplicateFaces":
                return Spec(mesh.DuplicateFaces(Which(mesh, s.GetProperty("faces"))));
            case "mergeFaces":
                return IdOrNull(mesh, mesh.MergeFaces(Which(mesh, s.GetProperty("faces"))));
            case "triangulateFaces":
                return Ids(mesh, mesh.TriangulateFaces(Which(mesh, s.GetProperty("faces"))));
            case "conformNormals":
                return Opt(s, "faces") is { } faces ? mesh.ConformNormals(Which(mesh, faces)) : mesh.ConformNormals();
            case "centrePivotOn":
                return Vec(mesh.CentrePivotOn(Ints(s.GetProperty("verts"))));
            case "mirrored":
                return Spec(mesh.Mirrored(Flag(s, "x"), Flag(s, "y"), Flag(s, "z")));
            default:
                throw new InvalidOperationException($"no step {s.GetProperty("do")}");
        }
    }

    [SuppressMessage("Maintainability", "CA1502:Avoid excessive complexity", Justification = "F2: a list of cases is data.")]
    private static JsonNode? UvStep(Mesh mesh, JsonElement s)
    {
        switch (s.GetProperty("do").GetString())
        {
            case "freezeUvs":
                mesh.FreezeUvs(Which(mesh, s.GetProperty("faces")));
                return null;
            case "releaseUvs":
                MeshUvs.ReleaseUvs(Which(mesh, s.GetProperty("faces")));
                return null;
            case "nudgeUvs":
                MeshUvs.NudgeUvs(Which(mesh, s.GetProperty("faces")), V2(s.GetProperty("by")));
                return null;
            case "scaleUvs":
                mesh.ScaleUvs(Which(mesh, s.GetProperty("faces")), V2(s.GetProperty("by")), Opt(s, "about") is { } a ? V2(a) : null);
                return null;
            case "turnUvs":
                mesh.TurnUvs(Which(mesh, s.GetProperty("faces")), D(s.GetProperty("n")), Opt(s, "about") is { } t ? V2(t) : null);
                return null;
            case "projectPlanar":
                mesh.ProjectPlanar(Which(mesh, s.GetProperty("faces")));
                return null;
            case "projectBox":
                mesh.ProjectBox(Which(mesh, s.GetProperty("faces")));
                return null;
            case "fitUvs":
                mesh.FitUvs(Which(mesh, s.GetProperty("faces")), OrFlag(s, "keepShape", true));
                return null;
            case "flipUvs":
                mesh.FlipUvs(Which(mesh, s.GetProperty("faces")), Flag(s, "u"), Flag(s, "v"));
                return null;
            case "uvsOf":
                return Arr(mesh.UvsOf(FaceAt(mesh, s)), Vec2);
            default:
                return Box2(mesh.UvBoundsOf(Which(mesh, s.GetProperty("faces"))));
        }
    }

    private static JsonNode? CutStep(Mesh mesh, JsonElement s)
    {
        var face = FaceAt(mesh, s);
        if (s.GetProperty("do").GetString() == "whereOn")
        {
            var where = Opt(s, "reach") is { } reach
                ? mesh.WhereOn(face, V3(s.GetProperty("at")), D(reach))
                : mesh.WhereOn(face, V3(s.GetProperty("at")));
            return where switch
            {
                AtCorner c => new JsonArray("corner", c.Corner),
                AlongEdge e => new JsonArray("along", e.Corner, Num(e.Along)),
                _ => new JsonArray("inside"),
            };
        }

        var path = Points(s.GetProperty("path"));
        return Ids(mesh, Opt(s, "reach") is { } r ? mesh.CutFace(face, path, D(r)) : mesh.CutFace(face, path));
    }
}

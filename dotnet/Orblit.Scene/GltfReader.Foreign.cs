using Orblit.Light;
using Orblit.Mesh;

namespace Orblit.Scene;

/// <summary>The half of the reader for a document from somewhere else, read from what glTF has in common with a scene.</summary>
internal sealed partial class GltfReader
{
    private const string Punctual = "KHR_lights_punctual";

    private SceneEntity Interpret(int at, string id)
    {
        var node = _nodes[at];
        var components = new OrderedDictionary<string, SceneComponent>();

        if (Transform(node) is { } placed)
        {
            components[SceneComponents.Transform] = placed;
        }

        if (Mesh(node) is { } mesh)
        {
            components[SceneComponents.Mesh] = mesh;
        }

        if (Light(node) is { } light)
        {
            components[SceneComponents.Light] = light;
        }

        if (Camera(node) is { } camera)
        {
            components[SceneComponents.Camera] = camera;
        }

        return new SceneEntity(id, GltfJson.Text(node.GetValueOrDefault("name")) ?? $"Node {at}", ParentId(at), components: components);
    }

    private static TransformComponent? Transform(JsonMap node)
    {
        var matrix = GltfJson.Doubles(node.GetValueOrDefault("matrix"));
        return matrix.Count == 16 ? TransformOfMatrix(matrix) : TransformOfParts(node);
    }

    private static TransformComponent TransformOfMatrix(List<double> values)
    {
        // Column-major, which is how glTF states it and how Matrix4 reads a flat list, so the two
        // need no rearranging between them.
        var m = Matrix4.Zero();
        for (var i = 0; i < 16; i++)
        {
            m[i] = values[i];
        }

        var scale = new Vector3(LengthOf(m.GetColumn(0)), LengthOf(m.GetColumn(1)), LengthOf(m.GetColumn(2)));
        var rotation = m.GetRotation();
        for (var column = 0; column < 3; column++)
        {
            var by = scale[column];
            if (by == 0)
            {
                continue;
            }

            for (var row = 0; row < 3; row++)
            {
                rotation[(column * 3) + row] = rotation.Entry(row, column) / by;
            }
        }

        return new TransformComponent(m.GetTranslation(), TransformComponent.AnglesOf(rotation), scale);
    }

    private static double LengthOf(Vector4 column) => new Vector3(column.X, column.Y, column.Z).Length;

    private static TransformComponent? TransformOfParts(JsonMap node)
    {
        var translation = GltfJson.Doubles(node.GetValueOrDefault("translation"));
        var quaternion = GltfJson.Doubles(node.GetValueOrDefault("rotation"));
        var scale = GltfJson.Doubles(node.GetValueOrDefault("scale"));
        if (translation.Count != 3 && quaternion.Count != 4 && scale.Count != 3)
        {
            return null;
        }

        return new TransformComponent(
            translation.Count == 3 ? new Vector3(translation[0], translation[1], translation[2]) : null,
            quaternion.Count == 4 ? TransformComponent.AnglesOf(QuaternionOf(quaternion).AsRotationMatrix()) : null,
            scale.Count == 3 ? new Vector3(scale[0], scale[1], scale[2]) : null);
    }

    private static Quaternion QuaternionOf(List<double> parts) => new(parts[0], parts[1], parts[2], parts[3]);

    private LightComponent? Light(JsonMap node)
    {
        if (LightOf(node) is not { } light)
        {
            return null;
        }

        var ours = GltfJson.OursOn(light);
        var kind = LightKindOf(light, ours);
        var (size, blend) = SpotOf(Values.Object(light.GetValueOrDefault("spot")));
        var colour = GltfJson.Doubles(light.GetValueOrDefault("color"));

        return new LightComponent(
            kind: kind,
            power: PowerOf(kind, GltfJson.Number(light.GetValueOrDefault("intensity")) ?? 1),
            colour: colour.Count == 3 ? Tint.FromLinear(new Vector3(colour[0], colour[1], colour[2])) : Tint.White,
            spotSize: size,
            spotBlend: blend,
            sourceRadius: GltfJson.Number(ours?.GetValueOrDefault("sourceRadius")) ?? 0.1);
    }

    /// <summary>The light a node names, or null with a problem when the document does not have it.</summary>
    private JsonMap? LightOf(JsonMap node)
    {
        if (GltfJson.Index(PunctualOn(node).GetValueOrDefault("light")) is not { } at)
        {
            return null;
        }

        var all = GltfJson.List(PunctualOn(_json).GetValueOrDefault("lights"));
        if (at >= all.Count)
        {
            _problems.Add($"A node names light {at}, which the document does not have.");
            return null;
        }

        return Values.Object(all[(int)at]);
    }

    private static JsonMap PunctualOn(JsonMap holder) =>
        Values.Object(Values.Object(holder.GetValueOrDefault("extensions")).GetValueOrDefault(Punctual));

    private static LightType LightKindOf(JsonMap light, JsonMap? ours) => GltfJson.Text(light.GetValueOrDefault("type")) switch
    {
        "directional" => LightType.Sun,
        "spot" => LightType.Spot,

        // An area light is written as a point, because glTF has no word for a shape that emits. What
        // says it was one is the note the exporter left.
        _ => GltfJson.Text(ours?.GetValueOrDefault("kind")) == "area" ? LightType.Area : LightType.Point,
    };

    /// <summary>
    /// The exporter's conversion, run backwards. A sun's strength is an illuminance and everything
    /// else's is an intensity, which is the one place the two units part company.
    /// </summary>
    private static double PowerOf(LightType kind, double intensity) => kind == LightType.Sun
        ? intensity / Photometry.LuminousEfficacy
        : Photometry.CandelaToLumens(intensity) / Photometry.LuminousEfficacy;

    private static (double Size, double Blend) SpotOf(JsonMap spot)
    {
        var outer = GltfJson.Number(spot.GetValueOrDefault("outerConeAngle")) ?? Math.PI / 4;
        var inner = GltfJson.Number(spot.GetValueOrDefault("innerConeAngle")) ?? 0;
        return (Doubles.Degrees(outer) * 2, outer <= 0 ? 0 : Doubles.Clamp(1 - (inner / outer), 0, 1));
    }

    private CameraComponent? Camera(JsonMap node)
    {
        if (GltfJson.Index(node.GetValueOrDefault("camera")) is not { } at || at >= _cameras.Count)
        {
            return null;
        }

        var camera = _cameras[(int)at];
        if (GltfJson.Text(camera.GetValueOrDefault("type")) == "orthographic")
        {
            _problems.Add(
                $"Camera {at} is orthographic, which this engine has no equivalent for; "
                + "it was read as a perspective one.");
        }

        // The angle the exporter wrote down, when it is there. glTF states the vertical angle and
        // Orblit states the angle across the shorter side, so the two agree on a landscape frame and
        // part company on a portrait one. What was authored is worth more than a conversion either way.
        var lens = Values.Object(camera.GetValueOrDefault("perspective"));
        var stated = GltfJson.Number(GltfJson.OursOn(camera)?.GetValueOrDefault("fieldOfView"));

        return new CameraComponent(
            stated ?? Doubles.Degrees(GltfJson.Number(lens.GetValueOrDefault("yfov")) ?? Doubles.Radians(50)),
            GltfJson.Number(lens.GetValueOrDefault("znear")) ?? 0.1,
            GltfJson.Number(lens.GetValueOrDefault("zfar")) ?? 1000);
    }

    private MeshComponent? Mesh(JsonMap node)
    {
        if (GltfJson.Index(node.GetValueOrDefault("mesh")) is not { } at || at >= _meshes.Count)
        {
            return null;
        }

        var positions = new List<Vector3>();
        var faces = new List<Face>();
        var colour = Tint.Hex(0xD9634F);
        var toldAboutModes = false;

        foreach (var primitive in GltfJson.Maps(_meshes[(int)at].GetValueOrDefault("primitives")))
        {
            // 4 is a triangle list. A strip, a fan or a line is geometry this engine's editable mesh
            // has no way to hold, and quietly dropping a third of a model is worse than saying which
            // third went.
            if ((GltfJson.Index(primitive.GetValueOrDefault("mode")) ?? 4) != 4)
            {
                if (!toldAboutModes)
                {
                    _problems.Add($"Mesh {at} has primitives that are not triangle lists; those were not read.");
                    toldAboutModes = true;
                }

                continue;
            }

            var corners = CornersOf(primitive);
            if (corners.Count == 0)
            {
                continue;
            }

            faces.AddRange(TrianglesOf(primitive, positions.Count, corners.Count));
            positions.AddRange(corners);
            colour = WornBy(primitive) ?? colour;
        }

        // Geometry that arrived as corners and faces rather than as a file is geometry this document
        // owns, and something somebody can still pull a face off.
        return positions.Count == 0 ? null : new MeshComponent(geometry: new MeshGeometry(positions, faces), colour: colour, authored: true);
    }

    private List<Vector3> CornersOf(JsonMap primitive)
    {
        var accessor = GltfJson.Index(Values.Object(primitive.GetValueOrDefault("attributes")).GetValueOrDefault("POSITION"));
        var numbers = _numbers.Floats(accessor is { } at ? GltfJson.Saturate(at) : null, 3);

        var corners = new List<Vector3>();
        for (var i = 0; i + 2 < numbers.Count; i += 3)
        {
            corners.Add(new Vector3(numbers[i], numbers[i + 1], numbers[i + 2]));
        }

        return corners;
    }

    /// <summary>The triangles of a primitive whose corners will start at <paramref name="first"/>.</summary>
    private List<Face> TrianglesOf(JsonMap primitive, int first, int count)
    {
        var order = GltfJson.Index(primitive.GetValueOrDefault("indices")) is { } indices
            ? _numbers.Indices(GltfJson.Saturate(indices))
            : [.. Enumerable.Range(0, count).Select(static i => (long)i)];

        int Corner(int i) => GltfJson.Saturate(first + order[i]);

        var faces = new List<Face>();
        for (var i = 0; i + 2 < order.Count; i += 3)
        {
            faces.Add(new Face([Corner(i), Corner(i + 1), Corner(i + 2)]));
        }

        return faces;
    }

    /// <summary>The colour of the material a primitive wears, or null when it names none that says one.</summary>
    private Tint? WornBy(JsonMap primitive)
    {
        if (GltfJson.Index(primitive.GetValueOrDefault("material")) is not { } wears || wears >= _materials.Count)
        {
            return null;
        }

        var metallic = Values.Object(_materials[(int)wears].GetValueOrDefault("pbrMetallicRoughness"));
        var factor = GltfJson.Doubles(metallic.GetValueOrDefault("baseColorFactor"));
        return factor.Count >= 3 ? Tint.FromLinear(new Vector3(factor[0], factor[1], factor[2])) : null;
    }
}

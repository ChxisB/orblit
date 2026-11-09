using System.Numerics;

namespace Orblit.Editor.Bridge;

/// <summary>
/// The orange box the viewport draws around the selected entity: its 12 edges, once solid against the scene and
/// once faint through it. The boxes sit under the stage root beside the scene, so a save never packs them and the
/// outline never lists them.
/// </summary>
public sealed class SelectionBox
{
    // The box of an entity that has no size of its own, such as a camera or a directional light.
    static readonly Aabb Fallback = new(new Vector3(-0.2f), new Vector3(0.4f));

    readonly EngineHost _host;
    readonly nint[] _boxes;

    /// <summary>Adds both boxes to the running engine, hidden. Call it once, after the engine has started.</summary>
    public SelectionBox(EngineHost host)
    {
        _host = host;
        var root = EditorWorld.StageRoot(host);
        _boxes = [Make(host, root, "EditorSelection", Shader(1, "")), Make(host, root, "EditorSelectionXray", Shader(0.15f, ", depth_test_disabled"))];
        Hide();
    }

    /// <summary>Draws the box around the entity. Zero, or anything that is not an Entity3D, draws none.</summary>
    public void Show(nint entity)
    {
        if (entity == 0 || !_host.ObjectIsClass(entity, "Entity3D"))
        {
            Hide();
            return;
        }

        var size = _host.ObjectIsClass(entity, "VisualInstance3D") ? _host.VisualInstance3DGetAabb(entity) : default;
        var placed = Around(_host.Entity3DGetGlobalTransform(entity), size == default ? Fallback : size);
        foreach (var box in _boxes)
        {
            _host.Entity3DSetGlobalTransform(box, placed);
            _host.Entity3DSetVisible(box, true);
        }
    }

    public void Hide()
    {
        foreach (var box in _boxes)
        {
            _host.Entity3DSetVisible(box, false);
        }
    }

    /// <summary>
    /// The placement of a box of size 1 centered on its own origin that covers the bounds of an entity placed at
    /// <paramref name="place"/>. The basis columns are stretched by the size and the origin moves to the center.
    /// </summary>
    public static Transform3D Around(Transform3D place, Aabb bounds)
    {
        var center = bounds.Position + bounds.Size / 2;
        return new Transform3D(
            place.Row0 * bounds.Size,
            place.Row1 * bounds.Size,
            place.Row2 * bounds.Size,
            new Vector3(Vector3.Dot(place.Row0, center), Vector3.Dot(place.Row1, center), Vector3.Dot(place.Row2, center)) + place.Origin);
    }

    static nint Make(EngineHost host, nint root, string name, string code)
    {
        var mesh = host.Construct("BoxMesh");
        var shader = host.Construct("Shader");
        host.ShaderSetCode(shader, code);
        var material = host.Construct("ShaderMaterial");
        host.ShaderMaterialSetShader(material, shader);
        var box = EditorWorld.Add(host, root, "MeshInstance3D", name);
        host.MeshInstance3DSetMesh(box, mesh);
        host.GeometryInstance3DSetMaterialOverride(box, material);
        EditorWorld.Release(host, mesh, shader, material);
        return box;
    }

    // A face of the unit box is on an edge where it is also near one of the other two faces. The distance to a
    // face in pixels is the distance in box units over how many box units one pixel covers. The middle one of
    // the three distances is the nearer of the two other faces, because the face the pixel lies on has none.
    static string Shader(float alpha, string modes) =>
        EditorWorld.Header.Replace("cull_disabled;", "cull_disabled" + modes + ";") + EditorWorld.ToLinear
        + FormattableString.Invariant($"const float STRENGTH = {alpha:0.0#####};\n")
        + """
        const vec3 LINE = vec3(1.0, 0.5, 0.0);

        varying vec3 local;

        void vertex() {
            local = VERTEX;
        }

        void fragment() {
            vec3 pixels = (vec3(0.5) - abs(local)) / max(fwidth(local), vec3(0.00001));
            float near = max(min(pixels.x, pixels.y), min(max(pixels.x, pixels.y), pixels.z));
            ALBEDO = OUTPUT_IS_SRGB ? LINE : to_linear(LINE);
            ALPHA = STRENGTH * (1.0 - smoothstep(0.5, 1.5, near));
        }
        """;
}

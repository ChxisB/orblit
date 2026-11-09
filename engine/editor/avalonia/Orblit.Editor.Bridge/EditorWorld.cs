using System.Numerics;

namespace Orblit.Editor.Bridge;

/// <summary>
/// What the viewport shows around the scene: a sky, a grid on the ground that fades with distance and a line
/// for each axis. It all sits under the stage root, beside the scene and not in it, so the outline never lists
/// it and a save never packs it. A scene with an environment of its own is drawn with that one, because the
/// engine takes the first environment in tree order and the scene comes first.
/// </summary>
public static class EditorWorld
{
    const string SkyName = "EditorEnvironment";

    // Environment.BGMode, PlaneMesh.Orientation.
    const long SkyBackground = 2;
    const long FaceX = 0;
    const long FaceY = 1;

    // The grid fades out at 192 steps of its coarsest level, which is 192 * 64 units. The plane covers that
    // in every direction. shortcut: a camera further than 12800 units from the origin sees the plane end,
    // follow the camera when that matters.
    const float GridExtent = 25600;
    // shortcut: the lines end 4000 units from the origin, where the camera's far plane ends them anyway.
    const float AxisLength = 4000;

    static readonly Vector4 SkyTop = new(0.385f, 0.454f, 0.55f, 1);
    static readonly Vector4 SkyGround = new(0.2f, 0.169f, 0.133f, 1);

    /// <summary>One axis line: where it points, how its carrier is turned to lie along it, and its colour.</summary>
    sealed record Axis(string Name, Vector3 Direction, Vector3 Turn, Vector3 Colour);

    // A plane facing X has its long side on its own Z, so it is turned until that side lies along the axis.
    static readonly Axis[] Axes =
    [
        new("EditorAxisX", Vector3.UnitX, new Vector3(0, 90, 0), new Vector3(0.96f, 0.20f, 0.32f)),
        new("EditorAxisY", Vector3.UnitY, new Vector3(-90, 0, 0), new Vector3(0.53f, 0.84f, 0.01f)),
        new("EditorAxisZ", Vector3.UnitZ, Vector3.Zero, new Vector3(0.16f, 0.55f, 0.96f)),
    ];

    /// <summary>What one drawn plane is: its name, which way it faces, how big it is and the shader that draws it.</summary>
    sealed record Drawing(string Name, long Orientation, Vector2 Size, string Code);

    internal static nint StageRoot(EngineHost host) => host.StageGetRoot(host.EngineGetMainLoop(host.Singleton("Engine")));

    /// <summary>Adds the world to the running engine. Call it once, after the engine has started.</summary>
    public static void Build(EngineHost host)
    {
        var root = StageRoot(host);
        AddSky(host, root);
        Draw(host, root, new Drawing("EditorGrid", FaceY, new Vector2(GridExtent, GridExtent), GridShader));
        foreach (var axis in Axes)
        {
            AddAxis(host, root, axis);
        }
    }

    /// <summary>
    /// Puts the sky after the scene again. A scene opened after <see cref="Build"/> is added last, and the engine
    /// takes the first environment in tree order, so the sky would hide the scene's own.
    /// </summary>
    public static void PutSkyLast(EngineHost host)
    {
        var root = StageRoot(host);
        var count = host.EntityGetChildCount(root, false);
        for (var i = 0; i < count; i++)
        {
            var child = host.EntityGetChild(root, i, false);
            if (host.EntityGetName(child) == SkyName)
            {
                host.EntityMoveChild(root, child, count - 1);
                return;
            }
        }
    }

    // The sky colours are the old editor's preview. The horizon is halfway between sky and ground, pulled
    // halfway to a grey three times as bright, so it reads as haze.
    static void AddSky(EngineHost host, nint root)
    {
        var mid = Vector4.Lerp(SkyTop, SkyGround, 0.5f);
        var grey = (0.2126f * mid.X + 0.7152f * mid.Y + 0.0722f * mid.Z) * 3.333f;
        var horizon = Vector4.Lerp(mid, new Vector4(grey, grey, grey, 1), 0.5f);

        var material = host.Construct("ProceduralSkyMaterial");
        host.ProceduralSkyMaterialSetSkyTopColor(material, SkyTop);
        host.ProceduralSkyMaterialSetSkyHorizonColor(material, horizon);
        host.ProceduralSkyMaterialSetGroundBottomColor(material, SkyGround);
        host.ProceduralSkyMaterialSetGroundHorizonColor(material, horizon);
        var sky = host.Construct("Sky");
        host.SkySetMaterial(sky, material);
        var environment = host.Construct("Environment");
        host.EnvironmentSetBackground(environment, SkyBackground);
        host.EnvironmentSetSky(environment, sky);
        var world = Add(host, root, "WorldEnvironment", SkyName);
        host.WorldEnvironmentSetEnvironment(world, environment);
        Release(host, material, sky, environment);
    }

    // The carrier is a thin plane as long as the line, so the engine culls the line only when all of it is
    // out of view. The shader places the vertices.
    static void AddAxis(EngineHost host, nint root, Axis axis)
    {
        var carrier = new Drawing(axis.Name, FaceX, new Vector2(2 * AxisLength, 1), AxisShader(axis));
        host.Entity3DSetRotationDegrees(Draw(host, root, carrier), axis.Turn);
    }

    static nint Draw(EngineHost host, nint root, Drawing what)
    {
        var plane = host.Construct("PlaneMesh");
        host.PlaneMeshSetOrientation(plane, what.Orientation);
        host.PlaneMeshSetSize(plane, what.Size);
        var shader = host.Construct("Shader");
        host.ShaderSetCode(shader, what.Code);
        var material = host.Construct("ShaderMaterial");
        host.ShaderMaterialSetShader(material, shader);
        var drawn = Add(host, root, "MeshInstance3D", what.Name);
        host.MeshInstance3DSetMesh(drawn, plane);
        host.GeometryInstance3DSetMaterialOverride(drawn, material);
        Release(host, plane, shader, material);
        return drawn;
    }

    // An entity of the class, named and put under the root. The root owns it.
    internal static nint Add(EngineHost host, nint root, string className, string name)
    {
        var made = host.Construct(className);
        host.EntitySetName(made, name);
        host.EntityAddChild(root, made, false, 0);
        return made;
    }

    // Whatever was handed to another object keeps its own reference, so the one from Construct goes.
    internal static void Release(EngineHost host, params nint[] made)
    {
        foreach (var one in made)
        {
            host.Release(one);
        }
    }

    // Both shaders draw flat colour over the scene and take no light.
    internal const string Header = "shader_type spatial;\nrender_mode unshaded, fog_disabled, cull_disabled;\n\n";

    // Colours are written as they look. A renderer that outputs linear light takes them as sRGB.
    internal const string ToLinear = """
        vec3 to_linear(vec3 c) {
            return mix(pow((c + vec3(0.055)) * (1.0 / 1.055), vec3(2.4)), c * (1.0 / 12.92), lessThan(c, vec3(0.04045)));
        }

        """;

    // The levels, colours and fades are the old editor's. Lines are found per pixel, a pixel wide, so the
    // grid needs no mesh. The origin has no grid line because an axis is drawn there.
    const string GridShader = Header + ToLinear + """
        const float STEPS = 8.0;
        const float GRID_SIZE = 200.0;
        const float LEVEL_BIAS = -0.2;
        const float LEVEL_MAX = 2.0;

        // How much of a pixel the lines at whole numbers of p cover. They thin out once a cell is under a pixel.
        float lines(vec2 p, vec2 width) {
            vec2 cell = round(p);
            vec2 d = abs(p - cell) / width;
            vec2 line = (1.0 - min(d, vec2(1.0))) * step(0.5, abs(cell));
            return max(line.x, line.y) * (1.0 - smoothstep(0.3, 0.7, max(width.x, width.y)));
        }

        void fragment() {
            vec3 world_pos = (INV_VIEW_MATRIX * vec4(VERTEX, 1.0)).xyz;
            vec3 camera_pos = INV_VIEW_MATRIX[3].xyz;

            float raw_level = log(max(abs(camera_pos.y), 0.001)) / log(STEPS) + LEVEL_BIAS;
            float level = clamp(raw_level, 0.0, LEVEL_MAX);
            float whole = floor(level);
            float decimals = level - whole;
            vec2 p = world_pos.xz / pow(STEPS, whole);

            float small = lines(p, fwidth(p));
            float big = lines(p / STEPS, fwidth(p / STEPS));
            vec4 small_colour = vec4(0.38, 0.38, 0.38, 0.5 * (1.0 - decimals));
            vec4 big_colour = mix(vec4(0.56, 0.56, 0.56, 0.5), vec4(0.38, 0.38, 0.38, 0.5), decimals);
            vec3 colour = mix(small_colour.rgb, big_colour.rgb, big);

            float angle_fade = smoothstep(0.05, 0.2, abs(dot(VIEW, NORMAL)));
            float fade_size = (GRID_SIZE - STEPS) * clamp(pow(STEPS, raw_level - 1.0), 1.0, pow(STEPS, LEVEL_MAX));
            float away = distance(world_pos, vec3(camera_pos.x, 0.0, camera_pos.z)) / fade_size;
            float dist_fade = smoothstep(0.02, 0.3, 1.0 - away);

            ALBEDO = OUTPUT_IS_SRGB ? colour : to_linear(colour);
            ALPHA = max(small * small_colour.a, big * big_colour.a) * dist_fade * angle_fade;
        }
        """;

    static string Vec(Vector3 v) => FormattableString.Invariant($"vec3({v.X:0.0######}, {v.Y:0.0######}, {v.Z:0.0######})");

    // Both ends of the line are projected to the screen and each corner of the quad is pushed 3 pixels to one
    // side of the line they make, so the line keeps its width at any distance. An end behind the camera is
    // first cut back to the near plane, because it would project to the wrong side. The corners are told
    // apart by the signs of their position on the carrier. The distance across the line is scaled by the
    // depth, which cancels the perspective correction of a varying, and measured against its change per pixel.
    // Only the middle 2 pixels of the width show.
    static string AxisShader(Axis axis) =>
        Header + $"const vec3 LINE = {Vec(axis.Colour)};\nconst vec3 FROM = {Vec(-AxisLength * axis.Direction)};\nconst vec3 TO = {Vec(AxisLength * axis.Direction)};\n"
        + ToLinear + """
        const float NEAR = 1.0;

        varying float across;

        void vertex() {
            vec3 a = (VIEW_MATRIX * vec4(FROM, 1.0)).xyz;
            vec3 b = (VIEW_MATRIX * vec4(TO, 1.0)).xyz;
            bool hidden = min(a.z, b.z) > -NEAR;
            vec3 cut_a = a.z > -NEAR ? mix(a, b, (-NEAR - a.z) / (b.z - a.z)) : a;
            vec3 cut_b = b.z > -NEAR ? mix(b, a, (-NEAR - b.z) / (a.z - b.z)) : b;
            vec4 clip_a = PROJECTION_MATRIX * vec4(cut_a, 1.0);
            vec4 clip_b = PROJECTION_MATRIX * vec4(cut_b, 1.0);
            vec2 screen_a = VIEWPORT_SIZE * (0.5 * clip_a.xy / clip_a.w + 0.5);
            vec2 screen_b = VIEWPORT_SIZE * (0.5 * clip_b.xy / clip_b.w + 0.5);
            vec2 along = normalize(screen_b - screen_a);
            float end = VERTEX.z > 0.0 ? 1.0 : 0.0;
            float side = 3.0 * sign(VERTEX.y);
            vec2 point = mix(screen_a, screen_b, end) + side * vec2(-along.y, along.x);
            vec4 clip = mix(clip_a, clip_b, end);
            across = side * clip.w;
            vec4 placed = vec4(clip.w * (2.0 * point / VIEWPORT_SIZE - 1.0), clip.z, clip.w);
            POSITION = hidden ? vec4(2.0, 2.0, 2.0, 1.0) : placed;
        }

        void fragment() {
            ALBEDO = OUTPUT_IS_SRGB ? LINE : to_linear(LINE);
            float pixel = fwidth(across);
            ALPHA = smoothstep(pixel, pixel * 0.25, abs(across));
        }
        """;
}

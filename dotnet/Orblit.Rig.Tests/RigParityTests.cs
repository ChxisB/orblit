using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using Orblit.Parity;
using static Orblit.Parity.Dump;
using static Orblit.Rig.Tests.RigDump;

namespace Orblit.Rig.Tests;

public class RigParityTests
{
    private static void Check(string op, Func<JsonElement, JsonNode?> run) =>
        Fixture.Load("orblit_rig", "rig").Check(op, run);

    // reading a spec

    private static JsonElement? Opt(JsonElement spec, string name) =>
        spec.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null ? value : null;

    private static double D(JsonElement value) => value.GetDouble();

    private static double Or(JsonElement spec, string name, double fallback) =>
        Opt(spec, name) is { } value ? D(value) : fallback;

    private static string? Str(JsonElement spec, string name) => Opt(spec, name)?.GetString();

    private static bool Flag(JsonElement spec, string name) =>
        Opt(spec, name) is { ValueKind: JsonValueKind.True };

    private static List<string> Strings(JsonElement list) => [.. list.EnumerateArray().Select(item => item.GetString()!)];

    private static Vector3 V3(JsonElement json) => new(D(json[0]), D(json[1]), D(json[2]));

    private static Vector3? OptV3(JsonElement spec, string name) => Opt(spec, name) is { } json ? V3(json) : null;

    private static Quaternion Q4(JsonElement json) => new(D(json[0]), D(json[1]), D(json[2]), D(json[3]));

    private static Matrix4 M16(JsonElement json)
    {
        var matrix = Matrix4.Zero();
        for (var index = 0; index < 16; index++)
        {
            matrix[index] = D(json[index]);
        }

        return matrix;
    }

    private static TrackAxis Axis(string? name, string fallback) =>
        Enum.Parse<TrackAxis>(name ?? fallback, ignoreCase: true);

    private static Bone BoneFrom(JsonElement json) => new(
        json[0].GetString()!,
        V3(json[1]),
        V3(json[2]),
        roll: D(json[3]),
        parent: json[4].GetString(),
        connected: json[5].GetBoolean(),
        deform: json[6].GetBoolean());

    private static Armature ArmatureFrom(JsonElement json) => new([.. json.EnumerateArray().Select(BoneFrom)]);

    private static void ApplyEdits(Pose pose, JsonElement? edits)
    {
        if (edits is not { } list)
        {
            return;
        }

        foreach (var edit in list.EnumerateArray())
        {
            var transform = pose[edit[0].GetString()!];
            if (edit[1].ValueKind != JsonValueKind.Null)
            {
                transform.Location = V3(edit[1]);
            }

            if (edit[2].ValueKind != JsonValueKind.Null)
            {
                transform.Rotation = Q4(edit[2]);
            }

            if (edit[3].ValueKind != JsonValueKind.Null)
            {
                transform.Scale = V3(edit[3]);
            }
        }
    }

    private static BoneConstraint ConstraintFrom(JsonElement s)
    {
        var influence = Or(s, "influence", 1);
        var property = Str(s, "influenceProperty");
        var invertInfluence = Flag(s, "invertInfluence");
        return s.GetProperty("kind").GetString() switch
        {
            "copyRotation" => new CopyRotation(
                source: Str(s, "source")!,
                influence: influence,
                influenceProperty: property,
                invertInfluence: invertInfluence,
                invert: Flag(s, "invert")),
            "dampedTrack" => new DampedTrack(
                target: Str(s, "target")!,
                axis: Axis(Str(s, "axis"), "y"),
                influence: influence,
                influenceProperty: property,
                invertInfluence: invertInfluence),
            "limitRotation" => new LimitRotation(
                maximumAngle: D(s.GetProperty("maximumAngle")),
                influence: influence,
                influenceProperty: property,
                invertInfluence: invertInfluence),
            "copyTransform" => new CopyTransform(
                source: Str(s, "source")!,
                influence: influence,
                influenceProperty: property,
                invertInfluence: invertInfluence),
            "stretchTo" => new StretchTo(
                target: Str(s, "target")!,
                restLength: D(s.GetProperty("restLength")),
                volume: Or(s, "volume", 1),
                influence: influence,
                influenceProperty: property,
                invertInfluence: invertInfluence),
            var kind => throw new ArgumentException($"no constraint {kind}"),
        };
    }

    private static IkChain ChainFrom(JsonElement s) => new(
        root: Str(s, "root")!,
        mid: Str(s, "mid")!,
        target: Str(s, "target")!,
        pole: Str(s, "pole"),
        influence: Or(s, "influence", 1),
        influenceProperty: Str(s, "influenceProperty"),
        stretch: Or(s, "stretch", 0),
        stretchProperty: Str(s, "stretchProperty"));

    /// <summary>A point in model space: fixed, the head of a bone, or nothing at all.</summary>
    private static ModelPoint? PointFrom(JsonElement? json)
    {
        if (json is not { } s)
        {
            return null;
        }

        if (Flag(s, "none"))
        {
            return (pose, frame) => null;
        }

        if (OptV3(s, "at") is { } at)
        {
            return (pose, frame) => at.Clone();
        }

        var head = Str(s, "head")!;
        return (pose, frame) => pose.HeadOf(head);
    }

    private static SpringCollider ColliderFrom(JsonElement s)
    {
        var bone = Str(s, "bone");
        return s.GetProperty("kind").GetString() switch
        {
            "sphere" => new SphereCollider(V3(s.GetProperty("centre")), D(s.GetProperty("radius")), bone),
            "capsule" => new CapsuleCollider(V3(s.GetProperty("start")), V3(s.GetProperty("end")), D(s.GetProperty("radius")), bone),
            "plane" => new PlaneCollider(V3(s.GetProperty("point")), V3(s.GetProperty("normal")), bone),
            var kind => throw new ArgumentException($"no collider {kind}"),
        };
    }

    /// <summary>The ground as a plane through (0, height, 0): a ray that crosses it hits it.</summary>
    private static GroundProbe ProbeFrom(JsonElement s)
    {
        var height = D(s.GetProperty("height"));
        var normal = V3(s.GetProperty("normal")).Normalized();
        var point = new Vector3(0, height, 0);
        return (origin, target) =>
        {
            var start = normal.Dot(origin - point);
            var end = normal.Dot(target - point);
            if (start * end > 0 || start == end)
            {
                return null;
            }

            var along = start / (start - end);
            return new GroundHit(origin + ((target - origin) * along), normal);
        };
    }

    private static FootLeg LegFrom(JsonElement leg) => new(
        thigh: Str(leg, "thigh")!,
        shin: Str(leg, "shin")!,
        foot: Str(leg, "foot"),
        ankleHeight: Or(leg, "ankleHeight", 0),
        soleAxis: Axis(Str(leg, "soleAxis"), "negativeZ"));

    private static SplineIk SplineFrom(JsonElement s)
    {
        var controls = Opt(s, "points") is { } points ? [.. points.EnumerateArray().Select(V3)] : (List<Vector3>?)null;
        return new SplineIk(
            root: Str(s, "root")!,
            tip: Str(s, "tip")!,
            points: (pose, frame) => controls,
            detail: Opt(s, "detail")?.GetInt32() ?? 12);
    }

    private static SpringBones SpringFrom(JsonElement s) => new(
        bones: Strings(s.GetProperty("bones")),
        stiffness: Or(s, "stiffness", 80),
        drag: Or(s, "drag", 6),
        gravity: OptV3(s, "gravity"),
        margin: Or(s, "margin", 0),
        colliders: [.. (Opt(s, "colliders")?.EnumerateArray() ?? Enumerable.Empty<JsonElement>()).Select(ColliderFrom)],
        snapDistance: Or(s, "snapDistance", 2));

    private static FootPlacement FootFrom(JsonElement s) => new(
        legs: [.. s.GetProperty("legs").EnumerateArray().Select(LegFrom)],
        probe: ProbeFrom(s.GetProperty("probe")),
        pelvis: Str(s, "pelvis"),
        up: OptV3(s, "up"),
        rise: Or(s, "rise", 0.5),
        depth: Or(s, "depth", 0.5),
        ease: Or(s, "ease", 0.1),
        maxTilt: Or(s, "maxTilt", Math.PI / 4),
        airborne: Or(s, "airborne", 0.1));

    [SuppressMessage("Maintainability", "CA1502:Avoid excessive complexity", Justification = "F2: a list of cases is data.")]
    private static Modifier ModifierFrom(JsonElement s)
    {
        var bone = Str(s, "bone");
        return s.GetProperty("kind").GetString() switch
        {
            "twoBoneIk" => new TwoBoneIk(
                root: Str(s, "root")!,
                mid: Str(s, "mid")!,
                target: PointFrom(Opt(s, "target"))!,
                pole: PointFrom(Opt(s, "pole")),
                stretch: Or(s, "stretch", 0)),
            "chainIk" => new ChainIk(
                root: Str(s, "root")!,
                tip: Str(s, "tip")!,
                target: PointFrom(Opt(s, "target"))!,
                iterations: Opt(s, "iterations")?.GetInt32() ?? 12),
            "splineIk" => SplineFrom(s),
            "coneLimit" => new ConeLimit(bone!, D(s.GetProperty("swing")), Or(s, "twist", Math.PI)),
            "hingeLimit" => new HingeLimit(bone!, D(s.GetProperty("min")), D(s.GetProperty("max")), OptV3(s, "axis")),
            "twistSpread" => new TwistSpread(
                source: Str(s, "source")!,
                bones: Strings(s.GetProperty("bones")),
                shares: Opt(s, "shares") is { } shares ? [.. shares.EnumerateArray().Select(D)] : null),
            "lookAt" => new LookAt(
                bone: bone!,
                target: PointFrom(Opt(s, "target"))!,
                axis: Axis(Str(s, "axis"), "y"),
                limit: Or(s, "limit", Math.PI / 2),
                ease: Or(s, "ease", 0)),
            "speedLimit" => new SpeedLimit(
                bones: Strings(s.GetProperty("bones")),
                turnSpeed: Opt(s, "turnSpeed") is { } turn ? D(turn) : null,
                moveSpeed: Opt(s, "moveSpeed") is { } move ? D(move) : null),
            "boneAttachment" => new BoneAttachment(bone!, Opt(s, "offset") is { } offset ? M16(offset) : null),
            "constrain" => new Constrain(bone!, ConstraintFrom(s.GetProperty("constraint"))),
            "springBones" => SpringFrom(s),
            "footPlacement" => FootFrom(s),
            var kind => throw new ArgumentException($"no modifier {kind}"),
        };
    }

    private static IRigType RigTypeFrom(JsonElement s) => s.GetProperty("kind").GetString() switch
    {
        "limb" => new LimbRig(Or(s, "stretch", 0)),
        "finger" => new FingerRig(),
        "spine" => new SpineRig(),
        "copy" => new CopyRig(),
        "jaw" => Opt(s, "maximumAngle") is { } angle ? new JawRig(D(angle)) : new JawRig(),
        "eye" => Opt(s, "distance") is { } distance ? new EyeRig(D(distance)) : new EyeRig(),
        "eyelid" => Opt(s, "follow") is { } follow
            ? new EyelidRig(Str(s, "eye")!, D(follow))
            : new EyelidRig(Str(s, "eye")!),
        var kind => throw new ArgumentException($"no rig type {kind}"),
    };

    // bone

    [Fact]
    public void BoneMatchesTheDartTwin() => Check("bone", c => Attempt(() =>
    {
        var bone = BoneFrom(c.GetProperty("bone"));
        return new JsonObject
        {
            ["length"] = Num(bone.Length),
            ["direction"] = Vec(bone.Direction),
            ["orientation"] = Quat(bone.Orientation),
            ["rest"] = Mat(bone.RestMatrix),
        };
    }));

    // naming

    private static BoneRole Role(JsonElement name) => Enum.Parse<BoneRole>(name.GetString()!, ignoreCase: true);

    private static Side? SideOrNull(JsonElement name) =>
        name.ValueKind == JsonValueKind.Null ? null : Enum.Parse<Side>(name.GetString()!, ignoreCase: true);

    [Fact]
    public void NamingMatchesTheDartTwin() => Check("naming", c => Attempt(() =>
    {
        var a = c.GetProperty("args");
        return c.GetProperty("fn").GetString() switch
        {
            "unique" => Arr(BoneNaming.Unique(Strings(a[0])), Text),
            "roleOf" => Text(Lower(BoneNaming.RoleOf(a[0].GetString()!).ToString())),
            "sideOf" => NullableText(BoneNaming.SideOf(a[0].GetString()!) is { } side ? Lower(side.ToString()) : null),
            "baseOf" => Text(BoneNaming.BaseOf(a[0].GetString()!)),
            "compose" => Text(BoneNaming.Compose(a[0].GetString()!, Role(a[1]), SideOrNull(a[2]))),
            "asRole" => Text(BoneNaming.AsRole(a[0].GetString()!, Role(a[1]))),
            "mirror" => NullableText(BoneNaming.Mirror(a[0].GetString()!)),
            "areMirrors" => JsonValue.Create(BoneNaming.AreMirrors(a[0].GetString()!, a[1].GetString()!)),
            var fn => throw new ArgumentException($"no naming function {fn}"),
        };
    }));

    // IK

    [Fact]
    public void TwoBoneIkMatchesTheDartTwin() => Check("twoBoneIk", c => Attempt(() => IkDump(
        Ik.SolveTwoBoneIk(
            root: V3(c.GetProperty("root")),
            pole: V3(c.GetProperty("pole")),
            target: V3(c.GetProperty("target")),
            upperLength: D(c.GetProperty("upper")),
            lowerLength: D(c.GetProperty("lower")),
            stretch: Or(c, "stretch", 0)))));

    [Fact]
    public void FabrikMatchesTheDartTwin() => Check("fabrik", c => Attempt(() => Arr(
        Ik.SolveFabrik(
            points: [.. c.GetProperty("points").EnumerateArray().Select(V3)],
            target: V3(c.GetProperty("target")),
            iterations: Opt(c, "iterations")?.GetInt32() ?? 12,
            tolerance: Or(c, "tolerance", 1e-4)),
        Vec)));

    // curves and widgets

    [Fact]
    public void CurveMatchesTheDartTwin() => Check("curve", c => Attempt(() => Arr(
        SplineIk.SampleCurve([.. c.GetProperty("controls").EnumerateArray().Select(V3)], c.GetProperty("detail").GetInt32()),
        Vec)));

    [Fact]
    public void WidgetMatchesTheDartTwin() => Check("widget", c => Attempt(() => Arr(
        new BoneWidget(
            shape: Enum.Parse<WidgetShape>(Str(c, "shape")!, ignoreCase: true),
            size: Or(c, "size", 1),
            offset: OptV3(c, "offset"),
            roll: Or(c, "roll", 0)).Outline(D(c.GetProperty("length"))),
        line => Arr(line, Vec))));

    // a pose

    private static void SetProperties(Pose pose, JsonElement spec)
    {
        if (Opt(spec, "properties") is not { } properties)
        {
            return;
        }

        foreach (var property in properties.EnumerateObject())
        {
            pose.SetProperty(property.Name, D(property.Value));
        }
    }

    [Fact]
    public void PoseMatchesTheDartTwin() => Check("pose", c => Attempt(() =>
    {
        var pose = new Pose(ArmatureFrom(c.GetProperty("bones")));
        SetProperties(pose, c);
        foreach (var constraint in (Opt(c, "constraints")?.EnumerateArray() ?? Enumerable.Empty<JsonElement>()))
        {
            pose.Constrain(constraint[0].GetString()!, ConstraintFrom(constraint[1]));
        }

        foreach (var chain in (Opt(c, "chains")?.EnumerateArray() ?? Enumerable.Empty<JsonElement>()))
        {
            pose.AddIkChain(ChainFrom(chain));
        }

        ApplyEdits(pose, Opt(c, "edits"));
        pose.Evaluate();
        var result = new JsonObject { ["first"] = PoseDump(pose) };
        if (Opt(c, "then") is { } then)
        {
            ApplyEdits(pose, Opt(then, "edits"));
            pose.Refresh(Str(then, "from"));
            result["second"] = PoseDump(pose);
        }

        return result;
    }));

    // modifiers

    private static JsonObject FrameDump(Pose pose, IReadOnlyList<string> watch, IEnumerable<Modifier> built) => new JsonObject
    {
        ["worlds"] = Arr(watch, bone => Mat(pose.WorldOf(bone))),
        ["rest"] = Arr(watch, bone => JsonValue.Create(pose[bone].IsRest)),
        ["attachments"] = Arr(built.OfType<BoneAttachment>(), attachment => new JsonObject
        {
            ["model"] = attachment.Model is { } model ? Mat(model) : null,
            ["world"] = attachment.World is { } world ? Mat(world) : null,
        }),
    };

    [Fact]
    public void ModifiersMatchTheDartTwin() => Check("modifier", c => Attempt(() =>
    {
        var pose = new Pose(ArmatureFrom(c.GetProperty("bones")));
        SetProperties(pose, c);
        var stack = new ModifierStack();
        var built = new List<Modifier>();
        foreach (var slot in c.GetProperty("modifiers").EnumerateArray())
        {
            var modifier = ModifierFrom(slot.GetProperty("spec"));
            built.Add(modifier);
            stack.Add(
                slot.GetProperty("name").GetString()!,
                modifier,
                influence: Or(slot, "influence", 1),
                influenceProperty: Str(slot, "influenceProperty"));
        }

        var watch = Strings(c.GetProperty("watch"));
        var output = new List<JsonNode?>();
        foreach (var frame in c.GetProperty("frames").EnumerateArray())
        {
            for (var i = 0; i < (Opt(frame, "repeat")?.GetInt32() ?? 1); i++)
            {
                pose.Reset();
                ApplyEdits(pose, Opt(frame, "edits"));
                pose.Evaluate();
                stack.Apply(
                    pose,
                    new ModifierFrame(dt: Or(frame, "dt", 0), modelToWorld: Opt(frame, "toWorld") is { } toWorld ? M16(toWorld) : null));
            }

            output.Add(FrameDump(pose, watch, built));
        }

        return new JsonArray([.. output]);
    }));

    // generated rigs

    [Fact]
    public void GeneratedRigsMatchTheDartTwin() => Check("generate", c => Attempt(() =>
    {
        var meta = new MetaRig(ArmatureFrom(c.GetProperty("bones")));
        foreach (var assignment in c.GetProperty("assign").EnumerateArray())
        {
            meta.Assign(assignment[0].GetString()!, RigTypeFrom(assignment[1]));
        }

        var rig = new RigGenerator().Generate(meta);
        JsonNode Skins() => Arr(rig.Armature.Bones, bone => new JsonObject
        {
            ["name"] = Text(bone.Name),
            ["skin"] = Mat(rig.Pose.SkinningOf(bone.Name)),
        });

        rig.Pose.Evaluate();
        var atRest = Skins();

        var names = rig.Controls.Order(StringComparer.Ordinal).ToList();
        for (var i = 0; i < names.Count; i++)
        {
            var transform = rig.Pose[names[i]];
            transform.Location = new Vector3(0.02 * (i + 1), 0.01 * (i % 3), -0.015 * i);
            transform.Rotation = Quaternion.AxisAngle(new Vector3(1, 2, 3).Normalized(), 0.1 * ((i % 5) + 1));
        }

        rig.Pose.Evaluate();
        return GeneratedDump(rig, atRest, Skins());
    }));

    [Fact]
    public void CollectionsMatchTheDartTwin() => Check("collections", c => Attempt(() =>
    {
        var collections = new BoneCollections();
        var bones = new HashSet<string> { "nobody" };
        foreach (var add in c.GetProperty("adds").EnumerateArray())
        {
            collections.Add(
                add[0].GetString()!,
                add[1].GetString()!,
                visible: add[2].GetBoolean(),
                colour: add[3].ValueKind == JsonValueKind.Null ? null : add[3].GetInt32());
            bones.Add(add[0].GetString()!);
        }

        return new JsonObject
        {
            ["all"] = Arr(collections.All, collection => new JsonObject
            {
                ["name"] = Text(collection.Name),
                ["visible"] = collection.Visible,
                ["colour"] = collection.Colour is { } colour ? Big(colour) : null,
                ["members"] = Arr(collections.MembersOf(collection.Name), Text),
            }),
            ["sorted"] = Arr(collections.Sorted, collection => Text(collection.Name)),
            ["of"] = new JsonObject(bones.Select(bone =>
                KeyValuePair.Create(bone, (JsonNode?)Arr(collections.CollectionsOf(bone), Text)))),
        };
    }));

    private static JsonObject GeneratedDump(GeneratedRig rig, JsonNode atRest, JsonNode driven) => new JsonObject
    {
        ["hasProblems"] = rig.HasProblems,
        ["bones"] = Arr(rig.Armature.Bones, bone => new JsonObject
        {
            ["name"] = Text(bone.Name),
            ["head"] = Vec(bone.Head),
            ["tail"] = Vec(bone.Tail),
            ["roll"] = Num(bone.Roll),
            ["parent"] = NullableText(bone.Parent),
            ["connected"] = bone.Connected,
            ["deform"] = bone.Deform,
            ["constraints"] = Arr(rig.Pose.ConstraintsOn(bone.Name), ConstraintJson),
        }),
        ["controls"] = Arr(rig.Controls, Text),
        ["deformBones"] = Arr(rig.DeformBones, Text),
        ["properties"] = Map(rig.Properties, Num),
        ["poseProperties"] = Map(rig.Pose.Properties, Num),
        ["widgets"] = Map(rig.Widgets, WidgetJson),
        ["collections"] = Arr(rig.Collections.All, collection => CollectionJson(rig.Collections, collection)),
        ["sorted"] = Arr(rig.Collections.Sorted, collection => Text(collection.Name)),
        ["collectionsOf"] = new JsonObject(rig.Armature.Bones.Select(bone =>
            KeyValuePair.Create(bone.Name, (JsonNode?)Arr(rig.Collections.CollectionsOf(bone.Name), Text)))),
        ["unassigned"] = Arr(rig.Unassigned, Text),
        ["problems"] = Arr(rig.Problems, problem => new JsonArray(Text(problem.Bone), Text(problem.Message))),
        ["chains"] = Arr(rig.Pose.IkChains, ChainJson),
        ["atRest"] = atRest,
        ["driven"] = driven,
    };
}

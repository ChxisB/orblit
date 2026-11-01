namespace Orblit.Motion.Tests;

/// <summary>How a bone is called in a scheme, by the part of the body it is.</summary>
internal delegate string Naming(HumanoidBone slot, bool end);

/// <summary>
/// A person written out by hand, standing in Y up and facing +Z, so their left is +X. The forward kinematics
/// are here too, so a check never goes through the code under test.
/// </summary>
internal sealed class Person(
    List<string> names,
    List<int> parents,
    List<(Vector3 Position, Quaternion Turn)> locals,
    Dictionary<HumanoidBone, string> slots)
{
    public List<string> Names { get; } = names;

    public List<int> Parents { get; } = parents;

    public List<(Vector3 Position, Quaternion Turn)> Locals { get; } = locals;

    /// <summary>Which bone really is each part of the body, for a check to compare to.</summary>
    public Dictionary<HumanoidBone, string> Slots { get; } = slots;

    public RestSkeleton Skeleton => new(
        Names,
        Parents,
        [.. Locals.Select(static local => Matrix4.Compose(local.Position, local.Turn, Vector3.All(1)))]);

    public int IndexOf(string name) => Names.IndexOf(name);

    /// <summary>Every bone in the world, at rest or posed by <paramref name="frame"/>.</summary>
    public List<Matrix4> World(IReadOnlyDictionary<string, BoneLocal>? frame = null)
    {
        var world = new List<Matrix4>();
        for (var i = 0; i < Names.Count; i++)
        {
            var local = frame?.GetValueOrDefault(Names[i]);
            var matrix = Matrix4.Compose(
                local?.Position ?? Locals[i].Position,
                local?.Rotation ?? Locals[i].Turn,
                local?.Scale ?? Vector3.All(1));
            world.Add(Parents[i] < 0 ? matrix : world[Parents[i]].Multiplied(matrix));
        }

        return world;
    }

    public Vector3 OriginOf(string name, IReadOnlyDictionary<string, BoneLocal>? frame = null) =>
        World(frame)[IndexOf(name)].GetTranslation();
}

internal static class People
{
    private static readonly HumanoidBone[][] SpineSlots =
    [
        [HumanoidBone.Spine],
        [HumanoidBone.Spine, HumanoidBone.Chest],
        [HumanoidBone.Spine, HumanoidBone.Chest, HumanoidBone.UpperChest],
    ];

    private static readonly Dictionary<HumanoidBone, string> MannequinBases = new()
    {
        [HumanoidBone.Hips] = "pelvis",
        [HumanoidBone.Spine] = "spine_01",
        [HumanoidBone.Chest] = "spine_02",
        [HumanoidBone.UpperChest] = "spine_03",
        [HumanoidBone.Neck] = "neck_01",
        [HumanoidBone.Head] = "head",
        [HumanoidBone.LeftShoulder] = "clavicle_l",
        [HumanoidBone.LeftUpperArm] = "upperarm_l",
        [HumanoidBone.LeftLowerArm] = "lowerarm_l",
        [HumanoidBone.LeftHand] = "hand_l",
        [HumanoidBone.RightShoulder] = "clavicle_r",
        [HumanoidBone.RightUpperArm] = "upperarm_r",
        [HumanoidBone.RightLowerArm] = "lowerarm_r",
        [HumanoidBone.RightHand] = "hand_r",
        [HumanoidBone.LeftUpperLeg] = "thigh_l",
        [HumanoidBone.LeftLowerLeg] = "calf_l",
        [HumanoidBone.LeftFoot] = "foot_l",
        [HumanoidBone.LeftToes] = "ball_l",
        [HumanoidBone.RightUpperLeg] = "thigh_r",
        [HumanoidBone.RightLowerLeg] = "calf_r",
        [HumanoidBone.RightFoot] = "foot_r",
        [HumanoidBone.RightToes] = "ball_r",
    };

    private static readonly Dictionary<HumanoidBone, string> CaptureBases = new()
    {
        [HumanoidBone.Hips] = "Hips",
        [HumanoidBone.Spine] = "Spine",
        [HumanoidBone.Chest] = "Spine1",
        [HumanoidBone.UpperChest] = "Spine2",
        [HumanoidBone.Neck] = "Neck",
        [HumanoidBone.Head] = "Head",
        [HumanoidBone.LeftShoulder] = "LeftShoulder",
        [HumanoidBone.LeftUpperArm] = "LeftArm",
        [HumanoidBone.LeftLowerArm] = "LeftForeArm",
        [HumanoidBone.LeftHand] = "LeftHand",
        [HumanoidBone.RightShoulder] = "RightShoulder",
        [HumanoidBone.RightUpperArm] = "RightArm",
        [HumanoidBone.RightLowerArm] = "RightForeArm",
        [HumanoidBone.RightHand] = "RightHand",
        [HumanoidBone.LeftUpperLeg] = "LeftUpLeg",
        [HumanoidBone.LeftLowerLeg] = "LeftLeg",
        [HumanoidBone.LeftFoot] = "LeftFoot",
        [HumanoidBone.LeftToes] = "LeftToeBase",
        [HumanoidBone.RightUpperLeg] = "RightUpLeg",
        [HumanoidBone.RightLowerLeg] = "RightLeg",
        [HumanoidBone.RightFoot] = "RightFoot",
        [HumanoidBone.RightToes] = "RightToeBase",
    };

    /// <summary>Bone names as <c>bone_&lt;index&gt;</c>: nothing but the shape to go on.</summary>
    public static string PlainName(HumanoidBone slot, bool end) => string.Empty;

    /// <summary>Names as a game-engine mannequin writes them.</summary>
    public static string Mannequin(HumanoidBone slot, bool end) =>
        end ? $"{MannequinBases[slot]}_end" : MannequinBases[slot];

    /// <summary>Names as a motion-capture service writes them.</summary>
    public static string Capture(HumanoidBone slot, bool end) =>
        end ? $"mixamorig:{CaptureBases[slot]}_End" : $"mixamorig:{CaptureBases[slot]}";

    /// <summary>
    /// A person <paramref name="height"/> metres tall with their arms <paramref name="armsDown"/> radians
    /// below level, so zero is a T-pose and a bit under a right angle is an A.
    /// </summary>
    /// <remarks>
    /// <paramref name="spine"/> is how many bones there are between hips and neck, from one to three.
    /// <paramref name="rightFirst"/> makes the right arm and leg before the left, so that a bone's place in the
    /// list says nothing about its side.
    /// </remarks>
    // F2: seven options, called by name.
    public static Person Build(
        Naming? naming = null,
        double height = 1.8,
        double armsDown = 0,
        int spine = 2,
        bool shoulders = true,
        bool rightFirst = false,
        bool endSites = false)
    {
        var who = new PersonBuilder
        {
            Naming = naming ?? PlainName,
            Height = height,
            ArmsDown = armsDown,
            Shoulders = shoulders,
            EndSites = endSites,
        };
        var hips = who.Add(HumanoidBone.Hips, -1, new Vector3(0, 0.55, 0));
        var top = hips;
        foreach (var slot in SpineSlots[spine - 1])
        {
            top = who.Add(slot, top, new Vector3(0, 0.25 / spine, 0));
        }

        var neck = who.Add(HumanoidBone.Neck, top, new Vector3(0, 0.07, 0));
        var head = who.Add(HumanoidBone.Head, neck, new Vector3(0, 0.06, 0));
        if (endSites)
        {
            who.Add(HumanoidBone.Head, head, new Vector3(0, 0.07, 0), end: true);
        }

        foreach (var left in rightFirst ? new[] { false, true } : new[] { true, false })
        {
            who.AddArm(top, left);
            who.AddLeg(hips, left);
        }

        return who.Done();
    }
}

internal sealed class PersonBuilder
{
    private readonly List<string> _names = [];
    private readonly List<int> _parents = [];
    private readonly List<(Vector3, Quaternion)> _locals = [];
    private readonly Dictionary<HumanoidBone, string> _slots = [];

    public required Naming Naming { get; init; }

    public required double Height { get; init; }

    public required double ArmsDown { get; init; }

    public required bool Shoulders { get; init; }

    public required bool EndSites { get; init; }

    // F2: a bone is a slot, a parent, an offset, a turn and whether it only marks where the last one stops.
    public int Add(HumanoidBone slot, int parent, Vector3 offset, bool end = false, double turn = 0)
    {
        var index = _names.Count;
        var given = Naming(slot, end);
        var label = given.Length == 0 ? $"bone_{index}" : given;
        _names.Add(label);
        _parents.Add(parent);
        _locals.Add((offset * Height, Quaternion.AxisAngle(new Vector3(0, 0, 1), turn)));
        if (!end)
        {
            _slots[slot] = label;
        }

        return index;
    }

    public void AddArm(int top, bool left)
    {
        var s = left ? 1.0 : -1.0;
        var upper = left ? HumanoidBone.LeftUpperArm : HumanoidBone.RightUpperArm;
        var at = top;
        if (Shoulders)
        {
            at = Add(left ? HumanoidBone.LeftShoulder : HumanoidBone.RightShoulder, at, new Vector3(s * 0.03, 0.05, 0));
        }

        at = Add(
            upper,
            at,
            new Vector3(s * (Shoulders ? 0.07 : 0.1), Shoulders ? 0 : 0.05, 0),
            turn: -s * ArmsDown);
        at = Add(left ? HumanoidBone.LeftLowerArm : HumanoidBone.RightLowerArm, at, new Vector3(s * 0.17, 0, 0));
        var hand = left ? HumanoidBone.LeftHand : HumanoidBone.RightHand;
        at = Add(hand, at, new Vector3(s * 0.14, 0, 0));
        if (EndSites)
        {
            Add(hand, at, new Vector3(s * 0.05, 0, 0), end: true);
        }
    }

    public void AddLeg(int hips, bool left)
    {
        var s = left ? 1.0 : -1.0;
        var leg = Add(left ? HumanoidBone.LeftUpperLeg : HumanoidBone.RightUpperLeg, hips, new Vector3(s * 0.05, -0.03, 0));
        leg = Add(left ? HumanoidBone.LeftLowerLeg : HumanoidBone.RightLowerLeg, leg, new Vector3(0, -0.24, 0));
        leg = Add(left ? HumanoidBone.LeftFoot : HumanoidBone.RightFoot, leg, new Vector3(0, -0.24, 0));
        var toes = left ? HumanoidBone.LeftToes : HumanoidBone.RightToes;
        leg = Add(toes, leg, new Vector3(0, -0.035, 0.07));
        if (EndSites)
        {
            Add(toes, leg, new Vector3(0, 0, 0.03), end: true);
        }
    }

    public Person Done() => new(_names, _parents, [.. _locals.Select(static local => (local.Item1, local.Item2))], _slots);
}

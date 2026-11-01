using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Orblit.Scene;

/// <summary>How a joint lets its body move.</summary>
public enum JointKind
{
    /// <summary>Not at all: welded where it stands.</summary>
    Fixed,

    /// <summary>
    /// Any way it likes about the joint's point, and never away from it. A pendulum, a ball and
    /// socket.
    /// </summary>
    Point,

    /// <summary>
    /// About the joint's x axis only, like a door. Limited by <see cref="JointAxis.AboutX"/>, and it
    /// takes a motor.
    /// </summary>
    Hinge,

    /// <summary>
    /// Along the joint's x axis only, never turning, like a drawer. Limited by
    /// <see cref="JointAxis.AlongX"/>, and it takes a motor.
    /// </summary>
    Slider,

    /// <summary>
    /// Keeps the joint's point and the middle of its body a distance apart, and lets both turn.
    /// Limited by <see cref="JointAxis.AlongX"/>, which is then a range of lengths: a rope is nought
    /// to its length. Unlimited, it is a rod.
    /// </summary>
    Distance,

    /// <summary>
    /// About its point, with its body's x axis kept within <see cref="JointComponent.Swing"/> of the
    /// joint's. A shoulder or a hip. <see cref="JointAxis.AboutX"/> limits the twist.
    /// </summary>
    Cone,

    /// <summary>
    /// Each of the six ways set on its own in <see cref="JointComponent.Limits"/>: free where there
    /// is no limit, and locked where a limit's low is its high.
    /// </summary>
    SixAxis,

    /// <summary>
    /// Ties how far the body turns to how far the body it hangs from does: it turns
    /// <see cref="JointComponent.Ratio"/> times as far, about <see cref="JointComponent.AxisB"/>.
    /// Meshed gears turn opposite ways, so their ratio is negative. Both are usually hinged to
    /// something already.
    /// </summary>
    Gear,

    /// <summary>
    /// Ties how far the body it hangs from slides, along the joint's x axis, to how far the body
    /// turns, about <see cref="JointComponent.AxisB"/>: <see cref="JointComponent.Ratio"/> metres for
    /// each radian. The rack is the holder, and the pinion is the body.
    /// </summary>
    Rack,

    /// <summary>
    /// A rope over two fixed points, <see cref="JointComponent.Over"/>. The length from the first
    /// point to the holder, plus <see cref="JointComponent.Ratio"/> times the length from the second
    /// point to the body, stays what it was. It is a rod, so it pushes as well as pulls.
    /// </summary>
    Pulley,

    /// <summary>
    /// Lets the body slide along the bent line <see cref="JointComponent.Path"/>, held on it.
    /// Limited by <see cref="JointAxis.AlongX"/>, measured along the line from its first point, and
    /// it takes a motor.
    /// </summary>
    Path,
}

/// <summary>
/// One of the six ways a joint's body can move: along the joint's three axes and about them.
/// </summary>
public enum JointAxis
{
    AlongX,
    AlongY,
    AlongZ,
    AboutX,
    AboutY,
    AboutZ,
}

/// <summary>Reads of <see cref="JointAxis"/>.</summary>
public static class JointAxes
{
    /// <summary>Whether <paramref name="axis"/> is a turn, measured in degrees, rather than a distance in metres.</summary>
    public static bool Turns(JointAxis axis) => axis >= JointAxis.AboutX;
}

/// <summary>
/// The least and the most of one <see cref="JointAxis"/>: metres along an axis, degrees about one.
/// </summary>
public sealed record JointRange(double Low, double High)
{
    /// <summary>A range that is one value: locked there.</summary>
    public static JointRange At(double value) => new(value, value);

    public bool Locked => Low == High;
}

/// <summary>
/// A spring on one <see cref="JointAxis"/>: it pulls that way to <paramref name="Rest"/> as a spring
/// of <paramref name="Hertz"/> cycles a second would, with damping <paramref name="Ratio"/>.
/// </summary>
/// <remarks>
/// <paramref name="Rest"/> is in metres along an axis and degrees about one, from where the joint was
/// made. Null is where it was made, which for a distance joint is the length it was made at and not
/// nought. Nought for <paramref name="Ratio"/> rings for ever. One settles as quickly as it can
/// without overshooting. Above one is sluggish.
/// <para>
/// On a way the joint locks, a spring takes the lock's place, so the joint gives there. On a way it
/// leaves free, the spring is the only thing that holds it.
/// </para>
/// </remarks>
public sealed record JointGive(double Hertz, double Ratio = 1, double? Rest = null);

/// <summary>
/// A joint: holds the body this entity belongs to to the body it hangs from.
/// </summary>
/// <remarks>
/// Which two bodies is where the entity sits, not ids it names. The joint's body is the nearest body
/// at or above this entity, this entity's own, or its parent's, and so on up, and what it hangs from
/// is the nearest body above that, or the world when there is none. A forearm hangs from an upper arm
/// because it is under it in the tree; a door on the world has nothing above it with a body. Named
/// that way because an id named in a component is an id every copy, prefab and paste has to find and
/// change, and a parent link is one they already do. It cannot close a loop, a chain tied at both
/// ends, for the same reason.
/// <para>
/// The joint's point and axes are this entity's own, where it stands when the scene begins: a hinge
/// turns about this entity's x axis through its origin. So a joint that is not at its body's middle,
/// a door's hinge, an elbow, is an entity of its own under the body, placed there. A distance joint
/// keeps this point a distance from the middle of its body.
/// </para>
/// <para>
/// What it holds is how the bodies stood when the scene began, so every limit is measured from there,
/// and each measure is the joint's body as the body it hangs from sees it: a hinge made with its door
/// shut reads nought when the door is shut, and an elbow's angle turns with the upper arm.
/// </para>
/// <para>
/// Every field is written every time, including the ones this kind does not use, for the reason
/// <see cref="BodyComponent"/> gives: a hinge switched to a slider and back should come back with the
/// range it had.
/// </para>
/// </remarks>
public sealed class JointComponent : SceneComponent
{
    private static readonly JointAxis[] AllAxes = Enum.GetValues<JointAxis>();

    // F2: nineteen fields of one joint, called by name.
    public JointComponent(
        JointKind kind = JointKind.Hinge,
        IReadOnlyDictionary<JointAxis, JointRange>? limits = null,
        double swing = 45,
        double speed = 0,
        double strength = 0,
        double breakingForce = 0,
        double breakingTorque = 0,
        bool collide = false,
        IReadOnlyDictionary<JointAxis, JointGive>? springs = null,
        double softHertz = 0,
        double softRatio = 1,
        double goal = 0,
        double goalHertz = 0,
        double goalRatio = 1,
        double ratio = 1,
        Vector3? axisB = null,
        IReadOnlyList<Vector3>? over = null,
        IReadOnlyList<Vector3>? path = null,
        bool follow = false)
    {
        Kind = kind;
        Limits = new ReadOnlyDictionary<JointAxis, JointRange>(
            new Dictionary<JointAxis, JointRange>(limits ?? new Dictionary<JointAxis, JointRange>()));
        Swing = swing;
        Speed = speed;
        Strength = strength;
        BreakingForce = breakingForce;
        BreakingTorque = breakingTorque;
        Collide = collide;
        Springs = new ReadOnlyDictionary<JointAxis, JointGive>(
            new Dictionary<JointAxis, JointGive>(springs ?? new Dictionary<JointAxis, JointGive>()));
        SoftHertz = softHertz;
        SoftRatio = softRatio;
        Goal = goal;
        GoalHertz = goalHertz;
        GoalRatio = goalRatio;
        Ratio = ratio;
        AxisB = axisB?.Clone() ?? Vector3.Zero();
        Over = new ReadOnlyCollection<Vector3>([.. (over ?? []).Select(static p => p.Clone())]);
        Path = new ReadOnlyCollection<Vector3>([.. (path ?? []).Select(static p => p.Clone())]);
        Follow = follow;
    }

    public JointKind Kind { get; }

    /// <summary>
    /// The range each axis is kept within, in metres along and degrees about. An axis not here is
    /// free, or whatever the kind makes it; a kind reads only the axes it says it does.
    /// </summary>
    public IReadOnlyDictionary<JointAxis, JointRange> Limits { get; }

    /// <summary>How far a cone lets its body's x axis swing from the joint's, in degrees.</summary>
    public double Swing { get; }

    /// <summary>
    /// A hinge's or a slider's motor: the speed it drives at, in degrees or metres a second, and the
    /// most it pushes with, in newton-metres or newtons. No strength is no motor; no speed with a
    /// little strength is friction in the joint.
    /// </summary>
    public double Speed { get; }

    public double Strength { get; }

    /// <summary>
    /// Past this force in newtons, or this torque in newton-metres, the joint breaks. Nought never
    /// breaks.
    /// </summary>
    public double BreakingForce { get; }

    public double BreakingTorque { get; }

    /// <summary>
    /// Whether the two bodies still collide with each other. Off by default, because two bodies
    /// joined nearly always overlap where they are joined.
    /// </summary>
    public bool Collide { get; }

    /// <summary>
    /// The spring on each way that has one, with <see cref="JointGive.Rest"/> in metres and degrees.
    /// A kind reads only the ways <see cref="Axes"/> names.
    /// </summary>
    public IReadOnlyDictionary<JointAxis, JointGive> Springs { get; }

    /// <summary>
    /// How soft the ends of the limits are, as a spring of this many cycles a second with this
    /// damping ratio. Nought is a wall. A lock stays hard.
    /// </summary>
    public double SoftHertz { get; }

    public double SoftRatio { get; }

    /// <summary>
    /// A hinge's or a slider's motor holds <see cref="Goal"/>, in degrees or metres from where the
    /// joint was made, as a spring of <see cref="GoalHertz"/> would, and never pushes harder than
    /// <see cref="Strength"/>. Nought hertz is a motor of <see cref="Speed"/>.
    /// </summary>
    public double Goal { get; }

    public double GoalHertz { get; }

    public double GoalRatio { get; }

    /// <summary>What a gear, a rack or a pulley ties the two sides at.</summary>
    public double Ratio { get; }

    /// <summary>
    /// The axis the body turns about for a gear or a rack, in the joint's own frame. Nought is the
    /// joint's x axis, the same as the holder's.
    /// </summary>
    public Vector3 AxisB { get; }

    /// <summary>
    /// A pulley's two fixed points, in the joint's own frame, where it stands when the scene begins.
    /// </summary>
    public IReadOnlyList<Vector3> Over { get; }

    /// <summary>
    /// A path's points in the joint's own frame, from where the body starts. Close a loop by
    /// repeating the first point.
    /// </summary>
    public IReadOnlyList<Vector3> Path { get; }

    /// <summary>
    /// Whether a path keeps the body's x axis along the line, free to roll about it, and does not
    /// leave the body's turn as it was.
    /// </summary>
    public bool Follow { get; }

    /// <summary>The limits this kind reads, which are all the solver will hear of.</summary>
    [SuppressMessage("Maintainability", "CA1502", Justification = "F2: a list of cases is data.")]
    public IReadOnlyList<JointAxis> Axes => Kind switch
    {
        JointKind.Fixed or JointKind.Point => [],
        JointKind.Hinge or JointKind.Cone => [JointAxis.AboutX],
        JointKind.Slider or JointKind.Distance or JointKind.Path => [JointAxis.AlongX],
        JointKind.Gear or JointKind.Rack or JointKind.Pulley => [],
        JointKind.SixAxis => AllAxes,
        _ => throw new UnreachableException(),
    };

    public override string Type => SceneComponents.Joint;

    public static JointComponent FromJson(JsonMap json) => new(
        Values.Named<JointKind>(Values.At(json, "kind")) ?? JointKind.Hinge,
        LimitsOf(Values.At(json, "limits")),
        Values.Number(json, "swing", 45),
        Values.Number(json, "speed", 0),
        Values.Number(json, "strength", 0),
        Values.Number(json, "breakingForce", 0),
        Values.Number(json, "breakingTorque", 0),
        Values.Flag(json, "collide", false),
        SpringsOf(Values.At(json, "springs")),
        Values.Number(json, "softHertz", 0),
        Values.Number(json, "softRatio", 1),
        Values.Number(json, "goal", 0),
        Values.Number(json, "goalHertz", 0),
        Values.Number(json, "goalRatio", 1),
        Values.Number(json, "ratio", 1),
        Values.Vector(Values.At(json, "axisB")),
        PointsOf(Values.At(json, "over")),
        PointsOf(Values.At(json, "path")),
        Values.Flag(json, "follow", false));

    /// <summary>
    /// The two bodies the joint on entity <paramref name="id"/> holds together: <c>Body</c>, the
    /// nearest entity at or above <paramref name="id"/> with a body, and <c>Holder</c>, the nearest
    /// one above that. <c>Holder</c> is null for the world. <c>Body</c> is null when there is nothing
    /// to hold, and the joint does nothing.
    /// </summary>
    /// <remarks>
    /// Written against two lookups rather than a document, so everything that keeps a tree, a
    /// document, an editor's own scene, answers the same.
    /// </remarks>
    public static (string? Body, string? Holder) EndsOf(
        string id,
        Func<string, string?> parentOf,
        Func<string, bool> hasBody)
    {
        // A tree that loops back on itself ends the climb where it comes round, and never at the
        // body it started from.
        var seen = new HashSet<string>();

        string? Nearest(string? from)
        {
            for (var at = from; at is not null && seen.Add(at); at = parentOf(at))
            {
                if (hasBody(at))
                {
                    return at;
                }
            }

            return null;
        }

        var body = Nearest(id);
        return body is null ? (null, null) : (body, Nearest(parentOf(body)));
    }

    /// <summary>
    /// The same joint with some of it changed. <paramref name="limits"/>, when given, replaces every
    /// limit; <see cref="Limit"/> and <see cref="Free"/> change one.
    /// </summary>
    // F2: nineteen fields of one joint, called by name.
    [SuppressMessage("Maintainability", "CA1502", Justification = "F2: a list of fields is data.")]
    public JointComponent CopyWith(
        JointKind? kind = null,
        IReadOnlyDictionary<JointAxis, JointRange>? limits = null,
        double? swing = null,
        double? speed = null,
        double? strength = null,
        double? breakingForce = null,
        double? breakingTorque = null,
        bool? collide = null,
        IReadOnlyDictionary<JointAxis, JointGive>? springs = null,
        double? softHertz = null,
        double? softRatio = null,
        double? goal = null,
        double? goalHertz = null,
        double? goalRatio = null,
        double? ratio = null,
        Vector3? axisB = null,
        IReadOnlyList<Vector3>? over = null,
        IReadOnlyList<Vector3>? path = null,
        bool? follow = null) => new(
        kind ?? Kind,
        limits ?? Limits,
        swing ?? Swing,
        speed ?? Speed,
        strength ?? Strength,
        breakingForce ?? BreakingForce,
        breakingTorque ?? BreakingTorque,
        collide ?? Collide,
        springs ?? Springs,
        softHertz ?? SoftHertz,
        softRatio ?? SoftRatio,
        goal ?? Goal,
        goalHertz ?? GoalHertz,
        goalRatio ?? GoalRatio,
        ratio ?? Ratio,
        axisB ?? AxisB,
        over ?? Over,
        path ?? Path,
        follow ?? Follow);

    /// <summary>The same joint with a spring on <paramref name="axis"/>.</summary>
    public JointComponent Spring(JointAxis axis, JointGive give) =>
        CopyWith(springs: new Dictionary<JointAxis, JointGive>(Springs) { [axis] = give });

    /// <summary>The same joint with no spring on <paramref name="axis"/>.</summary>
    public JointComponent Unspring(JointAxis axis)
    {
        var rest = new Dictionary<JointAxis, JointGive>(Springs);
        rest.Remove(axis);
        return CopyWith(springs: rest);
    }

    /// <summary>The same joint with <paramref name="axis"/> limited to <paramref name="range"/>.</summary>
    public JointComponent Limit(JointAxis axis, JointRange range) =>
        CopyWith(limits: new Dictionary<JointAxis, JointRange>(Limits) { [axis] = range });

    /// <summary>The same joint with <paramref name="axis"/> free.</summary>
    public JointComponent Free(JointAxis axis)
    {
        var rest = new Dictionary<JointAxis, JointRange>(Limits);
        rest.Remove(axis);
        return CopyWith(limits: rest);
    }

    public override JsonMap ToJson() => new OrderedDictionary<string, object?>
    {
        ["kind"] = EnumNames.Of(Kind),
        ["limits"] = Written(Limits, static range => Values.NumbersToJson([range.Low, range.High])),
        ["swing"] = Swing,
        ["speed"] = Speed,
        ["strength"] = Strength,
        ["breakingForce"] = BreakingForce,
        ["breakingTorque"] = BreakingTorque,
        ["collide"] = Collide,
        ["springs"] = Written(Springs, static give => Values.NumbersToJson(
            give.Rest is { } rest ? [give.Hertz, give.Ratio, rest] : [give.Hertz, give.Ratio])),
        ["softHertz"] = SoftHertz,
        ["softRatio"] = SoftRatio,
        ["goal"] = Goal,
        ["goalHertz"] = GoalHertz,
        ["goalRatio"] = GoalRatio,
        ["ratio"] = Ratio,
        ["axisB"] = Values.VectorToJson(AxisB),
        ["over"] = Flat(Over),
        ["path"] = Flat(Path),
        ["follow"] = Follow,
    };

    /// <summary>One entry per axis that has a value, under the axis's name, in the axis order.</summary>
    private static OrderedDictionary<string, object?> Written<T>(
        IReadOnlyDictionary<JointAxis, T> byAxis,
        Func<T, List<object?>> write)
    {
        var json = new OrderedDictionary<string, object?>();
        foreach (var axis in AllAxes)
        {
            if (byAxis.TryGetValue(axis, out var value))
            {
                json[EnumNames.Of(axis)] = write(value);
            }
        }

        return json;
    }

    private static List<object?> Flat(IReadOnlyList<Vector3> points) =>
        [.. points.SelectMany(static point => Values.VectorToJson(point))];

    /// <summary>
    /// Each spring a file gives as hertz, ratio and rest under an axis's name. A spring with no
    /// frequency above nought is skipped, as it would be no spring. A missing rest is kept missing.
    /// </summary>
    private static Dictionary<JointAxis, JointGive> SpringsOf(object? raw)
    {
        var json = Values.Object(raw);
        var springs = new Dictionary<JointAxis, JointGive>();
        foreach (var axis in AllAxes)
        {
            if (Values.At(json, EnumNames.Of(axis)) is JsonList list && GiveOf(list) is { } give)
            {
                springs[axis] = give;
            }
        }

        return springs;
    }

    private static JointGive? GiveOf(JsonList list)
    {
        if (list.Count > 0 && Values.Num(list[0]) is { } hertz && hertz > 0)
        {
            return new JointGive(
                hertz,
                list.Count > 1 ? Values.Num(list[1]) ?? 1 : 1,
                list.Count > 2 ? Values.Num(list[2]) : null);
        }

        return null;
    }

    /// <summary>
    /// Points written three numbers to a corner. A trailing one or two numbers are not a point and
    /// are dropped.
    /// </summary>
    private static List<Vector3> PointsOf(object? raw)
    {
        var flat = Values.Numbers(raw);
        var points = new List<Vector3>();
        for (var i = 0; i + 2 < flat.Count; i += 3)
        {
            points.Add(new Vector3(flat[i], flat[i + 1], flat[i + 2]));
        }

        return points;
    }

    /// <summary>
    /// Each limit a file gives as two numbers under an axis's name. Anything else there is skipped,
    /// so a hand-edit that gets one wrong loses that one and keeps the rest.
    /// </summary>
    private static Dictionary<JointAxis, JointRange> LimitsOf(object? raw)
    {
        var json = Values.Object(raw);
        var limits = new Dictionary<JointAxis, JointRange>();
        foreach (var axis in AllAxes)
        {
            if (Values.At(json, EnumNames.Of(axis)) is JsonList { Count: 2 } pair
                && Values.Num(pair[0]) is { } low
                && Values.Num(pair[1]) is { } high)
            {
                limits[axis] = new JointRange(low, high);
            }
        }

        return limits;
    }
}

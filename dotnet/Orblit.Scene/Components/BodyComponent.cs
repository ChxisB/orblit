using System.Collections.Frozen;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

namespace Orblit.Scene;

/// <summary>The shape a body is simulated as.</summary>
/// <remarks>
/// Deliberately few, and each one exactly: a box, a ball, a capsule, a cylinder, a convex hull, a
/// compound, a triangle mesh and the ground. A shape the solver cannot hold is not offered here as
/// something it will approximate, because a body that behaves like a box while the file says cone is
/// a bug somebody tunes around rather than reports.
/// </remarks>
public enum BodyShape
{
    /// <summary>A box <see cref="BodyComponent.Size"/> across.</summary>
    Box,

    /// <summary>A ball of <see cref="BodyComponent.Radius"/>.</summary>
    Sphere,

    /// <summary>
    /// A cylinder with a hemisphere on each end, <see cref="BodyComponent.Height"/> tall from tip to
    /// tip and <see cref="BodyComponent.Radius"/> round, standing along the entity's own up. What a
    /// character or a limb is made of: it has no corner to catch on the seam between two floor tiles.
    /// </summary>
    Capsule,

    /// <summary>
    /// A cylinder with flat ends, <see cref="BodyComponent.Height"/> tall from end to end and
    /// <see cref="BodyComponent.Radius"/> round, standing along the entity's own up. A barrel, a
    /// wheel, a pillar.
    /// </summary>
    Cylinder,

    /// <summary>
    /// The smallest convex solid round the points in <see cref="BodyComponent.Hull"/>. A rock, a
    /// wedge, a crate with its corners knocked off. Convex means no dents: a bowl is a solid lump to
    /// the solver, so a body that must be hollow is several.
    /// </summary>
    Hull,

    /// <summary>The convex pieces in <see cref="BodyComponent.Parts"/>, including their empty gaps.</summary>
    Compound,

    /// <summary>A fixed, two-sided surface from <see cref="BodyComponent.MeshVertices"/> and indices.</summary>
    Mesh,

    /// <summary>
    /// Endless ground. Everything below the entity's own up, through the point the body is centred
    /// on, is solid. It never moves, whatever <see cref="BodyMotion"/> says, because a half-space has
    /// no middle to spin about.
    /// </summary>
    Plane,
}

/// <summary>How a body moves.</summary>
public enum BodyMotion
{
    /// <summary>Never moves. The floor, the walls.</summary>
    Fixed,

    /// <summary>
    /// Goes exactly where it is sent and pushes whatever is in the way without being pushed back. A
    /// lift, a door, a moving platform.
    /// </summary>
    Driven,

    /// <summary>Falls, is pushed, and pushes back.</summary>
    Free,

    /// <summary>
    /// Walks. Swept through the world rather than solved, so it slides along walls, climbs a step,
    /// stands on a slope and rides what it stands on. It goes where it is driven and nowhere else,
    /// so it does not fall until something asks it to. Its shape is the body's own, normally a
    /// capsule.
    /// </summary>
    Character,
}

/// <summary>
/// One of the six ways a free body can be held still, in the world's axes rather than the body's
/// own: a body locked from moving along <c>z</c> stays in its plane however it is turned.
/// </summary>
public enum BodyLock
{
    MoveX,
    MoveY,
    MoveZ,
    TurnX,
    TurnY,
    TurnZ,
}

/// <summary>
/// A surface by what it is made of: how much it grips and how much it bounces.
/// </summary>
/// <remarks>
/// A preset is a shortcut for setting two numbers, so a body does not store one. It is a preset
/// while its friction and restitution are exactly its own, and it stops being one the moment either
/// is dragged.
/// </remarks>
public sealed class BodyMaterial(string name, double friction, double restitution)
{
    /// <summary>The presets, slipperiest first.</summary>
    public static readonly IReadOnlyList<BodyMaterial> Presets =
    [
        new("Ice", 0.05, 0.05),
        new("Metal", 0.4, 0.15),
        new("Wood", 0.5, 0.2),
        new("Stone", 0.7, 0.1),
        new("Sandbag", 0.9, 0),
        new("Rubber", 0.95, 0.8),
    ];

    public string Name { get; } = name;

    public double Friction { get; } = friction;

    public double Restitution { get; } = restitution;

    /// <summary>The preset <paramref name="body"/> is made of, or null when its numbers are its own.</summary>
    public static BodyMaterial? Of(BodyComponent body) =>
        Presets.FirstOrDefault(preset => preset.Friction == body.Friction && preset.Restitution == body.Restitution);
}

/// <summary>
/// Something the physics simulates.
/// </summary>
/// <remarks>
/// Not the same thing as the boundary a mesh carries. A boundary says where an object begins and ends
/// for picking it and for asking what a ray hits in the scene's geometry; a body says what the
/// simulation does with it, whether it falls, how heavy it is, what it bounces off. A crate usually
/// has both, a decoration has a boundary and no body, and a trigger in empty space has a body and
/// nothing to draw.
/// <para>
/// Sizes are in the entity's own units, so a body scales with its entity: a crate scaled to two is a
/// crate with a body twice the size, and fitting a body to a mesh is copying the mesh's own
/// dimensions rather than working out what they come to in the world. A ball takes the largest of its
/// entity's three scales, and a capsule or a cylinder its larger sideways scale for the radius and
/// its upright one for the height, because neither can be stretched into anything but a bigger
/// version of itself. A hull can be stretched, and each of its points moves by the entity's scale
/// along each axis.
/// </para>
/// <para>
/// Every field is written every time, including the ones this shape does not use, for the reason
/// <see cref="LightComponent"/> gives: a box switched to a ball and back should come back the size it
/// was.
/// </para>
/// </remarks>
public sealed class BodyComponent : SceneComponent
{
    /// <summary>All thirty-two layers.</summary>
    public const long EveryLayer = 0xFFFFFFFFL;

    // F2: thirty-four fields of one body, called by name.
    [SuppressMessage("Maintainability", "CA1502", Justification = "F2: a list of fields is data.")]
    public BodyComponent(
        BodyShape shape = BodyShape.Box,
        Vector3? size = null,
        double radius = 0.5,
        double height = 2,
        IReadOnlyList<double>? hull = null,
        IReadOnlyList<BodyPart>? parts = null,
        IReadOnlyList<double>? meshVertices = null,
        IReadOnlyList<long>? meshIndices = null,
        Vector3? shapeScale = null,
        Vector3? centre = null,
        BodyMotion motion = BodyMotion.Free,
        double mass = 1,
        double friction = 0.5,
        double restitution = 0,
        double linearDamping = 0.05,
        double angularDamping = 0.05,
        long layers = 1,
        long cares = EveryLayer,
        bool startsAsleep = false,
        bool trigger = false,
        bool stay = false,
        Vector3? surface = null,
        IReadOnlySet<BodyLock>? locks = null,
        double gravityScale = 1,
        double maxSpeed = 0,
        double maxSpin = 0,
        bool continuous = false,
        Vector3? centreOfMass = null,
        Vector3? inertia = null,
        double stepHeight = 0.3,
        double steepest = 0.7853981633974483,
        double skin = 0.01,
        double strength = 500,
        double crouchHeight = 0)
    {
        Shape = shape;
        Size = size ?? Vector3.All(1);
        Radius = radius;
        Height = height;
        Hull = new ReadOnlyCollection<double>([.. hull ?? []]);
        Parts = new ReadOnlyCollection<BodyPart>([.. parts ?? []]);
        MeshVertices = new ReadOnlyCollection<double>([.. meshVertices ?? []]);
        MeshIndices = new ReadOnlyCollection<long>([.. meshIndices ?? []]);
        ShapeScale = (shapeScale ?? Vector3.All(1)).Clone();
        Centre = centre ?? Vector3.Zero();
        Motion = motion;
        Mass = mass;
        Friction = friction;
        Restitution = restitution;
        LinearDamping = linearDamping;
        AngularDamping = angularDamping;
        Layers = layers;
        Cares = cares;
        StartsAsleep = startsAsleep;
        Trigger = trigger;
        Stay = stay;
        Surface = surface ?? Vector3.Zero();
        Locks = (locks ?? FrozenSet<BodyLock>.Empty).ToFrozenSet();
        GravityScale = gravityScale;
        MaxSpeed = maxSpeed;
        MaxSpin = maxSpin;
        Continuous = continuous;
        CentreOfMass = centreOfMass ?? Vector3.Zero();
        Inertia = inertia ?? Vector3.Zero();
        StepHeight = stepHeight;
        Steepest = steepest;
        Skin = skin;
        Strength = strength;
        CrouchHeight = crouchHeight;
    }

    public BodyShape Shape { get; }

    /// <summary>Convex pieces of a compound. A plane, mesh or nested compound is inert.</summary>
    public IReadOnlyList<BodyPart> Parts { get; }

    /// <summary>
    /// Triangle positions measured from <see cref="Centre"/>, in the entity's own units. A mesh is a
    /// sheet with two sides, not a solid volume or a moving body.
    /// </summary>
    public IReadOnlyList<double> MeshVertices { get; }

    public IReadOnlyList<long> MeshIndices { get; }

    /// <summary>
    /// Stretch of the collision geometry before the entity's own transform. Rounded shapes stretch
    /// exactly. Every component must be positive.
    /// </summary>
    public Vector3 ShapeScale { get; }

    /// <summary>
    /// How big a box is, edge to edge, not from the middle, because a one metre crate should say one.
    /// </summary>
    public Vector3 Size { get; }

    /// <summary>How round a ball, a capsule or a cylinder is, in metres.</summary>
    public double Radius { get; }

    /// <summary>
    /// How tall a capsule is from tip to tip, hemispheres included, because that is how tall a
    /// character is. A capsule no taller than twice its radius is all ends and no middle, and is
    /// simulated as the ball that makes it. A cylinder's is from one flat end to the other.
    /// </summary>
    public double Height { get; }

    /// <summary>
    /// The corners of a <see cref="BodyShape.Hull"/>, three numbers each: <c>x0, y0, z0, x1, ...</c>.
    /// In the entity's own units and measured from <see cref="Centre"/>, so a hull scales with its
    /// entity as a box does.
    /// </summary>
    /// <remarks>
    /// The body is the smallest convex solid round them, so a point inside it changes nothing, and a
    /// hull cut from a mesh fills the mesh's dents. Fewer than four points, or points that all lie
    /// in one plane, make a hull with no inside, and no body is simulated for it. Every other shape
    /// ignores it.
    /// </remarks>
    public IReadOnlyList<double> Hull { get; }

    /// <summary>
    /// Where the shape sits relative to the entity, for the times the entity's origin is not the
    /// middle of what it is: a character stands on its feet, and its capsule belongs round its waist.
    /// </summary>
    public Vector3 Centre { get; }

    public BodyMotion Motion { get; }

    /// <summary>Kilograms. Only a free body is moved by what it weighs.</summary>
    public double Mass { get; }

    /// <summary>How much it grips, from nothing (ice) up; half is ordinary.</summary>
    public double Friction { get; }

    /// <summary>How much it bounces, from nothing (a sandbag) to fully (a perfect ball).</summary>
    public double Restitution { get; }

    /// <summary>
    /// How quickly it slows down and stops spinning on its own, as a fraction a second. What stands
    /// in for air.
    /// </summary>
    public double LinearDamping { get; }

    public double AngularDamping { get; }

    /// <summary>Which layers it is in, one bit each.</summary>
    public long Layers { get; }

    /// <summary>
    /// Which layers it wants to meet. A pair meets when either cares about the other, not only when
    /// both do, so a bullet that cares about walls hits a wall that cares about nothing, the same
    /// rule every query in Orblit uses.
    /// </summary>
    public long Cares { get; }

    /// <summary>
    /// Whether it begins at rest, waiting to be touched. A tower of crates that starts asleep costs
    /// nothing until somebody knocks it.
    /// </summary>
    public bool StartsAsleep { get; }

    /// <summary>
    /// Whether it is a place rather than a thing: nothing collides with it and it pushes nothing, and
    /// it reports the bodies that come into it and leave it. A pickup, a door's sensor, the region a
    /// <see cref="ZoneComponent"/> takes effect over. Solid queries and characters do not see it.
    /// <para>
    /// Only a fixed or driven body can be one. A free body has to be moved by the solver, so the flag
    /// is ignored for it.
    /// </para>
    /// </summary>
    public bool Trigger { get; }

    /// <summary>
    /// Whether it hears every step that a contact goes on, or that a body is still inside it, and not
    /// only when one begins and ends. Most bodies do not want a message a step, so it is asked for.
    /// </summary>
    public bool Stay { get; }

    /// <summary>
    /// How fast its surface moves, in world metres per second, while the body stays where it is: a
    /// conveyor belt. What stands on it is carried along. Only the part along the face it touches
    /// counts.
    /// </summary>
    public Vector3 Surface { get; }

    /// <summary>
    /// The ways it may not move, in the world's axes. A locked move drops that part of every
    /// velocity, push and contact, and a locked turn does the same to spin. Only a free body is held:
    /// a fixed or driven one is placed, not pushed.
    /// </summary>
    public IReadOnlySet<BodyLock> Locks { get; }

    /// <summary>
    /// How much of the world's gravity, and of a <see cref="ZoneComponent"/>'s, it feels. One is
    /// ordinary, zero floats and a negative number rises.
    /// </summary>
    public double GravityScale { get; }

    /// <summary>
    /// The fastest it may go in metres per second, and the fastest it may spin in radians per
    /// second. Zero is no limit.
    /// </summary>
    public double MaxSpeed { get; }

    public double MaxSpin { get; }

    /// <summary>
    /// Stops it at the first fixed body its move would take it through, instead of finding it on the
    /// far side. The world already holds anything fast with contacts it makes before the touch. This
    /// is for a small body at a walking pace against a thin wall, and one that is knocked at a wall
    /// in the same step. It costs one cast a step while the body moves, so it is off. A driven body
    /// and a character are not cast, and nor is a sensor or another free body.
    /// </summary>
    public bool Continuous { get; }

    /// <summary>
    /// Where its weight is, from the middle of its shape, in the entity's own units so it scales with
    /// the entity. It turns about this point, and a push through it does not spin it. Zero is the
    /// middle. Not <see cref="Centre"/>, which is where the shape sits on the entity.
    /// </summary>
    public Vector3 CentreOfMass { get; }

    /// <summary>
    /// How hard it is to turn about each of its own axes through the centre of mass, in kilogram
    /// square metres. All three above zero and it is used as given. Otherwise the shape's is used.
    /// </summary>
    public Vector3 Inertia { get; }

    /// <summary>
    /// The tallest step a <see cref="BodyMotion.Character"/> walks up without being asked to jump, in
    /// metres. Only a character reads this, and the four after it.
    /// </summary>
    public double StepHeight { get; }

    /// <summary>The steepest slope a character stands on, in radians from flat. On a steeper one it slides.</summary>
    public double Steepest { get; }

    /// <summary>
    /// How far a character keeps from everything it touches, in metres. What stops it snagging on the
    /// seam between two floor tiles.
    /// </summary>
    public double Skin { get; }

    /// <summary>The most force in newtons a character pushes a free body with.</summary>
    public double Strength { get; }

    /// <summary>
    /// How tall a character's capsule is while it crouches, tip to tip like <see cref="Height"/>.
    /// Zero is a character that does not crouch. It keeps its feet where they were, and stands again
    /// only where there is room.
    /// </summary>
    public double CrouchHeight { get; }

    public override string Type => SceneComponents.Body;

    public static BodyComponent FromJson(JsonMap json) => new(
        Values.Named<BodyShape>(Values.At(json, "shape")) ?? BodyShape.Box,
        Values.Vector(Values.At(json, "size"), 1),
        Values.Number(json, "radius", 0.5),
        Values.Number(json, "height", 2),
        Values.Numbers(Values.At(json, "hull")),
        BodyPart.ListFromJson(Values.At(json, "parts")),
        MeshPositions(Values.At(json, "meshVertices")),
        MeshIndicesOf(Values.At(json, "meshIndices")),
        Values.Vector(Values.At(json, "shapeScale"), 1),
        Values.Vector(Values.At(json, "centre")),
        Values.Named<BodyMotion>(Values.At(json, "motion")) ?? BodyMotion.Free,
        Values.Number(json, "mass", 1),
        Values.Number(json, "friction", 0.5),
        Values.Number(json, "restitution", 0),
        Values.Number(json, "linearDamping", 0.05),
        Values.Number(json, "angularDamping", 0.05),
        Values.Bits(json, "layers", 1),
        Values.Bits(json, "cares", EveryLayer),
        Values.Flag(json, "asleep", false),
        Values.Flag(json, "trigger", false),
        Values.Flag(json, "stay", false),
        Values.Vector(Values.At(json, "surface")),
        LocksOf(Values.At(json, "locks")),
        Values.Number(json, "gravityScale", 1),
        Values.Number(json, "maxSpeed", 0),
        Values.Number(json, "maxSpin", 0),
        Values.Flag(json, "continuous", false),
        Values.Vector(Values.At(json, "centreOfMass")),
        Values.Vector(Values.At(json, "inertia")),
        Values.Number(json, "stepHeight", 0.3),
        Values.Number(json, "steepest", 0.7853981633974483),
        Values.Number(json, "skin", 0.01),
        Values.Number(json, "strength", 500),
        Values.Number(json, "crouchHeight", 0));

    /// <summary>
    /// The same body with some of it changed.
    /// </summary>
    /// <remarks>
    /// A component is replaced rather than edited, so this is how one field of a body changes: an
    /// inspector's mass slider makes a new body a frame. The vectors are copied, so the two never
    /// share a size.
    /// </remarks>
    // F2: thirty-four fields of one body, called by name.
    [SuppressMessage("Maintainability", "CA1502", Justification = "F2: a list of fields is data.")]
    public BodyComponent CopyWith(
        BodyShape? shape = null,
        Vector3? size = null,
        double? radius = null,
        double? height = null,
        IReadOnlyList<double>? hull = null,
        IReadOnlyList<BodyPart>? parts = null,
        IReadOnlyList<double>? meshVertices = null,
        IReadOnlyList<long>? meshIndices = null,
        Vector3? shapeScale = null,
        Vector3? centre = null,
        BodyMotion? motion = null,
        double? mass = null,
        double? friction = null,
        double? restitution = null,
        double? linearDamping = null,
        double? angularDamping = null,
        long? layers = null,
        long? cares = null,
        bool? startsAsleep = null,
        bool? trigger = null,
        bool? stay = null,
        Vector3? surface = null,
        IReadOnlySet<BodyLock>? locks = null,
        double? gravityScale = null,
        double? maxSpeed = null,
        double? maxSpin = null,
        bool? continuous = null,
        Vector3? centreOfMass = null,
        Vector3? inertia = null,
        double? stepHeight = null,
        double? steepest = null,
        double? skin = null,
        double? strength = null,
        double? crouchHeight = null) => new(
        shape ?? Shape,
        (size ?? Size).Clone(),
        radius ?? Radius,
        height ?? Height,
        hull ?? Hull,
        parts ?? Parts,
        meshVertices ?? MeshVertices,
        meshIndices ?? MeshIndices,
        shapeScale ?? ShapeScale,
        (centre ?? Centre).Clone(),
        motion ?? Motion,
        mass ?? Mass,
        friction ?? Friction,
        restitution ?? Restitution,
        linearDamping ?? LinearDamping,
        angularDamping ?? AngularDamping,
        layers ?? Layers,
        cares ?? Cares,
        startsAsleep ?? StartsAsleep,
        trigger ?? Trigger,
        stay ?? Stay,
        (surface ?? Surface).Clone(),
        locks ?? Locks,
        gravityScale ?? GravityScale,
        maxSpeed ?? MaxSpeed,
        maxSpin ?? MaxSpin,
        continuous ?? Continuous,
        (centreOfMass ?? CentreOfMass).Clone(),
        (inertia ?? Inertia).Clone(),
        stepHeight ?? StepHeight,
        steepest ?? Steepest,
        skin ?? Skin,
        strength ?? Strength,
        crouchHeight ?? CrouchHeight);

    /// <summary>
    /// The same body made of <paramref name="material"/>: its friction and its bounce, and nothing
    /// else about it.
    /// </summary>
    public BodyComponent MadeOf(BodyMaterial material) =>
        CopyWith(friction: material.Friction, restitution: material.Restitution);

    public override JsonMap ToJson() => new OrderedDictionary<string, object?>
    {
        ["shape"] = EnumNames.Of(Shape),
        ["size"] = Values.VectorToJson(Size),
        ["radius"] = Radius,
        ["height"] = Height,
        ["hull"] = Values.NumbersToJson(Hull),
        ["parts"] = Parts.Select(static part => (object?)part.ToJson()).ToList(),
        ["meshVertices"] = Values.NumbersToJson(MeshVertices),
        ["meshIndices"] = MeshIndices.Select(static index => (object?)index).ToList(),
        ["shapeScale"] = Values.VectorToJson(ShapeScale),
        ["centre"] = Values.VectorToJson(Centre),
        ["motion"] = EnumNames.Of(Motion),
        ["mass"] = Mass,
        ["friction"] = Friction,
        ["restitution"] = Restitution,
        ["linearDamping"] = LinearDamping,
        ["angularDamping"] = AngularDamping,
        ["layers"] = Layers,
        ["cares"] = Cares,
        ["asleep"] = StartsAsleep,
        ["trigger"] = Trigger,
        ["stay"] = Stay,
        ["surface"] = Values.VectorToJson(Surface),

        // In the enum's order, so two bodies with the same locks write the same list whichever
        // order the locks were added in.
        ["locks"] = Enum.GetValues<BodyLock>().Where(Locks.Contains).Select(static lck => (object?)EnumNames.Of(lck)).ToList(),
        ["gravityScale"] = GravityScale,
        ["maxSpeed"] = MaxSpeed,
        ["maxSpin"] = MaxSpin,
        ["continuous"] = Continuous,
        ["centreOfMass"] = Values.VectorToJson(CentreOfMass),
        ["inertia"] = Values.VectorToJson(Inertia),
        ["stepHeight"] = StepHeight,
        ["steepest"] = Steepest,
        ["skin"] = Skin,
        ["strength"] = Strength,
        ["crouchHeight"] = CrouchHeight,
    };

    // Keep position indices stable: a malformed coordinate makes the mesh inert.
    private static List<double> MeshPositions(object? raw)
    {
        var read = new List<double>();
        if (raw is not JsonList list)
        {
            return read;
        }

        foreach (var value in list)
        {
            if (Values.Num(value) is not { } number || !double.IsFinite(number))
            {
                return [];
            }

            read.Add(number);
        }

        return read;
    }

    // A whole number only: an index written 1.0 is not one, and -1 names no vertex.
    private static List<long> MeshIndicesOf(object? raw) =>
        raw is JsonList list ? [.. list.Select(static index => index is long whole ? whole : -1)] : [];

    private static HashSet<BodyLock> LocksOf(object? raw)
    {
        var locks = new HashSet<BodyLock>();
        foreach (var name in Values.Texts(raw))
        {
            if (Values.Named<BodyLock>(name) is { } lck)
            {
                locks.Add(lck);
            }
        }

        return locks;
    }
}

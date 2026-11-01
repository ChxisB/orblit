namespace Orblit.Scene;

/// <summary>
/// A region that changes how the bodies inside it move.
/// </summary>
/// <remarks>
/// It goes on an entity that has a body component, and that body is the region: its shape, placed
/// where the entity is. Having a zone makes the body a trigger, so nothing collides with it and it
/// pushes nothing, whether or not the body's trigger flag is set. An entity with a zone and no body
/// does nothing, as a joint with nothing to hold does.
/// <para>
/// Each field that is set replaces what the body inside has, and one left out leaves the body's own.
/// Water is a weak gravity and a lot of damping. A lift shaft is a gravity that points up.
/// </para>
/// <para>
/// Where two zones overlap, each field is decided on its own by the higher <see cref="Priority"/>,
/// and by the entity that was made first at a tie. A zone acts on free bodies that are awake. A
/// character asks for its own gravity, and a fixed or driven body is not moved by any.
/// </para>
/// <para>
/// A field that is left out is left out of the file too. Writing a value for it would say the zone
/// changes something it does not.
/// </para>
/// </remarks>
public sealed class ZoneComponent(
    Vector3? gravity = null,
    double? linearDamping = null,
    double? angularDamping = null,
    long priority = 0) : SceneComponent
{
    /// <summary>
    /// Metres per second squared, as a vector: nought holds a body where it is, and up lifts it.
    /// Null leaves the world's gravity.
    /// </summary>
    public Vector3? Gravity { get; } = gravity?.Clone();

    /// <summary>
    /// How quickly a body slows down and stops spinning inside, as a fraction a second, in place of
    /// its own. Null leaves the body's.
    /// </summary>
    public double? LinearDamping { get; } = linearDamping;

    public double? AngularDamping { get; } = angularDamping;

    /// <summary>Which zone wins where two overlap. Higher wins, field by field.</summary>
    public long Priority { get; } = priority;

    public override string Type => SceneComponents.Zone;

    public static ZoneComponent FromJson(JsonMap json) => new(
        Values.At(json, "gravity") is JsonList gravity ? Values.Vector(gravity) : null,
        Values.MaybeNumber(json, "linearDamping"),
        Values.MaybeNumber(json, "angularDamping"),
        Doubles.Round(Values.Number(json, "priority", 0)));

    /// <summary>
    /// The same zone with some of it changed. A field cannot be cleared here, only set; build a new
    /// zone to leave one out.
    /// </summary>
    public ZoneComponent CopyWith(
        Vector3? gravity = null,
        double? linearDamping = null,
        double? angularDamping = null,
        long? priority = null) => new(
        gravity ?? Gravity,
        linearDamping ?? LinearDamping,
        angularDamping ?? AngularDamping,
        priority ?? Priority);

    public override JsonMap ToJson() => Values.Pruned(new OrderedDictionary<string, object?>
    {
        ["gravity"] = Gravity is null ? null : Values.VectorToJson(Gravity),
        ["linearDamping"] = LinearDamping,
        ["angularDamping"] = AngularDamping,
        ["priority"] = Priority,
    });
}

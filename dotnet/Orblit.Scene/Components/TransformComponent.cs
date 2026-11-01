namespace Orblit.Scene;

/// <summary>
/// Where an entity sits, relative to its parent.
/// </summary>
/// <remarks>
/// Rotation in degrees around X, Y and Z, because that is what the file is read by people and the
/// number somebody typed into the inspector should be the number they find in the file. The
/// conversion to a matrix belongs to whoever is drawing.
/// <para>
/// Its own component rather than three fields on the entity: a thing that is only a name and some
/// children, a folder in the outliner, has no position, and giving it one at the origin means every
/// reparent silently moves its contents.
/// </para>
/// </remarks>
public sealed class TransformComponent(Vector3? position = null, Vector3? rotation = null, Vector3? scale = null)
    : SceneComponent
{
    public Vector3 Position { get; } = position ?? Vector3.Zero();

    public Vector3 Rotation { get; } = rotation ?? Vector3.Zero();

    public Vector3 Scale { get; } = scale ?? Vector3.All(1);

    public override string Type => SceneComponents.Transform;

    public static TransformComponent FromJson(JsonMap json) => new(
        Values.Vector(Values.At(json, "position")),
        Values.Vector(Values.At(json, "rotation")),
        Values.Vector(Values.At(json, "scale"), 1));

    /// <summary>
    /// The rotation <paramref name="degrees"/> stands for, composed Z, then Y, then X. That order
    /// is the scene's, and every reader of a transform has to use it: Euler angles do not commute,
    /// so another order turns anything rotated about more than one axis by an amount that looks
    /// plausible and is wrong.
    /// </summary>
    public static Matrix3 RotationOf(Vector3 degrees)
    {
        var turn = Matrix3.RotationZ(Doubles.Radians(degrees.Z));
        turn.Multiply(Matrix3.RotationY(Doubles.Radians(degrees.Y)));
        turn.Multiply(Matrix3.RotationX(Doubles.Radians(degrees.X)));
        return turn;
    }

    /// <summary>
    /// The three angles that <see cref="RotationOf"/> turns back into <see cref="Rotation"/>. At the
    /// pole, where a quarter turn about Y leaves the other two indistinguishable, the roll is folded
    /// into the yaw, because at that point the rotation itself no longer says which of the two it was.
    /// </summary>
    public static Vector3 AnglesOf(Matrix3 rotation)
    {
        var sinY = -rotation.Entry(2, 0);
        if (Math.Abs(sinY) >= 0.9999999)
        {
            return new Vector3(
                0,
                double.IsNegative(sinY) ? -90 : 90,
                Doubles.Degrees(Math.Atan2(-rotation.Entry(0, 1), rotation.Entry(1, 1))));
        }

        return new Vector3(
            Doubles.Degrees(Math.Atan2(rotation.Entry(2, 1), rotation.Entry(2, 2))),
            Doubles.Degrees(Math.Asin(Doubles.Clamp(sinY, -1.0, 1.0))),
            Doubles.Degrees(Math.Atan2(rotation.Entry(1, 0), rotation.Entry(0, 0))));
    }

    public override JsonMap ToJson() => new OrderedDictionary<string, object?>
    {
        ["position"] = Values.VectorToJson(Position),
        ["rotation"] = Values.VectorToJson(Rotation),
        ["scale"] = Values.VectorToJson(Scale),
    };
}

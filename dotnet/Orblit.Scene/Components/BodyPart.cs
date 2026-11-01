using System.Collections.ObjectModel;

namespace Orblit.Scene;

/// <summary>
/// A convex part of a body. Scale, turn and centre are local to the compound. Dynamics and material
/// belong to the body that holds the part.
/// </summary>
public sealed class BodyPart
{
    // F2: eight fields of one part, called by name.
    public BodyPart(
        BodyShape shape = BodyShape.Box,
        Vector3? size = null,
        double radius = 0.5,
        double height = 2,
        IReadOnlyList<double>? hull = null,
        Vector3? centre = null,
        Quaternion? rotation = null,
        Vector3? scale = null)
    {
        Shape = shape;
        Size = (size ?? Vector3.All(1)).Clone();
        Radius = radius;
        Height = height;
        Hull = new ReadOnlyCollection<double>([.. hull ?? []]);
        Centre = (centre ?? Vector3.Zero()).Clone();
        Rotation = (rotation ?? Quaternion.Identity()).Clone();
        Scale = (scale ?? Vector3.All(1)).Clone();
    }

    public BodyShape Shape { get; }

    public Vector3 Size { get; }

    public double Radius { get; }

    public double Height { get; }

    public IReadOnlyList<double> Hull { get; }

    public Vector3 Centre { get; }

    public Quaternion Rotation { get; }

    public Vector3 Scale { get; }

    public static BodyPart FromJson(JsonMap json)
    {
        var turn = Values.Numbers(Values.At(json, "rotation"));
        return new BodyPart(
            Values.Named<BodyShape>(Values.At(json, "shape")) ?? BodyShape.Box,
            Values.Vector(Values.At(json, "size"), 1),
            Values.Number(json, "radius", 0.5),
            Values.Number(json, "height", 2),
            Values.Numbers(Values.At(json, "hull")),
            Values.Vector(Values.At(json, "centre")),
            turn.Count == 4 ? new Quaternion(turn[0], turn[1], turn[2], turn[3]) : Quaternion.Identity(),
            Values.Vector(Values.At(json, "scale"), 1));
    }

    public static List<BodyPart> ListFromJson(object? raw) =>
        raw is JsonList list ? [.. list.OfType<JsonMap>().Select(FromJson)] : [];

    // F2: eight fields of one part, called by name.
    public BodyPart CopyWith(
        BodyShape? shape = null,
        Vector3? size = null,
        double? radius = null,
        double? height = null,
        IReadOnlyList<double>? hull = null,
        Vector3? centre = null,
        Quaternion? rotation = null,
        Vector3? scale = null) => new(
        shape ?? Shape,
        size ?? Size,
        radius ?? Radius,
        height ?? Height,
        hull ?? Hull,
        centre ?? Centre,
        rotation ?? Rotation,
        scale ?? Scale);

    public JsonMap ToJson() => new OrderedDictionary<string, object?>
    {
        ["shape"] = EnumNames.Of(Shape),
        ["size"] = Values.VectorToJson(Size),
        ["radius"] = Radius,
        ["height"] = Height,
        ["hull"] = Values.NumbersToJson(Hull),
        ["centre"] = Values.VectorToJson(Centre),
        ["rotation"] = Values.NumbersToJson([Rotation.X, Rotation.Y, Rotation.Z, Rotation.W]),
        ["scale"] = Values.VectorToJson(Scale),
    };
}

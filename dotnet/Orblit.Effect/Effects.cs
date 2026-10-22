using Orblit.VectorMath;

namespace Orblit.Effect;

/// <summary>The functions that sit beside the effects: <c>applied</c>, <c>lerp</c> and <c>shortestTurn</c>.</summary>
public static class Effects
{
    /// <summary>
    /// A change applied to a transform. Here rather than in the renderer
    /// because it is the same arithmetic wherever it happens, and a host that
    /// wrote it itself would be a host that composed the rotation in a
    /// different order.
    /// </summary>
    public static Matrix4 Applied(Matrix4 to, Change change)
    {
        var translation = to.GetTranslation();
        var rotation = Quaternion.FromRotation(to.GetRotation());
        var scale = new Vector3(to.GetColumn(0).Length, to.GetColumn(1).Length, to.GetColumn(2).Length);

        return Matrix4.Compose(
            translation + change.Move,
            rotation * change.Turn,
            new Vector3(scale.X * change.Grow.X, scale.Y * change.Grow.Y, scale.Z * change.Grow.Z));
    }

    /// <summary>A number eased between two values, the smallest useful effect there is.</summary>
    public static double Lerp(double from, double to, double t) => from + ((to - from) * t);

    /// <summary>The angle between two directions, the short way round.</summary>
    public static double ShortestTurn(double from, double to)
    {
        var difference = Doubles.Mod(to - from, 2 * Math.PI);
        if (difference > Math.PI)
        {
            difference -= 2 * Math.PI;
        }

        if (difference < -Math.PI)
        {
            difference += 2 * Math.PI;
        }

        return difference;
    }
}

using Orblit.VectorMath;

namespace Orblit.Collide;

internal static class Facing
{
    /// <summary>
    /// Which way along an axis an offset points. An offset of nothing points up
    /// the axis, so a normal is never zero. NaN stays NaN, as in Dart.
    /// </summary>
    internal static double Of(double offset)
    {
        var sign = Doubles.Sign(offset);
        return sign == 0 ? 1 : sign;
    }
}

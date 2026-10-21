using Orblit.VectorMath;

namespace Orblit.Pathing.Tests;

internal static class PathSupport
{
    public static double LengthOf(IReadOnlyList<Vector3> path)
    {
        var total = 0.0;
        for (var i = 1; i < path.Count; i++)
        {
            total += path[i].DistanceTo(path[i - 1]);
        }

        return total;
    }
}

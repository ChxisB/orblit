using Orblit.VectorMath;

namespace Orblit.Stage.Tests;

// Tests compare doubles from different routes to the same place, so exact equality is the wrong check.
internal static class Near
{
    public const double Tolerance = 1e-5;

    public static void Vector(Vector3 actual, Vector3 expected, string? reason = null)
    {
        var because = reason is null ? "" : $" ({reason})";
        Assert.True(Math.Abs(actual.X - expected.X) <= Tolerance, $"x of {actual}{because}");
        Assert.True(Math.Abs(actual.Y - expected.Y) <= Tolerance, $"y of {actual}{because}");
        Assert.True(Math.Abs(actual.Z - expected.Z) <= Tolerance, $"z of {actual}{because}");
    }

    public static void Matrix(Matrix4 actual, Matrix4 expected, string? reason = null)
    {
        var because = reason is null ? "" : $" of {reason}";
        for (var at = 0; at < 16; at++)
        {
            Assert.True(Math.Abs(actual[at] - expected[at]) <= Tolerance, $"element {at}{because}");
        }
    }

    public static double Largest(Matrix4 difference) =>
        Enumerable.Range(0, 16).Max(at => Math.Abs(difference[at]));
}

using Orblit.VectorMath;

namespace Orblit.Rig;

/// <summary>
/// Lays a chain of bones along a curve, such as a tail along a path or a spine along an arc. The curve
/// passes through every control point, as a Catmull-Rom spline. The first bone's head is put on the
/// start of the curve and each bone after it reaches along the curve for a distance of its own length,
/// so the chain keeps its length and does not stretch. A chain longer than the curve carries on
/// straight from where the curve ended.
/// </summary>
/// <param name="root">The first bone of the chain.</param>
/// <param name="tip">The last bone.</param>
/// <param name="points">
/// The control points in the model's space, at least two. Null leaves the chain as it is.
/// </param>
/// <param name="detail">Straight pieces per span of the curve.</param>
public sealed class SplineIk(
    string root,
    string tip,
    Func<Pose, ModifierFrame, IReadOnlyList<Vector3>?> points,
    int detail = 12) : Modifier
{
    public string Root { get; } = root;

    public string Tip { get; } = tip;

    public Func<Pose, ModifierFrame, IReadOnlyList<Vector3>?> Points { get; } = points;

    public int Detail { get; } = detail;

    public override void Check(Armature armature)
    {
        BoneEdit.RequireBone(armature, Root, "so the chain has no start");
        BoneEdit.RequireBone(armature, Tip, "so the chain has no end");
        if (BoneEdit.BonePath(armature, Root, Tip) is null)
        {
            throw new ArmatureException($"Bone \"{Tip}\" is not below \"{Root}\", so they are not one chain.");
        }
    }

    public override void Solve(Pose pose, ModifierFrame frame)
    {
        var controls = Points(pose, frame);
        if (controls is null || controls.Count < 2)
        {
            return;
        }

        var curve = SampleCurve(controls, Detail);

        // Check has already shown there is a path.
        var path = BoneEdit.BonePath(pose.Armature, Root, Tip)!;

        var start = pose.WorldOf(Root).Clone();
        start.SetTranslation(curve[0]);
        BoneEdit.PlaceBone(pose, Root, start);

        var lengths = new List<double>();
        for (var i = 0; i < path.Count; i++)
        {
            var end = i + 1 < path.Count ? pose.HeadOf(path[i + 1]) : pose.TailOf(path[i]);
            lengths.Add(end.DistanceTo(pose.HeadOf(path[i])));
        }

        ChainIk.PointAlong(pose, path, WalkCurve(curve, lengths));
    }

    /// <summary>A Catmull-Rom curve through <paramref name="controls"/> as a polyline, <paramref name="detail"/> pieces to a span.</summary>
    public static List<Vector3> SampleCurve(IReadOnlyList<Vector3> controls, int detail)
    {
        var steps = detail < 1 ? 1 : detail;
        var result = new List<Vector3> { controls[0].Clone() };
        for (var i = 0; i + 1 < controls.Count; i++)
        {
            var before = controls[i == 0 ? 0 : i - 1];
            var after = controls[i + 2 < controls.Count ? i + 2 : i + 1];
            for (var step = 1; step <= steps; step++)
            {
                var t = (double)step / steps;
                result.Add(CatmullRom(before, controls[i], controls[i + 1], after, t));
            }
        }

        return result;
    }

    private static Vector3 CatmullRom(Vector3 a, Vector3 b, Vector3 c, Vector3 d, double t)
    {
        var t2 = t * t;
        var t3 = t2 * t;
        return ((b * 2.0)
            + ((c - a) * t)
            + (((a * 2.0) - (b * 5.0) + (c * 4.0) - d) * t2)
            + (((b * 3.0) - a - (c * 3.0) + d) * t3)) * 0.5;
    }

    /// <summary>
    /// The points reached by stepping along <paramref name="curve"/> with a stride of each of
    /// <paramref name="lengths"/> in turn, a straight line away from the point before. Starts at the
    /// curve's first point and so returns one more point than there are lengths. Where the curve ends
    /// the walk goes on in the direction of its last piece.
    /// </summary>
    public static List<Vector3> WalkCurve(IReadOnlyList<Vector3> curve, IReadOnlyList<double> lengths)
    {
        var result = new List<Vector3> { curve[0].Clone() };
        var piece = 0;
        foreach (var length in lengths)
        {
            var from = result[^1];
            var found = false;
            while (piece + 1 < curve.Count)
            {
                var hit = LeaveSphere(from, length, curve[piece], curve[piece + 1]);
                if (hit is not null)
                {
                    result.Add(hit);
                    found = true;
                    break;
                }

                piece++;
            }

            if (!found)
            {
                result.Add(from + (LastDirection(curve) * length));
            }
        }

        return result;
    }

    /// <summary>
    /// Where the segment from <paramref name="a"/> to <paramref name="b"/> leaves the sphere of
    /// <paramref name="radius"/> about <paramref name="centre"/>, or null when it does not leave it. The
    /// far crossing is the one wanted: the near one is behind the walker.
    /// </summary>
    private static Vector3? LeaveSphere(Vector3 centre, double radius, Vector3 a, Vector3 b)
    {
        var along = b - a;
        var offset = a - centre;
        var qa = along.Dot(along);
        var qb = 2 * offset.Dot(along);
        var qc = offset.Dot(offset) - (radius * radius);
        var discriminant = (qb * qb) - (4 * qa * qc);
        if (qa < 1e-18 || discriminant < 0)
        {
            return null;
        }

        var t = (-qb + Math.Sqrt(discriminant)) / (2 * qa);
        if (t < 0 || t > 1)
        {
            return null;
        }

        return a + (along * t);
    }

    private static Vector3 LastDirection(IReadOnlyList<Vector3> curve)
    {
        for (var i = curve.Count - 1; i > 0; i--)
        {
            var line = curve[i] - curve[i - 1];
            if (line.Length2 > 1e-18)
            {
                return line.Normalized();
            }
        }

        return new Vector3(0, 1, 0);
    }
}

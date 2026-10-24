using Orblit.Filament;
using Orblit.Particles;
using Orblit.VectorMath;

namespace Orblit.Stage;

/// <summary>A trail drawn as a ribbon that turns its face to the eye.</summary>
/// <remarks>
/// The renderer takes no new vertices each frame, so this is a sheet two particles wide: one for
/// each edge of the ribbon, one row for each point of the trail. The sheet's joints are set to where
/// the edges are, as for cloth. Row <c>i</c> is point <c>i</c> of the <see cref="TrailStrip"/>, the
/// newest first.
/// </remarks>
public sealed class TrailRibbon
{
    private readonly ParticleSheet _sheet;

    public TrailRibbon(int points, IReadOnlyList<double>? colour = null)
    {
        _sheet = new ParticleSheet(2, points, 1, colour ?? [1, 1, 1]);
        Points = points;
    }

    public int Points { get; }

    /// <summary>The ribbon as a binary glb, to hand to the renderer's resources. Both faces are drawn.</summary>
    public byte[] ToGlb() => _sheet.ToGlb();

    /// <summary>
    /// Every joint set for <paramref name="strip"/>, for a model's joints. <paramref name="eye"/> is where
    /// the camera is, in the same space as the strip.
    /// </summary>
    /// <remarks>
    /// The poses are relative to <c>strip.Head</c>, so draw the object with a move to the head. The
    /// renderer culls from the sheet at rest, a box 1 m wide that hangs <c>Points - 1</c> m below the
    /// head, and a trail outside it can be culled while in view.
    ///
    /// Rows past the end of the strip are folded onto its last point, so they draw nothing. Where a
    /// point has no direction, no width, or lies in line with the eye, its two edges meet and the
    /// ribbon pinches there.
    /// </remarks>
    public IReadOnlyList<JointPose> Poses(TrailStrip strip, Vector3 eye)
    {
        var count = Math.Min(strip.Count, Points);
        var head = strip.Head;
        var centres = Enumerable.Range(0, count)
            .Select(i => new Vector3(strip.Positions[i * 3], strip.Positions[(i * 3) + 1], strip.Positions[(i * 3) + 2]) - head)
            .ToList();
        var seen = eye - head;
        var tail = centres.Count == 0 ? Vector3.Zero() : centres[^1];
        var edges = new List<Vector3>();
        for (var i = 0; i < Points; i++)
        {
            edges.AddRange(i < count ? EdgesOf(centres, i, strip.Widths[i], seen) : [tail, tail]);
        }

        return _sheet.Poses(particle => edges[particle]);
    }

    private static Vector3[] EdgesOf(List<Vector3> centres, int i, double width, Vector3 eye)
    {
        var here = centres[i];
        var tangent = centres[Math.Min(i + 1, centres.Count - 1)] - centres[Math.Max(i - 1, 0)];
        var towards = eye - here;
        var side = tangent.Cross(towards);

        // Short of a direction, a band has no side to lie on, so it pinches.
        if (tangent.Length2 < 1e-18
            || towards.Length2 < 1e-18
            || side.Length2 < 1e-12 * tangent.Length2 * towards.Length2)
        {
            return [here, here];
        }

        side.Scale(0.5 * width / side.Length);
        return [here - side, here + side];
    }
}

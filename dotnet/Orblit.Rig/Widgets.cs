using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Orblit.VectorMath;

namespace Orblit.Rig;

/// <summary>
/// The outline drawn in place of a control bone. A generated rig has more bones than a viewport can
/// show as bones, and most of them are machinery. Shapes are how an animator tells the controls
/// apart at a glance: a ring is something that turns, a box is something that moves, and neither
/// looks like the stick that a bone draws as.
/// </summary>
public enum WidgetShape
{
    /// <summary>A ring lying flat across the bone. The default for anything rotated.</summary>
    Circle,

    /// <summary>Three rings at right angles. A control free to turn any way.</summary>
    Sphere,

    /// <summary>A box. Something dragged about: a hand or foot goal.</summary>
    Cube,

    /// <summary>A flat square. A plane of movement, and the usual pole vector.</summary>
    Square,

    /// <summary>The stick a bone draws as, in outline. For originals kept visible.</summary>
    Bone,

    /// <summary>A flat chevron pointing along the bone. Direction, and root controls.</summary>
    Arrow,
}

/// <summary>
/// The shape drawn for a control, and how it sits. Kept beside the rig rather than on the bone: a
/// shape is how a rig is presented, and a renderer that never draws controls should not have to
/// carry them through skinning.
/// </summary>
public sealed class BoneWidget(WidgetShape shape = WidgetShape.Circle, double size = 1, Vector3? offset = null, double roll = 0)
{
    private readonly Vector3? _offset = offset;

    public WidgetShape Shape { get; } = shape;

    /// <summary>
    /// A multiplier on the bone's own length, so a widget stays in proportion when a character is
    /// rebuilt at a different scale.
    /// </summary>
    public double Size { get; } = size;

    /// <summary>Where the shape sits along the bone, in bone lengths. Zero is the head.</summary>
    public Vector3 Offset => _offset?.Clone() ?? Vector3.Zero();

    /// <summary>Twist about the bone's own length, in radians.</summary>
    public double Roll { get; } = roll;

    /// <summary>
    /// The shape as polylines in bone space, scaled for a bone of <paramref name="boneLength"/>.
    /// Polylines rather than triangles because a control is drawn as an outline over the character.
    /// A solid one hides the thing being animated, which is the only reason to be looking.
    /// </summary>
    public List<List<Vector3>> Outline(double boneLength)
    {
        var scale = boneLength * Size;
        // As a matrix, because a quaternion's own rotate would turn it the other way to a bone with
        // the same roll.
        var twist = Roll == 0 ? null : Quaternion.AxisAngle(new Vector3(0, 1, 0), Roll).AsRotationMatrix();
        var shift = Offset * boneLength;

        return ShapeOutline(Shape)
            .Select(line => line.Select(point => ((twist?.Transformed(point) ?? point) * scale) + shift).ToList())
            .ToList();
    }

    public BoneWidget CopyWith(WidgetShape? shape = null, double? size = null, double? roll = null) =>
        new(shape ?? Shape, size ?? Size, _offset?.Clone(), roll ?? Roll);

    // Unit outlines, before a bone's length is applied. All of them span roughly -0.5 to 0.5 across,
    // and sit at the head rather than straddling it, except the bone, which runs the bone's length
    // because that is the thing it is drawing.
    [SuppressMessage("Maintainability", "CA1502:Avoid excessive complexity", Justification = "F2: a list of cases is data.")]
    private static List<List<Vector3>> ShapeOutline(WidgetShape shape) => shape switch
    {
        WidgetShape.Circle => [Ring(Plane.XZ, 0.5, 24)],
        WidgetShape.Sphere =>
        [
            Ring(Plane.XZ, 0.5, 24),
            Ring(Plane.XY, 0.5, 24),
            Ring(Plane.YZ, 0.5, 24),
        ],
        WidgetShape.Cube => Box(0.5),
        WidgetShape.Square =>
        [
            [
                new Vector3(-0.5, 0, -0.5),
                new Vector3(0.5, 0, -0.5),
                new Vector3(0.5, 0, 0.5),
                new Vector3(-0.5, 0, 0.5),
                new Vector3(-0.5, 0, -0.5),
            ],
        ],
        // The octahedron a bone draws as: a narrow waist near the head, tapering to a point at the
        // tail, which is what makes a bone's direction readable.
        WidgetShape.Bone => Octahedron(),
        WidgetShape.Arrow =>
        [
            [
                new Vector3(-0.35, 0, -0.2),
                new Vector3(0, 0, 0.45),
                new Vector3(0.35, 0, -0.2),
                new Vector3(0, 0, -0.05),
                new Vector3(-0.35, 0, -0.2),
            ],
        ],
        _ => throw new UnreachableException(),
    };

    private enum Plane
    {
        XY,
        XZ,
        YZ,
    }

    private static List<Vector3> Ring(Plane plane, double radius, int segments)
    {
        var ring = new List<Vector3>();
        for (var i = 0; i <= segments; i++)
        {
            var angle = (double)i / segments * Math.PI * 2;
            var a = Math.Cos(angle) * radius;
            var b = Math.Sin(angle) * radius;
            ring.Add(plane switch
            {
                Plane.XY => new Vector3(a, b, 0),
                Plane.XZ => new Vector3(a, 0, b),
                Plane.YZ => new Vector3(0, a, b),
                _ => throw new UnreachableException(),
            });
        }

        return ring;
    }

    private static List<List<Vector3>> Box(double half)
    {
        Vector3 Corner(int x, int y, int z) => new(x * half, y * half, z * half);

        return
        [
            // The two faces, then the four struts between them: six polylines rather than twelve
            // separate edges, which halves the vertices a viewport sends.
            [Corner(-1, -1, -1), Corner(1, -1, -1), Corner(1, -1, 1), Corner(-1, -1, 1), Corner(-1, -1, -1)],
            [Corner(-1, 1, -1), Corner(1, 1, -1), Corner(1, 1, 1), Corner(-1, 1, 1), Corner(-1, 1, -1)],
            [Corner(-1, -1, -1), Corner(-1, 1, -1)],
            [Corner(1, -1, -1), Corner(1, 1, -1)],
            [Corner(1, -1, 1), Corner(1, 1, 1)],
            [Corner(-1, -1, 1), Corner(-1, 1, 1)],
        ];
    }

    private static List<List<Vector3>> Octahedron()
    {
        const double waist = 0.1;
        List<Vector3> ring =
        [
            new(-waist, waist, 0),
            new(0, waist, waist),
            new(waist, waist, 0),
            new(0, waist, -waist),
        ];

        List<List<Vector3>> lines = [[.. ring, ring[0]]];
        lines.AddRange(ring.Select(point => new List<Vector3> { Vector3.Zero(), point }));
        lines.AddRange(ring.Select(point => new List<Vector3> { point, new Vector3(0, 1, 0) }));
        return lines;
    }
}

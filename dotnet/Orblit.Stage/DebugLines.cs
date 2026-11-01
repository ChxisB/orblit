using Orblit.Filament;
using Orblit.VectorMath;

namespace Orblit.Stage;

/// <summary>
/// Draws segments over a scene while <see cref="Enabled"/>, for finding out what a simulation is
/// doing: the contact points and normals of a physics world, a ray, a path.
/// The renderer has no line primitive, so each segment is the built-in cube stretched along it. It
/// is lit like anything else, so a segment in shadow is dark. A segment shorter than the width is
/// drawn as a square of that size, which is how a point shows.
/// Stateless: <see cref="Draw"/> answers with a scene and keeps nothing, so call it where the scene
/// is made each frame. The stage depends on no simulation. The caller turns its own data into
/// <see cref="DebugLine"/>s.
/// </summary>
public sealed class DebugLines
{
    private readonly double _width;

    public DebugLines(bool enabled = false, double width = 0.02, long keyBase = 0x44420000)
    {
        if (!(width > 0 && double.IsFinite(width)))
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "must be above zero");
        }

        Enabled = enabled;
        _width = width;
        KeyBase = keyBase;
    }

    /// <summary>Whether <see cref="Draw"/> adds anything. Off, it costs a branch.</summary>
    public bool Enabled { get; set; }

    /// <summary>The key of the first segment, clear of the scene's own keys.</summary>
    public long KeyBase { get; }

    /// <summary>
    /// <paramref name="scene"/> with a segment for each of the lines, or <paramref name="scene"/>
    /// itself while disabled. <paramref name="lines"/> is not called then, so a world that lists its
    /// contacts by copying them pays nothing for a switch that is off.
    /// </summary>
    public RenderScene Draw(RenderScene scene, Func<IEnumerable<DebugLine>> lines)
    {
        if (!Enabled)
        {
            return scene;
        }

        var marks = new List<RenderObject>();
        foreach (var line in lines())
        {
            var mark = MarkOf(KeyBase + marks.Count, line);
            if (mark is not null)
            {
                marks.Add(mark);
            }
        }

        return marks.Count == 0 ? scene : scene with { Objects = [.. scene.Objects, .. marks] };
    }

    // The segment as a cube, or null when an end is not a finite point.
    private RenderObject? MarkOf(long key, DebugLine line)
    {
        var along = line.To - line.From;
        var length = along.Length;

        // An end that is not a number makes a matrix the renderer cannot place.
        if (!double.IsFinite(length))
        {
            return null;
        }

        var heading = length > 1e-9 ? along / length : new Vector3(0, 1, 0);
        var half = new Vector3(_width, length < _width ? _width : length, _width) / 2;
        return new RenderObject
        {
            Key = key,
            Transform = Matrix4.Compose((line.From + line.To) / 2, Quaternion.FromTwoVectors(new Vector3(0, 1, 0), heading), half),
            Colour = line.Colour,
            CastShadows = false,
            ReceiveShadows = false,
        };
    }
}

using Orblit.VectorMath;

namespace Orblit.Filament;

/// <summary>One mesh of a level of detail, and the distance up to which it is the one drawn.</summary>
/// <param name="Mesh">The mesh to draw, or null to draw nothing while this step holds.</param>
/// <param name="Until">The distance in metres up to which this step holds. Infinity is allowed on the last.</param>
public sealed record LevelStep(string? Mesh, double Until);

/// <summary>
/// Which mesh of an object to draw at what distance: the full one close up and cheaper ones as it
/// recedes. The last step's limit is the range, past which nothing is drawn.
/// </summary>
/// <param name="Steps">The steps, nearest first.</param>
/// <param name="Hysteresis">
/// How far past a boundary the camera must go, as a fraction of it, before the step it leaves is
/// given up. Without it an object sitting on a boundary flickers between two meshes.
/// </param>
public sealed record LevelOfDetail(IReadOnlyList<LevelStep> Steps, double Hysteresis = 0.1)
{
    /// <summary>Whether the limits only grow, which is what lets <see cref="StepAt"/> walk them in order.</summary>
    public bool IsOrdered
    {
        get
        {
            for (var i = 1; i < Steps.Count; i++)
            {
                if (Steps[i].Until < Steps[i - 1].Until)
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>The distance past which nothing is drawn. Nought when there are no steps.</summary>
    public double Range => Steps.Count == 0 ? 0 : Steps[^1].Until;

    /// <summary>
    /// The step at <paramref name="distance"/>. <paramref name="was"/> is the step shown last time,
    /// whose outer boundary moves out by the hysteresis so the camera has to mean it.
    /// </summary>
    public int StepAt(double distance, int? was = null)
    {
        for (var i = 0; i < Steps.Count; i++)
        {
            var boundary = Steps[i].Until;
            if (was == i && double.IsFinite(boundary))
            {
                boundary *= 1 + Hysteresis;
            }

            if (distance <= boundary)
            {
                return i;
            }
        }

        return Math.Max(Steps.Count - 1, 0);
    }

    /// <summary>The mesh at <paramref name="distance"/>, or null past the range.</summary>
    public string? MeshAt(double distance, int? was = null) =>
        Steps.Count == 0 || distance > Range ? null : Steps[StepAt(distance, was)].Mesh;

    public bool IsVisibleAt(double distance) => Steps.Count > 0 && distance <= Range;
}

/// <summary>
/// The step each object was last shown at, which is what <see cref="LevelOfDetail.Hysteresis"/>
/// needs to know and nothing else in a scene keeps.
/// </summary>
public sealed class LevelOfDetailState
{
    private readonly Dictionary<long, int> _showing = [];

    public int Length => _showing.Count;

    /// <summary>
    /// The step for <paramref name="key"/> at <paramref name="distance"/>, remembered as the one
    /// shown. F5: the memory is the point, so asking and remembering are one step.
    /// </summary>
    public int StepFor(long key, LevelOfDetail lod, double distance)
    {
        var step = lod.StepAt(distance, _showing.TryGetValue(key, out var was) ? was : null);
        _showing[key] = step;
        return step;
    }

    /// <summary>
    /// The mesh for an object at <paramref name="at"/> seen from <paramref name="camera"/>. An object
    /// out of range is forgotten and not remembered as the last step, so one that was away for a
    /// minute picks its step from where it is and not from where it was.
    /// </summary>
    public string? MeshFor(long key, LevelOfDetail lod, Vector3 at, Vector3 camera)
    {
        var distance = (at - camera).Length;
        if (!lod.IsVisibleAt(distance))
        {
            _showing.Remove(key);
            return null;
        }

        return lod.Steps[StepFor(key, lod, distance)].Mesh;
    }

    public void Clear() => _showing.Clear();

    public void Forget(long key) => _showing.Remove(key);
}

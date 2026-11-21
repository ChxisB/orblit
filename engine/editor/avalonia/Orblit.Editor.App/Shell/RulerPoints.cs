using System.Globalization;
using System.Numerics;

namespace Orblit.Editor.App.Shell;

/// <summary>The world points the 3D ruler measures between. A third point starts a new measure.</summary>
internal sealed class RulerPoints
{
    readonly List<Vector3> _points = [];

    /// <summary>The distance between the two points, or null until both are down.</summary>
    public float? Length => _points is [var from, var to] ? Vector3.Distance(from, to) : null;

    public void Add(Vector3 point)
    {
        if (_points.Count == 2)
        {
            _points.Clear();
        }

        _points.Add(point);
    }

    public void Clear() => _points.Clear();

    /// <summary>The ruler in the frame's pixels, or null when no point is down or the first is behind the camera.</summary>
    public RulerShown? Shown(Func<Vector3, Vector2?> toFrame) =>
        _points is [var from, ..] && toFrame(from) is { } start
            ? new RulerShown(start, _points is [_, var to] ? toFrame(to) : null, Length is { } length ? Label(length) : "")
            : null;

    public static string Label(float length) => length.ToString("0.00 m", CultureInfo.InvariantCulture);
}

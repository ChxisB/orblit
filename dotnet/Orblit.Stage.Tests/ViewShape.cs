using Orblit.Filament;
using Orblit.VectorMath;

namespace Orblit.Stage.Tests;

// Everything about a staged scene that a frame depends on, as text. Two views are the same when
// their shapes are, and a difference reads as the line that moved.
internal static class ViewShape
{
    public static string Of(DocumentView view)
    {
        var scene = view.Scene;
        var lines = new List<string>();
        lines.AddRange(scene.Objects.Select(one =>
            $"object {Cells(one.Transform)} {one.Mesh} {one.Visible} {Point(one.Colour)} {one.CastShadows}"));
        lines.AddRange(scene.Lights.Select(one =>
            $"light {one.Kind} {one.Intensity} {Point(one.Position)} {Point(one.Direction)} {Point(one.Colour)}"));
        lines.AddRange(scene.Sprites.Select(one => $"sprites {Cells(one.Transform)} {one.Blend}"));
        lines.AddRange(scene.Splats.Select(one => $"splats {one.Path} {one.Limit}"));
        lines.Add($"sky {Point(scene.Sky.Colour)} {scene.Sky.Ambient}");
        lines.Add($"fog {scene.Fog.Density} {Point(scene.Fog.Colour)} {scene.Fog.Height}");
        lines.Add($"rain {scene.Precipitation.Amount} {scene.Precipitation.Wind.X},{scene.Precipitation.Wind.Y}");
        lines.Add($"camera {Point(scene.Camera.Position)} {Point(scene.Camera.Target)} {scene.Camera.FieldOfView}");
        return string.Join('\n', lines);
    }

    public static string Positions(DocumentView view) =>
        string.Join('\n', view.Scene.Objects.Select(one => Cells(one.Transform)).Order());

    private static string Cells(Matrix4 matrix) => string.Join(',', Enumerable.Range(0, 16).Select(at => matrix[at]));

    private static string Point(Vector3 point) => $"{point.X},{point.Y},{point.Z}";
}

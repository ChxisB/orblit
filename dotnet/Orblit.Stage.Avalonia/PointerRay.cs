using Avalonia;
using Orblit.Filament;
using Orblit.VectorMath;

namespace Orblit.Stage.Avalonia;

/// <summary>
/// A line from the eye through a point on the screen. What a click on a view means in the world: the
/// first thing along this line is what was clicked. It holds no scene, so it does not know what is on it.
/// </summary>
/// <param name="Origin">Where the line starts.</param>
/// <param name="Direction">Which way it runs. It need not be unit length.</param>
public sealed record PointerRay(Vector3 Origin, Vector3 Direction)
{
    /// <summary>
    /// The ray through <paramref name="point"/>, in pixels from the view's top left, as
    /// <paramref name="camera"/> sees a view of <paramref name="view"/> pixels. A perspective ray starts at
    /// the camera and fans out. An orthographic ray starts at a place on the lens and every one runs the
    /// same way.
    /// </summary>
    public static PointerRay FromCamera(RenderCamera camera, Point point, Size view)
    {
        var forward = (camera.Target - camera.Position).Normalized();

        // shortcut: up is world Y because the camera has no roll, so a camera looking straight up or down
        // has no right-hand side. Upgrade when the camera gains an up vector.
        var right = forward.Cross(new Vector3(0, 1, 0)).Normalized();
        var up = right.Cross(forward);
        var aspect = view.Width / view.Height;
        var across = (2 * point.X / view.Width) - 1;
        var over = 1 - (2 * point.Y / view.Height);

        if (camera.Orthographic)
        {
            var half = camera.ViewHeight / 2;
            return new PointerRay(camera.Position + (right * (across * half * aspect)) + (up * (over * half)), forward);
        }

        // The renderer measures the angle of view across the shorter side.
        var reach = Math.Tan(camera.FieldOfView * Math.PI / 360);
        var wide = aspect < 1 ? reach : reach * aspect;
        var tall = aspect < 1 ? reach / aspect : reach;
        return new PointerRay(camera.Position.Clone(), (forward + (right * (across * wide)) + (up * (over * tall))).Normalized());
    }
}

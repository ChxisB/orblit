using Orblit.VectorMath;

namespace Orblit.Camera;

/// <summary>
/// A rectangle over the frame, in fractions of it. Measured from the top left corner because
/// that is where every drawing surface starts, and in fractions because a rule written in
/// pixels stops meaning what it meant the moment the window is resized.
/// </summary>
public sealed record ScreenRect(double Left, double Top, double Width, double Height)
{
    public double Right => Left + Width;

    public double Bottom => Top + Height;

    /// <summary>Whether a point is inside, edges included.</summary>
    public bool Contains(double x, double y) => x >= Left && x <= Right && y >= Top && y <= Bottom;

    public override string ToString() =>
        $"ScreenRect({Doubles.ToStringAsFixed(Left, 3)}, {Doubles.ToStringAsFixed(Top, 3)}, "
        + $"{Doubles.ToStringAsFixed(Width, 3)} x {Doubles.ToStringAsFixed(Height, 3)})";
}

/// <summary>
/// What a camera's framing looks like, for something to draw. Handed out as plain fractions
/// because this package has no idea what it is being drawn with.
/// </summary>
/// <param name="ScreenX">Where the subject is meant to sit, in fractions of the frame.</param>
/// <param name="ScreenY">Where the subject is meant to sit, in fractions of the frame.</param>
/// <param name="Dead">Where the camera does not move. A subject drifting inside is left alone.</param>
/// <param name="Soft">
/// Where the camera eases after the subject. Past its edge the camera is dragged rigidly,
/// because by then keeping the subject in frame matters more than holding the shot.
/// </param>
public sealed record CameraGuides(double ScreenX, double ScreenY, ScreenRect Dead, ScreenRect Soft);

using Orblit.VectorMath;

namespace Orblit.Game;

/// <summary>
/// The pointer for one frame, as the world sees it.
/// </summary>
/// <param name="Origin">Where the ray into the world starts.</param>
/// <param name="Direction">Which way it goes.</param>
/// <param name="Pressed">The primary button went down since the last update.</param>
/// <param name="Drag">Pixels the pointer moved since the last update with the secondary button held, or null when it is not held.</param>
/// <param name="Wheel">Notches the wheel turned since the last update, up positive.</param>
/// <remarks>
/// A ray rather than a screen position, because the host knows the view and the camera and the game
/// does not. The game asks what the ray hits and never has to know how big the window is.
/// </remarks>
public sealed record GamePointer(
    Vector3 Origin,
    Vector3 Direction,
    bool Pressed = false,
    Vector2? Drag = null,
    double Wheel = 0);

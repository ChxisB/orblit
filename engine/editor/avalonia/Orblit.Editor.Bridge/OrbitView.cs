using System.Numerics;

namespace Orblit.Editor.Bridge;

/// <summary>
/// Where an orbiting editor camera stands: a point it looks at, two angles and a distance. A value never
/// changes. Each move returns the next view.
/// </summary>
public readonly record struct OrbitView(Vector3 Target, float Yaw, float Pitch, float Distance)
{
    const float MaxPitch = 1.5f;
    const float MinDistance = 0.5f;
    const float ZoomStep = 1.1f;

    public static OrbitView Start { get; } = new(Vector3.Zero, 0.6f, 0.4f, 8f);

    /// <summary>The camera position. Yaw turns around the up axis and pitch lifts the camera above the target.</summary>
    public Vector3 Eye => Target + Distance * new Vector3(
        MathF.Cos(Pitch) * MathF.Sin(Yaw),
        MathF.Sin(Pitch),
        MathF.Cos(Pitch) * MathF.Cos(Yaw));

    /// <summary>Turns the camera by radians. Pitch stops short of straight up and down.</summary>
    public OrbitView Orbited(float yaw, float pitch) =>
        this with { Yaw = Yaw + yaw, Pitch = Math.Clamp(Pitch + pitch, -MaxPitch, MaxPitch) };

    /// <summary>Looks at another point from the same angles and distance.</summary>
    public OrbitView Framed(Vector3 target) => this with { Target = target };

    /// <summary>Moves the camera in for a positive step count and out for a negative one.</summary>
    public OrbitView Zoomed(float steps) =>
        this with { Distance = Math.Max(MinDistance, Distance * MathF.Pow(ZoomStep, -steps)) };
}

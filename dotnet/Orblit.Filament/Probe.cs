using Orblit.VectorMath;

namespace Orblit.Filament;

/// <summary>
/// A reflection captured from a point in the world. An environment is a photograph of somewhere
/// else, and indoors that is the wrong picture. A probe is the scene taking its own photograph
/// from a point inside itself, and lighting from that instead. It is taken once and kept until
/// <see cref="Version"/> changes, because six renders of the scene are not a per frame cost.
/// A value with many fields, so it is changed with <c>with</c>.
/// </summary>
public sealed record Probe
{
    /// <summary>Floats a probe occupies in the packed array.</summary>
    public const int Stride = 8;

    /// <summary>This probe's identity, stable for as long as it exists. Objects and lights share the one key space.</summary>
    public required long Key { get; init; }

    /// <summary>Where the photograph is taken from, usually the middle of the room at head height.</summary>
    public required Vector3 Position { get; init; }

    /// <summary>How far its influence reaches, in metres. Where two overlap the nearer middle wins.</summary>
    public double Radius { get; init; } = 12;

    /// <summary>The size of one face of the captured cube.</summary>
    public int Resolution { get; init; } = 256;

    /// <summary>Bump this to take the photograph again. Nothing else does, because the renderer cannot tell what moved mattered.</summary>
    public int Version { get; init; }

    /// <summary>
    /// Which layers the capture draws. Leaving out the reflective things is how a reflection
    /// shows the room and not a smaller copy of the thing doing the reflecting.
    /// </summary>
    public int Layers { get; init; } = 0xFF;

    /// <summary>
    /// How much of the captured light counts, as a multiplier. One, because a probe is the
    /// scene's own light rendered with the exposure held at one, so it arrives in the units the
    /// rest of the frame is in.
    /// </summary>
    public double Intensity { get; init; } = 1;

    internal void Pack(float[] into, int at) => Packing.Floats(
        Position.X, Position.Y, Position.Z,
        Radius,
        Resolution,
        Version,
        Layers,
        Intensity).CopyTo(into, at);
}

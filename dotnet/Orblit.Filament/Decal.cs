using Orblit.VectorMath;

namespace Orblit.Filament;

/// <summary>
/// A picture thrown along a box onto whatever is inside it: a poster on a wall, a scorch on a
/// floor, a puddle, the white line down a road. Each is a change to a surface that is already
/// there, and a quad floating above that surface flickers through it and fails to bend round a
/// corner. So a decal is a box, and every lit surface inside it takes the picture painted into
/// its base colour, and its roughness and metalness if asked, before it is lit.
/// </summary>
/// <remarks>
/// The box's own y is the projection axis. An unturned decal is thrown straight down, which is a
/// floor marking. It does not reach surfaces drawn with a glTF file's own materials, the unlit or
/// video surfaces, or populations.
/// </remarks>
public sealed record Decal
{
    /// <summary>Floats one decal occupies.</summary>
    public const int Stride = 22;

    /// <summary>How many one view paints. The renderer reports any past this.</summary>
    public const int Budget = 32;

    /// <summary>Sixty degrees: a floor that slopes like a steep ramp still takes the whole picture.</summary>
    public const double DefaultFadeStart = 60 * Math.PI / 180;

    /// <summary>Eighty: gone well before the surface is edge-on.</summary>
    public const double DefaultFadeEnd = 80 * Math.PI / 180;

    /// <summary>Pass for both fade angles to paint every surface in the box whichever way it faces.</summary>
    public const double NoFade = Math.PI;

    /// <summary>This decal's identity, for an editor to select it by.</summary>
    public required long Key { get; init; }

    /// <summary>The centre of the projection box, in the world.</summary>
    public required Vector3 Position { get; init; }

    /// <summary>The box's full extent along its own x, y and z, in metres. Keep y shallow: everything inside it is painted.</summary>
    public required Vector3 Size { get; init; }

    /// <summary>How the box is turned. Its y is the direction it throws along, towards minus.</summary>
    public Quaternion Rotation { get; init; } = Quaternion.Identity();

    /// <summary>The picture, or null for a decal that is only its colour. Always read as colour, whatever its flag says.</summary>
    public Texture? Texture { get; init; }

    /// <summary>Linear RGB, multiplied into the picture.</summary>
    public Vector3 Colour { get; init; } = new(1, 1, 1);

    /// <summary>How much of the surface it covers where the picture is solid, 0 to 1.</summary>
    public double Opacity { get; init; } = 1;

    /// <summary>The angle from the projection axis, in radians, at which the decal starts to fade. This stops a decal smearing down a wall.</summary>
    public double FadeStartAngle { get; init; } = DefaultFadeStart;

    /// <summary>The angle by which the decal has gone.</summary>
    public double FadeEndAngle { get; init; } = DefaultFadeEnd;

    /// <summary>Paints this roughness where the decal covers, or leaves the surface's own when null.</summary>
    public double? Roughness { get; init; }

    /// <summary>Paints this metalness where the decal covers, or leaves it when null.</summary>
    public double? Metallic { get; init; }

    /// <summary>Which layers receive it, or null for every layer.</summary>
    public IReadOnlySet<int>? Layers { get; init; }

    /// <summary>Which decals are painted over which. Higher is later, so on top; equal orders keep the order listed.</summary>
    public int SortOrder { get; init; }

    /// <summary>The seven layers as bits, the way the renderer holds them.</summary>
    internal int LayerMask
    {
        get
        {
            if (Layers is not { } named)
            {
                return 0x7F;
            }

            var mask = 0;
            foreach (var layer in named.Where(layer => layer is >= 0 and <= 6))
            {
                mask |= 1 << layer;
            }

            return mask;
        }
    }

    /// <summary>Writes this decal's floats into the scene's decal block, in the layout the renderer reads.</summary>
    internal void Pack(float[] into, int at) => Packing.Floats(
        Position.X, Position.Y, Position.Z,
        Rotation.X, Rotation.Y, Rotation.Z, Rotation.W,
        Size.X, Size.Y, Size.Z,
        Colour.X, Colour.Y, Colour.Z,
        Opacity,
        FadeStartAngle,
        FadeEndAngle,
        Roughness ?? 0,
        Roughness is null ? 0 : 1,
        Metallic ?? 0,
        Metallic is null ? 0 : 1,
        LayerMask,
        SortOrder).CopyTo(into, at);
}

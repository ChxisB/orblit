using Orblit.VectorMath;

namespace Orblit.Input;

/// <summary>The travel of one axis, as the driver reports it. A trigger can be 8 bits or 10, and only the driver knows.</summary>
public sealed class PadAxisRange(int minimum, int maximum, int flat = 0, int fuzz = 0)
{
    public static PadAxisRange Stick { get; } = new(-32768, 32767);

    public static PadAxisRange Trigger { get; } = new(0, 255);

    public int Minimum { get; } = minimum;

    public int Maximum { get; } = maximum;

    /// <summary>The driver's own dead zone. Read and kept, and not applied here.</summary>
    public int Flat { get; } = flat;

    public int Fuzz { get; } = fuzz;

    public bool IsUnipolar => Minimum >= 0;

    /// <summary>Maps the range to -1 to 1, centred on the middle of its travel.</summary>
    public double ToSigned(int value)
    {
        var middle = ((double)Minimum + Maximum) / 2;
        var half = ((double)Maximum - Minimum) / 2;
        return half <= 0 ? 0 : Doubles.Clamp((value - middle) / half, -1.0, 1.0);
    }

    /// <summary>Maps the range to 0 to 1.</summary>
    public double ToUnsigned(int value)
    {
        var span = (long)Maximum - Minimum;
        return span <= 0 ? 0 : Doubles.Clamp((double)((long)value - Minimum) / span, 0.0, 1.0);
    }
}

using Orblit.VectorMath;

namespace Orblit.Filament;

/// <summary>How a surface sways. A bearing is degrees clockwise from north, which is negative Z.</summary>
public sealed record Wind
{
    /// <summary>Still air on a rigid surface, which is what a material that never mentions wind has.</summary>
    public static readonly Wind None = new();

    public double Bearing { get; init; }

    /// <summary>Metres a second.</summary>
    public double Speed { get; init; }

    /// <summary>How far the surface gives, nought for rigid.</summary>
    public double Strength { get; init; } = 1;

    /// <summary>
    /// Whether anything moves. Still air and a rigid surface both mean no, and the shader has one
    /// early return for them, so a material sends nought for both whenever either is nought.
    /// </summary>
    public bool Moves => Speed > 0 && Strength > 0;

    /// <summary>The bearing as a direction on the ground, x then z.</summary>
    public Vector2 Direction
    {
        get
        {
            var radians = Bearing * Math.PI / 180;
            return new Vector2(Math.Sin(radians), Math.Cos(radians));
        }
    }
}

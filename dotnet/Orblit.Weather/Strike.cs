using Orblit.VectorMath;

namespace Orblit.Weather;

/// <summary>
/// A strike of lightning: how bright it is this instant, where it is, and which
/// strike it is. It is worked out from the clock and not rolled, so the same
/// second of the same storm looks the same twice.
/// </summary>
/// <param name="Flash">How bright the sky is this instant, from nothing to one.</param>
/// <param name="Direction">Which way the strike is. It is scattered around the sky.</param>
/// <param name="Seed">What the bolt's shape is drawn from.</param>
public sealed record Strike(double Flash, Vector3 Direction, double Seed)
{
    /// <summary>Nothing happening. One object.</summary>
    public static readonly Strike None = new(0, new Vector3(0, 0.35, 1), 0);

    /// <summary>
    /// The strike at <paramref name="clock"/> seconds, for a storm striking at
    /// <paramref name="frequency"/>. A frequency is a rate, so zero is a dry
    /// storm and one is close to continuous. A NaN or infinite clock throws.
    /// </summary>
    public static Strike At(double clock, double frequency)
    {
        if (frequency <= 0 || clock < 0)
        {
            return None;
        }

        var window = 14 / (0.2 + (frequency * 3));
        var index = Doubles.Floor(clock / window);
        var into = clock - (index * window);

        // Not every window has a strike in it, or the storm would be a metronome.
        if (Scatter(index) > 0.3 + (frequency * 0.65))
        {
            return new Strike(0, PlaceOf(index), SeedOf(index));
        }

        var at = Scatter((index * 7) + 3) * Math.Max(window - 0.8, 0.1);
        var since = into - at;

        // A stroke and then a weaker one behind it, so it reads as lightning.
        var stroke = since < 0
            ? 0.0
            : Math.Exp(-since * 14) + (since > 0.18 ? 0.45 * Math.Exp(-(since - 0.18) * 10) : 0);

        return new Strike(
            Doubles.Clamp(stroke * (0.6 + (0.4 * Scatter((index * 13) + 5))), 0.0, 1.0),
            PlaceOf(index),
            SeedOf(index));
    }

    private static Vector3 PlaceOf(long index)
    {
        var bearing = Scatter((index * 17) + 11) * 2 * Math.PI;
        var height = 0.12 + (Scatter((index * 23) + 4) * 0.34);
        return new Vector3(
            Math.Cos(height) * Math.Sin(bearing),
            Math.Sin(height),
            Math.Cos(height) * Math.Cos(bearing));
    }

    private static double SeedOf(long index) => Scatter((index * 31) + 7) * 100;

    /// <summary>
    /// A number between zero and one that is the same for the same input. The
    /// index wraps at 64 bits, so a huge clock still gives an index.
    /// </summary>
    private static double Scatter(long step)
    {
        var value = Math.Sin(step * 12.9898) * 43758.5453;
        return value - Math.Floor(value);
    }
}

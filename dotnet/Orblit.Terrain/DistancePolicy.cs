namespace Orblit.Terrain;

/// <summary>
/// Steps the view distance down when frames cost too much and back up when there is room.
/// The distance is a whole number of steps from <see cref="Floor"/> to <see cref="Ceiling"/>. A game
/// reads <see cref="Steps"/> as the clipmap's ring count and <see cref="Fraction"/> as the share of
/// each scatter layer's range to keep. Cost is in whatever unit the game measures, such as frame
/// time in seconds, and <see cref="High"/> and <see cref="Low"/> are in the same unit.
/// It decides once every <see cref="Settle"/> seconds, on the mean cost since the last decision, so
/// one slow frame moves nothing. It moves one step, down above <see cref="High"/> and up below
/// <see cref="Low"/>. Between the two it holds. That gap is what stops a cost that falls when the
/// distance does from stepping it straight back.
/// Time is passed in, not read, so a test can script it. Nothing uses a policy unless the game makes
/// one.
/// </summary>
public sealed class DistancePolicy
{
    private double? _since;
    private double _total;
    private int _frames;

    // F2: five parameters, three of them required and all called by name.
    public DistancePolicy(int ceiling, double high, double low, int floor = 1, double settle = 2)
    {
        if (floor < 1 || floor > ceiling)
        {
            throw new ArgumentOutOfRangeException(nameof(floor), floor, $"Must be 1 to {ceiling}.");
        }

        if (!(low < high))
        {
            throw new ArgumentOutOfRangeException(nameof(low), low, $"Must be below {high}.");
        }

        if (!(settle > 0))
        {
            throw new ArgumentOutOfRangeException(nameof(settle), settle, "Must be above zero.");
        }

        Ceiling = ceiling;
        High = high;
        Low = low;
        Floor = floor;
        Settle = settle;
        Steps = ceiling;
    }

    public int Ceiling { get; }

    public int Floor { get; }

    public double High { get; }

    public double Low { get; }

    /// <summary>Seconds between decisions.</summary>
    public double Settle { get; }

    /// <summary>How far the view reaches now, from <see cref="Floor"/> to <see cref="Ceiling"/>.</summary>
    public int Steps { get; private set; }

    /// <summary><see cref="Steps"/> as a share of <see cref="Ceiling"/>, 1 when nothing has been taken away.</summary>
    public double Fraction => (double)Steps / Ceiling;

    /// <summary>
    /// Takes the cost of one frame, drawn at <paramref name="seconds"/> on any clock that only goes
    /// forward.
    /// The first call starts the first interval, so a game's loading frames do not count against it.
    /// </summary>
    public void Update(double cost, double seconds)
    {
        _since ??= seconds;
        _total += cost;
        _frames++;
        if (seconds - _since.Value < Settle)
        {
            return;
        }

        var mean = _total / _frames;
        if (mean > High && Steps > Floor)
        {
            Steps--;
        }
        else if (mean < Low && Steps < Ceiling)
        {
            Steps++;
        }

        _since = seconds;
        _total = 0;
        _frames = 0;
    }
}

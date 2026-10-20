namespace Orblit.Profile;

/// <summary>
/// The last few numbers, fixed in size so a profiler that runs for hours holds
/// the same memory it held in the first minute.
/// </summary>
internal sealed class Ring
{
    private readonly double[] _values;
    private int _next;

    public Ring(int size)
    {
        if (size <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size), size, "must be above 0");
        }

        _values = new double[size];
    }

    public int Length { get; private set; }

    public void Add(double value)
    {
        _values[_next] = value;
        _next = (_next + 1) % _values.Length;
        if (Length < _values.Length)
        {
            Length++;
        }
    }

    /// <summary>Oldest first.</summary>
    public List<double> Values
    {
        get
        {
            var start = Mod(_next - Length);
            var values = new List<double>(Length);
            for (var i = 0; i < Length; i++)
            {
                values.Add(_values[(start + i) % _values.Length]);
            }

            return values;
        }
    }

    public double Last => Length == 0 ? 0 : _values[Mod(_next - 1)];

    public double Mean
    {
        get
        {
            if (Length == 0)
            {
                return 0;
            }

            var sum = 0.0;
            foreach (var value in Values)
            {
                sum += value;
            }

            return sum / Length;
        }
    }

    public double Max
    {
        get
        {
            var max = 0.0;
            foreach (var value in Values)
            {
                if (value > max)
                {
                    max = value;
                }
            }

            return max;
        }
    }

    /// <summary>The value <paramref name="fraction"/> of the way up the sorted ring, nearest rank.</summary>
    public double Percentile(double fraction)
    {
        if (Length == 0)
        {
            return 0;
        }

        var sorted = Values;
        sorted.Sort();
        return sorted[(int)Math.Round((Length - 1) * fraction, MidpointRounding.AwayFromZero)];
    }

    // Dart's % is never negative, and the cursor sits at or below the count.
    private int Mod(int index) => ((index % _values.Length) + _values.Length) % _values.Length;
}

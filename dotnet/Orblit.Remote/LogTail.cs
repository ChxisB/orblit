namespace Orblit.Remote;

/// <summary>
/// The last lines a game logged, so a session can read what just happened
/// without finding the console.
/// </summary>
public sealed class LogTail
{
    private readonly Queue<string> _lines = new();

    /// <summary>Throws <see cref="ArgumentOutOfRangeException"/> for a capacity below one.</summary>
    public LogTail(int capacity = 500)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        Capacity = capacity;
    }

    public int Capacity { get; }

    /// <summary>Keeps <paramref name="line"/>, and drops the oldest when the ring is full.</summary>
    public void Add(string line)
    {
        // A game logs from any thread and a session reads from a socket's.
        lock (_lines)
        {
            _lines.Enqueue(line);
            if (_lines.Count > Capacity)
            {
                _lines.Dequeue();
            }
        }
    }

    /// <summary>
    /// The last <paramref name="count"/> lines, oldest first. Throws
    /// <see cref="ArgumentOutOfRangeException"/> when <paramref name="count"/> is negative.
    /// </summary>
    public IReadOnlyList<string> Tail(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        lock (_lines)
        {
            return [.. _lines.Skip(Math.Max(0, _lines.Count - count))];
        }
    }
}

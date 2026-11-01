namespace Orblit.Asset;

/// <summary>One thing a <see cref="MapTransport"/> serves.</summary>
public sealed class TransportPage(byte[] bytes, string? etag = null, int cut = 0, int? stopAfter = null, List<Fault>? faults = null)
{
    public byte[] Bytes { get; } = bytes;

    public string? ETag { get; } = etag;

    /// <summary>How many bytes per chunk, or nought for one chunk.</summary>
    public int Cut { get; } = cut;

    /// <summary>
    /// Sends this many bytes and then drops the connection. Cleared once it has happened, so the retry that
    /// follows gets a whole reply, which is the sequence resuming has to survive.
    /// </summary>
    public int? StopAfter { get; set; } = stopAfter;

    /// <summary>Failures to hand out before answering properly, one per request.</summary>
    public List<Fault> Faults { get; } = faults ?? [];

    /// <summary>The next fault to hand out, removing it from the list.</summary>
    public Fault? Take()
    {
        if (Faults.Count == 0)
        {
            return null;
        }

        var next = Faults[0];
        Faults.RemoveAt(0);
        return next;
    }
}

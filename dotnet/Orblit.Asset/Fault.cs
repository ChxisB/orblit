namespace Orblit.Asset;

/// <summary>A failure a <see cref="MapTransport"/> hands out once.</summary>
public sealed class Fault
{
    private Fault(int? status, Exception? error)
    {
        Status = status;
        Error = error;
    }

    /// <summary>A status code rather than a body.</summary>
    public static Fault OfStatus(int status) => new(status, null);

    /// <summary>A thrown failure: no answer at all. Defaults to "no route to host".</summary>
    public static Fault Thrown(Exception? error = null) => new(null, error ?? new SocketFailureException("no route to host"));

    public int? Status { get; }

    public Exception? Error { get; }
}

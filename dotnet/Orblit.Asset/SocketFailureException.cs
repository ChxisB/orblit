namespace Orblit.Asset;

/// <summary>
/// A connection that failed, named by this package so that code catching it does not have to know which
/// client is underneath.
/// </summary>
public sealed class SocketFailureException(string reason) : Exception($"The connection failed: {reason}.")
{
    public string Reason { get; } = reason;
}

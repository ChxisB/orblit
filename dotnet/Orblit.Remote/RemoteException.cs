namespace Orblit.Remote;

/// <summary>A request the surface refuses, with the JSON-RPC code a client reads.</summary>
public sealed class RemoteException(int code, string message) : Exception(message)
{
    public const int Parse = -32700;
    public const int InvalidRequest = -32600;
    public const int NoMethod = -32601;
    public const int InvalidParams = -32602;
    public const int Internal = -32603;
    public const int NotFound = -32001;
    public const int ReadOnly = -32002;
    public const int TimedOut = -32003;
    public const int Unavailable = -32004;

    public int Code { get; } = code;
}

namespace Orblit.Remote;

/// <summary>The parts of <see cref="RemoteServer.Start"/> a game may replace.</summary>
public sealed class RemoteServerOptions
{
    /// <summary>
    /// Receives the line <c>orblit-remote ws://127.0.0.1:PORT/?token=TOKEN</c>, the only place the
    /// token is written. It defaults to the console. If it throws, the server closes and the
    /// exception goes to the caller, so no port stays open.
    /// </summary>
    public Action<string>? Announce { get; init; }

    /// <summary>
    /// Where the token comes from. It defaults to the system's cryptographic generator, and a
    /// test passes a seeded one.
    /// </summary>
    public Random? Random { get; init; }

    // Null reads the entry assembly. A test sets it, because a release build of the tests
    // would otherwise be refused.
    internal bool? DebugBuild { get; init; }

    internal TimeSpan HeadTimeout { get; init; } = TimeSpan.FromSeconds(10);
}

namespace Orblit.Game;

/// <summary>What part a game plays when more than one copy of it runs.</summary>
public enum GameRole
{
    /// <summary>One copy, no network. The game owns everything.</summary>
    Offline,

    /// <summary>The copy that decides. It moves what it has authority over.</summary>
    Server,

    /// <summary>A copy that follows a server and moves only what it owns.</summary>
    Client,
}

/// <summary>
/// What a game asks about the network it runs on. A network implementation plugs in through
/// <see cref="GameHost.UseNet"/>. A game that never asks runs offline.
/// </summary>
/// <remarks>
/// Declared here, next to the code that calls it, so the engine does not depend on any network.
/// Clients and entities are named by string, as they are in the scene and on the wire.
/// <para>
/// The game reads the first four members. The host calls the last three, so a game never has to
/// pump the network itself.
/// </para>
/// </remarks>
public interface IGameNet
{
    GameRole Role { get; }

    /// <summary>This copy's client id, or null when it has none, as offline and on a host.</summary>
    string? LocalClient { get; }

    /// <summary>True on the server copy.</summary>
    bool IsServer => Role == GameRole.Server;

    /// <summary>True on a client copy.</summary>
    bool IsClient => Role == GameRole.Client;

    /// <summary>The client that owns the entity, or null when it has no owner.</summary>
    string? OwnerOf(string entityId);

    /// <summary>Called once, by <see cref="GameHost.UseNet"/>, with the scene the game plays in.</summary>
    void Attach(GameScene scene);

    /// <summary>Called each frame before the game's update. A client puts what arrived into the scene.</summary>
    void BeforeUpdate();

    /// <summary>Called each frame after the game's update. A server sends what changed.</summary>
    void AfterUpdate();
}

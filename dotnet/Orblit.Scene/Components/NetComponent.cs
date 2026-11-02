namespace Orblit.Scene;

/// <summary>Who may change a networked entity.</summary>
public enum NetAuthority
{
    /// <summary>Only the server writes it. Every client reads.</summary>
    Server,

    /// <summary>The client that owns the entity writes the synced state. The server relays it.</summary>
    Owner,
}

/// <summary>
/// This entity is shared between the players of a game.
/// </summary>
/// <remarks>
/// A tag and nothing more. The scene says which things are networked and who may change them, and
/// the game host does the rest: the server spawns the entity for every client, moves it, and removes
/// it. Nothing about sockets or messages lives here, so a scene opens and plays the same with no
/// network at all.
/// <para>
/// <see cref="Sync"/> names the state that travels. <c>transform</c> is the entity's place. Any
/// other name is a variable the game declares for itself, which keeps the list a plain set of words
/// and leaves the meaning of each to the code that owns it.
/// </para>
/// <para>
/// The authority defaults to the server, because a client that may write anything it likes is a
/// client that may cheat. A game opts an entity into owner authority on purpose, for the player's
/// own avatar.
/// </para>
/// </remarks>
public sealed class NetComponent : SceneComponent
{
    public const string TransformState = "transform";

    private static readonly string[] DefaultSync = [TransformState];

    public NetComponent(NetAuthority authority = NetAuthority.Server, IEnumerable<string>? sync = null)
    {
        if (!Enum.IsDefined(authority))
        {
            throw new ArgumentException($"The authority {(int)authority} is not server or owner.", nameof(authority));
        }

        var names = (sync ?? DefaultSync).ToArray();
        foreach (var name in names)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A synced name cannot be empty.", nameof(sync));
            }
        }

        var repeated = names
            .GroupBy(static name => name, StringComparer.Ordinal)
            .FirstOrDefault(static group => group.Count() > 1);
        if (repeated is not null)
        {
            throw new ArgumentException($"The synced name \"{repeated.Key}\" is listed twice.", nameof(sync));
        }

        Authority = authority;
        Sync = names;
    }

    public NetAuthority Authority { get; }

    /// <summary>The state that is sent, in the order it was listed. Empty shares the entity and no state.</summary>
    public IReadOnlyList<string> Sync { get; }

    public override string Type => SceneComponents.Net;

    /// <summary>
    /// Lenient on purpose: a scene with a typo in a tag still opens. An authority this version does
    /// not know reads as the server's, never as the owner's, so a mistake cannot hand a client the
    /// entity.
    /// </summary>
    public static NetComponent FromJson(JsonMap json) => new(
        Values.Named<NetAuthority>(Values.At(json, "authority")) ?? NetAuthority.Server,
        Values.At(json, "sync") is JsonList sync
            ? Values.Texts(sync)
                .Where(static name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.Ordinal)
            : null);

    public override JsonMap ToJson() => Values.Pruned(new OrderedDictionary<string, object?>
    {
        ["authority"] = EnumNames.Of(Authority),
        ["sync"] = Sync,
    });
}

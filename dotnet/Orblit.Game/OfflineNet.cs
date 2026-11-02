namespace Orblit.Game;

/// <summary>
/// The net of a game that runs alone: no role to play, no client and no owners. Each host makes its
/// own, so what a network package keeps against a net, such as call handlers, ends with the game.
/// </summary>
public sealed class OfflineNet : IGameNet
{
    public GameRole Role => GameRole.Offline;

    public string? LocalClient => null;

    public string? OwnerOf(string entityId) => null;

    public void Attach(GameScene scene)
    {
    }

    public void BeforeUpdate()
    {
    }

    public void AfterUpdate()
    {
    }
}

namespace Orblit.Game;

/// <summary>The net of a game that runs alone: no role to play, no client and no owners.</summary>
public sealed class OfflineNet : IGameNet
{
    public static OfflineNet Instance { get; } = new();

    private OfflineNet()
    {
    }

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

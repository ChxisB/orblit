using Orblit.Scene;

namespace Orblit.Game;

/// <summary>
/// The scene a game plays in, as the game sees it.
/// </summary>
/// <remarks>
/// Read through <see cref="Document"/> and changed through <see cref="Apply"/> and nothing else.
/// One way in means the host hears every change, and a host that draws from diffs never has to
/// compare two whole documents to find out what the game did.
/// </remarks>
public sealed class GameScene
{
    private readonly Action<SceneDiff> _changed;

    internal GameScene(SceneDocument document, Action<SceneDiff> changed)
    {
        Document = document;
        _changed = changed;
    }

    /// <summary>The scene now, with every diff so far applied.</summary>
    public SceneDocument Document { get; private set; }

    /// <summary>
    /// Makes the change and tells the host. An empty diff is not a change. Call it from the game's own
    /// calls only. The scene is not locked, so a timer or thread the game starts must leave its change
    /// for the next update.
    /// </summary>
    public void Apply(SceneDiff diff)
    {
        ArgumentNullException.ThrowIfNull(diff);
        if (diff.IsEmpty)
        {
            return;
        }

        Document = diff.ApplyTo(Document);
        _changed(diff);
    }

    // A change made by whoever hosts the game, such as an edit in the editor while the game plays. The
    // host made it, so it is not heard back as a change the game made.
    internal void Take(SceneDiff diff)
    {
        if (!diff.IsEmpty)
        {
            Document = diff.ApplyTo(Document);
        }
    }
}

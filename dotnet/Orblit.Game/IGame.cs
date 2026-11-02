namespace Orblit.Game;

/// <summary>
/// A game: the code of one project.
/// </summary>
/// <remarks>
/// One class per project, so a project has one place to start reading. The host calls these on the
/// thread that owns the scene, which is the interface thread in the editor, and never two at once.
/// A game that blocks in <see cref="Update"/> blocks that thread.
/// <para>
/// A game changes its world by handing <see cref="GameScene.Apply"/> a diff, not by reaching into
/// the host's objects. That is what lets the same class run in a window of its own and inside the
/// editor, where the scene is drawn by something else.
/// </para>
/// </remarks>
public interface IGame
{
    /// <summary>Called once, before the first update. Put in the scene what the game places itself.</summary>
    void Start(GameContext context);

    /// <summary>Called once a frame with the seconds since the last one.</summary>
    void Update(GameContext context, double seconds);

    /// <summary>
    /// Called once, when play ends or the game throws. Release everything the game started that
    /// outlives a call: a timer, a thread, an event it subscribed to. The editor unloads the game's
    /// code after this, and anything still reachable keeps that code in memory.
    /// </summary>
    void Stop(GameContext context);
}

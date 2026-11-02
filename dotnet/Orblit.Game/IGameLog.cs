namespace Orblit.Game;

/// <summary>
/// Where a game and its host say things. The host owns where they go: a console, a window or the
/// editor's log.
/// </summary>
public interface IGameLog
{
    void Say(string message);

    /// <summary>Something went wrong, with the exception for whoever has to find why.</summary>
    void Error(string message, Exception error);
}

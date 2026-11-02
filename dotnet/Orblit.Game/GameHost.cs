using System.Diagnostics.CodeAnalysis;
using Orblit.Scene;

namespace Orblit.Game;

/// <summary>
/// Runs one game: starts it, updates it, stops it, and keeps whatever it throws from reaching the
/// caller.
/// </summary>
/// <remarks>
/// The game is somebody else's code, and the caller may be an editor with unsaved work in it. So a
/// throw is caught, logged with its stack and treated as the end of the game. The host then calls
/// <see cref="IGame.Stop"/> once so the game can let go of what it started.
/// <para>
/// A host runs one game once. To play again, make another. That keeps "has this started" a fact
/// about the host and not something to reset.
/// </para>
/// </remarks>
public sealed class GameHost
{
    private readonly IGame _game;
    private readonly GameContext _context;
    private readonly IGameLog _log;
    private Phase _phase = Phase.Ready;

    public GameHost(IGame game, SceneDocument scene, string projectFolder, IGameLog log)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectFolder);
        ArgumentNullException.ThrowIfNull(log);
        _game = game;
        _log = log;
        _context = new GameContext(new GameScene(scene, Changed), projectFolder, log);
    }

    private enum Phase
    {
        Ready,
        Running,
        Done,
    }

    /// <summary>Raised for each change the game makes to its scene, for whoever draws it.</summary>
    public event Action<SceneDiff>? SceneChanged;

    /// <summary>The scene now.</summary>
    public SceneDocument Scene => _context.Scene.Document;

    /// <summary>The exception that ended the game, or null if it did not throw.</summary>
    public Exception? Fault { get; private set; }

    public bool Running => _phase == Phase.Running;

    public void Start()
    {
        if (_phase != Phase.Ready)
        {
            throw new InvalidOperationException("This host has already started its game.");
        }

        _phase = Phase.Running;
        End(Call("Start", () => _game.Start(_context)));
    }

    public void Update(GameInput input, double seconds)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (_phase == Phase.Ready)
        {
            throw new InvalidOperationException("Start the game before updating it.");
        }

        if (_phase == Phase.Done)
        {
            return;
        }

        _context.Receive(input);
        End(Call("Update", () => _game.Update(_context, seconds)));
    }

    /// <summary>
    /// Runs code of the game's that is not Start, Update or Stop, such as asking an interface game for
    /// its control, while the game runs. A throw ends the game as one from Update would, so the caller
    /// has one rule for everything the game does.
    /// </summary>
    public void Guard(string name, Action call)
    {
        ArgumentNullException.ThrowIfNull(call);
        if (_phase == Phase.Running)
        {
            End(Call(name, call));
        }
    }

    /// <summary>
    /// Puts a change made outside the game, such as an edit in the editor while the game plays, into the
    /// game's scene. The game reads it on its next call. It is not raised as <see cref="SceneChanged"/>,
    /// because whoever made it already has it. A change after the end is dropped.
    /// </summary>
    public void ApplyEdit(SceneDiff diff)
    {
        ArgumentNullException.ThrowIfNull(diff);
        if (_phase != Phase.Done)
        {
            _context.Scene.Take(diff);
        }
    }

    /// <summary>Stops the game if it is running. Stopping twice, or before starting, does nothing.</summary>
    public void Stop()
    {
        if (_phase != Phase.Running)
        {
            return;
        }

        _phase = Phase.Done;
        Call("Stop", () => _game.Stop(_context));
    }

    // A diff that arrives after the stop comes from something the game left running. It is dropped
    // here and not at the game, because the game is the code that forgot.
    private void Changed(SceneDiff diff)
    {
        if (_phase == Phase.Running)
        {
            SceneChanged?.Invoke(diff);
        }
    }

    private void End(Exception? error)
    {
        if (error is null)
        {
            return;
        }

        Fault = error;
        Stop();
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "The game is someone else's code and must not take its host down.")]
    private Exception? Call(string name, Action call)
    {
        try
        {
            call();
            return null;
        }
        catch (Exception error)
        {
            _log.Error($"The game threw in {name}.", error);
            return error;
        }
    }
}

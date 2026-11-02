using Orblit.Input;

namespace Orblit.Game;

/// <summary>
/// Everything the host gives a game. One object for the whole run, so a game can keep it.
/// </summary>
public sealed class GameContext
{
    internal GameContext(GameScene scene, string projectFolder, IGameLog log)
    {
        Scene = scene;
        ProjectFolder = projectFolder;
        Log = log;
        Input = new GameInput(new FrameControls());
    }

    public GameScene Scene { get; }

    /// <summary>The folder the project lives in, for the files the game reads at run time.</summary>
    public string ProjectFolder { get; }

    public IGameLog Log { get; }

    /// <summary>The input of the frame being updated. Nothing is held before the first update.</summary>
    public GameInput Input { get; private set; }

    /// <summary>The network the game runs on. It is <see cref="OfflineNet"/> unless the host was given another.</summary>
    public IGameNet Net { get; private set; } = new OfflineNet();

    internal void Receive(GameInput input) => Input = input;

    internal void Connect(IGameNet net) => Net = net;
}

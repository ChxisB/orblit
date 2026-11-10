using System.Numerics;

namespace Orblit.Web;

// A C# script on a scene entity. The scene names the class by its file name,
// such as Spinner.cs, and the game registers the class with WebHost.Register.
// One object exists for each entity that carries the script.
public abstract class WebScript
{
    private Vector3 position;

    // Names the entity in WebEngine calls. The engine frees it when the entity
    // leaves the stage, so do not release it.
    public int Handle { get; private set; }

    internal bool PositionChanged { get; set; }

    // The entity's position. A change goes to the engine once per frame, after
    // every OnFrame has run, in a single call for all scripts.
    public Vector3 Position
    {
        get => position;
        set
        {
            position = value;
            PositionChanged = true;
        }
    }

    // Runs once, when the entity enters the stage.
    public virtual void OnStart()
    {
    }

    public virtual void OnFrame(double delta)
    {
    }

    // Runs once, when the entity leaves the stage.
    public virtual void OnExit()
    {
    }

    internal void Attach(int handle, Vector3 startPosition)
    {
        Handle = handle;
        position = startPosition;
    }
}

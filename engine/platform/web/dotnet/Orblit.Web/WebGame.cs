namespace Orblit.Web;

// A game that runs in the page beside the engine. The host calls OnStart once
// after the engine has loaded the main scene. It then calls OnFrame for each
// engine process frame.
public abstract class WebGame
{
    // The engine's process frame counter, set before each OnFrame.
    public long FrameCount { get; internal set; }

    public virtual void OnStart() { }

    public virtual void OnFrame(double delta) { }
}

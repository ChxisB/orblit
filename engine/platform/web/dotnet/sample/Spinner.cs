using System.Numerics;
using Orblit.Web;

// Runs on the Orbiter entity in main.oscn. The scene names the script by file
// name, and Program registers the class.
public sealed class Spinner : WebScript
{
    private const float Radius = 1.0f;

    private Vector3 centre;
    private double time;

    public override void OnStart()
    {
        centre = Position;
        Console.WriteLine($"spinner started, handle {Handle}, at {centre}");
    }

    public override void OnFrame(double delta)
    {
        time += delta;
        Position = centre + new Vector3((float)Math.Cos(time * 2.0), 0.0f, (float)Math.Sin(time * 2.0)) * Radius;
    }

    public override void OnExit() => Console.WriteLine("spinner exited");
}

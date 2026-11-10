using Orblit.Web;

// Leaves the stage after two seconds, because the scene frees it with a timer.
public sealed class Fleeting : WebScript
{
    public override void OnStart() => Console.WriteLine($"fleeting started, handle {Handle}");

    public override void OnExit() => Console.WriteLine("fleeting exited");
}

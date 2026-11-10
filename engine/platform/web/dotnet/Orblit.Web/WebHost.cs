using System.Numerics;
using System.Runtime.InteropServices.JavaScript;

namespace Orblit.Web;

// The page calls these. Main registers the game and the script classes, and
// the page starts them once the engine is running.
public static partial class WebHost
{
    // Same values as ScriptEventKind in web_script_proxy.cpp.
    private const int AttachEvent = 0;
    private const int ExitEvent = 1;

    private static readonly Dictionary<string, Func<WebScript>> scriptFactories = [];
    private static readonly Dictionary<int, WebScript> scripts = [];
    private static WebGame? game;
    private static EntityPosition[] moved = new EntityPosition[16];

    public static void Run(WebGame webGame) => game = webGame;

    // Makes a scene's Name.cs script run the class called Name.
    public static void Register<T>() where T : WebScript, new() =>
        scriptFactories[typeof(T).Name] = () => new T();

    [JSExport]
    public static void Start() => game?.OnStart();

    [JSExport]
    public static void Frame(double delta, int frame)
    {
        if (game != null)
        {
            game.FrameCount = frame;
            game.OnFrame(delta);
        }
        foreach (WebScript script in scripts.Values)
        {
            script.OnFrame(delta);
        }
        SendMovedPositions();
    }

    [JSExport]
    public static void ScriptEvent(int kind, int handle, string className, double x, double y, double z)
    {
        if (kind == AttachEvent)
        {
            AttachScript(handle, className, new Vector3((float)x, (float)y, (float)z));
        }
        else if (kind == ExitEvent && scripts.Remove(handle, out WebScript? script))
        {
            script.OnExit();
        }
    }

    private static void AttachScript(int handle, string className, Vector3 position)
    {
        if (!scriptFactories.TryGetValue(className, out Func<WebScript>? create))
        {
            Console.Error.WriteLine($"No WebScript named {className} is registered, so its entity runs no C#.");
            return;
        }
        WebScript script = create();
        script.Attach(handle, position);
        scripts[handle] = script;
        script.OnStart();
    }

    private static void SendMovedPositions()
    {
        int count = 0;
        foreach (WebScript script in scripts.Values)
        {
            if (!script.PositionChanged)
            {
                continue;
            }
            script.PositionChanged = false;
            if (count == moved.Length)
            {
                Array.Resize(ref moved, count * 2);
            }
            moved[count++] = new EntityPosition
            {
                Handle = script.Handle,
                X = script.Position.X,
                Y = script.Position.Y,
                Z = script.Position.Z,
            };
        }
        if (count > 0)
        {
            WebEngine.SetPositions(moved.AsSpan(0, count));
        }
    }
}

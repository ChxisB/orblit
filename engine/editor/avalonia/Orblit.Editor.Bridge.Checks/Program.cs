using Orblit.Editor.Bridge.Checks;

// The engine's window system needs the process's main thread, which a test framework keeps for
// itself. This runner runs every check on the main thread against one engine. The engine allows one
// instance per process, so the headless editor checks, the windowed checks and the C# module checks
// are three runs.
using IChecks checks = args.Contains("--windowed") ? new WindowedChecks()
    : args.Contains("--csharp") ? new CsharpChecks()
    : new EditorChecks();
var failed = 0;
foreach (var (name, run) in checks.All)
{
    try
    {
        run();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception error)
    {
        failed++;
        Console.WriteLine($"FAIL {name}: {error.Message}");
    }
}
Console.WriteLine($"{checks.All.Count - failed} passed, {failed} failed");
return failed == 0 ? 0 : 1;

internal interface IChecks : IDisposable
{
    IReadOnlyList<(string Name, Action Run)> All { get; }
}

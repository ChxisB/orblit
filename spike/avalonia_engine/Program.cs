using Avalonia;
using System.Diagnostics;
using AvaloniaEngine;

// Usage: dotnet run -c Release -- --check <libgodot path> [engine args...]
if (args.Length == 3 && args[0] == "--editor-check")
{
    return EditorCheck.Run(args[1], args[2]);
}

if (args.Length == 4 && args[0] == "--readback-check")
{
    return ReadbackCheck.Run(args[1], args[2], args[3]);
}

if (args.Length == 4 && args[0] == "--window")
{
    SpikeApp.Options = new SpikeOptions { Library = args[1], Project = args[2], OutputPng = args[3] };
    var code = AppBuilder.Configure<SpikeApp>().UsePlatformDetect().StartWithClassicDesktopLifetime([]);
    SpikeApp.Readback?.Close();
    SpikeApp.Engine?.Dispose();
    Console.WriteLine("engine destroyed after Avalonia exit");
    return code;
}

if (args.Length < 2 || args[0] != "--check")
{
    Console.Error.WriteLine("usage: --check <libgodot path> [engine args...] | --editor-check <libgodot path> <project>");
    return 2;
}

// The engine changes directory when it starts, so a relative path would not load a second time.
var library = Path.GetFullPath(args[1]);
var engine = Engine.Create(library, args.Skip(2).ToList());
if (engine is null)
{
    Console.Error.WriteLine("libgodot_create_godot_instance returned null");
    return 1;
}

var timer = Stopwatch.StartNew();
var started = engine.Start();
Console.WriteLine($"start: {started} in {timer.ElapsedMilliseconds} ms, is_started: {engine.IsStarted()}");

// The engine keeps one global instance, so a second create must fail and leave the first alone.
var second = Engine.Create(library, args.Skip(2).ToList());
Console.WriteLine($"second instance: {(second is null ? "refused" : "created")}, first still started: {engine.IsStarted()}");

const int Frames = 120;
var quit = false;
timer.Restart();
var done = 0;
while (done < Frames && !quit)
{
    quit = engine.Iteration();
    done++;
}
Console.WriteLine($"iterations: {done} in {timer.ElapsedMilliseconds} ms, quit requested: {quit}");

engine.Dispose();
Console.WriteLine("destroyed");
return started ? 0 : 1;

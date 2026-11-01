using Orblit.Core;

namespace Orblit.Remote.Tests;

/// <summary>
/// A game with two entities and a loop of its own, which is the one place the gate is consulted.
/// The loop pumps a <see cref="GameLoopContext"/> once a frame, as a real game's would.
/// </summary>
internal sealed class Game : IAsyncDisposable
{
    private readonly CancellationTokenSource _stop = new();
    private readonly Thread _thread;
    private int _passed;

    private Game()
    {
        var transforms = World.RegisterTransforms();
        var a = World.CreateEntity();
        World.Add(a, transforms.Local, Transforms.Local(x: 4));
        var b = World.CreateEntity();
        World.Add(b, transforms.Local, Transforms.Local(y: 1));
        (A, B) = ((long)a, (long)b);
        Inspector = new CoreWorldInspector(World, [transforms.Local]);
        _thread = new Thread(Run) { IsBackground = true };
    }

    internal World World { get; } = new();

    internal TimeGate Gate { get; } = new();

    internal GameLoopContext Loop { get; } = new();

    internal CoreWorldInspector Inspector { get; }

    internal RemoteServer Server { get; private set; } = null!;

    internal long A { get; }

    internal long B { get; }

    internal int LoopThread => _thread.ManagedThreadId;

    /// <summary>Frames the loop has run with time passing.</summary>
    internal int Passed => Volatile.Read(ref _passed);

    /// <summary>The options every test uses, which keep the announce line off the console.</summary>
    internal static RemoteServerOptions Quiet(int seed = 7) =>
        new() { Announce = _ => { }, Random = new Random(seed), DebugBuild = true };

    internal static async Task<Game> Start(
        Func<RemoteHost, RemoteHost>? shape = null,
        RemoteServerOptions? options = null)
    {
        var game = new Game();
        var host = new RemoteHost { World = game.Inspector, Gate = game.Gate };
        game._thread.Start();
        game.Server = await RemoteServer.Start(shape?.Invoke(host) ?? host, game.Loop, options ?? Quiet());
        return game;
    }

    public async ValueTask DisposeAsync()
    {
        await Server.DisposeAsync();
        await _stop.CancelAsync();
        _thread.Join();
        World.Dispose();
        _stop.Dispose();
    }

    private void Run()
    {
        while (!_stop.IsCancellationRequested)
        {
            var delta = Gate.Advance(0.016);
            if (delta > 0)
            {
                Interlocked.Increment(ref _passed);
            }

            World.Tick(delta);
            Loop.Pump();
            Thread.Sleep(2);
        }
    }
}

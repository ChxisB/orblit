using Orblit.Input;
using Orblit.Scene;
using Orblit.VectorMath;

namespace Orblit.Game.Tests;

/// <summary>
/// The host is the one place a game is called from, so these are the guarantees every caller gets: the
/// game is started once, stopped once, and cannot take the caller down.
/// </summary>
public class GameHostTests
{
    private static readonly GameInput Idle = new(new FrameControls());

    [Fact]
    public void StartCallsTheGameOnceWithTheFolderAndTheScene()
    {
        var game = new ScriptedGame();
        var scene = World("a");
        var host = new GameHost(game, scene, "/project", new RecordingLog());

        host.Start();

        Assert.Equal(1, game.Starts);
        Assert.Equal("/project", game.Context!.ProjectFolder);
        Assert.Same(scene, game.Context.Scene.Document);
        Assert.True(host.Running);
    }

    [Fact]
    public void UpdateHandsOverTheSecondsAndTheInput()
    {
        var game = new ScriptedGame();
        var host = Started(game);
        var input = new GameInput(
            new FrameControls(["ArrowLeft"]),
            new GamePointer(new Vector3(0, 1, 0), new Vector3(0, -1, 0), Pressed: true, Wheel: 2));

        host.Update(input, 0.25);

        Assert.Equal([0.25], game.Seconds);
        Assert.Same(input, game.Context!.Input);
    }

    [Fact]
    public void AGameThatAddsAnEntityChangesTheSceneAndSaysSo()
    {
        var added = Add("b", after: "a");
        var game = new ScriptedGame { OnStart = context => context.Scene.Apply(added) };
        var host = new GameHost(game, World("a"), "/project", new RecordingLog());
        var heard = new List<SceneDiff>();
        host.SceneChanged += heard.Add;

        host.Start();

        Assert.Equal(["a", "b"], host.Scene.Entities.Select(entity => entity.Id));
        Assert.Same(added, Assert.Single(heard));
    }

    [Fact]
    public void AnEmptyDiffIsNotAChange()
    {
        var game = new ScriptedGame { OnUpdate = context => context.Scene.Apply(SceneDiff.None) };
        var host = Started(game);
        var heard = new List<SceneDiff>();
        host.SceneChanged += heard.Add;

        host.Update(Idle, 0.1);

        Assert.Empty(heard);
    }

    [Fact]
    public void AThrowInUpdateFaultsStopsTheGameOnceAndLogsIt()
    {
        var failure = new InvalidOperationException("no");
        var game = new ScriptedGame { OnUpdate = _ => throw failure };
        var log = new RecordingLog();
        var host = Started(game, log);

        host.Update(Idle, 0.1);

        Assert.Same(failure, host.Fault);
        Assert.False(host.Running);
        Assert.Equal(1, game.Stops);
        Assert.Same(failure, Assert.Single(log.Errors).Error);
        Assert.Contains("Update", log.Errors[0].Message);
    }

    [Fact]
    public void AFaultedHostIgnoresLaterCalls()
    {
        var game = new ScriptedGame { OnUpdate = _ => throw new InvalidOperationException() };
        var host = Started(game);
        host.Update(Idle, 0.1);

        host.Update(Idle, 0.1);
        host.Stop();

        Assert.Equal(1, game.Updates);
        Assert.Equal(1, game.Stops);
    }

    [Fact]
    public void AThrowInStartFaultsAndStopsTheGameToo()
    {
        var failure = new InvalidOperationException("no");
        var game = new ScriptedGame { OnStart = _ => throw failure };
        var log = new RecordingLog();
        var host = new GameHost(game, World(), "/project", log);

        host.Start();

        Assert.Same(failure, host.Fault);
        Assert.False(host.Running);
        Assert.Equal(1, game.Stops);
        Assert.Contains("Start", Assert.Single(log.Errors).Message);
    }

    [Fact]
    public void AThrowInStopIsLoggedAndIsNotAFault()
    {
        var game = new ScriptedGame { OnStop = _ => throw new InvalidOperationException() };
        var log = new RecordingLog();
        var host = Started(game, log);

        host.Stop();

        Assert.Null(host.Fault);
        Assert.False(host.Running);
        Assert.Contains("Stop", Assert.Single(log.Errors).Message);
    }

    [Fact]
    public void AFaultKeepsTheFirstThrowWhenStopThrowsToo()
    {
        var first = new InvalidOperationException("first");
        var game = new ScriptedGame
        {
            OnUpdate = _ => throw first,
            OnStop = _ => throw new InvalidOperationException("second"),
        };
        var log = new RecordingLog();
        var host = Started(game, log);

        host.Update(Idle, 0.1);

        Assert.Same(first, host.Fault);
        Assert.Equal(2, log.Errors.Count);
    }

    [Fact]
    public void StopCallsTheGameOnce()
    {
        var game = new ScriptedGame();
        var host = Started(game);

        host.Stop();
        host.Stop();

        Assert.Equal(1, game.Stops);
        Assert.False(host.Running);
    }

    [Fact]
    public void StopBeforeStartDoesNothing()
    {
        var game = new ScriptedGame();
        var host = new GameHost(game, World(), "/project", new RecordingLog());

        host.Stop();

        Assert.Equal(0, game.Stops);
        Assert.False(host.Running);
    }

    [Fact]
    public void StartingTwiceIsRefused()
    {
        var host = Started(new ScriptedGame());

        Assert.Throws<InvalidOperationException>(host.Start);
    }

    [Fact]
    public void UpdatingBeforeStartIsRefused()
    {
        var host = new GameHost(new ScriptedGame(), World(), "/project", new RecordingLog());

        Assert.Throws<InvalidOperationException>(() => host.Update(Idle, 0.1));
    }

    [Fact]
    public void ADiffAfterStopIsNotPassedOn()
    {
        GameContext? kept = null;
        var game = new ScriptedGame { OnStart = context => kept = context };
        var host = Started(game);
        var heard = new List<SceneDiff>();
        host.SceneChanged += heard.Add;
        host.Stop();

        // A timer the game forgot to stop can still call in after the host has let go.
        kept!.Scene.Apply(Add("late"));

        Assert.Empty(heard);
    }

    [Fact]
    public void GuardedCodeOfTheGameRunsWhileTheGameRuns()
    {
        var host = Started(new ScriptedGame());
        var ran = false;

        host.Guard("CreateInterface", () => ran = true);

        Assert.True(ran);
        Assert.True(host.Running);
    }

    [Fact]
    public void AThrowInGuardedCodeFaultsAsAThrowInUpdateDoes()
    {
        var failure = new InvalidOperationException("no");
        var game = new ScriptedGame();
        var log = new RecordingLog();
        var host = Started(game, log);

        host.Guard("CreateInterface", () => throw failure);

        Assert.Same(failure, host.Fault);
        Assert.False(host.Running);
        Assert.Equal(1, game.Stops);
        Assert.Contains("CreateInterface", Assert.Single(log.Errors).Message);
    }

    [Fact]
    public void GuardedCodeDoesNotRunBeforeStartOrAfterTheEnd()
    {
        var game = new ScriptedGame();
        var host = new GameHost(game, World(), "/project", new RecordingLog());
        var runs = 0;

        host.Guard("early", () => runs++);
        host.Start();
        host.Stop();
        host.Guard("late", () => runs++);

        Assert.Equal(0, runs);
    }

    [Fact]
    public void AnEditFromOutsideIsInTheGamesSceneAndIsNotHeardAsAChange()
    {
        var host = Started(new ScriptedGame());
        var heard = new List<SceneDiff>();
        host.SceneChanged += heard.Add;

        host.ApplyEdit(Add("b"));

        Assert.True(host.Scene.Contains("b"));
        Assert.Empty(heard);
    }

    [Fact]
    public void TheGameReadsAnEditFromOutsideOnItsNextUpdate()
    {
        var seen = new List<string>();
        var game = new ScriptedGame { OnUpdate = context => seen.AddRange(context.Scene.Document.Entities.Select(one => one.Id)) };
        var host = Started(game);

        host.ApplyEdit(Add("b"));
        host.Update(Idle, 0.1);

        Assert.Equal(["b"], seen);
    }

    [Fact]
    public void AnEditBeforeStartIsInTheSceneTheGameStartsWith()
    {
        var seen = new List<string>();
        var game = new ScriptedGame { OnStart = context => seen.AddRange(context.Scene.Document.Entities.Select(one => one.Id)) };
        var host = new GameHost(game, World(), "/project", new RecordingLog());

        host.ApplyEdit(Add("b"));
        host.Start();

        Assert.Equal(["b"], seen);
    }

    [Fact]
    public void AnEditAfterTheEndIsDropped()
    {
        var host = Started(new ScriptedGame());
        host.Stop();

        host.ApplyEdit(Add("late"));

        Assert.False(host.Scene.Contains("late"));
    }

    [Fact]
    public void AnEditThatNamesWhatTheGameDeletedChangesNothingAndDoesNotThrow()
    {
        var game = new ScriptedGame { OnStart = context => context.Scene.Apply(new SceneDiff([new RemoveEntity(new SceneEntity("a", "a").ToJson())])) };
        var host = new GameHost(game, World("a"), "/project", new RecordingLog());
        host.Start();

        host.ApplyEdit(new SceneDiff([new SetEntityName("a", "a", "door")]));

        Assert.False(host.Scene.Contains("a"));
    }

    [Fact]
    public void RefusesNothingToRun()
    {
        var game = new ScriptedGame();
        var log = new RecordingLog();

        Assert.Throws<ArgumentNullException>(() => new GameHost(null!, World(), "/project", log));
        Assert.Throws<ArgumentNullException>(() => new GameHost(game, null!, "/project", log));
        Assert.Throws<ArgumentNullException>(() => new GameHost(game, World(), "/project", null!));
        Assert.Throws<ArgumentException>(() => new GameHost(game, World(), "", log));
        Assert.Throws<ArgumentNullException>(() => new GameHost(game, World(), "/project", log).ApplyEdit(null!));
    }

    private static GameHost Started(ScriptedGame game, RecordingLog? log = null)
    {
        var host = new GameHost(game, World(), "/project", log ?? new RecordingLog());
        host.Start();
        return host;
    }

    private static SceneDocument World(params string[] ids) =>
        new(entities: [.. ids.Select(id => new SceneEntity(id, id))]);

    private static SceneDiff Add(string id, string? after = null) => new([new AddEntity(new SceneEntity(id, id).ToJson(), after)]);

    private sealed class ScriptedGame : IGame
    {
        public Action<GameContext>? OnStart { get; init; }

        public Action<GameContext>? OnUpdate { get; init; }

        public Action<GameContext>? OnStop { get; init; }

        public GameContext? Context { get; private set; }

        public int Starts { get; private set; }

        public int Updates { get; private set; }

        public int Stops { get; private set; }

        public List<double> Seconds { get; } = [];

        public void Start(GameContext context)
        {
            Context = context;
            Starts++;
            OnStart?.Invoke(context);
        }

        public void Update(GameContext context, double seconds)
        {
            Updates++;
            Seconds.Add(seconds);
            OnUpdate?.Invoke(context);
        }

        public void Stop(GameContext context)
        {
            Stops++;
            OnStop?.Invoke(context);
        }
    }

    private sealed class RecordingLog : IGameLog
    {
        public List<(string Message, Exception Error)> Errors { get; } = [];

        public void Say(string message)
        {
        }

        public void Error(string message, Exception error) => Errors.Add((message, error));
    }
}

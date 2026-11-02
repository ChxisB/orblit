using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Orblit.Stage;
using Orblit.Scene;

namespace Orblit.Game.Avalonia.Tests;

public sealed class GameWindowTests
{
    [Fact]
    public void ALongStallTakesNoMoreThanAFewTenthsOfASecond()
    {
        Assert.Equal(0.1, GameWindow.StepSince(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30)), 1e-9);
        Assert.Equal(0.016, GameWindow.StepSince(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1.016)), 1e-9);
    }

    [Fact]
    public void TheFirstFrameTakesNoTime() => Assert.Equal(0, GameWindow.StepSince(null, TimeSpan.FromSeconds(5)));

    [Fact]
    public Task OpeningStartsTheGameAndEachFrameUpdatesItAndDrawsTheScene() => Headless.Run(() =>
    {
        var run = Opened(new ScriptedGame());

        Assert.True(Headless.Until(() => run.Game.Seconds.Count >= 4));

        Assert.Equal(1, run.Game.Starts);
        Assert.All(run.Game.Seconds, seconds => Assert.InRange(seconds, 0, 0.1));
        Assert.Equal(run.Game.Seconds.Count, run.Screen.Drawn.Count);
        Assert.Equal(run.Screen.Drawn.Select(one => one.Seconds).Order(), run.Screen.Drawn.Select(one => one.Seconds));
        run.Window.Close();
    });

    [Fact]
    public Task WhatTheGameChangesIsWhatTheViewDraws() => Headless.Run(() =>
    {
        var added = new SceneDiff([new AddEntity(new SceneEntity("crate", "crate").ToJson())]);
        var game = new ScriptedGame { OnStart = context => context.Scene.Apply(added) };

        var run = Opened(game);

        Assert.True(run.View.Document.Contains("crate"));
        run.Window.Close();
    });

    [Fact]
    public Task AKeyDownReachesTheGameAsAControlAndKeyUpClearsIt() => Headless.Run(() =>
    {
        var run = Opened(new ScriptedGame());

        run.Window.KeyPressQwerty(PhysicalKey.W, RawInputModifiers.None);
        var down = Headless.Until(() => run.Game.Keys.Count > 0 && run.Game.Keys[^1].Contains("KeyW"));
        run.Window.KeyReleaseQwerty(PhysicalKey.W, RawInputModifiers.None);
        var up = Headless.Until(() => run.Game.Keys.Count > 0 && run.Game.Keys[^1].Count == 0);

        Assert.True(down);
        Assert.True(up);
        run.Window.Close();
    });

    [Fact]
    public Task AClickGivesOneUpdateThatIsPressedAndThenNone() => Headless.Run(() =>
    {
        var run = Opened(new ScriptedGame());
        Headless.Until(() => run.Game.Seconds.Count >= 2);
        var at = Headless.Centre(run.Window, run.Screen.View);

        run.Window.MouseDown(at, MouseButton.Left);
        run.Window.MouseUp(at, MouseButton.Left);
        var frames = run.Game.Seconds.Count;
        Headless.Until(() => run.Game.Seconds.Count >= frames + 3);

        Assert.Equal(1, run.Game.Presses);
        run.Window.Close();
    });

    [Fact]
    public Task AnInterfaceGamesControlIsOverTheViewWhileItRunsAndGoneAfterClose() => Headless.Run(() =>
    {
        var panel = new Border { Name = "Hud" };
        var run = Opened(new ScriptedGame(), () => panel);

        Assert.Contains(panel, Headless.Shown<Border>(run.Window));

        run.Window.Close();

        Assert.Null(panel.Parent);
        Assert.Equal(1, run.Game.Stops);
    });

    [Fact]
    public Task AThrowInUpdateShowsTheTextAndStopsAskingForFrames() => Headless.Run(() =>
    {
        var game = new ScriptedGame { OnUpdate = _ => throw new InvalidOperationException("the crate fell through") };
        var run = Opened(game);

        Assert.True(Headless.Until(() => run.Host.Fault is not null));

        var text = Headless.Find<TextBlock>(run.Window, "Fault");
        Assert.Contains("the crate fell through", text.Text);
        Assert.Null(run.Screen.View.Parent);
        var updates = game.Seconds.Count;
        Headless.Wait(100);
        Assert.Equal(updates, game.Seconds.Count);
        Assert.Equal(1, game.Stops);
        run.Window.Close();
    });

    [Fact]
    public Task AThrowWhileAskingForTheInterfaceShowsTheTextToo() => Headless.Run(() =>
    {
        var run = Opened(new ScriptedGame(), () => throw new InvalidOperationException("no hud"));

        Assert.Contains("no hud", Headless.Find<TextBlock>(run.Window, "Fault").Text);
        Assert.False(run.Host.Running);
        run.Window.Close();
    });

    [Fact]
    public Task ClosingStopsTheGameOnceAndFramesStopComing() => Headless.Run(() =>
    {
        var run = Opened(new ScriptedGame());
        Headless.Until(() => run.Game.Seconds.Count >= 2);

        run.Window.Close();
        var updates = run.Game.Seconds.Count;
        Headless.Wait(100);

        Assert.Equal(1, run.Game.Stops);
        Assert.Equal(updates, run.Game.Seconds.Count);
    });

    private static Run Opened(ScriptedGame game, Func<Control>? interfaceOf = null)
    {
        var view = new DocumentView(new SceneDocument());
        var host = new GameHost(game, view.Document, "/project", new SilentLog());
        var screen = new RecordedScreen();
        var window = new GameWindow(host, view, screen, interfaceOf) { Width = 300, Height = 200 };
        window.Show();
        window.UpdateLayout();
        return new Run(window, host, view, screen, game);
    }

    private sealed record Run(GameWindow Window, GameHost Host, DocumentView View, RecordedScreen Screen, ScriptedGame Game);

    private sealed class ScriptedGame : IGame
    {
        public Action<GameContext>? OnStart { get; init; }

        public Action<GameContext>? OnUpdate { get; init; }

        public int Starts { get; private set; }

        public int Stops { get; private set; }

        public int Presses { get; private set; }

        public List<double> Seconds { get; } = [];

        public List<IReadOnlyCollection<string>> Keys { get; } = [];

        public void Start(GameContext context)
        {
            Starts++;
            OnStart?.Invoke(context);
        }

        public void Update(GameContext context, double seconds)
        {
            Seconds.Add(seconds);
            Keys.Add(context.Input.Controls.Keys);
            Presses += context.Input.Pointer is { Pressed: true } ? 1 : 0;
            OnUpdate?.Invoke(context);
        }

        public void Stop(GameContext context) => Stops++;
    }

    private sealed class SilentLog : IGameLog
    {
        public void Say(string message)
        {
        }

        public void Error(string message, Exception error)
        {
        }
    }
}

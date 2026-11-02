using Orblit.Input;
using Orblit.Scene;

namespace Orblit.Game.Tests;

/// <summary>
/// A game asks its context what role it plays. These are the answers it gets with no network, with
/// one, and the rule that the network is chosen before the game starts and not during it.
/// </summary>
public class GameNetTests
{
    private static readonly GameInput Idle = new(new FrameControls());

    [Fact]
    public void ADefaultContextIsOfflineWithNoClientAndNoOwners()
    {
        var game = new ScriptedGame();
        var host = new GameHost(game, new SceneDocument(), "/project", new SilentLog());

        host.Start();

        var net = game.Context!.Net;
        Assert.Equal(GameRole.Offline, net.Role);
        Assert.False(net.IsServer);
        Assert.False(net.IsClient);
        Assert.Null(net.LocalClient);
        Assert.Null(net.OwnerOf("anything"));
    }

    [Fact]
    public void EachHostHasItsOwnOfflineNetSoTwoGamesShareNothing()
    {
        var first = new ScriptedGame();
        var second = new ScriptedGame();
        new GameHost(first, new SceneDocument(), "/project", new SilentLog()).Start();
        new GameHost(second, new SceneDocument(), "/project", new SilentLog()).Start();

        Assert.IsType<OfflineNet>(first.Context!.Net);
        Assert.NotSame(first.Context.Net, second.Context!.Net);
    }

    [Fact]
    public void ASetNetIsWhatTheGameSeesInStartAndInUpdate()
    {
        var net = new FakeNet(GameRole.Server, "host", "p1");
        var game = new ScriptedGame();
        var host = new GameHost(game, new SceneDocument(), "/project", new SilentLog());

        host.UseNet(net);
        host.Start();
        host.Update(Idle, 0.1);

        Assert.Same(net, game.NetAtStart);
        Assert.Same(net, game.NetInUpdate);
        Assert.Equal("p1", game.NetInUpdate!.OwnerOf("crate"));
    }

    [Fact]
    public void TheNetIsHandedTheSceneTheGamePlaysIn()
    {
        var net = new FakeNet(GameRole.Client, "me", null);
        var game = new ScriptedGame();
        var host = new GameHost(game, new SceneDocument(), "/project", new SilentLog());

        host.UseNet(net);
        host.Start();

        Assert.Same(game.Context!.Scene, net.Scene);
    }

    [Fact]
    public void TheNetRunsBeforeAndAfterEveryUpdate()
    {
        var calls = new List<string>();
        var net = new FakeNet(GameRole.Server, "host", null) { Calls = calls };
        var game = new ScriptedGame { Calls = calls };
        var host = new GameHost(game, new SceneDocument(), "/project", new SilentLog());
        host.UseNet(net);
        host.Start();

        host.Update(Idle, 0.1);
        host.Update(Idle, 0.1);

        Assert.Equal(["before", "update", "after", "before", "update", "after"], calls);
    }

    [Fact]
    public void ANetThatThrowsEndsTheGame()
    {
        var failure = new InvalidOperationException("link down");
        var net = new FakeNet(GameRole.Client, "me", null) { OnBefore = () => throw failure };
        var game = new ScriptedGame();
        var host = new GameHost(game, new SceneDocument(), "/project", new SilentLog());
        host.UseNet(net);
        host.Start();

        host.Update(Idle, 0.1);

        Assert.Same(failure, host.Fault);
        Assert.False(host.Running);
        Assert.Null(game.NetInUpdate);
    }

    [Fact]
    public void TheNetIsNotCalledOnceTheGameHasEnded()
    {
        var calls = new List<string>();
        var net = new FakeNet(GameRole.Server, "host", null) { Calls = calls };
        var host = new GameHost(new ScriptedGame(), new SceneDocument(), "/project", new SilentLog());
        host.UseNet(net);
        host.Start();
        host.Stop();

        host.Update(Idle, 0.1);

        Assert.Empty(calls);
    }

    [Theory]
    [InlineData(GameRole.Offline, false, false)]
    [InlineData(GameRole.Server, true, false)]
    [InlineData(GameRole.Client, false, true)]
    public void TheRoleFlagsFollowTheRole(GameRole role, bool server, bool client)
    {
        IGameNet net = new FakeNet(role, null, null);

        Assert.Equal(server, net.IsServer);
        Assert.Equal(client, net.IsClient);
    }

    [Fact]
    public void ANetCannotBeSetOnceTheGameHasStarted()
    {
        var host = new GameHost(new ScriptedGame(), new SceneDocument(), "/project", new SilentLog());
        host.Start();

        Assert.Throws<InvalidOperationException>(() => host.UseNet(new FakeNet(GameRole.Client, "me", null)));
    }

    [Fact]
    public void ANetCannotBeSetAfterTheGameHasEnded()
    {
        var host = new GameHost(new ScriptedGame(), new SceneDocument(), "/project", new SilentLog());
        host.Start();
        host.Stop();

        Assert.Throws<InvalidOperationException>(() => host.UseNet(new FakeNet(GameRole.Client, "me", null)));
    }

    [Fact]
    public void AFailedSetLeavesTheGameOnItsFirstNet()
    {
        var first = new FakeNet(GameRole.Server, "host", null);
        var game = new ScriptedGame();
        var host = new GameHost(game, new SceneDocument(), "/project", new SilentLog());
        host.UseNet(first);
        host.Start();

        Assert.Throws<InvalidOperationException>(() => host.UseNet(new FakeNet(GameRole.Client, "me", null)));

        Assert.Same(first, game.Context!.Net);
    }

    [Fact]
    public void RefusesANullNet()
    {
        var host = new GameHost(new ScriptedGame(), new SceneDocument(), "/project", new SilentLog());

        Assert.Throws<ArgumentNullException>(() => host.UseNet(null!));
    }

    private sealed class FakeNet(GameRole role, string? local, string? owner) : IGameNet
    {
        public GameRole Role { get; } = role;

        public string? LocalClient { get; } = local;

        public string? OwnerOf(string entityId) => entityId == "crate" ? owner : null;

        public List<string>? Calls { get; init; }

        public Action? OnBefore { get; init; }

        public GameScene? Scene { get; private set; }

        public void Attach(GameScene scene) => Scene = scene;

        public void BeforeUpdate()
        {
            Calls?.Add("before");
            OnBefore?.Invoke();
        }

        public void AfterUpdate() => Calls?.Add("after");
    }

    private sealed class ScriptedGame : IGame
    {
        public GameContext? Context { get; private set; }

        public List<string>? Calls { get; init; }

        public IGameNet? NetAtStart { get; private set; }

        public IGameNet? NetInUpdate { get; private set; }

        public void Start(GameContext context)
        {
            Context = context;
            NetAtStart = context.Net;
        }

        public void Update(GameContext context, double seconds)
        {
            Calls?.Add("update");
            NetInUpdate = context.Net;
        }

        public void Stop(GameContext context)
        {
        }
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

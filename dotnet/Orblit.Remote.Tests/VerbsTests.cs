using Orblit.Json;
using static Orblit.Remote.Tests.Rpc;

namespace Orblit.Remote.Tests;

public sealed class VerbsTests
{
    private readonly FakeWorld _world = new();
    private readonly RemoteVerbs _verbs;

    public VerbsTests() => _verbs = new RemoteVerbs(new RemoteHost { World = _world });

    private static RemoteVerbs With(RemoteHost host) => new(host);

    private static IReadOnlyDictionary<string, object?> ResultOf(IReadOnlyDictionary<string, object?> reply) =>
        (IReadOnlyDictionary<string, object?>)reply["result"]!;

    [Fact]
    public async Task WorldEntitiesListsEveryEntityOrThoseWithTheComponents()
    {
        var all = await Call(_verbs, "world.entities");
        Assert.Equal(
            """[{"id":1,"components":["Position"]},{"id":2,"components":["Position","Tag"]}]""",
            Result(all));

        var tagged = await Call(_verbs, "world.entities", """{"filter":["Tag"]}""");
        Assert.Single((IReadOnlyList<object?>)tagged["result"]!);
    }

    [Fact]
    public async Task EntityGetAnswersALiveEntityAndRefusesADeadOne()
    {
        var live = await Call(_verbs, "entity.get", """{"id":1}""");
        Assert.Equal("""{"id":1,"components":{"Position":{"x":1,"y":2}}}""", Result(live));
        Assert.Equal(RemoteException.NotFound, CodeOf(await Call(_verbs, "entity.get", """{"id":7}""")));
    }

    [Fact]
    public async Task EntitySetChangesOneFieldAndRefusesABadOne()
    {
        await Call(_verbs, "entity.set", """{"id":1,"component":"Position","field":"y","value":8}""");
        Assert.Equal(8.0, _world.Held[1]["Position"]["y"]);

        var bad = await Call(_verbs, "entity.set", """{"id":1,"component":"Position","field":"nope","value":8}""");
        Assert.Equal(RemoteException.InvalidParams, CodeOf(bad));
        var text = await Call(_verbs, "entity.set", """{"id":1,"component":"Position","field":"y","value":"high"}""");
        Assert.Equal(RemoteException.InvalidParams, CodeOf(text));
        var dead = await Call(_verbs, "entity.set", """{"id":7,"component":"Position","field":"y","value":1}""");
        Assert.Equal(RemoteException.NotFound, CodeOf(dead));
    }

    [Fact]
    public async Task AReadOnlySessionRefusesAWriteAndStillReads()
    {
        var locked = With(new RemoteHost { World = _world, ReadOnly = true, Input = _ => Task.CompletedTask });

        var set = await Call(locked, "entity.set", """{"id":1,"component":"Position","field":"y","value":8}""");
        Assert.Equal(RemoteException.ReadOnly, CodeOf(set));
        Assert.Equal(2L, _world.Held[1]["Position"]["y"]);
        Assert.Equal(RemoteException.ReadOnly, CodeOf(await Call(locked, "input.send", """{"event":{}}""")));
        Assert.True((await Call(locked, "entity.get", """{"id":1}""")).ContainsKey("result"));
    }

    [Fact]
    public async Task ADiffNamesExactlyTheFieldThatChanged()
    {
        var before = ResultOf(await Call(_verbs, "world.snapshot"));
        _world.Held[2]["Position"]["y"] = 40L;
        var after = ResultOf(await Call(_verbs, "world.snapshot"));

        var diff = await Call(_verbs, "world.diff", JsonText.Encode(Row(("a", before["id"]), ("b", after["id"]))));

        Assert.Equal("""[{"path":"entities/2/Position/y","from":4,"to":40}]""", Result(diff));
    }

    [Fact]
    public async Task ASnapshotCarriesTheExtrasTheHostGave()
    {
        var host = new RemoteHost
        {
            World = _world,
            Extras = new Dictionary<string, Func<IReadOnlyDictionary<string, object?>>>
            {
                ["physics"] = () => Row(("bodies", 3L)),
            },
        };

        var snap = ResultOf(await Call(With(host), "world.snapshot"));

        Assert.Equal("""{"physics":{"bodies":3}}""", JsonText.Encode(snap["extras"]));
    }

    [Fact]
    public async Task ASnapshotIsACopyOfWhatTheHostGave()
    {
        var shared = Row(("bodies", 3L));
        var host = new RemoteHost
        {
            World = _world,
            Extras = new Dictionary<string, Func<IReadOnlyDictionary<string, object?>>> { ["physics"] = () => shared },
        };
        var copying = With(host);
        var first = ResultOf(await Call(copying, "world.snapshot"));
        shared["bodies"] = 4L;
        var second = ResultOf(await Call(copying, "world.snapshot"));

        var diff = await Call(copying, "world.diff", JsonText.Encode(Row(("a", first["id"]), ("b", second["id"]))));

        Assert.Equal("""[{"path":"extras/physics/bodies","from":3,"to":4}]""", Result(diff));
    }

    [Fact]
    public async Task AFloatJsonCannotWriteIsKeptAsItsName()
    {
        var host = new RemoteHost
        {
            World = _world,
            Extras = new Dictionary<string, Func<IReadOnlyDictionary<string, object?>>>
            {
                ["physics"] = () => Row(("speed", double.NaN)),
            },
        };

        var snap = ResultOf(await Call(With(host), "world.snapshot"));

        Assert.Equal("""{"physics":{"speed":"NaN"}}""", JsonText.Encode(snap["extras"]));
    }

    [Fact]
    public async Task ASnapshotThatCannotBeWrittenIsNotKept()
    {
        var broken = true;
        var host = new RemoteHost
        {
            World = _world,
            Extras = new Dictionary<string, Func<IReadOnlyDictionary<string, object?>>>
            {
                ["physics"] = () => Row(("body", broken ? new object() : 1L)),
            },
        };
        var flaky = With(host);
        Assert.Equal(RemoteException.Internal, CodeOf(await Call(flaky, "world.snapshot")));

        broken = false;

        Assert.Equal("s1", ResultOf(await Call(flaky, "world.snapshot"))["id"]);
    }

    [Fact]
    public async Task ADiffOfASnapshotNobodyTookIsRefused()
    {
        var diff = await Call(_verbs, "world.diff", """{"a":"s1","b":"s2"}""");

        Assert.Equal(RemoteException.NotFound, CodeOf(diff));
    }

    [Fact]
    public async Task PauseAndResumeMoveTheGate()
    {
        var gate = new TimeGate();
        var timed = With(new RemoteHost { Gate = gate });

        Assert.Equal("""{"paused":true}""", Result(await Call(timed, "time.pause")));
        Assert.True(gate.Paused);
        Assert.Equal("""{"paused":false}""", Result(await Call(timed, "time.resume")));
        Assert.False(gate.Paused);
    }

    [Fact]
    public async Task StepAnswersOnceTheLoopHasPassedTheFrames()
    {
        var gate = new TimeGate();
        var timed = With(new RemoteHost { Gate = gate });
        var reply = Call(timed, "time.step", """{"n":3}""");
        Assert.Equal(3, gate.Pending);

        for (var i = 0; i < 3; i++)
        {
            gate.Advance(0.5);
        }

        Assert.Equal("""{"paused":true,"stepped":3}""", Result(await reply));
    }

    [Fact]
    public async Task StepGivesUpWhenNoLoopPassesTheFrames()
    {
        var gate = new TimeGate();
        var timed = With(new RemoteHost { Gate = gate, Timeout = TimeSpan.FromMilliseconds(20) });

        var reply = await Call(timed, "time.step", """{"n":2}""");

        Assert.Equal(RemoteException.TimedOut, CodeOf(reply));
        Assert.Equal(0, gate.Pending);
        Assert.True(gate.Paused);
    }

    [Fact]
    public async Task StepKeepsWaitingWhileFramesKeepPassing()
    {
        var gate = new TimeGate();
        var timed = With(new RemoteHost { Gate = gate, Timeout = TimeSpan.FromMilliseconds(400) });
        var reply = Call(timed, "time.step", """{"n":4}""");

        for (var i = 0; i < 4; i++)
        {
            await Task.Delay(50, TestContext.Current.CancellationToken);
            gate.Advance(0.5);
        }

        Assert.Equal("""{"paused":true,"stepped":4}""", Result(await reply));
    }

    [Fact]
    public async Task StepSaysHowManyFramesPassedWhenItIsCutShort()
    {
        var gate = new TimeGate();
        var timed = With(new RemoteHost { Gate = gate });
        var reply = Call(timed, "time.step", """{"n":5}""");
        gate.Advance(0.5);
        gate.Advance(0.5);
        gate.Pause();

        Assert.Equal("""{"paused":true,"stepped":2}""", Result(await reply));
    }

    [Fact]
    public async Task StepRefusesFewerThanOneFrame()
    {
        var timed = With(new RemoteHost { Gate = new TimeGate() });

        Assert.Equal(RemoteException.InvalidParams, CodeOf(await Call(timed, "time.step", """{"n":0}""")));
    }

    [Fact]
    public async Task LogTailAnswersTheLastLinesFiftyByDefault()
    {
        var log = new LogTail();
        log.Add("one");
        log.Add("two");
        log.Add("three");
        var logged = With(new RemoteHost { Log = log });

        Assert.Equal("""["two","three"]""", Result(await Call(logged, "log.tail", """{"n":2}""")));
        Assert.Equal("""["one","two","three"]""", Result(await Call(logged, "log.tail")));
        Assert.Equal(RemoteException.InvalidParams, CodeOf(await Call(logged, "log.tail", """{"n":-1}""")));
    }

    [Fact]
    public async Task ALogCountPastThirtyTwoBitsAnswersEveryLine()
    {
        var log = new LogTail();
        log.Add("one");

        var reply = await Call(With(new RemoteHost { Log = log }), "log.tail", """{"n":99999999999}""");

        Assert.Equal("""["one"]""", Result(reply));
    }

    [Fact]
    public async Task InputSendHandsTheEventToTheGame()
    {
        var seen = new List<IReadOnlyDictionary<string, object?>>();
        var sent = With(new RemoteHost
        {
            Input = sent =>
            {
                seen.Add(sent);
                return Task.CompletedTask;
            },
        });

        await Call(sent, "input.send", """{"event":{"key":"w"}}""");

        Assert.Equal("""{"key":"w"}""", JsonText.Encode(Assert.Single(seen)));
        Assert.Equal(RemoteException.InvalidParams, CodeOf(await Call(sent, "input.send", """{"event":"w"}""")));
    }

    [Fact]
    public async Task ViewScreenshotAnswersThePngAsBase64()
    {
        var shot = With(new RemoteHost { Screenshot = () => Task.FromResult<byte[]>([137, 80, 78, 71]) });

        Assert.Equal("""{"png":"iVBORw=="}""", Result(await Call(shot, "view.screenshot")));
    }

    [Fact]
    public async Task PerfSummaryAnswersWhatTheHostReports()
    {
        var timed = With(new RemoteHost { Perf = () => Row(("tickMs", 1.5)) });

        Assert.Equal("""{"tickMs":1.5}""", Result(await Call(timed, "perf.summary")));
    }

    [Fact]
    public async Task AVerbNeedsThePartItReadsAndSaysSoWhenItIsMissing()
    {
        var bare = With(new RemoteHost());

        foreach (var method in new[] { "world.entities", "time.pause", "view.screenshot", "log.tail", "perf.summary" })
        {
            Assert.True(RemoteException.Unavailable == CodeOf(await Call(bare, method)), method);
        }
    }

    [Fact]
    public async Task ACallbackThatFailsByItselfIsInternalNotABadRequest()
    {
        var broken = With(new RemoteHost
        {
            Input = _ => throw new ArgumentException("the game broke"),
            Screenshot = () => throw new InvalidOperationException("no surface"),
            Perf = () => throw new InvalidOperationException("no clock"),
        });

        Assert.Equal(RemoteException.Internal, CodeOf(await Call(broken, "input.send", """{"event":{}}""")));
        Assert.Equal(RemoteException.Internal, CodeOf(await Call(broken, "view.screenshot")));
        Assert.Equal(RemoteException.Internal, CodeOf(await Call(broken, "perf.summary")));
    }

    [Fact]
    public async Task ACallbackThatNeverAnswersTimesOut()
    {
        var hung = With(new RemoteHost
        {
            Timeout = TimeSpan.FromMilliseconds(20),
            Input = _ => new TaskCompletionSource().Task,
            Screenshot = () => new TaskCompletionSource<byte[]>().Task,
        });

        Assert.Equal(RemoteException.TimedOut, CodeOf(await Call(hung, "input.send", """{"event":{}}""")));
        Assert.Equal(RemoteException.TimedOut, CodeOf(await Call(hung, "view.screenshot")));
    }

    [Fact]
    public async Task AnInputSinkThatAnswersLaterIsWaitedFor()
    {
        var seen = new List<IReadOnlyDictionary<string, object?>>();
        var slow = With(new RemoteHost
        {
            Input = async sent =>
            {
                await Task.Delay(10, TestContext.Current.CancellationToken);
                seen.Add(sent);
            },
        });

        var reply = await Call(slow, "input.send", """{"event":{"key":"w"}}""");

        Assert.Null(reply["result"]);
        Assert.Equal("""{"key":"w"}""", JsonText.Encode(Assert.Single(seen)));
    }

    [Fact]
    public void ATimeoutOfZeroIsRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RemoteHost { Timeout = TimeSpan.Zero });
    }

    [Fact]
    public async Task AHostCallbackThatThrowsIsAnsweredNotRethrown()
    {
        var broken = With(new RemoteHost { World = _world, Perf = () => throw new FormatException("boom") });

        Assert.Equal(RemoteException.Internal, CodeOf(await Call(broken, "perf.summary")));
        Assert.True((await Call(broken, "entity.get", """{"id":1}""")).ContainsKey("result"));
    }

    [Fact]
    public async Task AnUnknownMethodIsRefused()
    {
        Assert.Equal(RemoteException.NoMethod, CodeOf(await Call(_verbs, "world.fly")));
    }

    [Fact]
    public async Task TextThatIsNotJsonIsAnsweredWithANullId()
    {
        var reply = Decode((await _verbs.HandleAsync("{"))!);

        Assert.Equal(RemoteException.Parse, CodeOf(reply));
        Assert.True(reply.ContainsKey("id"));
        Assert.Null(reply["id"]);
    }

    [Fact]
    public async Task ARequestThatIsNotAnObjectOrHasABadIdIsRefused()
    {
        foreach (var bad in new[] { "[1]", "7", """{"id":1.5,"method":"log.tail"}""" })
        {
            var reply = Decode((await _verbs.HandleAsync(bad))!);
            Assert.True(RemoteException.InvalidRequest == CodeOf(reply), bad);
        }
    }

    [Fact]
    public async Task ParamsAsAListAreRefused()
    {
        Assert.Equal(RemoteException.InvalidParams, CodeOf(await Call(_verbs, "entity.get", "[1]")));
    }

    [Fact]
    public async Task ANotificationIsRunAndNeverAnswered()
    {
        var reply = await _verbs.HandleAsync(
            """{"jsonrpc":"2.0","method":"entity.set","params":{"id":1,"component":"Position","field":"x","value":5}}""");

        Assert.Null(reply);
        Assert.Equal(5.0, _world.Held[1]["Position"]["x"]);
    }
}

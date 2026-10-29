using Orblit.Filament;
using Orblit.Input;
using Orblit.Test;
using Orblit.VectorMath;

namespace Orblit.Examples.Tests;

/// <summary>
/// The Runner played frame by frame, by its autopilot and by nobody, with no
/// renderer. Its two tests through a <see cref="TestStage"/> are here too.
/// </summary>
public sealed class RunnerTests
{
    private const double Step = 1 / 60.0;

    // Running, the body bobs between 0.73 and 0.86 and a slide drops it to
    // 0.53. A jump takes it past two. Anything above a metre is in the air.
    private const double Airborne = 1.0;

    private static readonly RenderCamera Camera = new ViewPoint().ToRenderCamera();

    [Fact]
    public void TheAutopilotGetsThroughAMinuteOfEveryRunItStarts()
    {
        // Which is the test that the game is fair: every row it lays has a way
        // through, found by something that only has the player's three moves.
        var game = Game();
        game.Autopilot = true;
        for (var run = 0; run < 3; run++)
        {
            game.Restart();
            for (var i = 0; i < 60 * 60; i++)
            {
                game.Update(Step);
                Assert.True(game.Phase == RunnerPhase.Running, $"Run {run + 1} ended at {Math.Round(game.Travelled)} m, {i * Step:F1} s in.");
            }

            Assert.True(game.Travelled > 900, $"{game.Travelled} m.");
            Assert.True(game.Coins > 20, $"{game.Coins} coins.");
            // Coins taken one after another are worth more than one each.
            Assert.True(game.Score - Math.Floor(game.Travelled) > game.Coins, $"Score {game.Score} for {game.Travelled} m and {game.Coins} coins.");
        }
    }

    [Fact]
    public void TheAutopilotGoesOverAHurdleRatherThanRoundIt()
    {
        // A hurdle is there to be jumped. Stepping into the next lane clears it
        // just as well and costs nothing, so an autopilot that only counts
        // coins always sidesteps. From behind the runner that reads as running
        // away from the thing rather than over it. This watches what the
        // renderer would have drawn: where the hurdles are, where the runner
        // is, and how high off the ground.
        // A renderer that keeps nothing, so the models reach the scene. One
        // game restarted, because a new game is always the same first run.
        var game = new RunnerExample((_, _) => { }, _ => null) { Autopilot = true };
        game.Scene(Camera, 0);
        var passes = new List<(bool SameLane, bool Up)>();
        for (var run = 0; run < 4; run++)
        {
            game.Restart();
            passes.AddRange(Hurdles(game, run * 60.0));
        }

        var mine = passes.Count;
        var round = passes.Count(pass => !pass.SameLane);
        var over = passes.Count(pass => pass is { SameLane: true, Up: true });
        Assert.True(mine > 15, "Too few hurdles to say anything.");
        Assert.True(over + round == mine, "A hurdle was run into.");
        Assert.True(over > mine * 2 / 3, $"Only {over} of {mine} hurdles were jumped. {round} were dodged.");
    }

    [Fact]
    public void ARunNobodySteersEndsInACrash()
    {
        var game = Game();
        game.Start();
        var seconds = 0.0;
        while (game.Phase == RunnerPhase.Running && seconds < 60)
        {
            game.Update(Step);
            seconds += Step;
        }

        Assert.Equal(RunnerPhase.Over, game.Phase);
        Assert.Equal(game.Score, game.Best);

        // And the score stops when the run does, rather than counting on while
        // the runner tumbles.
        var score = game.Score;
        for (var i = 0; i < 120; i++)
        {
            game.Update(Step);
        }

        Assert.Equal(score, game.Score);
    }

    [Fact]
    public void AHurdleNobodyJumpsEndsTheRun()
    {
        // The autopilot plays until a hurdle is coming in its lane, then lets
        // go. The runner reaches it in under 0.75 s, and the next row is at
        // least 14 m past it, so a crash in that time is the hurdle's.
        var game = new RunnerExample((_, _) => { }, _ => null) { Autopilot = true };
        game.Start();
        var frame = 0;
        while (game.Autopilot && frame < 60 * 60)
        {
            var objects = game.Scene(Camera, ++frame * Step).Objects;
            var body = objects.First(one => one.Key == 1).Transform.GetTranslation();
            game.Autopilot = !objects.Any(one => one.Key is >= 2000 and < 2200 && Coming(body, one.Transform.GetTranslation()));
        }

        Assert.False(game.Autopilot, "No hurdle came.");
        for (var i = 0; i < 45; i++)
        {
            game.Scene(Camera, ++frame * Step);
        }

        Assert.Equal(RunnerPhase.Over, game.Phase);
    }

    [Fact]
    public void WithCrashingTurnedOffNothingEndsTheRun()
    {
        var game = Game();
        game.Invincible = true;
        game.Start();
        for (var i = 0; i < 60 * 60; i++)
        {
            game.Update(Step);
        }

        Assert.Equal(RunnerPhase.Running, game.Phase);
    }

    [Fact]
    public void AJumpPressedJustBeforeLandingIsJumpedOnLanding()
    {
        // A jump lasts 0.63 s. The second press comes 0.08 s before the first
        // one lands, and the runner is looked at 0.2 s after it lands. The
        // first row is 60 m out, so nothing is in the way yet. The body is a
        // model, so it takes a renderer that keeps nothing to draw it.
        var game = new RunnerExample((_, _) => { }, _ => null);
        game.Start();
        var frame = 0;
        double Height() => game.Scene(Camera, ++frame * Step).Objects.First(one => one.Key == 1).Transform.GetTranslation().Y;

        Height();
        game.Jump();
        for (var i = 0; i < 33; i++)
        {
            Height();
        }

        Assert.True(Height() > Airborne, "The first jump came down early.");
        game.Jump();
        for (var i = 0; i < 16; i++)
        {
            Height();
        }

        Assert.True(Height() > Airborne, "The press before landing was lost.");
    }

    [Fact]
    public void TheWholeSceneIsDescribedFrameAfterFrame()
    {
        var game = Game();
        // Before the models have reached the renderer, which in a test they
        // never do, only what needs no file is drawn.
        var first = game.Scene(Camera, 0);
        Assert.DoesNotContain(first.Objects, one => one.Mesh is not null);
        Assert.Equal(11, first.Populations.Count);

        game.Autopilot = true;
        game.Start();
        for (var i = 1; i <= 600; i++)
        {
            var keys = game.Scene(Camera, i * Step).Objects.Select(one => one.Key).ToList();
            Assert.True(keys.Distinct().Count() == keys.Count, $"Frame {i} has a key twice.");
        }
    }

    [Fact]
    public void ATallWindowWidensTheViewSoTheLanesStillFit()
    {
        // At the line the camera is round in front, so they are compared once
        // the run is under way.
        var wide = Game();
        var tall = Game();
        tall.Aspect = 9.0 / 16;
        foreach (var game in new[] { wide, tall })
        {
            game.Autopilot = true;
            game.Start();
            for (var i = 0; i < 60; i++)
            {
                game.Update(Step);
            }
        }

        Assert.True(tall.Scene(Camera, 0).Camera.FieldOfView > wide.Scene(Camera, 0).Camera.FieldOfView);
    }

    [Fact]
    public void TheRunnerWaitsAtTheLineThenTheAutopilotSetsOff()
    {
        var game = Game();
        game.Autopilot = true;
        var stage = new TestStage(frame => game.Update(frame.Dt));

        stage.Step(TimeSpan.FromMilliseconds(500));
        Assert.Equal(RunnerPhase.Ready, game.Phase);

        stage.StepUntil(() => game.Phase == RunnerPhase.Running, TimeSpan.FromSeconds(2));
        Assert.Equal(0.0, game.Travelled);

        stage.Step(TimeSpan.FromSeconds(5));
        Assert.True(game.Travelled > 20, $"{game.Travelled} m.");
    }

    [Fact]
    public void APauseKeyHeldThroughAnActionMapStopsTheRunnerAndFreesIt()
    {
        // The runner has no key of its own. This frame callback is the game's
        // part, and the test covers the map, the stage and the pause and resume.
        var escape = new KeyControl("Escape");
        var map = new ActionMap(
            [InputAction.Button("pause")],
            [new InputContext("play", new Dictionary<string, IReadOnlyList<Binding>>
            {
                ["pause"] = [new Binding(escape)],
            })],
            active: ["play"]);
        var game = Game();
        game.Autopilot = true;
        game.Restart();
        var stage = new TestStage(
            frame =>
            {
                if (map.WasPressed("pause") && game.Phase == RunnerPhase.Paused)
                {
                    game.Resume();
                }
                else if (map.WasPressed("pause"))
                {
                    game.Pause();
                }

                game.Update(frame.Dt);
            },
            actions: map);

        stage.Step(TimeSpan.FromSeconds(2));
        Tap(stage, escape);
        var frozen = game.Travelled;
        Assert.Equal(RunnerPhase.Paused, game.Phase);
        Assert.True(frozen > 0);

        stage.Step(TimeSpan.FromSeconds(2));
        Assert.Equal(frozen, game.Travelled);

        Tap(stage, escape);
        stage.Step(TimeSpan.FromSeconds(1));
        Assert.Equal(RunnerPhase.Running, game.Phase);
        Assert.True(game.Travelled > frozen);
    }

    /// <summary>A game with no renderer to hand its models to, so it never draws them.</summary>
    private static RunnerExample Game() => new(ExampleFixture.NoRenderer, _ => null);

    private static void Tap(TestStage stage, KeyControl key)
    {
        stage.Controls.Press(key);
        stage.Step(TimeSpan.FromMilliseconds(50));
        stage.Controls.Release(key);
    }

    /// <summary>
    /// Plays the minute after <paramref name="from"/> seconds and gives each
    /// hurdle the runner had time to do something about, as it went past:
    /// whether the runner was over its lane, and whether it was in the air.
    /// </summary>
    private static List<(bool SameLane, bool Up)> Hurdles(RunnerExample game, double from)
    {
        // Hurdle key to whether it was in the runner's lane with time left to
        // act, whether the runner was over its lane as it passed, and whether
        // the runner was in the air then.
        var watched = new Dictionary<long, bool[]>();
        var passes = new List<(bool, bool)>();
        for (var i = 1; i <= 60 * 60; i++)
        {
            var objects = game.Scene(Camera, from + i * Step).Objects;
            var body = objects.First(one => one.Key == 1).Transform.GetTranslation();
            var hurdles = objects.Where(one => one.Key is >= 2000 and < 2200)
                .ToDictionary(one => one.Key, one => one.Transform.GetTranslation());
            Watch(watched, body, hurdles);
            foreach (var key in watched.Keys.Except(hurdles.Keys).ToList())
            {
                watched.Remove(key, out var row);
                if (row![0])
                {
                    passes.Add((row[1], row[2]));
                }
            }
        }

        return passes;
    }

    /// <summary>Whether a hurdle at <paramref name="at"/> is 6 to 10 m ahead, in the runner's lane.</summary>
    private static bool Coming(Vector3 body, Vector3 at) => Math.Abs(at.X - body.X) < 0.45 && body.Z - at.Z is > 6 and < 10;

    private static void Watch(Dictionary<long, bool[]> watched, Vector3 body, Dictionary<long, Vector3> hurdles)
    {
        foreach (var (key, at) in hurdles)
        {
            // The road runs towards the camera, so a hurdle still to come sits
            // further from it than the runner does.
            var ahead = body.Z - at.Z;
            var row = watched.TryGetValue(key, out var seen) ? seen : watched[key] = new bool[3];
            var sameLane = Math.Abs(at.X - body.X) < 0.45;
            var passing = Math.Abs(ahead) < 0.8 && sameLane;
            row[0] |= ahead > 6 && ahead < 22 && sameLane;
            row[1] |= passing;
            row[2] |= passing && body.Y > Airborne;
        }
    }
}

using Orblit.VectorMath;

namespace Orblit.Effect.Tests;

public class EffectTests
{
    private static Vector3 V(double x, double y = 0, double z = 0) => new(x, y, z);

    // curves

    [Fact]
    public void EveryOneStartsAtNothingAndEndsAtEverything()
    {
        var curves = new Dictionary<string, Ease>
        {
            ["linear"] = Eases.Linear,
            ["inQuad"] = Eases.InQuad,
            ["outQuad"] = Eases.OutQuad,
            ["inCubic"] = Eases.InCubic,
            ["outCubic"] = Eases.OutCubic,
            ["inOutQuad"] = Eases.InOutQuad,
            ["inOutCubic"] = Eases.InOutCubic,
            ["outBack"] = Eases.OutBack,
            ["outElastic"] = Eases.OutElastic,
            ["outBounce"] = Eases.OutBounce,
        };
        foreach (var (name, ease) in curves)
        {
            Assert.True(Math.Abs(ease(0)) <= 1e-6, $"{name} at 0 is {ease(0)}");
            Assert.True(Math.Abs(ease(1) - 1) <= 1e-6, $"{name} at 1 is {ease(1)}");
        }
    }

    [Fact]
    public void EasesInSlowerThanOutAndOutSlowerThanIn()
    {
        // The two are each other's mirror, which is the whole distinction.
        Assert.True(Eases.InQuad(0.25) < 0.25);
        Assert.True(Eases.OutQuad(0.25) > 0.25);
    }

    [Fact]
    public void BackOvershootsAndComesHome()
    {
        var most = Enumerable.Range(0, 101).Select(i => Eases.OutBack(i / 100.0)).Max();
        Assert.True(most > 1);
        Assert.Equal(1, Eases.OutBack(1), 1e-9);
    }

    [Fact]
    public void ThereAndBackEndsWhereItBegan()
    {
        var curve = Eases.ThereAndBack(Eases.Linear);
        Assert.Equal(0, curve(0), 1e-9);
        Assert.Equal(1, curve(0.5), 1e-9);
        Assert.Equal(0, curve(1), 1e-9);
    }

    [Fact]
    public void ReversedRunsTheOtherWay()
    {
        var curve = Eases.Reversed(Eases.InQuad);
        Assert.Equal(0, curve(0), 1e-9);
        Assert.Equal(1, curve(1), 1e-9);
        Assert.True(curve(0.25) > 0.25);
    }

    // one effect

    [Fact]
    public void AMoveIsNothingAtTheStartAndEverythingAtTheEnd()
    {
        var move = new MoveBy(Vector3.Zero(), 1);
        Assert.Equal(0, move.At(0).Move.Length);

        var along = new MoveBy(V(10), 2, Eases.Linear);
        Assert.Equal(0, along.At(0).Move.X, 1e-9);
        Assert.Equal(5, along.At(1).Move.X, 1e-9);
        Assert.Equal(10, along.At(2).Move.X, 1e-9);
    }

    [Fact]
    public void ItHoldsItsEndRatherThanSnappingBack()
    {
        // A sequence asks its earlier members what they finished as while a
        // later one runs; an effect that reset would undo them.
        var along = new MoveBy(V(10), 2, Eases.Linear);
        Assert.Equal(10, along.At(50).Move.X, 1e-9);
    }

    [Fact]
    public void AskingAboutTheMiddleDoesNotNeedTheBeginningRun()
    {
        // The whole reason for sampling rather than stepping.
        var along = new MoveBy(V(10), 2, Eases.Linear);
        var first = along.At(1).Move.X;
        var second = along.At(1).Move.X;
        Assert.Equal(5, first, 1e-9);
        Assert.Equal(first, second);
    }

    [Fact]
    public void AGrowGoesFromOneTowardsTheFactor()
    {
        // Halfway through is halfway there, not half the size.
        var grow = new GrowTo(V(3, 3, 3), 2, Eases.Linear);
        Assert.Equal(1, grow.At(0).Grow.X, 1e-9);
        Assert.Equal(2, grow.At(1).Grow.X, 1e-9);
        Assert.Equal(3, grow.At(2).Grow.X, 1e-9);
    }

    [Fact]
    public void AFadeGoesFromOneTowardsTheOpacity()
    {
        var fade = new FadeTo(0, 2);
        Assert.Equal(1, fade.At(0).Fade, 1e-9);
        Assert.Equal(0.5, fade.At(1).Fade, 1e-9);
        Assert.Equal(0, fade.At(2).Fade, 1e-9);
    }

    [Fact]
    public void ATurnTurns()
    {
        // Tested through the matrix, which is the path that actually ships.
        var turn = new TurnBy(Math.PI / 2, 2, ease: Eases.Linear);
        var matrix = Effects.Applied(Matrix4.Identity(), turn.At(2));
        var facing = matrix.Transform3(V(1));

        Assert.Equal(0, facing.X, 1e-6);
        Assert.Equal(-1, facing.Z, 1e-6);
    }

    [Fact]
    public void AnEffectOfNoDurationIsFinishedImmediately()
    {
        var instant = new MoveBy(V(5), 0);
        Assert.Equal(5, instant.At(0).Move.X, 1e-9);
        Assert.True(instant.DoneBy(0));
    }

    // changes together

    [Fact]
    public void OffsetsAddAndScalesMultiply()
    {
        var a = new Change(move: V(1, 2), grow: V(2, 2, 2), fade: 0.5);
        var b = new Change(move: V(3), grow: V(3, 3, 3), fade: 0.5);
        var both = a.And(b);

        Assert.Equal(4, both.Move.X, 1e-9);
        Assert.Equal(2, both.Move.Y, 1e-9);
        Assert.Equal(6, both.Grow.X, 1e-9);
        Assert.Equal(0.25, both.Fade, 1e-9);
    }

    [Fact]
    public void NothingAddedToSomethingIsThatSomething()
    {
        var one = new Change(move: V(1, 2, 3), fade: 0.4);
        var same = Change.None.And(one);
        Assert.Equal(one.Move, same.Move);
        Assert.Equal(one.Fade, same.Fade, 1e-9);
    }

    [Fact]
    public void NothingIsRecognisablyNothing()
    {
        Assert.True(Change.None.IsNothing);
        Assert.False(new Change(move: V(1)).IsNothing);
    }

    // composing

    [Fact]
    public void ASequenceRunsItsStepsInOrderAndKeepsWhatTheyDid()
    {
        // Without holding the earlier ends, a move followed by a turn snaps
        // back to the start of the move the moment the turn begins.
        var story = new Then([
            new MoveBy(V(10), 1, Eases.Linear),
            new MoveBy(V(0, 10), 1, Eases.Linear),
        ]);

        Assert.Equal(2, story.Duration, 1e-9);
        Assert.Equal(5, story.At(0.5).Move.X, 1e-9);
        Assert.Equal(0, story.At(0.5).Move.Y, 1e-9);
        Assert.Equal(10, story.At(1.5).Move.X, 1e-9);
        Assert.Equal(5, story.At(1.5).Move.Y, 1e-9);
        Assert.Equal(10, story.At(9).Move.Y, 1e-9);
    }

    [Fact]
    public void TwoAtOnceAreBothApplied()
    {
        var both = new Both([
            new MoveBy(V(10), 2, Eases.Linear),
            new FadeTo(0, 2),
        ]);
        Assert.Equal(2, both.Duration, 1e-9);
        Assert.Equal(5, both.At(1).Move.X, 1e-9);
        Assert.Equal(0.5, both.At(1).Fade, 1e-9);
    }

    [Fact]
    public void APauseIsPartOfTheLengthAndChangesNothing()
    {
        var story = new Then([new Wait(1), new MoveBy(V(10), 1, Eases.Linear)]);
        Assert.Equal(2, story.Duration, 1e-9);
        Assert.Equal(0, story.At(0.5).Move.X, 1e-9);
        Assert.Equal(5, story.At(1.5).Move.X, 1e-9);
    }

    [Fact]
    public void ADelayHoldsEverythingOff()
    {
        var late = new After(2, new MoveBy(V(10), 1, Eases.Linear));
        Assert.Equal(3, late.Duration, 1e-9);
        Assert.Equal(0, late.At(1).Move.X, 1e-9);
        Assert.Equal(5, late.At(2.5).Move.X, 1e-9);
    }

    [Fact]
    public void ARepeatIsAsCheapToAskAboutLateAsEarly()
    {
        var again = new Again(new MoveBy(V(10), 1, Eases.Linear), times: 3);
        Assert.Equal(3, again.Duration, 1e-9);
        Assert.Equal(5, again.At(0.5).Move.X, 1e-9);

        // The thousandth run of a forever effect costs what the first does.
        var forever = new Again(new MoveBy(V(10), 1, Eases.Linear));
        Assert.Equal(double.PositiveInfinity, forever.Duration);
        Assert.Equal(5, forever.At(1000.5).Move.X, 1e-9);
    }

    [Fact]
    public void OutAndBackEndsWhereItStarted()
    {
        var pulse = new OutAndBack(new GrowTo(V(2, 2, 2), 1, Eases.Linear));
        Assert.Equal(2, pulse.Duration, 1e-9);
        Assert.Equal(2, pulse.At(1).Grow.X, 1e-9);
        Assert.Equal(1, pulse.At(2).Grow.X, 1e-9);
    }

    [Fact]
    public void ASequenceOfSequencesIsStillASequence()
    {
        var nested = new Then([
            new Then([new MoveBy(V(1), 1, Eases.Linear)]),
            new Both([new MoveBy(V(0, 1), 1, Eases.Linear), new FadeTo(0.5, 1)]),
        ]);
        Assert.Equal(2, nested.Duration, 1e-9);
        Assert.Equal(1, nested.At(2).Move.X, 1e-9);
        Assert.Equal(1, nested.At(2).Move.Y, 1e-9);
        Assert.Equal(0.5, nested.At(2).Fade, 1e-9);
    }

    // a shake

    [Fact]
    public void TheSameShakeLooksTheSameTwice()
    {
        var shake = new Shake(0.5, 1, seed: 4);
        Assert.Equal(shake.At(0.3).Move.X, shake.At(0.3).Move.X);
    }

    [Fact]
    public void TwoShakesWithDifferentSeedsDiffer() =>
        Assert.NotEqual(new Shake(0.5, 1, seed: 1).At(0.3).Move.X, new Shake(0.5, 1, seed: 2).At(0.3).Move.X);

    [Fact]
    public void ItFadesOutRatherThanStopping()
    {
        var shake = new Shake(1, 1, seed: 9);
        var early = Enumerable.Range(0, 20).Select(i => shake.At(i * 0.005).Move.Length).Max();
        var late = Enumerable.Range(0, 20).Select(i => shake.At(0.9 + (i * 0.005)).Move.Length).Max();
        Assert.True(late < early);
        Assert.Equal(0, shake.At(1).Move.Length, 1e-9);
    }

    [Fact]
    public void ItStaysInsideItsStrength()
    {
        var shake = new Shake(0.5, 2, seed: 3);
        for (var i = 0; i < 200; i++)
        {
            Assert.True(Math.Abs(shake.At(i * 0.01).Move.X) <= 0.5 + 1e-9, $"sample {i}");
        }
    }

    // playing them

    [Fact]
    public void ItAddsUpEverythingRunning()
    {
        var playing = new Playing();
        playing.Start(new MoveBy(V(10), 2, Eases.Linear));
        playing.Start(new FadeTo(0, 2));

        playing.Advance(1);
        Assert.Equal(5, playing.Change.Move.X, 1e-9);
        Assert.Equal(0.5, playing.Change.Fade, 1e-9);
    }

    [Fact]
    public void WhatHasFinishedKeepsContributingUntilItIsBaked()
    {
        // The trap this design has to avoid: a change is a difference, so an
        // effect dropped the moment it finishes takes its offset with it and
        // the thing snaps back to where it started.
        var playing = new Playing();
        playing.Start(new MoveBy(V(10), 1, Eases.Linear));
        playing.Advance(2);

        Assert.Equal(0, playing.Length);
        Assert.Equal(10, playing.Change.Move.X, 1e-9);

        // Once a host has folded it in, it stops being counted twice.
        Assert.Equal(10, playing.Bake().Move.X, 1e-9);
        Assert.Equal(0, playing.Change.Move.X, 1e-9);
    }

    [Fact]
    public void ItSaysWhatHasJustFinished()
    {
        // The moment a host wants to do the next thing; polling for it is worse.
        var playing = new Playing();
        playing.Start(new MoveBy(V(1), 1));
        Assert.Empty(playing.Advance(0.5));
        Assert.Single(playing.Advance(0.6));
        Assert.Equal(0, playing.Length);
    }

    [Fact]
    public void AnEffectStartedLaterRunsOnItsOwnClock()
    {
        var playing = new Playing();
        playing.Start(new MoveBy(V(10), 2, Eases.Linear));
        playing.Advance(1);
        playing.Start(new MoveBy(V(0, 10), 2, Eases.Linear));
        playing.Advance(1);

        Assert.Equal(10, playing.Change.Move.X, 1e-9);
        Assert.Equal(5, playing.Change.Move.Y, 1e-9);
    }

    [Fact]
    public void NothingRunningIsNoChange() => Assert.True(new Playing().Change.IsNothing);

    // applying a change

    [Fact]
    public void AMoveMovesAndAGrowGrows()
    {
        var start = Matrix4.Identity();
        start.SetTranslation(V(1, 2, 3));
        var moved = Effects.Applied(start, new Change(move: V(10), grow: V(2, 2, 2)));

        Assert.Equal(11, moved.GetTranslation().X, 1e-9);
        Assert.Equal(2, moved.GetColumn(0).Length, 1e-9);
    }

    [Fact]
    public void NothingAppliedLeavesItWhereItWas()
    {
        var start = Matrix4.Identity();
        start.SetTranslation(V(1, 2, 3));
        var same = Effects.Applied(start, Change.None);
        Assert.Equal(1, same.GetTranslation().X, 1e-9);
        Assert.Equal(1, same.GetColumn(0).Length, 1e-9);
    }

    // small helpers

    [Fact]
    public void LerpGoesFromOneToTheOther() => Assert.Equal(15, Effects.Lerp(10, 20, 0.5), 1e-9);

    [Fact]
    public void ATurnTakesTheShortWayRound()
    {
        // The long way is a character spinning 350 degrees to look 10 the other
        // way, which is the classic version of this bug.
        Assert.Equal(-0.283, Effects.ShortestTurn(0.1, 6.1), 0.01);
        Assert.Equal(Math.PI / 2, Effects.ShortestTurn(0, Math.PI / 2), 1e-9);
    }
}

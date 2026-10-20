namespace Orblit.Agent.Tests;

public class BehaviourTests
{
    private static bool Always(Tick tick) => true;

    private static bool Never(Tick tick) => false;

    /// <summary>A leaf that returns whatever it is told, and counts how often it is run.</summary>
    private static (Node Node, List<Status> Calls) Counted(Status answer)
    {
        var calls = new List<Status>();
        return (
            new Do("counted", tick =>
            {
                calls.Add(answer);
                return answer;
            }),
            calls);
    }

    // a sequence

    [Fact]
    public void SucceedsOnlyWhenEveryChildDoes()
    {
        var tree = new Sequence([new Check("yes", Always), new Check("yes", Always)]);
        Assert.Equal(Status.Success, new Brain(tree).Tick(0.1));
    }

    [Fact]
    public void StopsAtTheFirstFailure()
    {
        var after = Counted(Status.Success);
        var tree = new Sequence([new Check("no", Never), after.Node]);

        Assert.Equal(Status.Failure, new Brain(tree).Tick(0.1));

        // The step after a failed one is not run. Walking through a door that
        // did not open is the whole thing this prevents.
        Assert.Empty(after.Calls);
    }

    [Fact]
    public void ResumesAtTheChildThatWasRunningNotAtTheFirst()
    {
        // The commonest thing to get wrong. Restarting produces an agent that
        // re-opens a door it is halfway through.
        var first = Counted(Status.Success);
        var opened = 0;
        var tree = new Sequence(
        [
            first.Node,
            new Do("walk", tick => opened++ < 2 ? Status.Running : Status.Success),
        ]);

        var brain = new Brain(tree);
        Assert.Equal(Status.Running, brain.Tick(0.1));
        Assert.Equal(Status.Running, brain.Tick(0.1));
        Assert.Equal(Status.Success, brain.Tick(0.1));

        // Three ticks, and the first child ran once, on the first tick only.
        Assert.Single(first.Calls);
    }

    [Fact]
    public void StartsAgainOnceItHasFinished()
    {
        var first = Counted(Status.Success);
        var tree = new Sequence([first.Node]);
        var brain = new Brain(tree);

        Assert.Equal(Status.Success, brain.Tick(0.1));
        Assert.Equal(Status.Success, brain.Tick(0.1));
        Assert.Equal(2, first.Calls.Count);
    }

    // a selector

    [Fact]
    public void TakesTheFirstChildThatWorks()
    {
        var second = Counted(Status.Success);
        var tree = new Selector([new Check("no", Never), second.Node]);

        Assert.Equal(Status.Success, new Brain(tree).Tick(0.1));
        Assert.Single(second.Calls);
    }

    [Fact]
    public void FailsOnlyWhenEveryChildDoes()
    {
        var tree = new Selector([new Check("no", Never), new Check("no", Never)]);
        Assert.Equal(Status.Failure, new Brain(tree).Tick(0.1));
    }

    [Fact]
    public void ResumesAtTheChildThatWasRunning()
    {
        var first = Counted(Status.Failure);
        var chased = 0;
        var tree = new Selector(
        [
            first.Node,
            new Do("chase", tick => chased++ < 1 ? Status.Running : Status.Success),
        ]);

        var brain = new Brain(tree);
        Assert.Equal(Status.Running, brain.Tick(0.1));
        Assert.Equal(Status.Success, brain.Tick(0.1));

        // The condition in front of the running child is not re-asked while it
        // runs. An agent that re-checked would abandon its chase the moment
        // the thing it is chasing left range, mid-stride.
        Assert.Single(first.Calls);
    }

    // a parallel

    [Fact]
    public void RunsEveryChildEvenOnceTheAnswerIsKnown()
    {
        // A child that returned early would leave its siblings un-ticked, and
        // anything holding a timer would run slow whenever a sibling finished.
        var late = Counted(Status.Running);
        var tree = new Parallel([new Check("no", Never), late.Node]);

        Assert.Equal(Status.Failure, new Brain(tree).Tick(0.1));
        Assert.Single(late.Calls);
    }

    [Fact]
    public void SucceedsWhenEnoughChildrenHave()
    {
        var two = new Parallel(
            [new Check("yes", Always), new Check("yes", Always), new Check("no", Never)],
            succeedAfter: 2,
            failAfter: 2);
        Assert.Equal(Status.Success, new Brain(two).Tick(0.1));
    }

    // decorators

    [Fact]
    public void InvertSwapsTheTwoAnswersAndLeavesRunningAlone()
    {
        Assert.Equal(Status.Failure, new Brain(new Invert(new Check("yes", Always))).Tick(0.1));
        Assert.Equal(Status.Success, new Brain(new Invert(new Check("no", Never))).Tick(0.1));
        Assert.Equal(Status.Running, new Brain(new Invert(new Do("busy", tick => Status.Running))).Tick(0.1));
    }

    [Fact]
    public void SucceedHidesAFailureWorthTryingAnyway()
    {
        Assert.Equal(Status.Success, new Brain(new Succeed(new Check("no", Never))).Tick(0.1));
    }

    [Fact]
    public void WaitRunsOnTheTickClockNotARealOne()
    {
        var brain = new Brain(new Wait(0.25));
        Assert.Equal(Status.Running, brain.Tick(0.1));
        Assert.Equal(Status.Running, brain.Tick(0.1));
        Assert.Equal(Status.Success, brain.Tick(0.1));

        // And it is ready to wait again.
        Assert.Equal(Status.Success, brain.Tick(0.3));
    }

    [Fact]
    public void RepeatRunsItsChildAFixedNumberOfTimes()
    {
        var child = Counted(Status.Success);
        var brain = new Brain(new Repeat(child.Node, times: 3));

        Assert.Equal(Status.Running, brain.Tick(0.1));
        Assert.Equal(Status.Running, brain.Tick(0.1));
        Assert.Equal(Status.Success, brain.Tick(0.1));
        Assert.Equal(3, child.Calls.Count);
    }

    [Fact]
    public void CooldownRefusesForAWhileAfterItsChildFinishes()
    {
        var child = Counted(Status.Success);
        var brain = new Brain(new Cooldown(child.Node, 0.3));

        Assert.Equal(Status.Success, brain.Tick(0.1));
        Assert.Equal(Status.Failure, brain.Tick(0.1));
        Assert.Equal(Status.Failure, brain.Tick(0.1));
        Assert.Equal(Status.Failure, brain.Tick(0.1));
        Assert.Equal(Status.Success, brain.Tick(0.1));

        // Twice in five ticks rather than five times. Stopping an agent doing
        // the expensive thing every frame it is allowed to is the whole job.
        Assert.Equal(2, child.Calls.Count);
    }

    [Fact]
    public void ADeadlineGivesUpOnAChildThatWillNotFinish()
    {
        var brain = new Brain(new Deadline(new Do("forever", t => Status.Running), 0.25));
        Assert.Equal(Status.Running, brain.Tick(0.1));
        Assert.Equal(Status.Running, brain.Tick(0.1));
        Assert.Equal(Status.Failure, brain.Tick(0.1));
    }

    // a brain

    [Fact]
    public void TwoAgentsOnOneTreeDoNotShareWhereTheyAreInIt()
    {
        // The reason nodes are stateless: a patrol tree is a description of
        // patrolling, not a description of one guard patrolling.
        var tree = new Sequence([new Wait(0.2), new Check("yes", Always)]);
        var one = new Brain(tree);
        var two = new Brain(tree);

        Assert.Equal(Status.Running, one.Tick(0.15));
        Assert.Equal(Status.Running, two.Tick(0.05));
        Assert.Equal(Status.Success, one.Tick(0.1));

        // The second is still waiting on its own clock.
        Assert.Equal(Status.Running, two.Tick(0.05));
    }

    [Fact]
    public void InterruptingForgetsTheMiddleWithoutForgettingTheTree()
    {
        var brain = new Brain(new Wait(0.3));
        Assert.Equal(Status.Running, brain.Tick(0.2));

        brain.Interrupt();
        Assert.Null(brain.Status);

        // Waiting starts again rather than finishing early.
        Assert.Equal(Status.Running, brain.Tick(0.2));
    }

    [Fact]
    public void TheBlackboardCarriesWhatTheGameKnows()
    {
        var seen = new List<string>();
        var tree = new Sequence(
        [
            new Act("spot", tick => tick.Write("target", "crate")),
            new Do("name", tick =>
            {
                seen.Add(tick.Read<string>("target") ?? "nothing");
                return Status.Success;
            }),
        ]);

        Assert.Equal(Status.Success, new Brain(tree).Tick(0.1));
        Assert.Equal(["crate"], seen);
    }

    // a guard

    [Fact]
    public void FallsBackDownItsChoicesAsTheWorldChanges()
    {
        // The shape most of an agent's behaviour actually is: attack if it can,
        // otherwise chase, otherwise patrol.
        var distance = 20.0;
        var log = new List<string>();

        var tree = new Selector(
        [
            new Sequence(
            [
                new Check("in range", tick => distance < 2),
                new Act("attack", tick => log.Add("attack")),
            ]),
            new Sequence(
            [
                new Check("seen", tick => distance < 10),
                new Act("chase", tick => log.Add("chase")),
            ]),
            new Act("patrol", tick => log.Add("patrol")),
        ]);

        var brain = new Brain(tree);
        brain.Tick(0.1);
        distance = 8;
        brain.Tick(0.1);
        distance = 1;
        brain.Tick(0.1);

        Assert.Equal(["patrol", "chase", "attack"], log);
    }
}

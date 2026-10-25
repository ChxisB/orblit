using Orblit.Audio;
using Orblit.Input;

namespace Orblit.Test.Tests;

public sealed class TestStageTests
{
    [Fact]
    public void ASecondIsSixtyFramesAndNowSaysSoExactly()
    {
        var seen = new List<Frame>();
        var stage = new TestStage(seen.Add);

        stage.Step(TimeSpan.FromSeconds(1));

        Assert.Equal(60, stage.Frames);
        Assert.Equal(TimeSpan.FromSeconds(1), stage.Now);
        Assert.Equal(60, seen.Count);
        Assert.All(seen, frame => Assert.Equal(1.0 / 60, frame.Dt));
        Assert.Equal(TimeSpan.FromSeconds(1), seen[^1].Now);
    }

    [Fact]
    public void ADifferentRateChangesTheFrame()
    {
        var stage = new TestStage(_ => { }, fps: 30);

        stage.Step(TimeSpan.FromSeconds(1));

        Assert.Equal(30, stage.Frames);
        Assert.Equal(TimeSpan.FromSeconds(1), stage.Now);
    }

    [Fact]
    public void ASpanBetweenFramesWaitsForTheNextStep()
    {
        var stage = new TestStage(_ => { });

        stage.Step(TimeSpan.FromMilliseconds(20));

        Assert.Equal(1, stage.Frames);
    }

    [Fact]
    public void SmallStepsAddUpToTheTimeTheySay()
    {
        var stage = new TestStage(_ => { });

        for (var i = 0; i < 100; i++)
        {
            stage.Step(TimeSpan.FromMilliseconds(8));
        }

        Assert.Equal(48, stage.Frames);
    }

    [Fact]
    public void AStepBackInTimeIsRefused()
    {
        var stage = new TestStage(_ => { });

        Assert.Throws<ArgumentOutOfRangeException>(() => stage.Step(TimeSpan.FromMilliseconds(-16)));
    }

    [Fact]
    public void ARateOfNothingIsRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TestStage(_ => { }, fps: 0));
    }

    [Fact]
    public void StepUntilStopsOnTheFrameTheGameGetsThere()
    {
        var count = 0;
        var stage = new TestStage(_ => count++);

        stage.StepUntil(() => count == 7, within: TimeSpan.FromSeconds(1));

        Assert.Equal(7, stage.Frames);
    }

    [Fact]
    public void StepUntilRunsNothingWhenItIsAlreadyTrue()
    {
        var stage = new TestStage(_ => { });

        stage.StepUntil(() => true, within: TimeSpan.FromSeconds(1));

        Assert.Equal(0, stage.Frames);
    }

    [Fact]
    public void StepUntilFailsRatherThanHangWhenTheGameNeverGetsThere()
    {
        var stage = new TestStage(_ => { });

        Assert.Throws<InvalidOperationException>(
            () => stage.StepUntil(() => false, within: TimeSpan.FromMilliseconds(500)));
        Assert.Equal(30, stage.Frames);
    }

    [Fact]
    public void InputIsReadIntoTheActionsBeforeTheGameTakesItsFrame()
    {
        var map = new ActionMap(
            [InputAction.Button("jump")],
            [new InputContext("play", new Dictionary<string, IReadOnlyList<Binding>>
            {
                ["jump"] = [new Binding(new KeyControl("Space"))],
            })],
            active: ["play"]);
        var pressed = new List<bool>();
        var stage = new TestStage(_ => pressed.Add(map.WasPressed("jump")), actions: map);

        stage.Step(TimeSpan.FromMilliseconds(50));
        stage.Controls.Press(new KeyControl("Space"));
        stage.Step(TimeSpan.FromMilliseconds(50));
        stage.Controls.Release(new KeyControl("Space"));
        stage.Step(TimeSpan.FromMilliseconds(17));

        Assert.Equal(
            [
                false, false, false, // Before the key.
                true, false, false, // Pressed on the first frame only.
                false,
            ],
            pressed);
    }

    [Fact]
    public void SoundMovesOnWithTheFramesSoAFadeEnds()
    {
        var stage = new TestStage(_ => { });
        stage.Audio.Play("door").Stop(fadeSeconds: 0.5);

        stage.Step(TimeSpan.FromMilliseconds(250));
        Assert.Empty(stage.Recording.Calls.OfType<AudioStopped>());

        stage.Step(TimeSpan.FromMilliseconds(500));
        Assert.Single(stage.Recording.Calls.OfType<AudioStopped>());
    }
}

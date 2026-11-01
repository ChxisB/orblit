namespace Orblit.Remote.Tests;

public class TimeGateTests
{
    [Fact]
    public void AStepDeltaThatPassesNoTimeIsRefused()
    {
        foreach (var bad in new[] { 0.0, -1.0, double.NaN, double.PositiveInfinity })
        {
            Assert.ThrowsAny<ArgumentException>(() => new TimeGate(bad));
        }
    }

    [Fact]
    public void PassedCountsTheSteppedFramesTheLoopUsed()
    {
        var gate = new TimeGate();
        gate.Advance(0.5);
        Assert.Equal(0, gate.Passed);
        _ = gate.Step(2);
        gate.Advance(0.5);
        Assert.Equal(1, gate.Passed);
        gate.Pause();
        gate.Advance(0.5);
        Assert.Equal(1, gate.Passed);
    }

    [Fact]
    public void ARunningGatePassesTheFrameDeltaThrough()
    {
        var gate = new TimeGate();

        Assert.Equal(0.02, gate.Advance(0.02));
        Assert.False(gate.Paused);
    }

    [Fact]
    public void APausedGatePassesNothingUntilItResumes()
    {
        var gate = new TimeGate();
        gate.Pause();

        Assert.True(gate.Paused);
        Assert.Equal(0, gate.Advance(0.02));
        gate.Resume();
        Assert.Equal(0.02, gate.Advance(0.02));
    }

    [Fact]
    public async Task StepPassesAFixedDeltaForEachFrameThenHolds()
    {
        var gate = new TimeGate(stepDelta: 0.01);
        var stepped = gate.Step(3);

        Assert.True(gate.Paused);
        Assert.Equal(3, gate.Pending);
        Assert.Equal([0.01, 0.01], [gate.Advance(0.5), gate.Advance(0.5)]);
        await Task.Delay(TimeSpan.Zero, TestContext.Current.CancellationToken);
        Assert.False(stepped.IsCompleted);
        Assert.Equal(0.01, gate.Advance(0.5));
        await stepped;
        Assert.Equal(0, gate.Pending);
        Assert.Equal(0, gate.Advance(0.5));
    }

    [Fact]
    public async Task StepsAskedForTwiceAddUpAndFinishTogether()
    {
        var gate = new TimeGate();
        var first = gate.Step(1);
        var second = gate.Step(1);
        gate.Advance(0.5);
        gate.Advance(0.5);

        await Task.WhenAll(first, second);
        Assert.Equal(0, gate.Pending);
    }

    [Fact]
    public async Task ResumeAndPauseEndAStepThatHasFramesLeft()
    {
        var gate = new TimeGate();
        var stepped = gate.Step(5);
        gate.Advance(0.5);
        gate.Resume();
        await stepped;
        Assert.Equal(0, gate.Pending);
        Assert.Equal(0.5, gate.Advance(0.5));

        var again = gate.Step(5);
        gate.Pause();
        await again;
        Assert.Equal(0, gate.Advance(0.5));
    }

    [Fact]
    public void AStepOfNoFramesIsRefused()
    {
        foreach (var bad in new[] { 0, -1 })
        {
            Assert.ThrowsAny<ArgumentException>(() =>
            {
                _ = new TimeGate().Step(bad);
            });
        }
    }
}

using Orblit.VectorMath;

namespace Orblit.Filament.Tests;

public sealed class LevelOfDetailTests
{
    private static readonly LevelOfDetail Tree = new(
    [
        new LevelStep("/trees/oak_near.glb", 30),
        new LevelStep("/trees/oak_mid.glb", 120),
        new LevelStep("/trees/oak_far.glb", 400),
    ]);

    [Fact]
    public void NearTheCameraIsTheNearMesh()
    {
        Assert.Equal("/trees/oak_near.glb", Tree.MeshAt(5));
        Assert.Equal("/trees/oak_near.glb", Tree.MeshAt(29.9));
    }

    [Fact]
    public void EachBoundaryHandsOverToTheNext()
    {
        Assert.Equal("/trees/oak_near.glb", Tree.MeshAt(30));
        Assert.Equal("/trees/oak_mid.glb", Tree.MeshAt(30.1));
        Assert.Equal("/trees/oak_mid.glb", Tree.MeshAt(120));
        Assert.Equal("/trees/oak_far.glb", Tree.MeshAt(120.1));
    }

    [Fact]
    public void PastTheLastStepItIsNotDrawnAtAll()
    {
        // Null and not the crudest mesh: past its range is the application saying it is not worth
        // the pixels, and a default would put the worst version of it on screen for ever.
        Assert.Null(Tree.MeshAt(401));
        Assert.False(Tree.IsVisibleAt(401));
        Assert.True(Tree.IsVisibleAt(400));
    }

    [Fact]
    public void AStepListThatRunsInwardsIsAMistakeWorthSeeing()
    {
        var muddled = new LevelOfDetail([new LevelStep("/far.glb", 400), new LevelStep("/near.glb", 30)]);
        Assert.False(muddled.IsOrdered);
        Assert.True(Tree.IsOrdered);
    }

    [Fact]
    public void NoStepsAtAllDrawsNothingRatherThanThrowing()
    {
        // What a half written asset looks like. It should show up as a missing tree and not as a dead frame.
        var empty = new LevelOfDetail([]);
        Assert.Null(empty.MeshAt(1));
        Assert.False(empty.IsVisibleAt(0));
    }

    [Fact]
    public void OneStepIsARangeNotALevelOfDetailAndThatIsAllowed()
    {
        var simple = new LevelOfDetail([new LevelStep("/rock.glb", 80)]);
        Assert.Equal("/rock.glb", simple.MeshAt(79));
        Assert.Null(simple.MeshAt(81));
    }

    [Fact]
    public void ALastStepThatNeverEndsDrawsForEver()
    {
        var mountain = new LevelOfDetail([new LevelStep("/near.glb", 500), new LevelStep("/far.glb", double.PositiveInfinity)]);
        Assert.Equal("/far.glb", mountain.MeshAt(50000));
    }

    [Fact]
    public void SittingOnABoundaryDoesNotFlicker()
    {
        // The camera breathing across 30 m would otherwise swap the mesh on every frame.
        Assert.Equal(0, Tree.StepAt(30.5, was: 0));
        Assert.Equal(0, Tree.StepAt(32, was: 0));

        // Far enough past it to mean it.
        Assert.Equal(1, Tree.StepAt(34, was: 0));
    }

    [Fact]
    public void ComingBackTheOtherWayChangesAtTheBoundaryItself()
    {
        // Only the step being shown holds on. Coming inwards there is nothing to hold on to.
        Assert.Equal(0, Tree.StepAt(29, was: 1));
    }

    [Fact]
    public void WithNoHistoryTheBoundaryIsTakenAtFaceValue()
    {
        Assert.Equal(1, Tree.StepAt(30.5));
    }

    [Fact]
    public void EachObjectRemembersItsOwnStep()
    {
        var state = new LevelOfDetailState();
        var camera = Vector3.Zero();

        // One just inside the near boundary, one well inside it.
        Assert.Equal("/trees/oak_near.glb", state.MeshFor(1, Tree, new Vector3(29, 0, 0), camera));
        Assert.Equal("/trees/oak_near.glb", state.MeshFor(2, Tree, new Vector3(10, 0, 0), camera));

        // The first drifts out and the second does not move. One changing does not change the other.
        Assert.Equal("/trees/oak_mid.glb", state.MeshFor(1, Tree, new Vector3(40, 0, 0), camera));
        Assert.Equal("/trees/oak_near.glb", state.MeshFor(2, Tree, new Vector3(10, 0, 0), camera));
    }

    [Fact]
    public void SomethingOutOfRangeIsForgottenRatherThanFrozen()
    {
        var state = new LevelOfDetailState();
        var camera = Vector3.Zero();

        state.MeshFor(1, Tree, new Vector3(10, 0, 0), camera);
        Assert.Equal(1, state.Length);

        Assert.Null(state.MeshFor(1, Tree, new Vector3(900, 0, 0), camera));
        Assert.Equal(0, state.Length);

        // Back in shot, it picks its step from where it is now.
        Assert.Equal("/trees/oak_far.glb", state.MeshFor(1, Tree, new Vector3(200, 0, 0), camera));
    }

    [Fact]
    public void DistanceIsToTheCameraNotAlongOneAxis()
    {
        // A little over 30 metres away diagonally, so past the near step though no single axis is.
        var state = new LevelOfDetailState();
        Assert.Equal("/trees/oak_mid.glb", state.MeshFor(1, Tree, new Vector3(25, 25, 0), Vector3.Zero()));
    }

    [Fact]
    public void AReloadedSceneStartsAgain()
    {
        var state = new LevelOfDetailState();
        state.MeshFor(1, Tree, new Vector3(10, 0, 0), Vector3.Zero());
        state.Clear();
        Assert.Equal(0, state.Length);
    }
}

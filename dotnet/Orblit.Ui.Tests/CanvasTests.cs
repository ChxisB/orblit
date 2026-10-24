namespace Orblit.Ui.Tests;

public class CanvasTests
{
    private static readonly UiCanvas Grid = new() { Width = 1000, Columns = 4, Gutter = 20, SafeArea = 0.05 };

    [Fact]
    public void ABiggerScreenShowsABiggerInterfaceUpToAPoint()
    {
        var canvas = new UiCanvas { Fit = CanvasFit.Responsive };

        Assert.Equal(1, canvas.FluidScale(1920));
        Assert.Equal(1.5, canvas.FluidScale(2880));
        // Clamped: a television is not four times the type size.
        Assert.Equal(1.5, canvas.FluidScale(7680));
        Assert.Equal(0.8, canvas.FluidScale(390));
    }

    [Fact]
    public void ACanvasThatIsAlreadyScaledIsNotScaledTwice()
    {
        // Every other fit magnifies the whole picture. Growing the text inside it as well would compound.
        var canvas = new UiCanvas();

        Assert.Equal(CanvasFit.Contain, canvas.Fit);
        Assert.Equal(1, canvas.FluidScale(3840));
    }

    [Fact]
    public void ColumnsAndGuttersDivideTheSpaceInsideTheSafeArea()
    {
        var columns = Grid.ColumnsAcross(1000);

        Assert.Equal(4, columns.Count);
        // 900 usable, three 20px gutters, four 210px columns.
        Assert.Equal(50.0, columns[0].Left);
        Assert.Equal(260.0, columns[0].Right);
        Assert.Equal(950.0, columns[^1].Right, 0.001);
    }

    [Fact]
    public void ANarrowScreenGetsFewerColumnsNotThinnerOnes()
    {
        var canvas = new UiCanvas { Columns = 12, Gutter = 24 };

        Assert.Equal(12, canvas.ColumnsAt(1920));
        Assert.Equal(4, canvas.ColumnsAt(390));
        // A divisor every time, so a narrower grid is a subset of the wider one's lines.
        foreach (var width in new[] { 320.0, 480.0, 640.0, 900.0, 1280.0, 3840.0 })
        {
            Assert.Equal(0, 12 % canvas.ColumnsAt(width));
        }
    }

    [Fact]
    public void NumbersThatDoNotLeaveRoomDrawNothing()
    {
        var canvas = new UiCanvas { Columns = 1, Gutter = 0 };

        Assert.Empty(canvas.ColumnsAcross(40));
    }

    [Fact]
    public void APositionNearAColumnEdgeSnapsToIt()
    {
        Assert.Equal(50.0, Grid.SnapAcross(53, 1000));
        Assert.Equal(260.0, Grid.SnapAcross(257, 1000));
        // Something put deliberately between two columns stays there.
        Assert.Null(Grid.SnapAcross(150, 1000));
    }

    [Fact]
    public void TheGridAndTheScaleSurviveARoundTrip()
    {
        var canvas = new UiCanvas { Fit = CanvasFit.Responsive, Columns = 6, Gutter = 32, MinScale = 0.5, MaxScale = 2 };

        var read = UiCanvas.FromJson(canvas.ToJson());

        Assert.Equal(CanvasFit.Responsive, read.Fit);
        Assert.Equal(6, read.Columns);
        Assert.Equal(32, read.Gutter);
        Assert.Equal(0.5, read.MinScale);
        Assert.Equal(2, read.MaxScale);
    }

    [Fact]
    public void AFileWrittenBeforeAnyOfThisReadsWithTheOldBehaviour()
    {
        var read = UiCanvas.FromJson(new Dictionary<string, object?> { ["width"] = 1280L, ["height"] = 720L });

        Assert.Equal(CanvasFit.Contain, read.Fit);
        Assert.Equal(12, read.Columns);
        Assert.Equal(1, read.FluidScale(3840));
    }

    [Fact]
    public void FittingInsideNeverCutsAnythingOff()
    {
        var canvas = new UiCanvas { Width = 1920, Height = 1080 };

        // A screen that is too tall: width decides.
        Assert.Equal(0.5, canvas.ScaleFor(960, 1080), 1e-9);
        // A screen that is too wide: height decides.
        Assert.Equal(0.5, canvas.ScaleFor(1920, 540), 1e-9);
    }

    [Fact]
    public void MatchingOneDimensionLetsTheOtherOverflow()
    {
        var wide = new UiCanvas { Width = 1920, Height = 1080, Fit = CanvasFit.Width };
        var tall = new UiCanvas { Width = 1920, Height = 1080, Fit = CanvasFit.Height };

        Assert.Equal(2, wide.ScaleFor(3840, 540), 1e-9);
        Assert.Equal(0.5, tall.ScaleFor(3840, 540), 1e-9);
    }

    [Fact]
    public void ActualSizeDoesNotScale() => Assert.Equal(1, new UiCanvas { Fit = CanvasFit.None }.ScaleFor(100, 100));

    [Fact]
    public void ACanvasWithNoSizeDoesNotDivideByZero()
    {
        var canvas = new UiCanvas { Width = 0, Height = 0 };

        Assert.Equal(1, canvas.ScaleFor(100, 100));
        Assert.Equal(1, canvas.Aspect);
    }

    [Fact]
    public void AFitItDoesNotKnowFallsBackToFittingInside() =>
        Assert.Equal(CanvasFit.Contain, UiCanvas.FromJson(new Dictionary<string, object?> { ["fit"] = "sideways" }).Fit);

    [Fact]
    public void ASafeAreaOutsideThePossibleIsClamped()
    {
        Assert.Equal(0.45, UiCanvas.FromJson(new Dictionary<string, object?> { ["safeArea"] = 9.0 }).SafeArea);
        Assert.Equal(0, UiCanvas.FromJson(new Dictionary<string, object?> { ["safeArea"] = -1.0 }).SafeArea);
    }
}

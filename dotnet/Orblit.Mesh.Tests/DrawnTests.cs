namespace Orblit.Mesh.Tests;

public class DrawnTests
{
    /// <summary>A unit square on the ground, drawn so it faces up.</summary>
    private static List<Vector3> Square() =>
    [
        new Vector3(0, 0, 0),
        new Vector3(0, 0, 1),
        new Vector3(1, 0, 1),
        new Vector3(1, 0, 0),
    ];

    private static Vector3 MiddleOf(Mesh mesh)
    {
        var box = mesh.Bounds;
        return box.Min + ((box.Max - box.Min) / 2);
    }

    // the outline

    [Fact]
    public void UnderThreePointsThereIsNothingToBuild()
    {
        Assert.False(new PolyShape([Vector3.Zero()]).IsDrawable);
        Assert.True(new PolyShape([Vector3.Zero()]).Build().IsEmpty);
        Assert.True(new PolyShape(Square()).IsDrawable);
    }

    [Fact]
    public void ThePlaneIsTheOneThePointsBestLieOn()
    {
        var shape = new PolyShape(Square());
        Assert.Equal(1, shape.Plane.Normal.Y, 1e-9);
        Assert.Equal(0.5, shape.Plane.Centre.X, 1e-9);
        Assert.Equal(0, shape.Plane.Centre.Y, 1e-9);
    }

    [Fact]
    public void PointsNowhereNearAPlaneStillGiveOneRatherThanNothing()
    {
        // Every point in a line: no plane at all, and the honest answer is a lie that does not produce NaN
        // in everything downstream.
        var shape = new PolyShape([new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(2, 0, 0)]);
        Assert.Equal(1, shape.Plane.Normal.Length, 1e-9);
    }

    [Fact]
    public void AClickBackOnTheFirstPointClosesItAndOnlyThen()
    {
        var points = Square();
        Assert.True(Drawing.ClosesOutline(points, new Vector3(0, 0, 0)));
        Assert.False(Drawing.ClosesOutline(points, new Vector3(5, 0, 5)));

        // two points do not enclose anything to close
        Assert.False(Drawing.ClosesOutline(points.Take(2).ToList(), new Vector3(0, 0, 0)));
    }

    [Fact]
    public void ASecondClickInTheSamePlaceIsNotASecondPoint()
    {
        var points = new List<Vector3> { new(1, 0, 1) };
        Assert.False(Drawing.IsNewPoint(points, new Vector3(1, 0, 1)));
        Assert.True(Drawing.IsNewPoint(points, new Vector3(1.5, 0, 1)));
        Assert.True(Drawing.IsNewPoint([], Vector3.Zero()));
    }

    [Fact]
    public void WhichWayRoundItWasDrawnShowsInTheSignOfItsArea()
    {
        var up = new Vector3(0, 1, 0);
        Assert.Equal(1, Drawing.SignedAreaOf(Square(), up), 1e-9);
        Assert.Equal(-1, Drawing.SignedAreaOf(Enumerable.Reverse(Square()).ToList(), up), 1e-9);
    }

    [Fact]
    public void AFigureOfEightIsCaughtBeforeItBecomesGeometry()
    {
        var crossed = new List<Vector3>
        {
            new(0, 0, 0),
            new(1, 0, 1),
            new(1, 0, 0),
            new(0, 0, 1),
        };
        Assert.True(Drawing.OutlineCrosses(crossed, new Vector3(0, 1, 0)));
        Assert.False(Drawing.OutlineCrosses(Square(), new Vector3(0, 1, 0)));

        // a triangle cannot cross itself
        Assert.False(Drawing.OutlineCrosses(Square().Take(3).ToList(), new Vector3(0, 1, 0)));
    }

    [Fact]
    public void AnOutlineThatGrazesItselfIsAllowed()
    {
        // Two edges meeting at a point rather than passing through each other. Odd, drawable, and refusing it
        // would refuse a lot to catch one.
        var grazing = new List<Vector3>
        {
            new(0, 0, 0),
            new(2, 0, 0),
            new(1, 0, 1),
            new(2, 0, 2),
            new(0, 0, 2),
            new(1, 0, 1),
        };
        Assert.False(Drawing.OutlineCrosses(grazing, new Vector3(0, 1, 0)));
    }

    // building

    [Fact]
    public void ASquarePulledUpIsAClosedBox()
    {
        var mesh = new PolyShape(Square(), height: 2).Build();

        Assert.Equal(8, mesh.VertexCount);
        Assert.True(mesh.FaceCount == 6, "a top, a bottom and four walls");

        var box = mesh.Bounds;
        Assert.Equal(0, box.Min.Y, 1e-9);
        Assert.Equal(2, box.Max.Y, 1e-9);
    }

    [Fact]
    public void EveryFacePointsAwayFromTheMiddle()
    {
        var mesh = new PolyShape(Square(), height: 2).Build();
        var middle = MiddleOf(mesh);

        foreach (var face in mesh.Faces)
        {
            var outward = mesh.CentreOf(face) - middle;
            Assert.True(mesh.NormalOf(face).Dot(outward) > 0, "a face wound the wrong way is a hole in a solid");
        }
    }

    [Fact]
    public void DrawnTheOtherWayRoundItStillComesOutSolid()
    {
        var mesh = new PolyShape(Enumerable.Reverse(Square()).ToList(), height: 2).Build();
        var middle = MiddleOf(mesh);

        foreach (var face in mesh.Faces)
        {
            var outward = mesh.CentreOf(face) - middle;
            Assert.True(mesh.NormalOf(face).Dot(outward) > 0);
        }
    }

    [Fact]
    public void ANegativeHeightGoesTheOtherWayAndIsStillSolid()
    {
        var mesh = new PolyShape(Square(), height: -2).Build();
        var box = mesh.Bounds;
        Assert.Equal(-2, box.Min.Y, 1e-9);
        Assert.Equal(0, box.Max.Y, 1e-9);

        var middle = box.Min + ((box.Max - box.Min) / 2);
        foreach (var face in mesh.Faces)
        {
            Assert.True(mesh.NormalOf(face).Dot(mesh.CentreOf(face) - middle) > 0);
        }
    }

    [Fact]
    public void NoHeightAtAllLeavesOneFlatSurface()
    {
        var mesh = new PolyShape(Square(), height: 0).Build();
        Assert.Equal(1, mesh.FaceCount);
        Assert.Equal(4, mesh.VertexCount);
        Assert.Equal(1, mesh.NormalOf(mesh.Faces.Single()).Y, 1e-9);
    }

    [Fact]
    public void ASixSidedPlanGivesSixWalls()
    {
        var points = Enumerable.Range(0, 6).Select(i => new Vector3(i % 2 == 0 ? 1.0 : 0.6, 0, i)).ToList();
        var mesh = new PolyShape(points, height: 1).Build();
        Assert.True(mesh.FaceCount == 8, "six walls, a top and a bottom");
    }

    [Fact]
    public void AShapeOnASlopingPlaneStaysOnIt()
    {
        // A square tilted forty-five degrees, drawn on that slope.
        var tilted = new List<Vector3>
        {
            new(0, 0, 0),
            new(0, 1, 1),
            new(1, 1, 1),
            new(1, 0, 0),
        };
        var shape = new PolyShape(tilted, height: 1);
        var mesh = shape.Build();

        // The bottom face is the one somebody drew, so its normal is the plane's, turned over, because it is
        // the underside.
        var normal = shape.Plane.Normal;
        var bottom = mesh.Faces.First(face => mesh.NormalOf(face).Dot(normal) < -0.99);
        foreach (var at in mesh.PointsOf(bottom))
        {
            Assert.True(
                Math.Abs((at - shape.Plane.Centre).Dot(normal)) < 1e-9,
                "still on the plane it was drawn on");
        }
    }

    // placing a point

    [Fact]
    public void ARayMeetsThePlaneWhereItShould()
    {
        var at = PolyShape.OnPlane(new Vector3(0, 5, 0), new Vector3(0, -1, 0), Vector3.Zero(), new Vector3(0, 1, 0));
        Assert.Equal(0, at!.Y, 1e-9);
    }

    [Fact]
    public void ARayAlongThePlaneMeetsItNowhere()
    {
        Assert.Null(PolyShape.OnPlane(new Vector3(0, 5, 0), new Vector3(1, 0, 0), Vector3.Zero(), new Vector3(0, 1, 0)));
    }

    [Fact]
    public void BehindTheCameraIsNotOnThePlaneInFrontOfIt()
    {
        Assert.Null(PolyShape.OnPlane(new Vector3(0, 5, 0), new Vector3(0, 1, 0), Vector3.Zero(), new Vector3(0, 1, 0)));
    }

    [Fact]
    public void ThePointsSurviveARoundTripAndAreTheShape()
    {
        var shape = new PolyShape(Square(), height: 3, flipped: true);
        var back = PolyShape.FromJson(shape.ToJson())!;

        Assert.Equal(4, back.Points.Count);
        Assert.Equal(1.0, back.Points[2].X);
        Assert.Equal(3.0, back.Height);
        Assert.True(back.Flipped);

        // Which is the whole point of keeping them: the shape can be redrawn from a corner a week later.
        Assert.Equal(shape.Build().FaceCount, back.Build().FaceCount);
    }

    [Fact]
    public void ACopyCanBeEditedWithoutTouchingTheOriginal()
    {
        var shape = new PolyShape(Square(), height: 1);
        var copy = shape.Copy();
        copy.Height = 9;
        copy.Points[0].X = 42;

        Assert.Equal(1.0, shape.Height);
        Assert.Equal(0.0, shape.Points[0].X);
    }
}

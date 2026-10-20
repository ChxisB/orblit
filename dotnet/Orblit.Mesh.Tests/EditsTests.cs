namespace Orblit.Mesh.Tests;

public class EditsTests
{
    private static Mesh Cube() => new Shape { Kind = ShapeKind.Cube }.Build();

    /// <summary>The face pointing up, which is the one worth extruding in a test.</summary>
    private static Face TopOf(Mesh mesh) => mesh.Faces.First(f => mesh.NormalOf(f).Y > 0.9);

    // extruding

    [Fact]
    public void MakesAWallForEveryEdgeOfTheFace()
    {
        var mesh = Cube();
        var top = TopOf(mesh);

        mesh.Extrude([top], 1);

        // Six faces to begin with, four more round the sides of the extrusion.
        Assert.Equal(10, mesh.Faces.Count);
    }

    [Fact]
    public void MovesTheFaceRatherThanLeavingItBehind()
    {
        var mesh = Cube();
        var top = TopOf(mesh);

        mesh.Extrude([top], 2);

        // The face itself is the one that moved, so it kept its material and whatever else was on it.
        Assert.Equal(3, mesh.CentreOf(top).Y, 1e-9);
        Assert.Equal(3, mesh.Bounds.Max.Y, 1e-9);
    }

    [Fact]
    public void TheNewGeometryStillFacesOutwards()
    {
        var mesh = Cube();
        mesh.Extrude([TopOf(mesh)], 1);

        var centre = (mesh.Bounds.Min + mesh.Bounds.Max) / 2;
        foreach (var face in mesh.Faces)
        {
            var outwards = mesh.CentreOf(face) - centre;
            if (outwards.Length < 1e-6)
            {
                continue;
            }

            Assert.True(mesh.NormalOf(face).Dot(outwards) > 0);
        }
    }

    [Fact]
    public void InwardsMakesAHoleRatherThanTurningTheShapeInsideOut()
    {
        var mesh = Cube();
        var top = TopOf(mesh);

        mesh.Extrude([top], -0.5);

        Assert.Equal(0.5, mesh.CentreOf(top).Y, 1e-9);
        Assert.Equal(1, mesh.Bounds.Max.Y, 1e-9);
    }

    [Fact]
    public void DoesNotDragTheNeighbours()
    {
        var mesh = Cube();
        var bottom = mesh.Faces.First(f => mesh.NormalOf(f).Y < -0.9);
        var was = mesh.CentreOf(bottom).Clone();

        mesh.Extrude([TopOf(mesh)], 3);

        // Pulling one face copies its corners rather than moving the ones its neighbours are also using.
        Assert.Equal(was.Y, mesh.CentreOf(bottom).Y, 1e-9);
    }

    [Fact]
    public void TwoFacesAtOnceAreTwoSeparateExtrusions()
    {
        var mesh = Cube();
        var top = TopOf(mesh);
        var bottom = mesh.Faces.First(f => mesh.NormalOf(f).Y < -0.9);

        mesh.Extrude([top, bottom], 1);

        Assert.Equal(14, mesh.Faces.Count);
        Assert.Equal(2, mesh.Bounds.Max.Y, 1e-9);
        Assert.Equal(-1, mesh.Bounds.Min.Y, 1e-9);
    }

    [Fact]
    public void SaysWhichFacesEndedUpOutside()
    {
        var mesh = Cube();
        var top = TopOf(mesh);

        var after = mesh.Extrude([top], 1);

        // Which is what somebody wants selected, because they are usually about to do it again.
        Assert.Equal(new[] { top }, after);
    }

    [Fact]
    public void AFaceThatIsNotInTheMeshIsIgnored()
    {
        var mesh = Cube();
        Assert.Empty(mesh.Extrude([new Face([0, 1, 2])], 1));
        Assert.Equal(6, mesh.Faces.Count);
    }

    [Fact]
    public void NothingExtrudedChangesNothing()
    {
        var mesh = Cube();
        mesh.Extrude([], 5);
        Assert.Equal(6, mesh.Faces.Count);
        Assert.Equal(8, mesh.Positions.Count);
    }

    // insetting

    [Fact]
    public void LeavesABorderAndKeepsTheOuterEdgeWhereItWas()
    {
        var mesh = Cube();
        var top = TopOf(mesh);
        var wasWide = mesh.Bounds.Max.X;

        mesh.Inset([top], 0.2);

        Assert.Equal(10, mesh.Faces.Count);

        // The shape did not shrink; only the face inside it did.
        Assert.Equal(wasWide, mesh.Bounds.Max.X, 1e-9);
        Assert.True(mesh.AreaOf(top) < 1);
    }

    [Fact]
    public void AFaceSmallerThanTheInsetCollapsesRatherThanInverting()
    {
        var mesh = Cube();
        var top = TopOf(mesh);

        mesh.Inset([top], 10);

        // Scaling past the middle would turn it inside out; this stops there.
        Assert.Equal(0, mesh.AreaOf(top), 1e-9);
        Assert.All(mesh.Positions, at => Assert.True(double.IsFinite(at.X)));
    }

    [Fact]
    public void ThenExtrudingIsHowAWindowIsMade()
    {
        var mesh = Cube();
        var side = mesh.Faces.First(f => mesh.NormalOf(f).X > 0.9);

        mesh.Inset([side], 0.2);
        mesh.Extrude([side], -0.5);

        Assert.Equal(6 + 4 + 4, mesh.Faces.Count);
        Assert.Equal(0, mesh.CentreOf(side).X, 1e-9);
    }

    // subdividing

    [Fact]
    public void TurnsOneQuadIntoFour()
    {
        var mesh = Cube();
        var top = TopOf(mesh);

        var made = mesh.Subdivide([top]);

        Assert.Equal(4, made.Count);
        Assert.Equal(9, mesh.Faces.Count);
        Assert.DoesNotContain(top, mesh.Faces);
    }

    [Fact]
    public void ATriangleBecomesThreeQuads()
    {
        var mesh = new Shape { Kind = ShapeKind.Cone, Sides = 3 }.Build();
        var side = mesh.Faces[0];

        Assert.Equal(3, mesh.Subdivide([side]).Count);
    }

    [Fact]
    public void ThePiecesCoverWhatTheFaceCovered()
    {
        var mesh = Cube();
        var top = TopOf(mesh);
        var was = mesh.AreaOf(top);

        var made = mesh.Subdivide([top]);
        var now = made.Aggregate(0.0, (sum, f) => sum + mesh.AreaOf(f));

        Assert.Equal(was, now, 1e-9);
    }

    // welding

    [Fact]
    public void JoinsPointsInTheSamePlace()
    {
        var mesh = new Shape { Kind = ShapeKind.Stairs, Steps = 3 }.Build();
        var was = mesh.Positions.Count;

        var went = mesh.Weld();

        // Three boxes stacked share corners; welding is what makes them one solid rather than three that
        // happen to touch.
        Assert.True(went > 0);
        Assert.Equal(was - went, mesh.Positions.Count);
    }

    [Fact]
    public void LeavesAShapeThatHasNoDuplicatesAlone()
    {
        var mesh = Cube();
        Assert.Equal(0, mesh.Weld());
        Assert.Equal(8, mesh.Positions.Count);
    }

    [Fact]
    public void AFaceLeftWithTwoCornersIsDropped()
    {
        var mesh = new Mesh([new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(1.00001, 0, 0)]);
        mesh.AddFace([0, 1, 2]);

        mesh.Weld(within: 0.01);

        // Two of its corners welded together, and an edge of no length is not a face any more.
        Assert.Empty(mesh.Faces);
    }

    [Fact]
    public void EveryFaceStillNamesAPointThatExists()
    {
        var mesh = new Shape { Kind = ShapeKind.Stairs, Steps = 6 }.Build();
        mesh.Weld();

        foreach (var face in mesh.Faces)
        {
            foreach (var index in face.Vertices)
            {
                Assert.InRange(index, 0, mesh.Positions.Count - 1);
            }
        }
    }

    // tidying up

    [Fact]
    public void CompactingThrowsAwayPointsNothingUses()
    {
        var mesh = Cube();
        mesh.AddVertex(new Vector3(9, 9, 9));

        Assert.Equal(1, mesh.Compact());
        Assert.Equal(8, mesh.Positions.Count);
    }

    [Fact]
    public void AndLeavesTheFacesPointingAtTheRightOnes()
    {
        var mesh = Cube();
        var top = TopOf(mesh);
        var was = mesh.CentreOf(top).Clone();

        mesh.Positions.Insert(0, new Vector3(9, 9, 9));
        foreach (var face in mesh.Faces)
        {
            for (var i = 0; i < face.Vertices.Count; i++)
            {
                face.Vertices[i] += 1;
            }
        }

        mesh.Compact();

        Assert.Equal(was.Y, mesh.CentreOf(top).Y, 1e-9);
    }

    [Fact]
    public void DeletingAFaceLeavesItsPointsForWhateverElseUsesThem()
    {
        var mesh = Cube();
        mesh.DeleteFaces([TopOf(mesh)]);

        Assert.Equal(5, mesh.Faces.Count);
        Assert.Equal(8, mesh.Positions.Count);
        Assert.Equal(0, mesh.Compact());
    }

    // what is open

    [Fact]
    public void AClosedShapeHasNoBorder()
    {
        Assert.Empty(Cube().OpenFaces);
    }

    [Fact]
    public void AShapeWithAFaceTakenOutDoes()
    {
        var mesh = Cube();
        mesh.DeleteFaces([TopOf(mesh)]);

        // The four sides round the hole, which is what "select the border" should give somebody.
        Assert.Equal(4, mesh.OpenFaces.Count);
    }

    [Fact]
    public void APlaneIsAllBorder()
    {
        var mesh = new Shape { Kind = ShapeKind.Plane, WidthCuts = 0, HeightCuts = 0 }.Build();
        Assert.Single(mesh.OpenFaces);
    }

    // flipping

    [Fact]
    public void TurnsAShapeInsideOut()
    {
        var mesh = Cube();
        var top = TopOf(mesh);
        Assert.Equal(1, mesh.NormalOf(top).Y, 1e-9);

        mesh.Flip();

        Assert.Equal(-1, mesh.NormalOf(top).Y, 1e-9);
    }

    [Fact]
    public void OneFaceAtATimeToo()
    {
        var mesh = Cube();
        var top = TopOf(mesh);

        mesh.FlipFaces([top]);

        Assert.Equal(-1, mesh.NormalOf(top).Y, 1e-9);
        Assert.DoesNotContain(mesh.Faces, f => mesh.NormalOf(f).Y > 0.9);
    }
}

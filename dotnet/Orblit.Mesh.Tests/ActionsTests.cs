namespace Orblit.Mesh.Tests;

public class ActionsTests
{
    private static Mesh Cube() => Shape.Of(ShapeKind.Cube).Build();

    private static Face TopOf(Mesh mesh) => mesh.Faces.First(f => mesh.NormalOf(f).Y > 0.9);

    private static Mesh Plane(int widthCuts, int heightCuts) =>
        new Shape { Kind = ShapeKind.Plane, WidthCuts = widthCuts, HeightCuts = heightCuts }.Build();

    // growing and shrinking

    [Fact]
    public void GrowingTakesInTheNeighbours()
    {
        var mesh = Cube();
        var grown = mesh.Grow([TopOf(mesh)]);

        // The top and the four sides it touches; not the bottom.
        Assert.Equal(5, grown.Count);
    }

    [Fact]
    public void AnAngleKeepsItOnTheFlat()
    {
        var mesh = Plane(3, 3);
        var middle = mesh.Faces[5];

        // Every face of a plane points the same way, so nothing stops it.
        Assert.True(mesh.Grow([middle], withinAngle: 5).Count > 1);
    }

    [Fact]
    public void AnAngleStopsItTurningACorner()
    {
        var mesh = Cube();

        // Ninety degrees to every neighbour, so nothing within five is taken.
        Assert.Single(mesh.Grow([TopOf(mesh)], withinAngle: 5));
    }

    [Fact]
    public void ShrinkingDropsTheOnesOnTheEdge()
    {
        var mesh = Plane(2, 2);

        // Three by three; only the middle one has neighbours all round.
        Assert.Single(MeshActions.Shrink(mesh.Faces));
    }

    [Fact]
    public void ShrinkingOneFaceLeavesNothing()
    {
        var mesh = Cube();
        Assert.Empty(MeshActions.Shrink([TopOf(mesh)]));
    }

    // loops and rings

    [Fact]
    public void ARingGoesRoundACylinder()
    {
        var mesh = new Shape { Kind = ShapeKind.Cylinder, Sides = 8, Capped = false }.Build();
        var face = mesh.Faces[0];
        var edge = MeshActions.EdgeOf(face.Vertices[0], face.Vertices[1]);

        // Either the eight edges round the top or the eight round the bottom.
        var ring = mesh.EdgeRing(edge);
        Assert.True(ring.Count is 8 or 2);
    }

    [Fact]
    public void AFaceLoopGoesAllTheWayRound()
    {
        var mesh = new Shape { Kind = ShapeKind.Cylinder, Sides = 8, Capped = false }.Build();

        Assert.Equal(8, mesh.FaceLoop(mesh.Faces[0]).Count);
    }

    [Fact]
    public void ALoopStopsAtATriangleRatherThanWanderingOff()
    {
        var mesh = new Shape { Kind = ShapeKind.Cone, Sides = 8 }.Build();
        var side = mesh.Faces[0];
        var edge = MeshActions.EdgeOf(side.Vertices[0], side.Vertices[1]);

        // Nothing to cross: a cone's sides are triangles.
        Assert.Single(mesh.EdgeRing(edge));
    }

    // vertices

    [Fact]
    public void CollapsingBringsThemTogetherAndWeldsThem()
    {
        var mesh = Cube();
        var top = TopOf(mesh);
        var was = mesh.Positions.Count;

        mesh.Collapse(top.Vertices);

        // Four corners became one, so the top face is gone with them.
        Assert.True(mesh.Positions.Count < was);
        Assert.DoesNotContain(top, mesh.Faces);
    }

    [Fact]
    public void CollapsingToTheFirstKeepsThatOneWhereItWas()
    {
        var mesh = Cube();
        var top = TopOf(mesh);
        var first = mesh.Positions[top.Vertices[0]].Clone();

        mesh.Collapse(top.Vertices, toFirst: true);

        Assert.Contains(mesh.Positions, at => (at - first).Length < 1e-9);
    }

    [Fact]
    public void SplittingGivesEachFaceItsOwnCorner()
    {
        var mesh = Cube();
        var was = mesh.Positions.Count;

        // A box's corner is used by three faces.
        var made = mesh.Split([0]);

        Assert.Equal(2, made);
        Assert.Equal(was + 2, mesh.Positions.Count);
    }

    [Fact]
    public void SplittingACornerOnlyOneFaceUsesDoesNothing()
    {
        var mesh = Plane(0, 0);
        Assert.Equal(0, mesh.Split([0]));
    }

    // filling a hole

    [Fact]
    public void ClosesAFaceThatWasDeleted()
    {
        var mesh = Cube();
        mesh.DeleteFaces([TopOf(mesh)]);
        Assert.Equal(4, mesh.OpenEdges.Count);

        var made = mesh.FillHole([]);

        Assert.Single(made);
        Assert.Empty(mesh.OpenEdges);
    }

    [Fact]
    public void ThePatchFacesTheWayItsNeighboursDo()
    {
        var mesh = Cube();
        mesh.DeleteFaces([TopOf(mesh)]);

        var patch = mesh.FillHole([]).Single();

        // A patch facing the wrong way is a hole that looks like it is still there.
        Assert.Equal(1, mesh.NormalOf(patch).Y, 1e-9);
    }

    [Fact]
    public void AClosedShapeHasNothingToFill()
    {
        Assert.Empty(Cube().FillHole([]));
    }

    // bevelling

    [Fact]
    public void PutsAFaceWhereTheEdgeWas()
    {
        var mesh = Cube();
        var top = TopOf(mesh);
        var edge = MeshActions.EdgeOf(top.Vertices[0], top.Vertices[1]);

        var made = mesh.Bevel([edge], 0.2);

        Assert.Single(made);
        Assert.Equal(7, mesh.Faces.Count);
    }

    [Fact]
    public void AnOpenEdgeHasNothingToBevelBetween()
    {
        var mesh = Plane(0, 0);
        var edge = MeshActions.EdgeOf(mesh.Faces[0].Vertices[0], mesh.Faces[0].Vertices[1]);

        Assert.Empty(mesh.Bevel([edge], 0.2));
    }

    // bridging

    [Fact]
    public void PutsAFaceBetweenTwoOpenEdges()
    {
        var mesh = new Mesh(
        [
            new Vector3(0, 0, 0),
            new Vector3(1, 0, 0),
            new Vector3(0, 0, 2),
            new Vector3(1, 0, 2),
        ]);

        var made = mesh.Bridge(MeshActions.EdgeOf(0, 1), MeshActions.EdgeOf(2, 3));

        Assert.NotNull(made);
        Assert.Equal(2, mesh.AreaOf(made), 1e-9);
    }

    [Fact]
    public void DoesNotMakeABowTieOfIt()
    {
        var mesh = new Mesh(
        [
            new Vector3(0, 0, 0),
            new Vector3(1, 0, 0),
            new Vector3(1, 0, 2),
            new Vector3(0, 0, 2),
        ]);

        // The two edges run in opposite directions; joining them corner to corner would cross the face over
        // itself and halve its area.
        var made = mesh.Bridge(MeshActions.EdgeOf(0, 1), MeshActions.EdgeOf(2, 3))!;
        Assert.Equal(2, mesh.AreaOf(made), 1e-9);
    }

    // subdividing an edge

    [Fact]
    public void AddsAPointAndPutsItInBothFaces()
    {
        var mesh = Cube();
        var top = TopOf(mesh);
        var edge = MeshActions.EdgeOf(top.Vertices[0], top.Vertices[1]);

        var made = mesh.SubdivideEdges([edge]);

        Assert.Single(made);

        // Both faces along the edge grew a corner, so neither is left with a point in the middle of its
        // neighbour's edge.
        foreach (var face in mesh.FacesOn(MeshActions.EdgeOf(top.Vertices[0], made[0])))
        {
            Assert.Contains(made[0], face.Vertices);
        }
    }

    [Fact]
    public void SeveralCutsAtOnce()
    {
        var mesh = Cube();
        var top = TopOf(mesh);
        var edge = MeshActions.EdgeOf(top.Vertices[0], top.Vertices[1]);

        Assert.Equal(3, mesh.SubdivideEdges([edge], into: 3).Count);
        Assert.Equal(7, top.Vertices.Count);
    }

    // detaching and duplicating

    [Fact]
    public void DetachingTakesTheFacesOutAndHandsThemBack()
    {
        var mesh = Cube();
        var taken = mesh.Detach([TopOf(mesh)]);

        Assert.Equal(5, mesh.Faces.Count);
        Assert.Single(taken.Faces);
        Assert.Equal(4, taken.Positions.Count);
    }

    [Fact]
    public void DuplicatingLeavesTheOriginalAlone()
    {
        var mesh = Cube();
        var copy = mesh.DuplicateFaces([TopOf(mesh)]);

        Assert.Equal(6, mesh.Faces.Count);
        Assert.Single(copy.Faces);
    }

    // merging faces

    [Fact]
    public void TwoSideBySideBecomeOne()
    {
        var mesh = Plane(1, 0);
        var was = mesh.AreaOf(mesh.Faces[0]) + mesh.AreaOf(mesh.Faces[1]);

        var made = mesh.MergeFaces(mesh.Faces.ToList());

        Assert.NotNull(made);
        Assert.Single(mesh.Faces);
        Assert.Equal(was, mesh.AreaOf(made), 1e-9);
    }

    [Fact]
    public void TheMergedFacePointsTheWayThePiecesDid()
    {
        var mesh = Plane(1, 0);

        var made = mesh.MergeFaces(mesh.Faces.ToList())!;
        Assert.Equal(1, mesh.NormalOf(made).Y, 1e-9);
    }

    [Fact]
    public void OneFaceCannotBeMergedWithItself()
    {
        var mesh = Cube();
        Assert.Null(mesh.MergeFaces([TopOf(mesh)]));
    }

    // triangulating

    [Fact]
    public void AQuadBecomesTwoTriangles()
    {
        var mesh = Cube();
        var made = mesh.TriangulateFaces([TopOf(mesh)]);

        Assert.Equal(2, made.Count);
        Assert.Equal(7, mesh.Faces.Count);
        Assert.All(made, f => Assert.True(f.IsTriangle));
    }

    [Fact]
    public void ATriangleIsLeftAsItIs()
    {
        var mesh = new Shape { Kind = ShapeKind.Cone, Sides = 5 }.Build();
        var side = mesh.Faces[0];

        Assert.Equal(new[] { side }, mesh.TriangulateFaces([side]));
    }

    // conforming normals

    [Fact]
    public void TurnsTheOddOneBackTheRightWay()
    {
        var mesh = Cube();
        mesh.FlipFaces([TopOf(mesh)]);

        Assert.Equal(1, mesh.ConformNormals());
        Assert.Equal(1, mesh.NormalOf(TopOf(mesh)).Y, 1e-9);
    }

    [Fact]
    public void LeavesAShapeThatIsAlreadyConsistentAlone()
    {
        Assert.Equal(0, Cube().ConformNormals());
    }

    [Fact]
    public void AShapeThatIsMostlyInsideOutIsTurnedInsideOut()
    {
        var mesh = Cube();

        // Five of six flipped: the majority is what it conforms to.
        mesh.FlipFaces(mesh.Faces.Take(5));

        Assert.Equal(1, mesh.ConformNormals());
    }

    // the pivot

    [Fact]
    public void MovingItToAFacePutsThatFaceAtTheOrigin()
    {
        var mesh = Cube();
        var top = TopOf(mesh);

        var moved = mesh.CentrePivotOn(top.Vertices);

        Assert.Equal(1, moved.Y, 1e-9);
        Assert.Equal(0, mesh.CentreOf(top).Y, 1e-9);
    }

    [Fact]
    public void WithNothingSelectedItGoesToTheMiddle()
    {
        var mesh = Cube();
        mesh.CentrePivotOn([]);

        var box = mesh.Bounds;
        Assert.Equal(0, (box.Min + box.Max).Length, 1e-9);
    }

    // mirroring

    [Fact]
    public void ACopyOnTheOtherSideFacingTheRightWay()
    {
        var mesh = Cube();
        var copy = mesh.Mirrored(x: true);

        Assert.Equal(6, copy.Faces.Count);

        // Reflecting turns the winding inside out; a mirror that forgets is a shape you can only see from
        // inside.
        var top = copy.Faces.First(f => copy.CentreOf(f).Y > 0.9);
        Assert.Equal(1, copy.NormalOf(top).Y, 1e-9);
    }

    [Fact]
    public void AcrossTwoAxesIsNotInsideOut()
    {
        var copy = Cube().Mirrored(x: true, z: true);
        var top = copy.Faces.First(f => copy.CentreOf(f).Y > 0.9);
        Assert.Equal(1, copy.NormalOf(top).Y, 1e-9);
    }

    [Fact]
    public void AcrossNothingIsJustACopy()
    {
        var mesh = Cube();
        var copy = mesh.Mirrored();
        Assert.Equal(mesh.Positions[0].X, copy.Positions[0].X);
    }
}

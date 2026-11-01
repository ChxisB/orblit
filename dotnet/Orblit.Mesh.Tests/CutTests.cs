namespace Orblit.Mesh.Tests;

public class CutTests
{
    /// <summary>One square face on the ground, two metres across, facing up.</summary>
    private static Mesh Quad() => new(
        [new Vector3(0, 0, 0), new Vector3(0, 0, 2), new Vector3(2, 0, 2), new Vector3(2, 0, 0)],
        [new Face([0, 1, 2, 3])]);

    private static Mesh Cube() => Shape.Of(ShapeKind.Cube).Build();

    private static List<Vector3> Loop() =>
    [
        new Vector3(0.5, 0, 0.5),
        new Vector3(0.5, 0, 1.5),
        new Vector3(1.5, 0, 1.5),
        new Vector3(1.5, 0, 0.5),
        new Vector3(0.5, 0, 0.5),
    ];

    // where a point falls

    [Fact]
    public void OnACornerOnAnEdgeOrInTheMiddle()
    {
        var mesh = Quad();
        var face = mesh.Faces.Single();

        Assert.IsType<AtCorner>(mesh.WhereOn(face, new Vector3(0, 0, 0)));
        Assert.Equal(0, ((AtCorner)mesh.WhereOn(face, new Vector3(0, 0, 0))).Corner);
        Assert.IsType<AlongEdge>(mesh.WhereOn(face, new Vector3(1, 0, 0)));
        Assert.IsType<InsideFace>(mesh.WhereOn(face, new Vector3(1, 0, 1)));
    }

    [Fact]
    public void AnEdgePointSaysWhichEdgeAndHowFarAlong()
    {
        var mesh = Quad();
        var where = (AlongEdge)mesh.WhereOn(mesh.Faces.Single(), new Vector3(0, 0, 0.5));

        // the edge leaving corner nought
        Assert.Equal(0, where.Corner);
        Assert.Equal(0.25, where.Along, 1e-9);
    }

    [Fact]
    public void NearEnoughCountsAsOn()
    {
        var mesh = Quad();

        // Somebody aiming at an edge and missing by a centimetre means the edge; a cut that landed just
        // inside would leave a sliver.
        Assert.IsType<AlongEdge>(mesh.WhereOn(mesh.Faces.Single(), new Vector3(0.01, 0, 1)));
        Assert.IsType<InsideFace>(mesh.WhereOn(mesh.Faces.Single(), new Vector3(0.5, 0, 1)));
    }

    // cutting across

    [Fact]
    public void EdgeToEdgeMakesTwoFacesOutOfOne()
    {
        var mesh = Quad();
        var made = mesh.CutFace(mesh.Faces.Single(), [new Vector3(0, 0, 1), new Vector3(2, 0, 1)]);

        Assert.Equal(2, made.Count);
        Assert.Equal(2, mesh.FaceCount);

        // a new corner on each edge
        Assert.Equal(6, mesh.VertexCount);
    }

    [Fact]
    public void TheTwoHalvesCoverTheWholeOfWhatTheyReplaced()
    {
        var mesh = Quad();
        var was = mesh.AreaOf(mesh.Faces.Single());
        mesh.CutFace(mesh.Faces.Single(), [new Vector3(0, 0, 1), new Vector3(2, 0, 1)]);

        var now = mesh.Faces.Aggregate(0.0, (sum, face) => sum + mesh.AreaOf(face));

        // a cut divides, it does not eat
        Assert.Equal(was, now, 1e-9);
    }

    [Fact]
    public void BothHalvesStillFaceTheWayTheFaceDid()
    {
        var mesh = Quad();
        mesh.CutFace(mesh.Faces.Single(), [new Vector3(0, 0, 1), new Vector3(2, 0, 1)]);
        foreach (var face in mesh.Faces)
        {
            Assert.Equal(1, mesh.NormalOf(face).Y, 1e-9);
        }
    }

    [Fact]
    public void CornerToCornerNeedsNoNewVertices()
    {
        var mesh = Quad();
        var made = mesh.CutFace(mesh.Faces.Single(), [new Vector3(0, 0, 0), new Vector3(2, 0, 2)]);

        Assert.Equal(2, made.Count);

        // both ends were already corners
        Assert.Equal(4, mesh.VertexCount);
        foreach (var face in made)
        {
            // two triangles
            Assert.Equal(3, face.Vertices.Count);
        }
    }

    [Fact]
    public void APathThroughTheMiddleKeepsItsBends()
    {
        var mesh = Quad();
        var made = mesh.CutFace(
            mesh.Faces.Single(),
            [new Vector3(0, 0, 1), new Vector3(1, 0, 1.5), new Vector3(1, 0, 0.5), new Vector3(2, 0, 1)]);

        Assert.Equal(2, made.Count);

        // Two ends on edges plus two bends, and the bends belong to both halves.
        Assert.Equal(8, mesh.VertexCount);

        // four corners each side, shared along the cut
        Assert.Equal(12, made[0].Vertices.Count + made[^1].Vertices.Count);
    }

    [Fact]
    public void TheBendsAreSharedNotDuplicated()
    {
        var mesh = Quad();
        var made = mesh.CutFace(
            mesh.Faces.Single(),
            [new Vector3(0, 0, 1), new Vector3(1, 0, 1.2), new Vector3(2, 0, 1)]);
        var shared = made[0].Vertices.ToHashSet();
        shared.IntersectWith(made[^1].Vertices.ToHashSet());

        // both ends and the bend, or the cut is a crack
        Assert.Equal(3, shared.Count);
    }

    [Fact]
    public void APathThatStartsInsideCutsNothing()
    {
        var mesh = Quad();
        var made = mesh.CutFace(mesh.Faces.Single(), [new Vector3(1, 0, 1), new Vector3(2, 0, 1)]);
        Assert.Empty(made);

        // refused, and nothing changed
        Assert.Equal(1, mesh.FaceCount);
    }

    [Fact]
    public void APathFromAnEdgeBackToTheSamePointCutsNothing()
    {
        var mesh = Quad();
        Assert.Empty(mesh.CutFace(mesh.Faces.Single(), [new Vector3(0, 0, 1), new Vector3(0, 0, 1.0001)]));
    }

    [Fact]
    public void TheNewFacesKeepWhatTheOldOneWore()
    {
        var mesh = Quad();
        var worn = mesh.Faces.Single();
        worn.Material = 3;
        worn.Smooth = true;
        worn.Uv = new FaceUv(offset: new Vector2(0.5, 0));

        var made = mesh.CutFace(mesh.Faces.Single(), [new Vector3(0, 0, 1), new Vector3(2, 0, 1)]);

        foreach (var face in made)
        {
            Assert.Equal(3, face.Material);
            Assert.True(face.Smooth);

            // the rule carries over
            Assert.Equal(0.5, face.Uv.Offset.X);
        }
    }

    [Fact]
    public void DrawnCoordinatesDoNotCarryOverBecauseTheirCornersAreGone()
    {
        var mesh = Quad();
        mesh.Faces.Single().Uv = new FaceUv(
            manual: [new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0)]);

        var made = mesh.CutFace(mesh.Faces.Single(), [new Vector3(0, 0, 1), new Vector3(2, 0, 1)]);
        foreach (var face in made)
        {
            Assert.False(face.Uv.IsManual);
        }
    }

    [Fact]
    public void TheNewFacesSitWhereTheOldOneWasInTheList()
    {
        var mesh = Cube();
        var second = mesh.Faces[1];
        var points = mesh.PointsOf(second);

        // A cut corner to corner, which needs no positions worked out.
        mesh.CutFace(second, [points[0], points[2]]);

        Assert.Equal(7, mesh.FaceCount);

        // The faces either side of it are still either side of it.
        Assert.NotNull(mesh.Faces[0]);
        Assert.NotNull(mesh.Faces[3]);
    }

    [Fact]
    public void AFaceTheMeshDoesNotHaveIsRefused()
    {
        var mesh = Quad();
        var stranger = new Face([0, 1, 2]);
        Assert.Empty(mesh.CutFace(stranger, [Vector3.Zero(), new Vector3(1, 0, 0)]));
    }

    // cutting a loop inside

    [Fact]
    public void TheLoopBecomesAFaceAndTheRestSurroundsIt()
    {
        var mesh = Quad();
        var made = mesh.CutFace(mesh.Faces.Single(), Loop());

        Assert.Equal(2, made.Count);
        Assert.Equal(2, mesh.FaceCount);
        var inner = made[^1];
        Assert.Equal(4, inner.Vertices.Count);
        Assert.Equal(1, mesh.AreaOf(inner), 1e-9);
    }

    [Fact]
    public void TheInnerFacePointsTheSameWayWhicheverWayItWasDrawn()
    {
        foreach (var drawn in new[] { Loop(), Enumerable.Reverse(Loop()).ToList() })
        {
            var mesh = Quad();
            var made = mesh.CutFace(mesh.Faces.Single(), drawn);
            Assert.Equal(1, mesh.NormalOf(made[^1]).Y, 1e-9);
        }
    }

    [Fact]
    public void WhatIsLeftHasTheLoopTakenOutOfIt()
    {
        var mesh = Quad();
        var made = mesh.CutFace(mesh.Faces.Single(), Loop());
        var ring = made[0];

        // Four outer corners, four inner, and each end of the slit said twice.
        Assert.Equal(10, ring.Vertices.Count);

        // Area is the square less the loop, which is what "taken out" means.
        Assert.Equal(4 - 1, mesh.AreaOf(ring), 1e-6);
    }

    [Fact]
    public void TheRingAndTheInnerFaceShareNoCorners()
    {
        var mesh = Quad();
        var made = mesh.CutFace(mesh.Faces.Single(), Loop());
        var shared = made[0].Vertices.ToHashSet();
        shared.IntersectWith(made[^1].Vertices.ToHashSet());

        // they meet along the slit, they are not joined there
        Assert.Empty(shared);
    }

    [Fact]
    public void ALoopOfTwoPointsIsNotALoop()
    {
        var mesh = Quad();
        Assert.Empty(mesh.CutFace(mesh.Faces.Single(), [new Vector3(1, 0, 1), new Vector3(1, 0, 1)]));
    }

    // the helpers

    [Fact]
    public void APointInsideAConcaveOutlineIsFound()
    {
        // An L, where a bounding box would say yes to the corner that is out.
        var outline = new List<Vector2>
        {
            new(0, 0),
            new(2, 0),
            new(2, 1),
            new(1, 1),
            new(1, 2),
            new(0, 2),
        };
        Assert.True(MeshCut.PointInsideOutline(outline, new Vector2(0.5, 0.5)));
        Assert.False(MeshCut.PointInsideOutline(outline, new Vector2(1.5, 1.5)));
        Assert.False(MeshCut.PointInsideOutline(outline, new Vector2(3, 3)));
    }

    [Fact]
    public void FlatteningKeepsTheShapeOfAFace()
    {
        var mesh = Quad();
        var face = mesh.Faces.Single();
        var flat = MeshCut.FlattenFace(mesh.PointsOf(face), mesh.NormalOf(face));
        Assert.Equal(4, flat.Flat.Count);

        // A two-metre square is a two-metre square whichever plane it is on.
        var xs = flat.Flat.Select(at => at.X).ToList();
        xs.Sort();
        Assert.Equal(2, xs[^1] - xs[0], 1e-9);
    }

    [Fact]
    public void TheSignOfALoopAreaSaysWhichWayItGoes()
    {
        var up = new Vector3(0, 1, 0);
        var square = new List<Vector3>
        {
            new(0, 0, 0),
            new(0, 0, 1),
            new(1, 0, 1),
            new(1, 0, 0),
        };
        Assert.True(MeshCut.SignedLoopArea(square, up) > 0);
        Assert.True(MeshCut.SignedLoopArea(Enumerable.Reverse(square).ToList(), up) < 0);
    }
}

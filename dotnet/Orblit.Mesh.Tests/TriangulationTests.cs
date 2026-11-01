namespace Orblit.Mesh.Tests;

public class TriangulationTests
{
    /// <summary>
    /// An L, which is the shape of the commonest room somebody draws and the simplest thing a fan gets
    /// wrong.
    /// </summary>
    private static List<Vector3> Ell() =>
    [
        new Vector3(0, 0, 0),
        new Vector3(0, 0, 3),
        new Vector3(2, 0, 3),
        new Vector3(2, 0, 1),
        new Vector3(4, 0, 1),
        new Vector3(4, 0, 0),
    ];

    /// <summary>The area of the triangles a face is cut into.</summary>
    private static double AreaOfPieces(List<Vector3> points, List<int> corners)
    {
        var total = 0.0;
        for (var i = 0; i + 2 < corners.Count; i += 3)
        {
            var a = points[corners[i]];
            var b = points[corners[i + 1]];
            var c = points[corners[i + 2]];
            total += (b - a).Cross(c - a).Length / 2;
        }

        return total;
    }

    [Fact]
    public void ATriangleIsItself()
    {
        Assert.Equal(new[] { 0, 1, 2 }, MeshTriangles.CutUp(Ell().Take(3).ToList(), new Vector3(0, 1, 0)));
    }

    [Fact]
    public void UnderThreeCornersThereIsNothingToCut()
    {
        Assert.Empty(MeshTriangles.CutUp([], new Vector3(0, 1, 0)));
        Assert.Empty(MeshTriangles.CutUp(Ell().Take(2).ToList(), new Vector3(0, 1, 0)));
    }

    [Fact]
    public void ASquareComesOutAsTwoTriangles()
    {
        var square = new List<Vector3>
        {
            new(0, 0, 0),
            new(0, 0, 1),
            new(1, 0, 1),
            new(1, 0, 0),
        };
        Assert.Equal(6, MeshTriangles.CutUp(square, new Vector3(0, 1, 0)).Count);
    }

    [Fact]
    public void AConcaveFaceIsCutIntoPiecesThatCoverExactlyIt()
    {
        var points = Ell();
        var corners = MeshTriangles.CutUp(points, new Vector3(0, 1, 0));

        Assert.Equal((points.Count - 2) * 3, corners.Count);

        // A four-by-one strip and a two-by-two block: eight. A fan over this gives more, because its
        // triangles overlap outside the outline.
        Assert.Equal(8, AreaOfPieces(points, corners), 1e-9);
    }

    [Fact]
    public void EveryPieceIsWoundTheSameWayAsTheFace()
    {
        var points = Ell();
        var corners = MeshTriangles.CutUp(points, new Vector3(0, 1, 0));

        for (var i = 0; i + 2 < corners.Count; i += 3)
        {
            var a = points[corners[i]];
            var b = points[corners[i + 1]];
            var c = points[corners[i + 2]];
            Assert.True((b - a).Cross(c - a).Y > 0, "a piece facing the other way is a hole");
        }
    }

    [Fact]
    public void ItWorksTheOtherWayRoundToo()
    {
        var points = Enumerable.Reverse(Ell()).ToList();
        var corners = MeshTriangles.CutUp(points, new Vector3(0, -1, 0));
        Assert.Equal(8, AreaOfPieces(points, corners), 1e-9);
        for (var i = 0; i + 2 < corners.Count; i += 3)
        {
            var a = points[corners[i]];
            var b = points[corners[i + 1]];
            var c = points[corners[i + 2]];
            Assert.True((b - a).Cross(c - a).Y < 0);
        }
    }

    [Fact]
    public void AKeyholeOutlineIsCutWithoutOverlapping()
    {
        // What a loop cut leaves: round the outside, in through a slit, round the inside, and back out. Very
        // concave, and the case a fan is worst at.
        var mesh = new Mesh(
            [new Vector3(0, 0, 0), new Vector3(0, 0, 4), new Vector3(4, 0, 4), new Vector3(4, 0, 0)],
            [new Face([0, 1, 2, 3])]);
        mesh.CutFace(
            mesh.Faces.Single(),
            [
                new Vector3(1, 0, 1),
                new Vector3(1, 0, 3),
                new Vector3(3, 0, 3),
                new Vector3(3, 0, 1),
                new Vector3(1, 0, 1),
            ]);

        var ring = mesh.Faces[0];
        var corners = MeshTriangles.CutUp(mesh.PointsOf(ring), mesh.NormalOf(ring));

        // the square less the hole, and nothing counted twice
        Assert.Equal(16 - 4, AreaOfPieces(mesh.PointsOf(ring), corners), 1e-6);
    }

    [Fact]
    public void AFaceThatCrossesItselfStillProducesTriangles()
    {
        // A figure of eight has no ears at all. The answer is wrong, because there is no right one, but it is
        // visible, and it is not a spin or a crash.
        var crossed = new List<Vector3>
        {
            new(0, 0, 0),
            new(2, 0, 2),
            new(2, 0, 0),
            new(0, 0, 2),
        };
        var corners = MeshTriangles.CutUp(crossed, new Vector3(0, 1, 0));
        Assert.NotEmpty(corners);
        Assert.Equal(0, corners.Count % 3);
    }

    [Fact]
    public void AFaceWithNoAreaDoesNotSpin()
    {
        var flat = new List<Vector3>
        {
            new(0, 0, 0),
            new(1, 0, 0),
            new(2, 0, 0),
            new(3, 0, 0),
        };
        Assert.Equal(0, MeshTriangles.CutUp(flat, new Vector3(0, 1, 0)).Count % 3);
    }

    [Fact]
    public void ADrawnRoomIsDrawnAsTheRoomItIs()
    {
        var room = new PolyShape(Ell(), height: 2).Build();
        var tris = room.Triangulate();

        // The two caps are six corners each and cut into four; the six walls are quads and cut into two.
        Assert.Equal(4 + 4 + (6 * 2), tris.TriangleCount);

        // And the whole surface adds up to the walls plus both caps.
        var total = 0.0;
        for (var i = 0; i < tris.TriangleCount; i++)
        {
            var a = Corner(tris, i * 3);
            var b = Corner(tris, (i * 3) + 1);
            var c = Corner(tris, (i * 3) + 2);
            total += (b - a).Cross(c - a).Length / 2;
        }

        // Eight a cap, and the walls: 3 + 2 + 2 + 2 + 1 + 4 metres round, two high.
        Assert.Equal((8 * 2) + (14 * 2), total, 1e-6);
    }

    [Fact]
    public void ThePiecesStillIndexTheCornersTheFaceHas()
    {
        var points = Ell();
        foreach (var corner in MeshTriangles.CutUp(points, new Vector3(0, 1, 0)))
        {
            Assert.InRange(corner, 0, points.Count - 1);
        }

        Assert.Equal(0, Math.Min(0, 0));
    }

    private static Vector3 Corner(Triangles tris, int at)
    {
        var vertex = (int)tris.Indices[at];
        return new Vector3(tris.Positions[vertex * 3], tris.Positions[(vertex * 3) + 1], tris.Positions[(vertex * 3) + 2]);
    }
}

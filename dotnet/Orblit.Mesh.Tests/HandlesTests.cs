namespace Orblit.Mesh.Tests;

public class HandlesTests
{
    private static Mesh Cube() => Shape.Of(ShapeKind.Cube).Build();

    private static Face Facing(Mesh mesh, Vector3 way) => mesh.Faces.First(face => mesh.NormalOf(face).Dot(way) > 0.99);

    [Fact]
    public void TheCentreOfASelectionIsTheMiddleOfItsCorners()
    {
        var mesh = Cube();
        foreach (var face in mesh.Faces)
        {
            var centre = mesh.CentreOfPoints(face.Vertices)!;
            // the same answer as asking about the face itself
            Assert.Equal(mesh.CentreOf(face), centre);
        }

        // Every corner of a cube averages to the middle of it, which is the pivot a handle over the whole
        // thing should sit at.
        Assert.Equal(
            mesh.CentreOfPoints(mesh.Faces.SelectMany(face => face.Vertices)),
            mesh.CentreOfPoints(Enumerable.Range(0, mesh.VertexCount)));
    }

    [Fact]
    public void TheCentreOfNothingIsNothing()
    {
        Assert.Null(Cube().CentreOfPoints([]));

        // and of nothing that is
        Assert.Null(Cube().CentreOfPoints([99, -3]));
    }

    [Fact]
    public void MovingCornersMovesOnlyThoseCorners()
    {
        var mesh = Cube();
        var face = mesh.Faces[0];
        var before = mesh.Positions.Select(at => at.Clone()).ToList();

        mesh.MovePoints(face.Vertices, new Vector3(0, 2, 0));

        var moved = face.Vertices.ToHashSet();
        for (var i = 0; i < mesh.Positions.Count; i++)
        {
            var shift = mesh.Positions[i] - before[i];
            Assert.True(shift.Y == (moved.Contains(i) ? 2 : 0), $"vertex {i}");
            Assert.Equal(0.0, shift.X);
        }

        Assert.True(mesh.FaceCount == 6, "nothing was created or dropped");
    }

    [Fact]
    public void AnIndexTheMeshDoesNotHaveIsSkippedNotThrownAt()
    {
        var mesh = Cube();
        var before = mesh.Positions[0].Clone();
        mesh.MovePoints([-1, 900], new Vector3(5, 5, 5));
        Assert.Equal(before, mesh.Positions[0]);
    }

    [Fact]
    public void TurningAboutAPointLeavesThatPointWhereItIs()
    {
        var mesh = Cube();
        var about = new Vector3(0, 0, 0);
        var turn = Quaternion.AxisAngle(new Vector3(0, 1, 0), 3.14159265358979 / 2).AsRotationMatrix();
        var was = mesh.Positions[0].Clone();

        mesh.TurnPoints([0], turn, about);
        var now = mesh.Positions[0];

        // a rotation about the origin keeps the distance
        Assert.Equal(was.Length, now.Length, 1e-9);

        // and the axis it turns about
        Assert.Equal(was.Y, now.Y, 1e-9);

        // A quarter turn about +Y takes +X to -Z, which is the sense the object handle turns in. Taking a
        // quaternion here would give the opposite.
        Assert.Equal(-was.X, now.Z, 1e-9);
        Assert.Equal(was.Z, now.X, 1e-9);
    }

    [Fact]
    public void TurningAboutACornerPinsThatCorner()
    {
        var mesh = Cube();
        var pin = mesh.Positions[0].Clone();
        mesh.TurnPoints([0, 1], Quaternion.AxisAngle(new Vector3(0, 1, 0), 0.7).AsRotationMatrix(), pin);
        Assert.Equal(0, (mesh.Positions[0] - pin).Length, 1e-9);
    }

    [Fact]
    public void ScalingAboutAPointPushesAwayFromIt()
    {
        var mesh = Cube();
        var about = Vector3.Zero();
        var was = mesh.Positions[0].Clone();
        mesh.ScalePoints([0], new Vector3(2, 1, 1), about);
        Assert.Equal(was.X * 2, mesh.Positions[0].X, 1e-9);
        Assert.Equal(was.Y, mesh.Positions[0].Y, 1e-9);
    }

    [Fact]
    public void TwoFacesOfACornerPointAlongTheCorner()
    {
        var mesh = Cube();

        // The faces whose normals are +X and +Y, whichever way round the cube lists them.
        var across = mesh.NormalAcross([Facing(mesh, new Vector3(1, 0, 0)), Facing(mesh, new Vector3(0, 1, 0))])!;

        // normalised
        Assert.Equal(1, across.Length, 1e-9);

        // evenly between them
        Assert.Equal(across.Y, across.X, 1e-9);
        Assert.Equal(0, across.Z, 1e-9);
    }

    [Fact]
    public void TwoFacesBackToBackHaveNoDirectionToBePulledAlong()
    {
        var mesh = Cube();
        Assert.Null(mesh.NormalAcross([Facing(mesh, new Vector3(1, 0, 0)), Facing(mesh, new Vector3(-1, 0, 0))]));
    }

    [Fact]
    public void ADragIsAMoveRepeatedNotAMoveAccumulated()
    {
        // What the viewport does: the mesh is rebuilt from the one it started with and moved by the whole
        // shift, every frame. So the same shift applied to a fresh copy always lands in the same place.
        var start = Cube();
        var face = start.Faces[0].Vertices.ToList();

        var once = start.Copy();
        once.MovePoints(face, new Vector3(0, 3, 0));
        var again = start.Copy();
        again.MovePoints(face, new Vector3(0, 3, 0));

        for (var i = 0; i < once.Positions.Count; i++)
        {
            Assert.Equal(again.Positions[i], once.Positions[i]);
        }
    }
}

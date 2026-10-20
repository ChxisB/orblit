namespace Orblit.Mesh.Tests;

public class ShellTests
{
    private static Mesh Cube() => Shape.Of(ShapeKind.Cube).Build();

    private static Mesh Room() => new PolyShape(
        [
            new Vector3(0, 0, 0),
            new Vector3(0, 0, 3),
            new Vector3(2, 0, 3),
            new Vector3(2, 0, 1),
            new Vector3(4, 0, 1),
            new Vector3(4, 0, 0),
        ],
        height: 2).Build();

    [Fact]
    public void GrowingByNothingChangesNothing()
    {
        var was = Cube();
        var now = was.Grown(0);
        for (var i = 0; i < was.VertexCount; i++)
        {
            Assert.Equal(was.Positions[i], now.Positions[i]);
        }
    }

    [Fact]
    public void EveryFaceEndsUpExactlyTheDistanceOutItWasAskedFor()
    {
        var was = Cube();
        var now = was.Grown(0.25);

        for (var i = 0; i < was.Faces.Count; i++)
        {
            var normal = was.NormalOf(was.Faces[i]);
            var moved = now.CentreOf(now.Faces[i]) - was.CentreOf(was.Faces[i]);

            // Along the normal by exactly the distance, and not sideways at all.
            Assert.True(Math.Abs(moved.Dot(normal) - 0.25) <= 1e-9, $"face {i}");
            Assert.Equal(0, (moved - (normal * 0.25)).Length, 1e-9);
        }
    }

    [Fact]
    public void ACornerMovesFurtherThanAFaceWhichIsWhatKeepsItSharp()
    {
        var was = Cube();
        var now = was.Grown(0.5);

        // A cube corner is a metre-and-a-bit along its diagonal for half a metre of face: root three times
        // as far.
        var corner = (now.Positions[0] - was.Positions[0]).Length;
        Assert.Equal(0.5 * 1.7320508, corner, 1e-6);
    }

    [Fact]
    public void ANegativeDistanceShrinksIt()
    {
        var was = Cube();
        var now = was.Grown(-0.1);
        Assert.True(now.Bounds.Max.X < was.Bounds.Max.X);
        Assert.Equal(was.Bounds.Max.X - 0.1, now.Bounds.Max.X, 1e-9);
    }

    [Fact]
    public void NothingAboutTheShapeChangesButWhereItsCornersAre()
    {
        var was = Cube();
        was.Faces[0].Material = 2;
        was.Faces[0].Smooth = true;
        var now = was.Grown(0.3);

        Assert.Equal(was.FaceCount, now.FaceCount);
        Assert.Equal(was.VertexCount, now.VertexCount);
        Assert.Equal(2, now.Faces[0].Material);
        Assert.True(now.Faces[0].Smooth);
        Assert.Equal(was.Faces[0].Vertices, now.Faces[0].Vertices);
    }

    [Fact]
    public void TheOriginalIsLeftAlone()
    {
        var was = Cube();
        var before = was.Positions[0].Clone();
        was.Grown(1);
        Assert.Equal(before, was.Positions[0]);
    }

    [Fact]
    public void ADrawnRoomGrowsWithoutTurningInsideOut()
    {
        var room = Room();

        var grown = room.Grown(0.2);

        // Every face still points the way it did. Not "away from the middle": this room is concave, and the
        // faces of its dent genuinely point back towards its own centre.
        for (var i = 0; i < room.Faces.Count; i++)
        {
            var was = room.NormalOf(room.Faces[i]);
            var now = grown.NormalOf(grown.Faces[i]);
            Assert.True((now - was).Length < 1e-9, $"face {i}");
        }

        // And it is bigger everywhere.
        Assert.Equal(room.Bounds.Max.Y + 0.2, grown.Bounds.Max.Y, 1e-9);
        Assert.Equal(room.Bounds.Min.X - 0.2, grown.Bounds.Min.X, 1e-9);
    }

    [Fact]
    public void AReflexCornerIsGrownTooNotLeftBehind()
    {
        var room = Room();

        var was = room.Copy();
        var now = room.Grown(0.2);
        var moved = 0;
        for (var i = 0; i < was.VertexCount; i++)
        {
            if ((now.Positions[i] - was.Positions[i]).Length > 1e-9)
            {
                moved++;
            }
        }

        Assert.True(moved == was.VertexCount, "every corner, dents included");
    }

    [Fact]
    public void ACornerWhereFacesCancelIsLeftWhereItIs()
    {
        // Two faces back to back: their normals sum to nothing, and there is no direction to move the corner
        // in.
        var sheet = new Mesh(
            [new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 0, 1)],
            [new Face([0, 1, 2]), new Face([2, 1, 0])]);
        var grown = sheet.Grown(0.5);
        for (var i = 0; i < sheet.VertexCount; i++)
        {
            Assert.Equal(sheet.Positions[i], grown.Positions[i]);
        }
    }

    [Fact]
    public void AnEmptyMeshGrowsToNothingRatherThanThrowing()
    {
        Assert.True(new Mesh().Grown(1).IsEmpty);
    }
}

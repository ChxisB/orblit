namespace Orblit.Mesh.Tests;

public class UvTests
{
    private static Mesh Cube() => Shape.Of(ShapeKind.Cube).Build();

    /// <summary>The face pointing a given way.</summary>
    private static Face Facing(Mesh mesh, Vector3 way) =>
        mesh.Faces.First(face => mesh.NormalOf(face).Dot(way) > 0.99);

    private static Vector3 Up() => new(0, 1, 0);

    // the rule

    [Fact]
    public void AFaceNobodyTouchedIsAutomatic()
    {
        Assert.False(new FaceUv().IsManual);
        Assert.False(Cube().Faces[0].Uv.IsManual);
    }

    [Fact]
    public void TilingKeepsTheTextureTheSameSizeOnABiggerFace()
    {
        var small = Cube();
        var large = Shape.Of(ShapeKind.Cube).Build();

        // Twice as wide, and nothing else changed.
        foreach (var at in large.Positions)
        {
            at.X *= 2;
        }

        var a = small.UvBoundsOf([Facing(small, Up())])!.Value;
        var b = large.UvBoundsOf([Facing(large, Up())])!.Value;

        // Which of the two coordinate axes the world's X became is the projection's business, so
        // this asks about the wider of them rather than naming one.
        static double Longest((Vector2 Min, Vector2 Max) box) =>
            Math.Max(box.Max.X - box.Min.X, box.Max.Y - box.Min.Y);

        // twice the wall, twice the bricks
        Assert.Equal(Longest(a) * 2, Longest(b), 1e-9);
    }

    [Fact]
    public void StretchingPutsTheWholeTextureOnWhateverShapeTheFaceIs()
    {
        var mesh = Cube();
        foreach (var at in mesh.Positions)
        {
            at.X *= 3;
        }

        var face = Facing(mesh, Up());
        face.Uv = new FaceUv(fit: UvFit.Stretch);

        var box = mesh.UvBoundsOf([face])!.Value;
        Assert.Equal(0, box.Min.X, 1e-9);
        Assert.Equal(0, box.Min.Y, 1e-9);
        Assert.Equal(1, box.Max.X, 1e-9);

        // both axes filled, whatever that does to the picture
        Assert.Equal(1, box.Max.Y, 1e-9);
    }

    [Fact]
    public void FittingKeepsThePictureSquareAndLeavesTheSpareRoom()
    {
        var mesh = Cube();
        foreach (var at in mesh.Positions)
        {
            at.X *= 3;
        }

        var face = Facing(mesh, Up());
        face.Uv = new FaceUv(fit: UvFit.Fit);

        var box = mesh.UvBoundsOf([face])!.Value;
        var spans = new List<double> { box.Max.X - box.Min.X, box.Max.Y - box.Min.Y };
        spans.Sort();

        // the long way fills it
        Assert.Equal(1, spans[^1], 1e-9);

        // and the short way keeps its proportion
        Assert.Equal(1.0 / 3, spans[0], 1e-9);
    }

    [Fact]
    public void AnEdgeOnFaceDoesNotDivideByNothing()
    {
        var mesh = new Mesh(
            [new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(2, 0, 0)],
            [new Face([0, 1, 2], uv: new FaceUv(fit: UvFit.Stretch))]);
        foreach (var at in mesh.UvsOf(mesh.Faces[0]))
        {
            Assert.True(double.IsFinite(at.X));
            Assert.True(double.IsFinite(at.Y));
        }
    }

    [Fact]
    public void TwoFacesPointingTheSameWayGetTheSameAxes()
    {
        var a = FaceUv.AxesFor(Up());
        var b = FaceUv.AxesFor(Up());
        Assert.Equal(a.U, b.U);
        Assert.Equal(a.V, b.V);
    }

    [Fact]
    public void TheOffsetMovesTheTextureAndTheScaleRepeatsIt()
    {
        var mesh = Cube();
        var face = Facing(mesh, Up());
        var plain = mesh.UvsOf(face)[0].Clone();

        face.Uv = new FaceUv(offset: new Vector2(0.5, 0));
        Assert.Equal(plain.X + 0.5, mesh.UvsOf(face)[0].X, 1e-9);

        face.Uv = new FaceUv(scale: new Vector2(2, 1));
        Assert.Equal(plain.X * 2, mesh.UvsOf(face)[0].X, 1e-9);
    }

    [Fact]
    public void AQuarterTurnTakesOneAxisOntoTheOther()
    {
        var mesh = Cube();
        var face = Facing(mesh, Up());
        face.Uv = new FaceUv(rotation: 90);
        var plain = new Face(face.Vertices);
        var was = mesh.UvsOf(plain)[0];
        var now = mesh.UvsOf(face)[0];
        Assert.Equal(-was.Y, now.X, 1e-9);
        Assert.Equal(was.X, now.Y, 1e-9);
    }

    // freezing

    [Fact]
    public void CoordinatesComeOutTheSameAsTheRuleGave()
    {
        var mesh = Cube();
        var face = Facing(mesh, Up());
        var was = mesh.UvsOf(face);

        mesh.FreezeUvs([face]);

        Assert.True(face.Uv.IsManual);
        var now = mesh.UvsOf(face);
        for (var i = 0; i < was.Count; i++)
        {
            Assert.Equal(was[i].X, now[i].X, 1e-12);
            Assert.Equal(was[i].Y, now[i].Y, 1e-12);
        }
    }

    [Fact]
    public void AFrozenFaceStopsFollowingTheShape()
    {
        var mesh = Cube();
        var face = Facing(mesh, Up());
        mesh.FreezeUvs([face]);
        var was = mesh.UvsOf(face)[0].Clone();

        foreach (var at in mesh.Positions)
        {
            at.X *= 5;
        }

        // drawn coordinates belong to nobody but the person who drew them
        Assert.Equal(was.X, mesh.UvsOf(face)[0].X, 1e-12);
    }

    [Fact]
    public void LettingGoReturnsToTheRuleAndKeepsTheSettings()
    {
        var mesh = Cube();
        var face = Facing(mesh, Up());
        face.Uv = new FaceUv(offset: new Vector2(0.25, 0));
        mesh.FreezeUvs([face]);
        MeshUvs.ReleaseUvs([face]);

        Assert.False(face.Uv.IsManual);
        Assert.Equal(0.25, face.Uv.Offset.X);
    }

    [Fact]
    public void CoordinatesForTheWrongNumberOfCornersAreIgnored()
    {
        var mesh = Cube();
        var face = Facing(mesh, Up());

        // Two, for a face with four. What a face looks like after being cut.
        face.Uv = new FaceUv(manual: [Vector2.Zero(), new Vector2(1, 1)]);

        // back to the rule rather than out of range
        Assert.Equal(4, mesh.UvsOf(face).Count);
    }

    // editing

    [Fact]
    public void NudgingARuleMovesItsOffsetAndADrawnOneItsCorners()
    {
        var mesh = Cube();
        var auto = Facing(mesh, Up());
        var drawn = Facing(mesh, new Vector3(0, -1, 0));
        mesh.FreezeUvs([drawn]);

        var wasAuto = mesh.UvsOf(auto)[0].Clone();
        var wasDrawn = mesh.UvsOf(drawn)[0].Clone();
        MeshUvs.NudgeUvs([auto, drawn], new Vector2(0.3, -0.2));

        // still a rule
        Assert.False(auto.Uv.IsManual);
        Assert.Equal(wasAuto.X + 0.3, mesh.UvsOf(auto)[0].X, 1e-9);
        Assert.Equal(wasDrawn.Y - 0.2, mesh.UvsOf(drawn)[0].Y, 1e-9);
    }

    [Fact]
    public void ScalingASelectionKeepsTheFacesWhereTheyAreToEachOther()
    {
        var mesh = Cube();
        var faces = mesh.Faces.Take(3).ToList();
        mesh.ProjectBox(faces);
        MeshUvs.NudgeUvs([faces[1]], new Vector2(4, 0));

        var apart = mesh.UvsOf(faces[1])[0].X - mesh.UvsOf(faces[0])[0].X;
        mesh.ScaleUvs(faces, new Vector2(2, 2));
        var now = mesh.UvsOf(faces[1])[0].X - mesh.UvsOf(faces[0])[0].X;

        // about the selection, not each face on its own
        Assert.Equal(apart * 2, now, 1e-9);
    }

    [Fact]
    public void FittingPutsTheWholeSelectionInTheSquare()
    {
        var mesh = Cube();
        var faces = mesh.Faces.Take(4).ToList();
        mesh.FitUvs(faces);

        var box = mesh.UvBoundsOf(faces)!.Value;
        Assert.Equal(0, box.Min.X, 1e-9);
        Assert.Equal(0, box.Min.Y, 1e-9);
        Assert.Equal(1, Math.Max(box.Max.X, box.Max.Y), 1e-9);
    }

    [Fact]
    public void BoundsAreNullWhenThereIsNothingRatherThanAPoint()
    {
        Assert.Null(Cube().UvBoundsOf([]));
    }

    [Fact]
    public void APlanarProjectionRunsOneTextureAcrossSeveralFaces()
    {
        var mesh = Cube();

        // The top and one side, which point differently: projected together they share axes, so
        // the side is smeared, which is the thing this does and the reason not to use it on a box.
        var top = Facing(mesh, Up());
        var side = Facing(mesh, new Vector3(1, 0, 0));
        mesh.ProjectPlanar([top, side]);

        Assert.True(top.Uv.IsManual);
        Assert.True(side.Uv.IsManual);

        // Where they meet in the world they meet in the texture too.
        var shared = top.Vertices.ToHashSet();
        shared.IntersectWith(side.Vertices);
        Assert.NotEmpty(shared);
        foreach (var index in shared)
        {
            var a = mesh.UvsOf(top)[top.Vertices.IndexOf(index)];
            var b = mesh.UvsOf(side)[side.Vertices.IndexOf(index)];
            Assert.Equal(0, (a - b).Length, 1e-9);
        }
    }

    [Fact]
    public void ABoxProjectionPutsEachFaceOnItsOwnAxes()
    {
        var mesh = Cube();
        mesh.ProjectBox(mesh.Faces);
        foreach (var face in mesh.Faces)
        {
            Assert.True(face.Uv.IsManual);

            // Every face covers a unit square, because a unit cube's faces do.
            var box = mesh.UvBoundsOf([face])!.Value;
            Assert.Equal(1, box.Max.X - box.Min.X, 1e-9);
        }
    }

    [Fact]
    public void FlippingARuleSetsItsFlagAndADrawnOneMirrorsInPlace()
    {
        var mesh = Cube();
        var auto = Facing(mesh, Up());
        mesh.FlipUvs([auto], u: true);
        Assert.True(auto.Uv.FlipU);

        var drawn = Facing(mesh, new Vector3(0, -1, 0));
        mesh.FreezeUvs([drawn]);
        var was = mesh.UvBoundsOf([drawn])!.Value;
        mesh.FlipUvs([drawn], u: true);
        var now = mesh.UvBoundsOf([drawn])!.Value;

        // mirrored about its own middle, so it stays where it was
        Assert.Equal(was.Min.X, now.Min.X, 1e-9);
        Assert.Equal(was.Max.X, now.Max.X, 1e-9);
    }

    [Fact]
    public void TurningARuleAddsToItsAngle()
    {
        var mesh = Cube();
        var face = Facing(mesh, Up());
        mesh.TurnUvs([face], 30);
        mesh.TurnUvs([face], 15);
        Assert.Equal(45.0, face.Uv.Rotation);
        Assert.False(face.Uv.IsManual);
    }

    // through the file

    [Fact]
    public void ARuleSurvivesARoundTrip()
    {
        var face = new Face(
            [0, 1, 2],
            uv: new FaceUv(
                fit: UvFit.Fit,
                offset: new Vector2(0.25, -0.5),
                scale: new Vector2(2, 3),
                rotation: 45,
                flipV: true,
                swap: true));
        var back = Face.FromJson(face.ToJson())!;
        Assert.Equal(UvFit.Fit, back.Uv.Fit);
        Assert.Equal(0.25, back.Uv.Offset.X);
        Assert.Equal(3.0, back.Uv.Scale.Y);
        Assert.Equal(45.0, back.Uv.Rotation);
        Assert.True(back.Uv.FlipV);
        Assert.False(back.Uv.FlipU);
        Assert.True(back.Uv.Swap);
    }

    [Fact]
    public void DrawnCoordinatesSurviveOneToo()
    {
        var face = new Face(
            [0, 1, 2],
            uv: new FaceUv(manual: [new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5, 1)]));
        var back = Face.FromJson(face.ToJson())!;
        Assert.True(back.Uv.IsManual);
        Assert.Equal(3, back.Uv.Manual!.Count);
        Assert.Equal(1.0, back.Uv.Manual[2].Y);
    }

    [Fact]
    public void AFaceWithNothingSaidAboutItsTextureWritesNothing()
    {
        Assert.False(new Face([0, 1, 2]).ToJson().ContainsKey("uv"));
    }

    [Fact]
    public void ACopyOfAFaceGetsItsOwnCoordinates()
    {
        var face = new Face(
            [0, 1, 2],
            uv: new FaceUv(manual: [Vector2.Zero(), Vector2.Zero(), Vector2.Zero()]));
        var copy = face.Copy();
        Assert.NotNull(copy.Uv.Manual);

        // the rule is immutable, so sharing it is safe
        Assert.Same(copy.Uv, face.Uv);
    }

    [Fact]
    public void WhatAMeshIsDrawnWithComesFromTheFaces()
    {
        var mesh = Cube();
        var face = Facing(mesh, Up());
        face.Uv = new FaceUv(manual: [new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1)]);

        var tris = mesh.Triangulate();

        // The face's own corners, wherever they ended up in the buffer.
        var at = mesh.Faces.IndexOf(face);
        Assert.True(at >= 0);
        Assert.Equal(tris.VertexCount, tris.Uvs.Length / 2);

        // Somewhere in there is a corner at exactly one, one, which the tiling rule would never
        // produce for a cube centred on the origin.
        var found = false;
        for (var i = 0; i + 1 < tris.Uvs.Length; i += 2)
        {
            if (Math.Abs(tris.Uvs[i] - 1) < 1e-6 && Math.Abs(tris.Uvs[i + 1] - 1) < 1e-6)
            {
                found = true;
            }
        }

        Assert.True(found);
    }
}

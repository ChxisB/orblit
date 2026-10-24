using Orblit.Filament;
using Orblit.VectorMath;

namespace Orblit.Stage.Tests;

public class ParticleSheetTests
{
    private const int Columns = 4;
    private const int Rows = 3;
    private const double Spacing = 0.25;
    private const double Closely = 1e-9;

    private static ParticleSheet Sheet(int columns = Columns, int rows = Rows) => new(columns, rows, Spacing);

    // Where particle i sits when the sheet is as it was made.
    private static Vector3 Rest(int i) => new((i % Columns) * Spacing, -(i / Columns) * Spacing, 0);

    private static Matrix4 Move(Vector3 by) => Matrix4.TranslationValues(by.X, by.Y, by.Z);

    // What the renderer does to a vertex: the joint's world transform, then the inverse bind, applied
    // to the vertex at rest.
    private static Vector3 Skinned(IReadOnlyList<JointPose> poses, int i) =>
        poses[i].Transform.Multiplied(Move(-Rest(i))).Transformed3(Rest(i));

    private static Vector3 At(List<double> values, int i) =>
        new(values[i * 3], values[(i * 3) + 1], values[(i * 3) + 2]);

    [Fact]
    public void TheGlbHasAVertexAndAJointToAParticle()
    {
        var glb = new Glb(Sheet().ToGlb());

        Assert.Equal(Columns * Rows, glb.Accessors.Count(glb.Attribute("POSITION")));
        Assert.Equal(Columns * Rows, Glb.List(glb.First("skins")["joints"]).Count);
        Assert.Equal(1 + (Columns * Rows), glb.ListOf("nodes").Count);
    }

    [Fact]
    public void TheGlbLaysTheVerticesOutAsTheParticlesAreNumbered()
    {
        var glb = new Glb(Sheet().ToGlb());

        var positions = glb.Accessors.Floats(glb.Attribute("POSITION"), 3);

        for (var i = 0; i < Columns * Rows; i++)
        {
            Near.Vector(At(positions, i), Rest(i), tolerance: Closely);
        }
    }

    [Fact]
    public void TheGlbPutsEveryVertexWhollyOnItsOwnJoint()
    {
        var glb = new Glb(Sheet().ToGlb());

        var joints = glb.Accessors.Floats(glb.Attribute("JOINTS_0"), 4);
        var weights = glb.Accessors.Floats(glb.Attribute("WEIGHTS_0"), 4);

        for (var i = 0; i < Columns * Rows; i++)
        {
            Assert.Equal<double>([i, 0, 0, 0], joints.GetRange(i * 4, 4));
            Assert.Equal<double>([1, 0, 0, 0], weights.GetRange(i * 4, 4));
        }
    }

    [Fact]
    public void TheGlbBindsEachJointByMovingBackFromWhereItSits()
    {
        var glb = new Glb(Sheet().ToGlb());

        var bind = glb.Accessors.Floats(Glb.Whole(glb.First("skins")["inverseBindMatrices"]), 16);

        for (var i = 0; i < Columns * Rows; i++)
        {
            var matrix = Matrix4.FromList(bind.GetRange(i * 16, 16));
            Near.Vector(matrix.Transformed3(Rest(i)), Vector3.Zero(), tolerance: Closely);
        }
    }

    [Fact]
    public void TheGlbSetsEachJointNodeWhereItsVertexIs()
    {
        var glb = new Glb(Sheet().ToGlb());
        var nodes = glb.ListOf("nodes");

        for (var i = 0; i < Columns * Rows; i++)
        {
            var translation = Glb.List(Glb.Map(nodes[i + 1])["translation"]).Select(Glb.Number).ToList();
            Near.Vector(At(translation, 0), Rest(i), tolerance: Closely);
        }
    }

    [Fact]
    public void TheGlbWindsEveryTriangleToFaceForwardAtRest()
    {
        var glb = new Glb(Sheet().ToGlb());
        var positions = glb.Accessors.Floats(glb.Attribute("POSITION"), 3);

        var indices = glb.Accessors.Indices(Glb.Whole(glb.Primitive["indices"]));

        Assert.Equal((Columns - 1) * (Rows - 1) * 6, indices.Count);
        for (var t = 0; t < indices.Count; t += 3)
        {
            var a = At(positions, (int)indices[t]);
            var face = (At(positions, (int)indices[t + 1]) - a).Cross(At(positions, (int)indices[t + 2]) - a);
            Assert.True(face.Z > 0, $"triangle {t / 3}");
        }
    }

    [Fact]
    public void TheGlbDrawsBothFacesInTheColourItWasGiven()
    {
        Assert.Equal(true, new Glb(Sheet().ToGlb()).First("materials")["doubleSided"]);

        var red = new Glb(new ParticleSheet(2, 2, 1, colour: [0.9, 0.1, 0.1]).ToGlb());
        var factor = Glb.List(Glb.Map(red.First("materials")["pbrMetallicRoughness"])["baseColorFactor"]);

        Assert.Equal<double>([0.9, 0.1, 0.1, 1], factor.Select(Glb.Number));
    }

    [Fact]
    public void PosesArePlainMovesToTheParticleWhileTheSheetIsAsMade()
    {
        var poses = Sheet().Poses(Rest);

        Assert.Equal(Columns * Rows, poses.Count);
        for (var i = 0; i < poses.Count; i++)
        {
            Assert.Equal(0, poses[i].Skin);
            Assert.Equal(i, poses[i].Joint);
            Near.Matrix(poses[i].Transform, Move(Rest(i)), tolerance: 1e-12);
        }
    }

    [Fact]
    public void PosesPutEveryVertexOnItsParticleHoweverTheClothLies()
    {
        // Part of a turn, not a quarter of one: a quarter looks the same turned either way.
        var lie = Move(new Vector3(1, 2, 3)).Multiplied(Matrix4.RotationY(0.5));
        Vector3 Where(int i) => lie.Transformed3(Rest(i));

        var poses = Sheet().Poses(Where);

        for (var i = 0; i < poses.Count; i++)
        {
            Near.Vector(Skinned(poses, i), Where(i), tolerance: Closely);
        }
    }

    [Fact]
    public void PosesTurnAClothThatLiesTurnedByThatTurn()
    {
        var turn = Matrix4.RotationY(0.5);

        var poses = Sheet().Poses(i => turn.Transformed3(Rest(i)));

        foreach (var pose in poses)
        {
            var got = pose.Transform.GetRotation();
            var want = turn.GetRotation();
            for (var e = 0; e < 9; e++)
            {
                Assert.Equal(want[e], got[e], Closely);
            }
        }
    }

    [Fact]
    public void PosesStayARigidTurnWhereTheClothIsFoldedAndStretched()
    {
        // A crease down the middle, one side lifted, and the rows sheared.
        static Vector3 Folded(int i)
        {
            var at = Rest(i);
            var lift = at.X > 0.3 ? (at.X - 0.3) * 2 : 0.0;
            return new Vector3((at.X * 1.3) + (at.Y * 0.4), at.Y, at.Z + lift);
        }

        foreach (var pose in Sheet().Poses(Folded))
        {
            var turn = pose.Transform.GetRotation();
            Assert.Equal(1, turn.Determinant(), Closely);
            var shouldBeIdentity = pose.Transform.GetRotation();
            shouldBeIdentity.Transpose();
            shouldBeIdentity.Multiply(turn);
            for (var e = 0; e < 9; e++)
            {
                Assert.Equal(e % 4 == 0 ? 1 : 0, shouldBeIdentity[e], Closely);
            }
        }
    }

    [Fact]
    public void PosesTiltTheFaceWhereTheClothIsTilted()
    {
        // Each row 0.1 nearer than the one above it, over rows 0.25 apart. The face looks up as well as
        // out, in that ratio.
        var poses = Sheet().Poses(i =>
        {
            var at = Rest(i);
            at.Z += (i / Columns) * 0.1;
            return at;
        });

        var normal = poses[Columns + 1].Transform.GetRotation().Transformed(new Vector3(0, 0, 1));

        Assert.Equal(0.1 / 0.25, normal.Y / normal.Z, Closely);
    }

    [Fact]
    public void PosesKeepTheMoveAndDropTheTurnWhereTheClothHasNoFace()
    {
        var at = new Vector3(1, 2, 3);

        foreach (var pose in Sheet().Poses(_ => at))
        {
            Near.Matrix(pose.Transform, Move(at), tolerance: 1e-12);
        }
    }

    [Fact]
    public void ASheetIsRefusedWhenItHasNoCellsTooManyJointsOrNoSize()
    {
        Assert.Throws<ArgumentException>(() => Sheet(columns: 1));
        Assert.Throws<ArgumentException>(() => Sheet(rows: 1));
        Assert.Throws<ArgumentException>(() => new ParticleSheet(17, 16, 0.1));
        Assert.Null(Record.Exception(() => new ParticleSheet(16, 16, 0.1)));
        Assert.Throws<ArgumentException>(() => new ParticleSheet(2, 2, 0));
        Assert.Throws<ArgumentException>(() => new ParticleSheet(2, 2, double.NaN));
    }
}

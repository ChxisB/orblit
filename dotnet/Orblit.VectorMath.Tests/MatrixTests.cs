namespace Orblit.VectorMath.Tests;

public class MatrixTests
{
    private const double Tight = 1e-12;

    [Fact]
    public void TheIdentityHasOnesDownTheDiagonal()
    {
        var m = Matrix4.Identity();
        for (var i = 0; i < 16; i++)
        {
            Assert.Equal(i % 5 == 0 ? 1 : 0, m[i]);
        }
    }

    [Fact]
    public void ComposingNothingIsTheIdentity()
    {
        var m = Matrix4.Compose(Vector3.Zero(), Quaternion.Identity(), Vector3.All(1));
        for (var i = 0; i < 16; i++)
        {
            Assert.Equal(Matrix4.Identity()[i], m[i], Tight);
        }
    }

    [Fact]
    public void TheTranslationLivesAtTheLastColumn()
    {
        var m = Matrix4.Compose(new Vector3(1, 2, 3), Quaternion.Identity(), Vector3.All(1));
        Assert.Equal(1, m[12]);
        Assert.Equal(2, m[13]);
        Assert.Equal(3, m[14]);
        Assert.Equal(new Vector3(1, 2, 3), m.GetTranslation());
    }

    [Fact]
    public void ASetTranslationOverwritesTheOldOne()
    {
        var m = Matrix4.Identity();
        m.SetTranslation(new Vector3(4, 5, 6));
        m.SetTranslation(new Vector3(7, 8, 9));
        Assert.Equal(new Vector3(7, 8, 9), m.GetTranslation());
    }

    [Fact]
    public void ComposingScalesEachColumnByItsOwnFactor()
    {
        var m = Matrix4.Compose(Vector3.Zero(), Quaternion.Identity(), new Vector3(2, 3, 4));
        Assert.Equal(2, m.GetColumn(0).Length, Tight);
        Assert.Equal(3, m.GetColumn(1).Length, Tight);
        Assert.Equal(4, m.GetColumn(2).Length, Tight);
        Assert.Equal(1, m.GetColumn(3).Length, Tight);
    }

    [Fact]
    public void ARotationIsCopiedWithItsScaleStillIn()
    {
        var m = Matrix4.Compose(new Vector3(9, 9, 9), Quaternion.Identity(), new Vector3(2, 3, 4));
        var r = m.GetRotation();
        Assert.Equal(2, r[0], Tight);
        Assert.Equal(3, r[4], Tight);
        Assert.Equal(4, r[8], Tight);
        Assert.Equal(9, r.Trace, Tight);
        Assert.Equal(3, r.Entry(1, 1), Tight);
    }

    [Fact]
    public void ARotationMatrixIndexesByColumnThenRow()
    {
        var m = Matrix4.Identity();
        m[4] = 5;
        m[1] = 7;
        var r = m.GetRotation();
        Assert.Equal(5, r.Entry(0, 1));
        Assert.Equal(7, r.Entry(1, 0));
    }

    [Fact]
    public void ATurnAboutYSendsXToMinusZ()
    {
        var q = Quaternion.AxisAngle(new Vector3(0, 1, 0), Math.PI / 2);
        var m = Matrix4.Compose(Vector3.Zero(), q, Vector3.All(1));
        var facing = m.Transform3(new Vector3(1, 0, 0));
        Assert.Equal(0, facing.X, 1e-9);
        Assert.Equal(-1, facing.Z, 1e-9);
    }

    [Fact]
    public void TransformingWritesThroughTheArgumentAndReturnsIt()
    {
        var m = Matrix4.Identity();
        m.SetTranslation(new Vector3(1, 2, 3));
        var point = new Vector3(10, 20, 30);
        var result = m.Transform3(point);
        Assert.Same(point, result);
        Assert.Equal(new Vector3(11, 22, 33), point);
    }
}

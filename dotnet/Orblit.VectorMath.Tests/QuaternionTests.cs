namespace Orblit.VectorMath.Tests;

public class QuaternionTests
{
    private const double Tight = 1e-12;

    [Fact]
    public void AnAxisAngleHasTheHalfAngleSineInItsVectorPart()
    {
        var q = Quaternion.AxisAngle(new Vector3(0, 1, 0), Math.PI / 2);
        Assert.Equal(0, q.X, Tight);
        Assert.Equal(Math.Sqrt(0.5), q.Y, Tight);
        Assert.Equal(0, q.Z, Tight);
        Assert.Equal(Math.Sqrt(0.5), q.W, Tight);
    }

    [Fact]
    public void AnAxisThatIsNotOfLengthOneIsDividedByItsLength()
    {
        var scaledAxis = Quaternion.AxisAngle(new Vector3(0, 5, 0), 1);
        var unit = Quaternion.AxisAngle(new Vector3(0, 1, 0), 1);
        Assert.Equal(unit.Y, scaledAxis.Y, Tight);
        Assert.Equal(unit.W, scaledAxis.W, Tight);
    }

    [Fact]
    public void AnAxisOfNothingGivesTheZeroQuaternionAndNotTheIdentity()
    {
        var q = Quaternion.AxisAngle(Vector3.Zero(), 1);
        Assert.Equal(new Quaternion(0, 0, 0, 0), q);
    }

    [Fact]
    public void TheIdentityLeavesATurnAlone()
    {
        var q = Quaternion.AxisAngle(new Vector3(1, 2, 3), 0.7);
        Assert.Equal(q, Quaternion.Identity() * q);
        Assert.Equal(q, q * Quaternion.Identity());
    }

    [Fact]
    public void TwoQuarterTurnsMakeAHalfTurn()
    {
        var quarter = Quaternion.AxisAngle(new Vector3(0, 1, 0), Math.PI / 2);
        var half = quarter * quarter;
        Assert.Equal(0, half.W, Tight);
        Assert.Equal(1, half.Y, Tight);
    }

    [Fact]
    public void ProductsDoNotCommute()
    {
        var x = Quaternion.AxisAngle(new Vector3(1, 0, 0), Math.PI / 2);
        var y = Quaternion.AxisAngle(new Vector3(0, 1, 0), Math.PI / 2);
        Assert.NotEqual(x * y, y * x);
    }

    public static TheoryData<double, double, double, double> Turns => new()
    {
        { 1, 0, 0, 0.4 },
        { 0, 1, 0, 2.5 },
        { 0, 0, 1, 0.1 },
        { 1, 0, 0, Math.PI },
        { 0, 1, 0, Math.PI },
        { 0, 0, 1, Math.PI },
        { 1, 1, 0, 3.0 },
        { 0, 1, 1, 2.9 },
        { 1, 0, 1, 3.1 },
    };

    // Round trips through a matrix, which reaches both branches of FromRotation: a trace above
    // zero for a small turn, and the three largest-diagonal cases for a turn of about half a circle.
    [Theory]
    [MemberData(nameof(Turns))]
    public void ARotationReadBackFromItsMatrixIsTheSameTurn(double x, double y, double z, double radians)
    {
        var q = Quaternion.AxisAngle(new Vector3(x, y, z), radians);
        var matrix = Matrix4.Compose(Vector3.Zero(), q, Vector3.All(1));
        var back = Quaternion.FromRotation(matrix.GetRotation());

        // q and -q are one rotation.
        var sign = Math.Sign((q.X * back.X) + (q.Y * back.Y) + (q.Z * back.Z) + (q.W * back.W));
        Assert.Equal(q.X, sign * back.X, 1e-9);
        Assert.Equal(q.Y, sign * back.Y, 1e-9);
        Assert.Equal(q.Z, sign * back.Z, 1e-9);
        Assert.Equal(q.W, sign * back.W, 1e-9);
    }

    [Fact]
    public void NormalizingGivesLengthOneAndLeavesTheZeroQuaternionAlone()
    {
        var q = new Quaternion(1, 2, 3, 4);
        q.Normalize();
        Assert.Equal(1, q.Length, Tight);
        Assert.Equal(1.0 / Math.Sqrt(30), q.X, Tight);

        var nothing = new Quaternion(0, 0, 0, 0);
        nothing.Normalize();
        Assert.Equal(new Quaternion(0, 0, 0, 0), nothing);
    }

    [Fact]
    public void LengthSquaredIsTheSumOfTheFourSquares() =>
        Assert.Equal(30, new Quaternion(1, 2, 3, 4).Length2, Tight);

    [Fact]
    public void ConjugatingNegatesTheVectorPartAndKeepsW()
    {
        var q = new Quaternion(1, -2, 3, 4);
        q.Conjugate();
        Assert.Equal(new Quaternion(-1, 2, -3, 4), q);
    }

    [Fact]
    public void EulerWithNoAnglesIsTheIdentityAndAYawIsATurnAboutY()
    {
        Assert.Equal(Quaternion.Identity(), Quaternion.Euler(0, 0, 0));

        var yaw = Quaternion.Euler(Math.PI / 2, 0, 0);
        var about = Quaternion.AxisAngle(new Vector3(0, 1, 0), Math.PI / 2);
        Assert.Equal(about.X, yaw.X, Tight);
        Assert.Equal(about.Y, yaw.Y, Tight);
        Assert.Equal(about.Z, yaw.Z, Tight);
        Assert.Equal(about.W, yaw.W, Tight);
    }

    [Fact]
    public void EulerPitchIsATurnAboutXAndRollIsATurnAboutZ()
    {
        var pitch = Quaternion.Euler(0, 0.6, 0);
        Assert.Equal(Math.Sin(0.3), pitch.X, Tight);
        Assert.Equal(0, pitch.Y, Tight);
        var roll = Quaternion.Euler(0, 0, 0.6);
        Assert.Equal(Math.Sin(0.3), roll.Z, Tight);
        Assert.Equal(Math.Cos(0.3), roll.W, Tight);
    }

    [Fact]
    public void ConjugatedAndNormalizedAreCopies()
    {
        var q = new Quaternion(1, 2, 3, 4);
        Assert.Equal(new Quaternion(-1, -2, -3, 4), q.Conjugated());
        Assert.Equal(1, q.Normalized().Length, Tight);
        Assert.Equal(new Quaternion(1, 2, 3, 4), q);
    }

    [Fact]
    public void SetFromAndSetValuesWriteInPlace()
    {
        var q = Quaternion.Identity();
        q.SetValues(1, 2, 3, 4);
        Assert.Equal(new Quaternion(1, 2, 3, 4), q);
        q.SetFrom(new Quaternion(5, 6, 7, 8));
        Assert.Equal(new Quaternion(5, 6, 7, 8), q);
    }

    [Fact]
    public void TheRotationMatrixTurnsAVectorAsTheQuaternionIs()
    {
        var q = Quaternion.AxisAngle(new Vector3(0, 0, 1), Math.PI / 2);
        var turned = q.AsRotationMatrix().Transformed(new Vector3(1, 0, 0));
        Assert.Equal(0, turned.X, 1e-9);
        Assert.Equal(1, turned.Y, 1e-9);
    }

    [Fact]
    public void ARotationMatrixDividesOutTheScaleOfAQuaternionThatIsNotOfLengthOne()
    {
        var unit = Quaternion.AxisAngle(new Vector3(0, 1, 0), 0.7);
        var big = new Quaternion(unit.X * 3, unit.Y * 3, unit.Z * 3, unit.W * 3);
        var a = unit.AsRotationMatrix();
        var b = big.AsRotationMatrix();
        for (var i = 0; i < 9; i++)
        {
            Assert.Equal(a[i], b[i], 1e-12);
        }
    }
}

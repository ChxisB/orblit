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

    private static Matrix4 Sample() => Matrix4.Compose(
        new Vector3(1, -2, 3),
        Quaternion.AxisAngle(new Vector3(1, 2, 3), 0.9),
        new Vector3(2, 3, 0.5));

    private static void SameMatrix(Matrix4 expected, Matrix4 actual, double tolerance = 1e-9)
    {
        for (var i = 0; i < 16; i++)
        {
            Assert.Equal(expected[i], actual[i], tolerance);
        }
    }

    [Fact]
    public void AMatrixTimesItsInverseIsTheIdentity()
    {
        SameMatrix(Matrix4.Identity(), Sample().Multiplied(Matrix4.Inverted(Sample())));
    }

    [Fact]
    public void MultiplyAppliesTheRightHandMatrixFirst()
    {
        var move = Matrix4.Identity();
        move.SetTranslation(new Vector3(0, 5, 0));
        var turn = Matrix4.Compose(Vector3.Zero(), Quaternion.AxisAngle(new Vector3(0, 0, 1), Math.PI / 2), Vector3.All(1));
        var point = move.Multiplied(turn).Transform3(new Vector3(1, 0, 0));
        Assert.Equal(0, point.X, 1e-9);
        Assert.Equal(6, point.Y, 1e-9);
    }

    [Fact]
    public void MultiplyLeavesTheArgumentAndMultipliedLeavesBoth()
    {
        var a = Sample();
        var b = Matrix4.Identity();
        b.SetTranslation(new Vector3(1, 1, 1));
        _ = a.Multiplied(b);
        SameMatrix(Sample(), a, 0);
        a.Multiply(b);
        SameMatrix(Sample().Multiplied(b), a, 0);
    }

    [Fact]
    public void ACloneDoesNotShareStorage()
    {
        var a = Sample();
        var b = a.Clone();
        b[0] = 99;
        Assert.NotEqual(99, a[0]);
    }

    [Fact]
    public void AMatrixWithNoInverseIsRefusedByInvertedAndReportedByCopyInverse()
    {
        var flat = Matrix4.Compose(Vector3.Zero(), Quaternion.Identity(), new Vector3(1, 0, 1));
        Assert.Throws<ArgumentException>(() => Matrix4.Inverted(flat));
        var into = Matrix4.Zero();
        Assert.Equal(0, into.CopyInverse(flat));
        SameMatrix(flat, into, 0);
    }

    [Fact]
    public void TheDeterminantIsTheProductOfTheScalesAndTurnsNegativeWhenFlipped()
    {
        Assert.Equal(3, Sample().Determinant(), 1e-9);
        var flipped = Matrix4.Compose(Vector3.Zero(), Quaternion.Identity(), new Vector3(-1, 1, 1));
        Assert.True(flipped.Determinant() < 0);
    }

    [Fact]
    public void DecomposeGivesBackWhatComposeWasGiven()
    {
        var translation = Vector3.Zero();
        var rotation = Quaternion.Identity();
        var scale = Vector3.Zero();
        Sample().Decompose(translation, rotation, scale);
        Assert.Equal(new Vector3(1, -2, 3), translation);
        Assert.Equal(2, scale.X, 1e-9);
        Assert.Equal(3, scale.Y, 1e-9);
        Assert.Equal(0.5, scale.Z, 1e-9);
        SameMatrix(Sample(), Matrix4.Compose(translation, rotation, scale));
    }

    [Fact]
    public void DecomposeGivesAFlippedMatrixANegativeXScale()
    {
        var flipped = Matrix4.Compose(Vector3.Zero(), Quaternion.Identity(), new Vector3(-2, 1, 1));
        var scale = Vector3.Zero();
        flipped.Decompose(Vector3.Zero(), Quaternion.Identity(), scale);
        Assert.Equal(-2, scale.X, 1e-9);
    }

    [Fact]
    public void RotateIgnoresTheTranslationAndTransformDoesNot()
    {
        var m = Matrix4.Compose(new Vector3(10, 20, 30), Quaternion.Identity(), Vector3.All(1));
        Assert.Equal(new Vector3(1, 2, 3), m.Rotated3(new Vector3(1, 2, 3)));
        Assert.Equal(new Vector3(11, 22, 33), m.Transformed3(new Vector3(1, 2, 3)));
        var point = new Vector3(1, 2, 3);
        Assert.Same(point, m.Rotate3(point));
    }

    [Fact]
    public void TheNamedFactoriesPlaceTheirValuesInColumnMajorOrder()
    {
        Assert.Equal(new Vector3(11, 22, 33), Matrix4.TranslationValues(10, 20, 30).Transformed3(new Vector3(1, 2, 3)));
        Assert.Equal(new Vector3(2, 6, 12), Matrix4.Diagonal3Values(2, 3, 4).Transformed3(new Vector3(1, 2, 3)));
        var turned = Matrix4.RotationZ(Math.PI / 2).Rotated3(new Vector3(1, 0, 0));
        Assert.Equal(0, turned.X, 1e-9);
        Assert.Equal(1, turned.Y, 1e-9);
    }

    [Fact]
    public void RotationXTurnsYTowardsZ()
    {
        var turned = Matrix4.RotationX(Math.PI / 2).Rotated3(new Vector3(0, 1, 0));
        Assert.Equal(0, turned.Y, 1e-9);
        Assert.Equal(1, turned.Z, 1e-9);
    }

    [Fact]
    public void RotationYTurnsZTowardsX()
    {
        var turned = Matrix4.RotationY(Math.PI / 2).Rotated3(new Vector3(0, 0, 1));
        Assert.Equal(1, turned.X, 1e-9);
        Assert.Equal(0, turned.Z, 1e-9);
    }

    [Fact]
    public void RotateZIsTheMatrixTimesARotationAroundZ()
    {
        var turned = Matrix4.TranslationValues(0, 5, 0);
        turned.RotateZ(Math.PI / 2);
        var product = Matrix4.TranslationValues(0, 5, 0).Multiplied(Matrix4.RotationZ(Math.PI / 2));
        for (var i = 0; i < 16; i++)
        {
            Assert.Equal(product[i], turned[i], Tight);
        }
    }

    [Fact]
    public void RotateZLeavesTheTranslationAlone()
    {
        var turned = Matrix4.TranslationValues(1, 2, 3);
        turned.RotateZ(1);
        Assert.Equal(new Vector3(1, 2, 3), turned.GetTranslation());
    }

    /// <summary>Turned about all three axes and given a bottom row, so no entry a rotation reads is zero.</summary>
    private static Matrix4 Tilted()
    {
        var tilted = Matrix4.RotationZ(0.3).Multiplied(Matrix4.RotationX(0.5)).Multiplied(Matrix4.RotationY(0.2));
        tilted[3] = 0.25;
        tilted[7] = 0.5;
        tilted[11] = 0.75;
        return tilted;
    }

    [Fact]
    public void RotateXIsTheMatrixTimesARotationAroundX()
    {
        var turned = Tilted();
        turned.RotateX(0.7);
        var product = Tilted().Multiplied(Matrix4.RotationX(0.7));
        for (var i = 0; i < 16; i++)
        {
            Assert.Equal(product[i], turned[i], Tight);
        }
    }

    [Fact]
    public void RotateYIsTheMatrixTimesARotationAroundY()
    {
        var turned = Tilted();
        turned.RotateY(0.7);
        var product = Tilted().Multiplied(Matrix4.RotationY(0.7));
        for (var i = 0; i < 16; i++)
        {
            Assert.Equal(product[i], turned[i], Tight);
        }
    }

    [Fact]
    public void RotateXAndRotateYLeaveTheTranslationAlone()
    {
        var turned = Matrix4.TranslationValues(1, 2, 3);
        turned.RotateX(1);
        turned.RotateY(1);
        Assert.Equal(new Vector3(1, 2, 3), turned.GetTranslation());
    }

    [Fact]
    public void ScaleByDoubleIsTheMatrixTimesADiagonal()
    {
        var scaled = Matrix4.RotationY(0.4);
        scaled.SetTranslation(new Vector3(1, 2, 3));
        scaled.ScaleByDouble(2, 3, 4, 5);
        var product = Matrix4.RotationY(0.4);
        product.SetTranslation(new Vector3(1, 2, 3));
        var diagonal = Matrix4.Diagonal3Values(2, 3, 4);
        diagonal[15] = 5;
        product.Multiply(diagonal);
        for (var i = 0; i < 16; i++)
        {
            Assert.Equal(product[i], scaled[i], Tight);
        }
    }

    private static Matrix3 Diagonal(double x, double y, double z)
    {
        var matrix = new Matrix3();
        matrix[0] = x;
        matrix[4] = y;
        matrix[8] = z;
        return matrix;
    }

    [Fact]
    public void ADeterminantMultipliesTheDiagonalAndSubtractsTheCrossTerms()
    {
        Assert.Equal(24, Diagonal(2, 3, 4).Determinant());
        var m = new Matrix3();
        double[] values = [1, 2, 3, 0, 4, 5, 1, 0, 6];
        for (var i = 0; i < 9; i++)
        {
            m[i] = values[i];
        }

        Assert.Equal(22, m.Determinant(), Tight);
    }

    [Fact]
    public void CopyInverseGivesTheInverseAndTheDeterminant()
    {
        var m = new Matrix3();
        double[] values = [1, 2, 3, 0, 4, 5, 1, 0, 6];
        for (var i = 0; i < 9; i++)
        {
            m[i] = values[i];
        }

        var inverse = new Matrix3();
        Assert.Equal(22, inverse.CopyInverse(m), Tight);
        var back = m.Transformed(inverse.Transformed(new Vector3(1, 2, 3)));
        Assert.Equal(1, back.X, 1e-9);
        Assert.Equal(2, back.Y, 1e-9);
        Assert.Equal(3, back.Z, 1e-9);
    }

    [Fact]
    public void CopyInverseOfASingularMatrixCopiesItAndReturnsZero()
    {
        var singular = Diagonal(1, 0, 1);
        singular[1] = 7;
        var copy = new Matrix3();
        Assert.Equal(0, copy.CopyInverse(singular));
        for (var i = 0; i < 9; i++)
        {
            Assert.Equal(singular[i], copy[i]);
        }
    }

    [Fact]
    public void TransposeSwapsRowsAndColumns()
    {
        var m = new Matrix3();
        for (var i = 0; i < 9; i++)
        {
            m[i] = i;
        }

        m.Transpose();
        double[] expected = [0, 3, 6, 1, 4, 7, 2, 5, 8];
        for (var i = 0; i < 9; i++)
        {
            Assert.Equal(expected[i], m[i]);
        }
    }

    [Fact]
    public void TheNormalMatrixOfAStretchSquashesTheNormal()
    {
        var normal = new Matrix3();
        normal.CopyNormalMatrix(Matrix4.Diagonal3Values(4, 1, 2));
        Assert.Equal(0.25, normal[0], Tight);
        Assert.Equal(1, normal[4], Tight);
        Assert.Equal(0.5, normal[8], Tight);
    }

    [Fact]
    public void AQuarterTurnAboutEachAxisCarriesTheNextAxisOntoTheOneAfter()
    {
        var quarter = Math.PI / 2;
        var x = Matrix3.RotationX(quarter).Transformed(new Vector3(0, 1, 0));
        var y = Matrix3.RotationY(quarter).Transformed(new Vector3(0, 0, 1));
        var z = Matrix3.RotationZ(quarter).Transformed(new Vector3(1, 0, 0));
        Assert.Equal(new Vector3(0, 0, 1).ToString(), Rounded(x).ToString());
        Assert.Equal(new Vector3(1, 0, 0).ToString(), Rounded(y).ToString());
        Assert.Equal(new Vector3(0, 1, 0).ToString(), Rounded(z).ToString());
    }

    [Fact]
    public void MultiplyAppliesTheArgumentFirst()
    {
        var turn = Matrix3.RotationZ(Math.PI / 2);
        turn.Multiply(Matrix3.RotationX(Math.PI / 2));
        var through = Rounded(turn.Transformed(new Vector3(0, 1, 0)));
        Assert.Equal(new Vector3(0, 0, 1).ToString(), through.ToString());
    }

    [Fact]
    public void FromListReadsStorageOrderAndRefusesOtherCounts()
    {
        var matrix = Matrix4.FromList(Enumerable.Range(0, 16).Select(i => (double)i).ToList());
        for (var i = 0; i < 16; i++)
        {
            Assert.Equal(i, matrix[i]);
        }

        Assert.Throws<ArgumentException>(() => Matrix4.FromList([1.0, 2.0]));
    }

    private static Vector3 Rounded(Vector3 value) =>
        new(Math.Round(value.X, 9) + 0.0, Math.Round(value.Y, 9) + 0.0, Math.Round(value.Z, 9) + 0.0);
}

namespace Orblit.VectorMath;

/// <summary>
/// A 4 by 4 matrix of doubles in column major order, as <c>vector_math_64</c>
/// holds it: the translation is at indices 12 to 14.
/// </summary>
public sealed class Matrix4
{
    private readonly double[] _storage = new double[16];

    /// <summary>Reads or writes by storage index.</summary>
    public double this[int index]
    {
        get => _storage[index];
        set => _storage[index] = value;
    }

    public static Matrix4 Zero() => new();

    public static Matrix4 Identity()
    {
        var matrix = new Matrix4();
        matrix._storage[0] = 1;
        matrix._storage[5] = 1;
        matrix._storage[10] = 1;
        matrix._storage[15] = 1;
        return matrix;
    }

    public static Matrix4 TranslationValues(double x, double y, double z)
    {
        var matrix = Identity();
        matrix._storage[12] = x;
        matrix._storage[13] = y;
        matrix._storage[14] = z;
        return matrix;
    }

    public static Matrix4 Diagonal3Values(double x, double y, double z)
    {
        var matrix = Identity();
        matrix._storage[0] = x;
        matrix._storage[5] = y;
        matrix._storage[10] = z;
        return matrix;
    }

    public static Matrix4 RotationZ(double radians)
    {
        var matrix = Identity();
        var cosine = Math.Cos(radians);
        var sine = Math.Sin(radians);
        matrix._storage[0] = cosine;
        matrix._storage[1] = sine;
        matrix._storage[4] = -sine;
        matrix._storage[5] = cosine;
        return matrix;
    }

    public static Matrix4 RotationY(double radians)
    {
        var matrix = Identity();
        var cosine = Math.Cos(radians);
        var sine = Math.Sin(radians);
        matrix._storage[0] = cosine;
        matrix._storage[2] = -sine;
        matrix._storage[8] = sine;
        matrix._storage[10] = cosine;
        return matrix;
    }

    public static Matrix4 RotationX(double radians)
    {
        var matrix = Identity();
        var cosine = Math.Cos(radians);
        var sine = Math.Sin(radians);
        matrix._storage[5] = cosine;
        matrix._storage[6] = sine;
        matrix._storage[9] = -sine;
        matrix._storage[10] = cosine;
        return matrix;
    }

    /// <summary>
    /// A matrix that scales, then rotates, then translates. The rotation is the
    /// quaternion as given, so one that is not of length one scales as well.
    /// </summary>
    public static Matrix4 Compose(Vector3 translation, Quaternion rotation, Vector3 scale)
    {
        var x2 = rotation.X + rotation.X;
        var y2 = rotation.Y + rotation.Y;
        var z2 = rotation.Z + rotation.Z;
        var xx = rotation.X * x2;
        var xy = rotation.X * y2;
        var xz = rotation.X * z2;
        var yy = rotation.Y * y2;
        var yz = rotation.Y * z2;
        var zz = rotation.Z * z2;
        var wx = rotation.W * x2;
        var wy = rotation.W * y2;
        var wz = rotation.W * z2;

        var m = new Matrix4();
        m._storage[0] = 1.0 - (yy + zz);
        m._storage[1] = xy + wz;
        m._storage[2] = xz - wy;
        m._storage[3] = 0;
        m._storage[4] = xy - wz;
        m._storage[5] = 1.0 - (xx + zz);
        m._storage[6] = yz + wx;
        m._storage[7] = 0;
        m._storage[8] = xz + wy;
        m._storage[9] = yz - wx;
        m._storage[10] = 1.0 - (xx + yy);
        m._storage[11] = 0;
        m._storage[12] = translation.X;
        m._storage[13] = translation.Y;
        m._storage[14] = translation.Z;
        m._storage[15] = 1;

        for (var i = 0; i < 4; i++)
        {
            m._storage[i] *= scale.X;
            m._storage[4 + i] *= scale.Y;
            m._storage[8 + i] *= scale.Z;
        }

        return m;
    }

    public Matrix4 Clone()
    {
        var copy = new Matrix4();
        copy.SetFrom(this);
        return copy;
    }

    public void SetFrom(Matrix4 other) => Array.Copy(other._storage, _storage, 16);

    /// <summary>
    /// The inverse of <paramref name="other"/>, which throws <see cref="ArgumentException"/>
    /// when there is none.
    /// </summary>
    public static Matrix4 Inverted(Matrix4 other)
    {
        var result = new Matrix4();
        if (result.CopyInverse(other) == 0.0)
        {
            throw new ArgumentException("Matrix cannot be inverted", nameof(other));
        }

        return result;
    }

    /// <summary>
    /// Sets this to the inverse of <paramref name="other"/> and returns the
    /// determinant. A determinant of zero copies <paramref name="other"/> as it is
    /// and returns 0, so the caller has to look.
    /// </summary>
    public double CopyInverse(Matrix4 other)
    {
        var a = other._storage;
        double a00 = a[0], a01 = a[1], a02 = a[2], a03 = a[3];
        double a10 = a[4], a11 = a[5], a12 = a[6], a13 = a[7];
        double a20 = a[8], a21 = a[9], a22 = a[10], a23 = a[11];
        double a30 = a[12], a31 = a[13], a32 = a[14], a33 = a[15];
        var b00 = (a00 * a11) - (a01 * a10);
        var b01 = (a00 * a12) - (a02 * a10);
        var b02 = (a00 * a13) - (a03 * a10);
        var b03 = (a01 * a12) - (a02 * a11);
        var b04 = (a01 * a13) - (a03 * a11);
        var b05 = (a02 * a13) - (a03 * a12);
        var b06 = (a20 * a31) - (a21 * a30);
        var b07 = (a20 * a32) - (a22 * a30);
        var b08 = (a20 * a33) - (a23 * a30);
        var b09 = (a21 * a32) - (a22 * a31);
        var b10 = (a21 * a33) - (a23 * a31);
        var b11 = (a22 * a33) - (a23 * a32);
        var det = (b00 * b11) - (b01 * b10) + (b02 * b09) + (b03 * b08) - (b04 * b07) + (b05 * b06);
        if (det == 0.0)
        {
            SetFrom(other);
            return 0.0;
        }

        var invDet = 1.0 / det;
        _storage[0] = ((a11 * b11) - (a12 * b10) + (a13 * b09)) * invDet;
        _storage[1] = ((-a01 * b11) + (a02 * b10) - (a03 * b09)) * invDet;
        _storage[2] = ((a31 * b05) - (a32 * b04) + (a33 * b03)) * invDet;
        _storage[3] = ((-a21 * b05) + (a22 * b04) - (a23 * b03)) * invDet;
        _storage[4] = ((-a10 * b11) + (a12 * b08) - (a13 * b07)) * invDet;
        _storage[5] = ((a00 * b11) - (a02 * b08) + (a03 * b07)) * invDet;
        _storage[6] = ((-a30 * b05) + (a32 * b02) - (a33 * b01)) * invDet;
        _storage[7] = ((a20 * b05) - (a22 * b02) + (a23 * b01)) * invDet;
        _storage[8] = ((a10 * b10) - (a11 * b08) + (a13 * b06)) * invDet;
        _storage[9] = ((-a00 * b10) + (a01 * b08) - (a03 * b06)) * invDet;
        _storage[10] = ((a30 * b04) - (a31 * b02) + (a33 * b00)) * invDet;
        _storage[11] = ((-a20 * b04) + (a21 * b02) - (a23 * b00)) * invDet;
        _storage[12] = ((-a10 * b09) + (a11 * b07) - (a12 * b06)) * invDet;
        _storage[13] = ((a00 * b09) - (a01 * b07) + (a02 * b06)) * invDet;
        _storage[14] = ((-a30 * b03) + (a31 * b01) - (a32 * b00)) * invDet;
        _storage[15] = ((a20 * b03) - (a21 * b01) + (a22 * b00)) * invDet;
        return det;
    }

    public double Determinant()
    {
        var m = _storage;
        var det2_01_01 = (m[0] * m[5]) - (m[1] * m[4]);
        var det2_01_02 = (m[0] * m[6]) - (m[2] * m[4]);
        var det2_01_03 = (m[0] * m[7]) - (m[3] * m[4]);
        var det2_01_12 = (m[1] * m[6]) - (m[2] * m[5]);
        var det2_01_13 = (m[1] * m[7]) - (m[3] * m[5]);
        var det2_01_23 = (m[2] * m[7]) - (m[3] * m[6]);
        var det3_201_012 = (m[8] * det2_01_12) - (m[9] * det2_01_02) + (m[10] * det2_01_01);
        var det3_201_013 = (m[8] * det2_01_13) - (m[9] * det2_01_03) + (m[11] * det2_01_01);
        var det3_201_023 = (m[8] * det2_01_23) - (m[10] * det2_01_03) + (m[11] * det2_01_02);
        var det3_201_123 = (m[9] * det2_01_23) - (m[10] * det2_01_13) + (m[11] * det2_01_12);
        return (-det3_201_123 * m[12]) + (det3_201_023 * m[13]) - (det3_201_013 * m[14]) + (det3_201_012 * m[15]);
    }

    /// <summary>Multiplies this by <paramref name="other"/> on the right, in place.</summary>
    public void Multiply(Matrix4 other)
    {
        var m = _storage;
        var n = other._storage;
        double m00 = m[0], m01 = m[4], m02 = m[8], m03 = m[12];
        double m10 = m[1], m11 = m[5], m12 = m[9], m13 = m[13];
        double m20 = m[2], m21 = m[6], m22 = m[10], m23 = m[14];
        double m30 = m[3], m31 = m[7], m32 = m[11], m33 = m[15];
        double n00 = n[0], n01 = n[4], n02 = n[8], n03 = n[12];
        double n10 = n[1], n11 = n[5], n12 = n[9], n13 = n[13];
        double n20 = n[2], n21 = n[6], n22 = n[10], n23 = n[14];
        double n30 = n[3], n31 = n[7], n32 = n[11], n33 = n[15];
        m[0] = (m00 * n00) + (m01 * n10) + (m02 * n20) + (m03 * n30);
        m[4] = (m00 * n01) + (m01 * n11) + (m02 * n21) + (m03 * n31);
        m[8] = (m00 * n02) + (m01 * n12) + (m02 * n22) + (m03 * n32);
        m[12] = (m00 * n03) + (m01 * n13) + (m02 * n23) + (m03 * n33);
        m[1] = (m10 * n00) + (m11 * n10) + (m12 * n20) + (m13 * n30);
        m[5] = (m10 * n01) + (m11 * n11) + (m12 * n21) + (m13 * n31);
        m[9] = (m10 * n02) + (m11 * n12) + (m12 * n22) + (m13 * n32);
        m[13] = (m10 * n03) + (m11 * n13) + (m12 * n23) + (m13 * n33);
        m[2] = (m20 * n00) + (m21 * n10) + (m22 * n20) + (m23 * n30);
        m[6] = (m20 * n01) + (m21 * n11) + (m22 * n21) + (m23 * n31);
        m[10] = (m20 * n02) + (m21 * n12) + (m22 * n22) + (m23 * n32);
        m[14] = (m20 * n03) + (m21 * n13) + (m22 * n23) + (m23 * n33);
        m[3] = (m30 * n00) + (m31 * n10) + (m32 * n20) + (m33 * n30);
        m[7] = (m30 * n01) + (m31 * n11) + (m32 * n21) + (m33 * n31);
        m[11] = (m30 * n02) + (m31 * n12) + (m32 * n22) + (m33 * n32);
        m[15] = (m30 * n03) + (m31 * n13) + (m32 * n23) + (m33 * n33);
    }

    /// <summary>Sets this to this times <c>RotationZ(radians)</c>.</summary>
    public void RotateZ(double radians)
    {
        var cosine = Math.Cos(radians);
        var sine = Math.Sin(radians);
        var m = _storage;
        double[] turned =
        [
            (m[0] * cosine) + (m[4] * sine),
            (m[1] * cosine) + (m[5] * sine),
            (m[2] * cosine) + (m[6] * sine),
            (m[3] * cosine) + (m[7] * sine),
            (m[0] * -sine) + (m[4] * cosine),
            (m[1] * -sine) + (m[5] * cosine),
            (m[2] * -sine) + (m[6] * cosine),
            (m[3] * -sine) + (m[7] * cosine),
        ];
        Array.Copy(turned, m, 8);
    }

    /// <summary>A copy of this times <paramref name="other"/>.</summary>
    public Matrix4 Multiplied(Matrix4 other)
    {
        var copy = Clone();
        copy.Multiply(other);
        return copy;
    }

    /// <summary>
    /// Splits this into a translation, a rotation and a scale, written into the
    /// three arguments. A flipped matrix gives a negative x scale.
    /// </summary>
    public void Decompose(Vector3 translation, Quaternion rotation, Vector3 scale)
    {
        var sx = new Vector3(_storage[0], _storage[1], _storage[2]).Length;
        var sy = new Vector3(_storage[4], _storage[5], _storage[6]).Length;
        var sz = new Vector3(_storage[8], _storage[9], _storage[10]).Length;
        if (Determinant() < 0)
        {
            sx = -sx;
        }

        translation.SetValues(_storage[12], _storage[13], _storage[14]);

        var invSx = 1.0 / sx;
        var invSy = 1.0 / sy;
        var invSz = 1.0 / sz;
        var m = Clone();
        m._storage[0] *= invSx;
        m._storage[1] *= invSx;
        m._storage[2] *= invSx;
        m._storage[4] *= invSy;
        m._storage[5] *= invSy;
        m._storage[6] *= invSy;
        m._storage[8] *= invSz;
        m._storage[9] *= invSz;
        m._storage[10] *= invSz;

        rotation.SetFrom(Quaternion.FromRotation(m.GetRotation()));
        scale.SetValues(sx, sy, sz);
    }

    /// <summary>Turns <paramref name="argument"/> in place by the top left 3 by 3, and returns it.</summary>
    public Vector3 Rotate3(Vector3 argument)
    {
        var x = (_storage[0] * argument.X) + (_storage[4] * argument.Y) + (_storage[8] * argument.Z);
        var y = (_storage[1] * argument.X) + (_storage[5] * argument.Y) + (_storage[9] * argument.Z);
        var z = (_storage[2] * argument.X) + (_storage[6] * argument.Y) + (_storage[10] * argument.Z);
        argument.X = x;
        argument.Y = y;
        argument.Z = z;
        return argument;
    }

    public Vector3 Rotated3(Vector3 argument) => Rotate3(argument.Clone());

    public Vector3 Transformed3(Vector3 argument) => Transform3(argument.Clone());

    public Vector3 GetTranslation() => new(_storage[12], _storage[13], _storage[14]);

    public void SetTranslation(Vector3 translation)
    {
        _storage[12] = translation.X;
        _storage[13] = translation.Y;
        _storage[14] = translation.Z;
    }

    /// <summary>The top left 3 by 3, copied as it is. A scale in the matrix stays in.</summary>
    public Matrix3 GetRotation()
    {
        var rotation = new Matrix3();
        int[] from = [0, 1, 2, 4, 5, 6, 8, 9, 10];
        for (var i = 0; i < from.Length; i++)
        {
            rotation[i] = _storage[from[i]];
        }

        return rotation;
    }

    public Vector4 GetColumn(int column)
    {
        var i = column * 4;
        return new Vector4(_storage[i], _storage[i + 1], _storage[i + 2], _storage[i + 3]);
    }

    /// <summary>Transforms <paramref name="argument"/> in place as a point, and returns it.</summary>
    public Vector3 Transform3(Vector3 argument)
    {
        var x = (_storage[0] * argument.X) + (_storage[4] * argument.Y) + (_storage[8] * argument.Z) + _storage[12];
        var y = (_storage[1] * argument.X) + (_storage[5] * argument.Y) + (_storage[9] * argument.Z) + _storage[13];
        var z = (_storage[2] * argument.X) + (_storage[6] * argument.Y) + (_storage[10] * argument.Z) + _storage[14];
        argument.X = x;
        argument.Y = y;
        argument.Z = z;
        return argument;
    }
}

namespace Orblit.VectorMath;

/// <summary>
/// A 3 by 3 matrix of doubles in column major order, as <c>vector_math_64</c>
/// holds it: the element at row <c>r</c> and column <c>c</c> is index <c>c * 3 + r</c>.
/// </summary>
public sealed class Matrix3
{
    private readonly double[] _storage = new double[9];

    /// <summary>Reads or writes by storage index.</summary>
    public double this[int index]
    {
        get => _storage[index];
        set => _storage[index] = value;
    }

    public double Trace => _storage[0] + _storage[4] + _storage[8];

    /// <summary>A turn of <paramref name="radians"/> about X.</summary>
    public static Matrix3 RotationX(double radians)
    {
        var cosine = Math.Cos(radians);
        var sine = Math.Sin(radians);
        var matrix = new Matrix3();
        matrix._storage[0] = 1;
        matrix._storage[4] = cosine;
        matrix._storage[5] = sine;
        matrix._storage[7] = -sine;
        matrix._storage[8] = cosine;
        return matrix;
    }

    /// <summary>A turn of <paramref name="radians"/> about Y.</summary>
    public static Matrix3 RotationY(double radians)
    {
        var cosine = Math.Cos(radians);
        var sine = Math.Sin(radians);
        var matrix = new Matrix3();
        matrix._storage[0] = cosine;
        matrix._storage[2] = -sine;
        matrix._storage[4] = 1;
        matrix._storage[6] = sine;
        matrix._storage[8] = cosine;
        return matrix;
    }

    /// <summary>A turn of <paramref name="radians"/> about Z.</summary>
    public static Matrix3 RotationZ(double radians)
    {
        var cosine = Math.Cos(radians);
        var sine = Math.Sin(radians);
        var matrix = new Matrix3();
        matrix._storage[0] = cosine;
        matrix._storage[1] = sine;
        matrix._storage[3] = -sine;
        matrix._storage[4] = cosine;
        matrix._storage[8] = 1;
        return matrix;
    }

    public double Entry(int row, int column) => _storage[(column * 3) + row];

    public double Determinant()
    {
        var m = _storage;
        var x = m[0] * ((m[4] * m[8]) - (m[5] * m[7]));
        var y = m[1] * ((m[3] * m[8]) - (m[5] * m[6]));
        var z = m[2] * ((m[3] * m[7]) - (m[4] * m[6]));
        return x - y + z;
    }

    /// <summary>
    /// Sets this to the inverse of <paramref name="other"/> and returns its determinant. A matrix with
    /// no inverse is copied as it is, and the result is 0.
    /// </summary>
    public double CopyInverse(Matrix3 other)
    {
        var det = other.Determinant();
        if (det == 0)
        {
            Array.Copy(other._storage, _storage, 9);
            return 0;
        }

        var a = other._storage;
        var invDet = 1 / det;
        var ix = invDet * ((a[4] * a[8]) - (a[5] * a[7]));
        var iy = invDet * ((a[2] * a[7]) - (a[1] * a[8]));
        var iz = invDet * ((a[1] * a[5]) - (a[2] * a[4]));
        var jx = invDet * ((a[5] * a[6]) - (a[3] * a[8]));
        var jy = invDet * ((a[0] * a[8]) - (a[2] * a[6]));
        var jz = invDet * ((a[2] * a[3]) - (a[0] * a[5]));
        var kx = invDet * ((a[3] * a[7]) - (a[4] * a[6]));
        var ky = invDet * ((a[1] * a[6]) - (a[0] * a[7]));
        var kz = invDet * ((a[0] * a[4]) - (a[1] * a[3]));
        double[] values = [ix, iy, iz, jx, jy, jz, kx, ky, kz];
        Array.Copy(values, _storage, 9);
        return det;
    }

    /// <summary>Sets this to this times <paramref name="other"/>.</summary>
    public void Multiply(Matrix3 other)
    {
        var m = (double[])_storage.Clone();
        var n = other._storage;
        _storage[0] = (m[0] * n[0]) + (m[3] * n[1]) + (m[6] * n[2]);
        _storage[3] = (m[0] * n[3]) + (m[3] * n[4]) + (m[6] * n[5]);
        _storage[6] = (m[0] * n[6]) + (m[3] * n[7]) + (m[6] * n[8]);
        _storage[1] = (m[1] * n[0]) + (m[4] * n[1]) + (m[7] * n[2]);
        _storage[4] = (m[1] * n[3]) + (m[4] * n[4]) + (m[7] * n[5]);
        _storage[7] = (m[1] * n[6]) + (m[4] * n[7]) + (m[7] * n[8]);
        _storage[2] = (m[2] * n[0]) + (m[5] * n[1]) + (m[8] * n[2]);
        _storage[5] = (m[2] * n[3]) + (m[5] * n[4]) + (m[8] * n[5]);
        _storage[8] = (m[2] * n[6]) + (m[5] * n[7]) + (m[8] * n[8]);
    }

    public void Transpose()
    {
        (_storage[1], _storage[3]) = (_storage[3], _storage[1]);
        (_storage[2], _storage[6]) = (_storage[6], _storage[2]);
        (_storage[5], _storage[7]) = (_storage[7], _storage[5]);
    }

    /// <summary>
    /// Sets this to the inverse transpose of the top left 3 by 3 of <paramref name="other"/>, which
    /// turns a normal the way <paramref name="other"/> turns a surface.
    /// </summary>
    public void CopyNormalMatrix(Matrix4 other)
    {
        CopyInverse(other.GetRotation());
        Transpose();
    }

    /// <summary>Turns <paramref name="argument"/> in place, and returns it.</summary>
    public Vector3 Transform(Vector3 argument)
    {
        var x = (_storage[0] * argument.X) + (_storage[3] * argument.Y) + (_storage[6] * argument.Z);
        var y = (_storage[1] * argument.X) + (_storage[4] * argument.Y) + (_storage[7] * argument.Z);
        var z = (_storage[2] * argument.X) + (_storage[5] * argument.Y) + (_storage[8] * argument.Z);
        argument.X = x;
        argument.Y = y;
        argument.Z = z;
        return argument;
    }

    /// <summary>A turned copy of <paramref name="argument"/>.</summary>
    public Vector3 Transformed(Vector3 argument) => Transform(argument.Clone());
}

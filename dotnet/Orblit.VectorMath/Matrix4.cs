namespace Orblit.VectorMath;

/// <summary>
/// A 4 by 4 matrix of doubles in column major order, as <c>vector_math_64</c>
/// holds it: the translation is at indices 12 to 14.
/// </summary>
public sealed class Matrix4
{
    private readonly double[] _storage = new double[16];

    /// <summary>Reads or writes by storage index, as Dart's <c>[]</c> does.</summary>
    public double this[int index]
    {
        get => _storage[index];
        set => _storage[index] = value;
    }

    public static Matrix4 Identity()
    {
        var matrix = new Matrix4();
        matrix._storage[0] = 1;
        matrix._storage[5] = 1;
        matrix._storage[10] = 1;
        matrix._storage[15] = 1;
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

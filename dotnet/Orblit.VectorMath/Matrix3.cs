namespace Orblit.VectorMath;

/// <summary>
/// A 3 by 3 matrix of doubles in column major order, as <c>vector_math_64</c>
/// holds it: the element at row <c>r</c> and column <c>c</c> is index <c>c * 3 + r</c>.
/// </summary>
public sealed class Matrix3
{
    private readonly double[] _storage = new double[9];

    /// <summary>Reads or writes by storage index, as Dart's <c>[]</c> does.</summary>
    public double this[int index]
    {
        get => _storage[index];
        set => _storage[index] = value;
    }

    public double Trace => _storage[0] + _storage[4] + _storage[8];

    public double Entry(int row, int column) => _storage[(column * 3) + row];

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

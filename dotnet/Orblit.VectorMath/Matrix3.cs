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
}

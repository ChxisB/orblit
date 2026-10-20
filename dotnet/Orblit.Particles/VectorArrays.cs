using Orblit.VectorMath;

namespace Orblit.Particles;

internal static class VectorArrays
{
    /// <summary>Dart's <c>copyIntoArray</c> into single precision, which rounds as Dart does.</summary>
    public static void Write(Vector3 vector, float[] array, int offset)
    {
        array[offset] = (float)vector.X;
        array[offset + 1] = (float)vector.Y;
        array[offset + 2] = (float)vector.Z;
    }

    public static void Write(Vector3 vector, double[] array, int offset)
    {
        array[offset] = vector.X;
        array[offset + 1] = vector.Y;
        array[offset + 2] = vector.Z;
    }
}

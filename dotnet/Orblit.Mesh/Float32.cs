namespace Orblit.Mesh;

/// <summary>
/// The one quiet NaN a file holds. .NET makes a negative NaN from arithmetic where the file's is
/// positive, so a file that kept whichever came would differ with the CPU that made it.
/// </summary>
internal static class Float32
{
    private static readonly float QuietNan = BitConverter.Int32BitsToSingle(0x7FC00000);

    public static float Quiet(float value) => float.IsNaN(value) ? QuietNan : value;

    public static float[] Quiet(float[] values) => values.Any(float.IsNaN) ? [.. values.Select(Quiet)] : values;
}

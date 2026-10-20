namespace Orblit.VectorMath;

/// <summary>
/// Dart's answers where .NET's differ. Dart orders doubles by
/// <c>compareTo</c>: NaN above every number and <c>-0.0</c> below <c>0.0</c>.
/// .NET puts NaN first and treats the two zeros as equal.
/// </summary>
public static class Doubles
{
    /// <summary>Dart's <c>double.compareTo</c>.</summary>
    public static int Compare(double a, double b)
    {
        if (a < b)
        {
            return -1;
        }

        if (a > b)
        {
            return 1;
        }

        if (a == b)
        {
            return a == 0 ? double.IsNegative(b).CompareTo(double.IsNegative(a)) : 0;
        }

        return double.IsNaN(a) ? (double.IsNaN(b) ? 0 : 1) : -1;
    }

    /// <summary>
    /// Dart's <c>num.clamp</c>. A NaN value is above every limit, so it comes
    /// back as <paramref name="upper"/>, where <c>Math.Clamp</c> returns NaN.
    /// </summary>
    public static double Clamp(double value, double lower, double upper)
    {
        if (Compare(lower, upper) > 0)
        {
            throw new ArgumentException("The lower limit is above the upper one.", nameof(lower));
        }

        if (Compare(value, lower) < 0)
        {
            return lower;
        }

        return Compare(value, upper) > 0 ? upper : value;
    }
}

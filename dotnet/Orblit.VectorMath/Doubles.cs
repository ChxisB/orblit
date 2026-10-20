using System.Globalization;
using System.Numerics;

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

    /// <summary>
    /// Dart's <c>%</c> on doubles. The result is never negative, and a zero is
    /// always <c>0.0</c>. C# keeps the sign of the dividend, so <c>-1 % 3</c> is
    /// <c>-1</c> there and <c>2</c> in Dart.
    /// </summary>
    public static double Mod(double value, double divisor)
    {
        var remainder = value % divisor;
        if (remainder == 0)
        {
            return 0;
        }

        return remainder < 0 ? remainder + Math.Abs(divisor) : remainder;
    }

    /// <summary>
    /// Dart's <c>double.floor()</c>, which throws on NaN and infinity where a C#
    /// cast gives an unspecified number.
    /// </summary>
    public static long Floor(double value)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Only a finite number has a floor.");
        }

        return (long)Math.Floor(value);
    }

    /// <summary>
    /// Dart's <c>double.ceil()</c>, which throws on NaN and infinity where a C#
    /// cast gives an unspecified number.
    /// </summary>
    public static long Ceil(double value)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Only a finite number has a ceiling.");
        }

        return (long)Math.Ceiling(value);
    }

    /// <summary>
    /// Dart's <c>double.sign</c>: -1, 1, or the value itself for either zero
    /// and for NaN. <c>Math.Sign</c> returns an int and throws on NaN.
    /// </summary>
    public static double Sign(double value) => value > 0 ? 1 : (value < 0 ? -1 : value);

    /// <summary>
    /// Dart's <c>double.round()</c>: halves go away from zero, and NaN and
    /// infinity throw. <c>Math.Round</c> goes to the even number by default.
    /// </summary>
    public static long Round(double value)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Only a finite number can be rounded.");
        }

        return (long)Math.Round(value, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Dart's <c>toStringAsFixed</c>: the exact value rounded to <paramref name="digits"/> places,
    /// with a tie going away from zero where the .NET <c>F</c> format goes to the even digit, and the
    /// shortest form for NaN, infinity and anything from 1e21 up, where .NET writes every digit.
    /// A negative value that rounds to zero keeps its sign, as in Dart.
    /// </summary>
    public static string ToStringAsFixed(double value, int digits)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(digits);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(digits, 20);
        if (!double.IsFinite(value))
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        if (Math.Abs(value) >= 1e21)
        {
            return value.ToString("R", CultureInfo.InvariantCulture).ToLowerInvariant();
        }

        // The value is exactly mantissa * 2^shift, so the rounding can be done in whole numbers.
        var bits = BitConverter.DoubleToInt64Bits(value);
        var biased = (int)((bits >> 52) & 0x7FF);
        var fraction = bits & 0xFFFFFFFFFFFFFL;
        var mantissa = biased == 0 ? fraction : fraction | (1L << 52);
        var shift = Math.Max(biased, 1) - 1075;
        var scaled = new BigInteger(mantissa) * BigInteger.Pow(10, digits);
        var rounded = shift >= 0 ? scaled << shift : ((scaled << 1) + (BigInteger.One << -shift)) >> (1 - shift);

        var text = rounded.ToString(CultureInfo.InvariantCulture).PadLeft(digits + 1, '0');
        var sign = double.IsNegative(value) ? "-" : string.Empty;
        return digits == 0 ? sign + text : $"{sign}{text[..^digits]}.{text[^digits..]}";
    }

    /// <summary>
    /// Dart's <c>double.toString</c>. The digits are the shortest that read back as the same
    /// double, which .NET also gives, but the layout differs: Dart writes a point and a final
    /// <c>.0</c> for anything from 1e-6 up to 1e21, and otherwise <c>1.5e-7</c> or <c>1e+21</c>
    /// with no padding on the exponent.
    /// </summary>
    public static string ToDartString(double value)
    {
        if (!double.IsFinite(value))
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        var sign = double.IsNegative(value) ? "-" : string.Empty;
        if (value == 0)
        {
            return sign + "0.0";
        }

        var (digits, point) = ShortestDigits(Math.Abs(value));
        var exponent = point - 1;
        if (exponent is < -6 or >= 21)
        {
            var rest = digits.Length > 1 ? "." + digits[1..] : string.Empty;
            var power = exponent < 0 ? "-" : "+";
            return $"{sign}{digits[0]}{rest}e{power}{Math.Abs(exponent).ToString(CultureInfo.InvariantCulture)}";
        }

        if (point <= 0)
        {
            return $"{sign}0.{new string('0', -point)}{digits}";
        }

        return point >= digits.Length
            ? $"{sign}{digits}{new string('0', point - digits.Length)}.0"
            : $"{sign}{digits[..point]}.{digits[point..]}";
    }

    // The significant digits of a positive finite value, and where the point sits among them:
    // 0 puts it before the first digit and 1 after it.
    private static (string Digits, int Point) ShortestDigits(double value)
    {
        var text = value.ToString("R", CultureInfo.InvariantCulture);
        var split = text.IndexOf('E', StringComparison.Ordinal);
        var exponent = split < 0 ? 0 : int.Parse(text[(split + 1)..], CultureInfo.InvariantCulture);
        var mantissa = split < 0 ? text : text[..split];
        var dot = mantissa.IndexOf('.', StringComparison.Ordinal);
        var whole = dot < 0 ? mantissa : mantissa[..dot];
        var all = dot < 0 ? mantissa : whole + mantissa[(dot + 1)..];
        var trimmed = all.TrimStart('0');
        var point = whole.Length + exponent - (all.Length - trimmed.Length);
        return (trimmed.TrimEnd('0'), point);
    }
}

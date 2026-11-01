using System.Globalization;
using System.Text.RegularExpressions;

namespace Orblit.Asset;

/// <summary>Three whole numbers joined by dots, such as <c>1.2.0</c>, which is all a kit or an engine version is.</summary>
internal static partial class KitVersion
{
    [GeneratedRegex(@"\A(0|[1-9][0-9]{0,5})\.(0|[1-9][0-9]{0,5})\.(0|[1-9][0-9]{0,5})\z", RegexOptions.CultureInvariant)]
    private static partial Regex Shape();

    internal static bool IsValid(string value) => Shape().IsMatch(value);

    /// <summary>Throws a <see cref="FormatException"/> unless <paramref name="value"/> is three whole numbers.</summary>
    internal static void Check(string field, string value)
    {
        if (!IsValid(value))
        {
            throw new FormatException(
                $"\"{field}\" is \"{KitText.Show(value)}\". Write three whole numbers of up to six digits, such as 1.2.0.");
        }
    }

    /// <summary>Orders two versions that passed <see cref="Check"/>.</summary>
    internal static int Compare(string a, string b)
    {
        var left = a.Split('.');
        var right = b.Split('.');
        for (var i = 0; i < 3; i++)
        {
            var order = int.Parse(left[i], CultureInfo.InvariantCulture).CompareTo(int.Parse(right[i], CultureInfo.InvariantCulture));
            if (order != 0)
            {
                return order;
            }
        }

        return 0;
    }
}

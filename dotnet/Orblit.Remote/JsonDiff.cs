namespace Orblit.Remote;

/// <summary>
/// One place two documents differ, as a path of keys and indices joined with
/// <c>/</c>. A value that is absent on one side is null there.
/// </summary>
public readonly record struct Difference(string Path, object? From, object? To);

/// <summary>
/// Compares two documents as <see cref="Orblit.Json.JsonText"/> reads them: null, bool, long, double,
/// string, a list or a map.
/// </summary>
public static class JsonDiff
{
    /// <summary>
    /// Every place the documents <paramref name="a"/> and <paramref name="b"/> differ. Maps and
    /// lists are walked together. A value that changed kind is reported once, where it changed,
    /// whole. A missing key and a null look the same, which is the price of one comparison that
    /// serves entities and any extras beside them.
    /// </summary>
    public static IReadOnlyList<Difference> Find(object? a, object? b)
    {
        var found = new List<Difference>();
        Walk(a, b, "", found);
        return found;
    }

    private static void Walk(object? a, object? b, string path, List<Difference> found)
    {
        switch (a, b)
        {
            case (IReadOnlyDictionary<string, object?> left, IReadOnlyDictionary<string, object?> right):
                var keys = new SortedSet<string>(left.Keys, StringComparer.Ordinal);
                keys.UnionWith(right.Keys);
                foreach (var key in keys)
                {
                    Walk(left.GetValueOrDefault(key), right.GetValueOrDefault(key), Join(path, key), found);
                }

                break;
            case (IReadOnlyList<object?> left, IReadOnlyList<object?> right):
                for (var i = 0; i < Math.Max(left.Count, right.Count); i++)
                {
                    Walk(i < left.Count ? left[i] : null, i < right.Count ? right[i] : null, Join(path, $"{i}"), found);
                }

                break;
            default:
                if (!Same(a, b))
                {
                    found.Add(new Difference(path, a, b));
                }

                break;
        }
    }

    private static string Join(string path, string key) => path.Length == 0 ? key : $"{path}/{key}";

    // A whole number equals the double that holds it, as 1 equals 1.0. NaN equals nothing.
    private static bool Same(object? a, object? b) => (a, b) switch
    {
        (long whole, double real) => Holds(real, whole),
        (double real, long whole) => Holds(real, whole),
        (double x, double y) => x == y,
        _ => Equals(a, b),
    };

    private static bool Holds(double real, long whole) =>
        real >= -9223372036854775808.0 && real < 9223372036854775808.0 && real == Math.Floor(real) && (long)real == whole;
}

namespace Orblit.Particles;

internal static class Require
{
    /// <summary>
    /// Throws an <see cref="ArgumentException"/> naming <paramref name="name"/>
    /// unless <paramref name="ok"/>. Dart repeated this check in three files.
    /// </summary>
    public static void That(bool ok, string name, object? value, string why)
    {
        if (!ok)
        {
            throw new ArgumentException($"{name} {why}. Got {Show(value)}.", name);
        }
    }

    private static object? Show(object? value) =>
        value is IEnumerable<double> list ? $"[{string.Join(", ", list)}]" : value;
}

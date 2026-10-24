namespace Orblit.Filament;

internal static class Packing
{
    /// <summary>Rounds each value to the float the renderer reads.</summary>
    internal static float[] Floats(params ReadOnlySpan<double> values)
    {
        var packed = new float[values.Length];
        for (var i = 0; i < packed.Length; i++)
        {
            packed[i] = (float)values[i];
        }

        return packed;
    }

    /// <summary>The array the message holds under <paramref name="key"/>.</summary>
    internal static T Read<T>(IReadOnlyDictionary<string, object> message, string key)
        where T : class => (T)message[key];

    /// <summary>The array the message holds under <paramref name="key"/>, or <paramref name="absent"/> when it holds none.</summary>
    internal static T Read<T>(IReadOnlyDictionary<string, object> message, string key, T absent)
        where T : class => message.TryGetValue(key, out var value) ? (T)value : absent;
}

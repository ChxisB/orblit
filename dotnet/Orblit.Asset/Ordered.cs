namespace Orblit.Asset;

/// <summary>Order, which Dart keeps in every map and a plain <see cref="Dictionary{TKey,TValue}"/> does not promise.</summary>
internal static class Ordered
{
    /// <summary>Copies a map with its entries in the order they were given.</summary>
    public static OrderedDictionary<TKey, TValue> Copy<TKey, TValue>(IEnumerable<KeyValuePair<TKey, TValue>>? pairs)
        where TKey : notnull
    {
        var copy = new OrderedDictionary<TKey, TValue>();
        foreach (var (key, value) in pairs ?? [])
        {
            copy[key] = value;
        }

        return copy;
    }

    /// <summary>Ids in the order of their text, as Dart orders strings: by UTF-16 code unit.</summary>
    public static List<AssetId> ById(IEnumerable<AssetId> ids) =>
        [.. ids.OrderBy(id => id.ToString(), StringComparer.Ordinal)];
}

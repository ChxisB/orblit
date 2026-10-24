namespace Orblit.Filament;

/// <summary>
/// A shader somebody wrote, and what it is given. The package is a compiled material, and each
/// name is one of its parameters: a number of one to four floats, or a texture.
/// </summary>
public sealed class CustomSurface
{
    /// <summary>The widest a value goes, which is a vector of four.</summary>
    public const int MaxWidth = 4;

    public CustomSurface(
        string package,
        IReadOnlyDictionary<string, IReadOnlyList<double>>? values = null,
        IReadOnlyDictionary<string, Texture>? textures = null)
    {
        Package = package;
        Values = Copy(values, numbers => (IReadOnlyList<double>)numbers.ToArray());
        Textures = Copy(textures, texture => texture);

        foreach (var (name, numbers) in Values)
        {
            if (numbers.Count is < 1 or > MaxWidth)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(values), $"{name}: a surface value holds 1 to {MaxWidth} numbers, not {numbers.Count}.");
            }

            if (Textures.ContainsKey(name))
            {
                throw new ArgumentException(
                    $"{name}: the name is also a texture, and a surface has one name for each thing it is given.",
                    nameof(values));
            }
        }
    }

    /// <summary>An absolute path to the compiled material.</summary>
    public string Package { get; }

    /// <summary>Numbers by parameter name, in the order they were given.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<double>> Values { get; }

    /// <summary>Textures by parameter name, in the order they were given.</summary>
    public IReadOnlyDictionary<string, Texture> Textures { get; }

    /// <summary>A copy that keeps the order of the source, because the message lists entries in it.</summary>
    private static OrderedDictionary<string, TOut> Copy<TIn, TOut>(
        IReadOnlyDictionary<string, TIn>? source,
        Func<TIn, TOut> copy)
    {
        var made = new OrderedDictionary<string, TOut>();
        foreach (var (name, one) in source ?? new Dictionary<string, TIn>())
        {
            made.Add(name, copy(one));
        }

        return made;
    }
}

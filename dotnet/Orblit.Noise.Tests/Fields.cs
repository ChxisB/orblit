namespace Orblit.Noise.Tests;

/// <summary>
/// The six fields the ported tests and the fixture exporter both use, under the same
/// names. The parity fixture and the ported tests read from here, so they
/// cannot drift apart.
/// </summary>
internal static class Fields
{
    public static readonly ValueNoise Value = new(7);
    public static readonly GradientNoise Gradient = new(7);

    public static readonly IReadOnlyDictionary<string, NoiseField> All = new Dictionary<string, NoiseField>
    {
        ["value"] = Value,
        ["gradient"] = Gradient,
        ["fractal"] = new FractalNoise(Gradient),
        ["ridged"] = new RidgedNoise(Gradient),
        ["cells"] = new CellNoise(3),
        ["tiling"] = new TilingNoise(Gradient, 8),
    };

    public static TheoryData<string> Names => new(All.Keys);
}

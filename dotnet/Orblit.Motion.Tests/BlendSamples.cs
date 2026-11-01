namespace Orblit.Motion.Tests;

/// <summary>The inputs and weights the blend tests share.</summary>
internal static class BlendSamples
{
    public static readonly IReadOnlyDictionary<string, double> NoInputs = new Dictionary<string, double>();

    public static Dictionary<string, double> Inputs(params (string Input, double Value)[] pairs) =>
        pairs.ToDictionary(static pair => pair.Input, static pair => pair.Value);

    /// <summary>Reads inputs out of <paramref name="values"/>, and nought for anything else.</summary>
    public static Func<string, double> Reading(IReadOnlyDictionary<string, double> values) =>
        input => values.GetValueOrDefault(input);

    public static void Weights(OrderedDictionary<string, double> actual, params (string Clip, double Weight)[] expected)
    {
        Assert.Equal(
            expected.Select(static one => one.Clip).Order(StringComparer.Ordinal),
            actual.Keys.Order(StringComparer.Ordinal));
        foreach (var (clip, weight) in expected)
        {
            Assert.Equal(weight, actual[clip], 1e-9);
        }
    }
}

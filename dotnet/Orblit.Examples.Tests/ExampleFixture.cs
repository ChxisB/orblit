using System.Text.Json;
using Orblit.Parity;

namespace Orblit.Examples.Tests;

/// <summary>The Dart examples' ticks and control moves, one case per example.</summary>
internal static class ExampleFixture
{
    public static readonly Fixture Golden = Fixture.Load("orblit_examples", "ticks");

    /// <summary>Names the gallery changed, against what the fixture calls them.</summary>
    private static readonly Dictionary<string, string> Renamed = new()
    {
        ["A scene as a control"] = "A scene as a widget",
    };

    /// <summary>
    /// Examples whose Dart controls were plain widgets, which the exporter does
    /// not see. Their own tests move them.
    /// </summary>
    public static readonly HashSet<string> Unlisted = ["Scene files"];

    /// <summary>
    /// Controls the Dart examples drew as plain widgets beside listed controls,
    /// by example. The exporter did not see them. Their own tests move them.
    /// </summary>
    public static readonly Dictionary<string, HashSet<string>> Plain = new()
    {
        ["Bistro exterior"] = ["Film speed"],
        ["Bistro interior"] = ["Ambient"],
        ["Video"] = [""],
    };

    /// <summary>
    /// The keys each example fills from what the two runtimes do not share, so
    /// they match on shape only. Most are filled from <see cref="Random"/>, and
    /// decision 77 says why its numbers differ from the Dart generator's. The
    /// terrain's population revisions come from a count kept across the whole
    /// process, so they depend on what else ran first. The Bistro's mesh paths
    /// are absolute, so they name the machine that ran the exporter.
    /// </summary>
    public static readonly Dictionary<string, HashSet<string>> Seeded = new()
    {
        ["Gaussian splats"] = ["splatData"],
        ["A hundred thousand"] = ["populationTransforms", "populationColours"],
        ["Benchmark"] = ["populationTransforms", "populationColours"],
        ["Terrain"] = ["populationRevisions"],
        ["Blocks"] = ["populationTransforms", "populationColours", "populationCounts"],
        ["Runner"] = ["populationTransforms", "populationColours"],
        ["Bistro exterior"] = ["meshPaths"],
        ["Bistro interior"] = ["meshPaths"],
    };

    /// <summary>
    /// Seeded examples whose seeded keys change length with the generator, so
    /// not even their shapes match. Their frames are matched key by key with
    /// those keys left out, and each move by its rest digest.
    /// </summary>
    public static readonly HashSet<string> Grown = ["Blocks"];

    /// <summary>
    /// Every example, with nowhere to keep a picture, nothing set in the
    /// environment and no renderer. The Dart fixture was written before any
    /// example's pictures were, so its scenes name none. Its runner never had
    /// its models, so it drew none.
    /// </summary>
    public static IReadOnlyList<Example> All() => EngineExamples.All(new NoPictures(), _ => null, NoRenderer);

    /// <summary>Hands nothing to a renderer, because there is none.</summary>
    public static readonly Action<string, byte[]> NoRenderer = (name, _) =>
        throw new DllNotFoundException($"There is no renderer to hand {name} to.");

    public static TheoryData<string> Names() => [.. All().Select(example => example.Name)];

    public static Example Example(string name) => All().Single(example => example.Name == name);

    public static JsonElement Case(string name)
    {
        var named = Renamed.GetValueOrDefault(name, name);
        return Golden.Cases.SingleOrDefault(c => c.GetProperty("name").GetString() == named) is
        { ValueKind: JsonValueKind.Object } found
            ? found
            : throw new InvalidDataException($"The fixture has no example called {named}.");
    }

    internal sealed class NoPictures : IPictureFolder
    {
        public string? Keep(string example, string name, int size, byte[] rgba) => null;
    }
}

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
    /// Every example, with nowhere to keep a picture. The Dart fixture was
    /// written before any example's pictures were, so its scenes name none.
    /// </summary>
    public static IReadOnlyList<Example> All() => EngineExamples.All(new NoPictures());

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

    private sealed class NoPictures : IPictureFolder
    {
        public string? Keep(string example, string name, int size, byte[] rgba) => null;
    }
}

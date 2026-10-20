using System.Text.Json;

namespace Orblit.Parity;

/// <summary>
/// A file the Dart twin wrote. <paramref name="DartCommit"/> is the commit of
/// the Dart package that produced it, with "+dirty" if it was not committed.
/// </summary>
public sealed record Fixture(string Package, string DartCommit, IReadOnlyList<JsonElement> Cases)
{
    /// <summary>
    /// Reads <c>fixtures/{package}/{name}.json</c> from beside the test
    /// assembly, where the test project copies it.
    /// </summary>
    public static Fixture Load(string package, string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", package, name + ".json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        return new Fixture(
            root.GetProperty("package").GetString() ?? throw new InvalidDataException($"{path} has no package."),
            root.GetProperty("dartCommit").GetString() ?? throw new InvalidDataException($"{path} has no dartCommit."),
            [.. root.GetProperty("cases").EnumerateArray().Select(c => c.Clone())]);
    }
}

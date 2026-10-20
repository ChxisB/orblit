using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

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

    /// <summary>
    /// Runs <paramref name="run"/> over every case of <paramref name="op"/> and compares what it gives
    /// with what Dart gave. A case where the port throws is a failure, not a pass.
    /// </summary>
    public void Check(string op, Func<JsonElement, JsonNode?> run)
    {
        var cases = Cases.Where(c => c.GetProperty("op").GetString() == op).ToList();
        if (cases.Count == 0)
        {
            throw new ParityException($"the fixture has no {op} cases");
        }

        var failures = new List<string>();
        for (var index = 0; index < cases.Count && failures.Count < 5; index++)
        {
            var difference = Difference(cases[index], run);
            if (difference is not null)
            {
                var text = cases[index].GetRawText();
                failures.Add($"case {index} {(text.Length > 160 ? text[..160] : text)}: {difference}");
            }
        }

        if (failures.Count > 0)
        {
            throw new ParityException($"{op}: {string.Join(Environment.NewLine, failures)} (from Dart commit {DartCommit})");
        }
    }

    private static string? Difference(JsonElement c, Func<JsonElement, JsonNode?> run)
    {
        try
        {
            return JsonCompare.FirstDifference(c.GetProperty("expect"), JsonSerializer.SerializeToElement(run(c)));
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            return $"C# threw {e.GetType().Name}: {e.Message}";
        }
    }

    /// <summary>
    /// A double the exporter wrote with <c>number</c>: a JSON number, or one of
    /// the strings "NaN", "Infinity" and "-Infinity".
    /// </summary>
    public static double Number(JsonElement value) =>
        value.ValueKind == JsonValueKind.String
            ? double.Parse(value.GetString()!, CultureInfo.InvariantCulture)
            : value.GetDouble();
}

/// <summary>The port gave an answer that differs from Dart's.</summary>
public sealed class ParityException(string message) : Exception(message);

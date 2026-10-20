using System.Text.Json;
using System.Text.Json.Nodes;

namespace Orblit.Parity.Tests;

public class FixtureCheckTests
{
    private static Fixture Dart(params string[] cases) =>
        new("orblit_x", "abc123", [.. cases.Select(c => JsonDocument.Parse(c).RootElement)]);

    private static JsonNode? Double(JsonElement c) => JsonValue.Create(c.GetProperty("in").GetInt32() * 2);

    [Fact]
    public void PassesWhenEveryCaseOfTheOpAgrees() =>
        Dart("""{"op":"x","in":1,"expect":2}""", """{"op":"x","in":2,"expect":4}""").Check("x", Double);

    [Fact]
    public void LeavesTheCasesOfOtherOpsAlone() =>
        Dart("""{"op":"x","in":1,"expect":2}""", """{"op":"y","in":1,"expect":99}""").Check("x", Double);

    [Fact]
    public void FailsAndNamesTheDartCommitWhenACaseDiffers()
    {
        var failure = Assert.Throws<ParityException>(() => Dart("""{"op":"x","in":1,"expect":3}""").Check("x", Double));
        Assert.Contains("abc123", failure.Message);
    }

    [Fact]
    public void FailsWhenThePortThrowsWhereDartGaveAValue()
    {
        var failure = Assert.Throws<ParityException>(() =>
            Dart("""{"op":"x","in":1,"expect":2}""").Check("x", _ => throw new InvalidOperationException("boom")));
        Assert.Contains("C# threw InvalidOperationException: boom", failure.Message);
    }

    [Fact]
    public void FailsWhenTheFixtureHasNoCasesOfTheOp()
    {
        var failure = Assert.Throws<ParityException>(() => Dart("""{"op":"y","in":1,"expect":2}""").Check("x", Double));
        Assert.Contains("no x cases", failure.Message);
    }

    [Fact]
    public void ReportsAtMostFiveFailures()
    {
        var cases = Enumerable.Range(0, 9).Select(i => $$"""{"op":"x","in":{{i}},"expect":-1}""").ToArray();
        var failure = Assert.Throws<ParityException>(() => Dart(cases).Check("x", Double));
        Assert.Equal(5, failure.Message.Split("case ").Length - 1);
    }
}

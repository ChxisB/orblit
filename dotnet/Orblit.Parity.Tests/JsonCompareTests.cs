using System.Text.Json;

namespace Orblit.Parity.Tests;

public class JsonCompareTests
{
    private static string? Differ(string expected, string actual) =>
        JsonCompare.FirstDifference(JsonDocument.Parse(expected).RootElement, JsonDocument.Parse(actual).RootElement);

    [Fact]
    public void AgreesWhenObjectsHoldTheSameKeysInAnyOrder() =>
        Assert.Null(Differ("""{"a":1,"b":[true,null,"x"]}""", """{"b":[true,null,"x"],"a":1}"""));

    [Fact]
    public void AgreesOnANumberWithinTheTolerance() =>
        Assert.Null(Differ("""{"a":1.0}""", """{"a":1.0000000005}"""));

    [Fact]
    public void NamesThePathOfANumberOutsideTheTolerance() =>
        Assert.StartsWith("$.a[1]:", Differ("""{"a":[1,2]}""", """{"a":[1,2.1]}"""));

    [Fact]
    public void ReportsAKeyThatIsMissing() =>
        Assert.Contains("keys", Differ("""{"a":1,"b":2}""", """{"a":1}"""));

    [Fact]
    public void ReportsAnArrayOfAnotherLength() =>
        Assert.Contains("length", Differ("[1,2,3]", "[1,2]"));

    [Fact]
    public void ReportsAFlippedFlagAndANullThatIsNot()
    {
        Assert.NotNull(Differ("true", "false"));
        Assert.NotNull(Differ("null", "0"));
    }

    [Fact]
    public void ComparesStringsByTheirValueNotTheirEscapes() =>
        Assert.Null(Differ("\"café\"", "\"caf\\u00e9\""));
}

namespace Orblit.Json.Tests;

// The cases are the text rules every reader of an Orblit file shares: the asset, mesh and interface readers.
public class PlainTextTests
{
    [Theory]
    [InlineData("\u0085", true, false)]
    [InlineData("\uFEFF", true, true)]
    [InlineData("\u00A0", true, true)]
    [InlineData("x", false, false)]
    public void TrimAndPatternSpaceDifferOnlyAtTheNextLineCharacter(string text, bool trimmed, bool inPattern)
    {
        Assert.Equal(trimmed, PlainText.IsSpace(text[0]));
        Assert.Equal(inPattern, PlainText.IsPatternSpace(text[0]));
    }

    [Fact]
    public void TrimsBothEndsAndTrimEndOnlyTheEnd()
    {
        Assert.Equal("a b", PlainText.Trim("\u2003 a b\t\u3000"));
        Assert.Equal("\u2003 a b", PlainText.TrimEnd("\u2003 a b\t\u3000"));
        Assert.Equal(string.Empty, PlainText.Trim(" \u0085 "));
    }

    [Theory]
    [InlineData("", new[] { "" })]
    [InlineData("a  b", new[] { "a", "b" })]
    [InlineData(" a b ", new[] { "", "a", "b", "" })]
    [InlineData("a\u0085b", new[] { "a\u0085b" })]
    public void SplitsOnRunsOfPatternSpace(string text, string[] pieces) =>
        Assert.Equal(pieces, PlainText.SplitSpace(text));

    [Theory]
    [InlineData(" 1.5 ", 1.5)]
    [InlineData("-Infinity", double.NegativeInfinity)]
    [InlineData(".5e1", 5.0)]
    [InlineData("5.", 5.0)]
    public void ReadsAFraction(string text, double expected) => Assert.Equal(expected, PlainText.ParseDouble(text));

    [Theory]
    [InlineData("")]
    [InlineData("0x10")]
    [InlineData("1_0")]
    [InlineData("1e")]
    [InlineData("nan")]
    [InlineData("+ 1")]
    public void RefusesTextThatIsNotAFraction(string text) => Assert.Null(PlainText.ParseDouble(text));

    [Fact]
    public void ReadsNaNAsNaN() => Assert.True(double.IsNaN(PlainText.ParseDouble("NaN")!.Value));

    [Theory]
    [InlineData(" 12 ", 12L)]
    [InlineData("-0x10", -16L)]
    [InlineData("0xFFFFFFFFFFFFFFFF", -1L)]
    [InlineData("-9223372036854775808", long.MinValue)]
    public void ReadsAWholeNumber(string text, long expected) => Assert.Equal(expected, PlainText.ParseInt(text));

    [Theory]
    [InlineData("9223372036854775808")]
    [InlineData("0x10000000000000000")]
    [InlineData("-0x8000000000000001")]
    [InlineData("1.0")]
    public void RefusesTextThatIsNotAWholeNumber(string text) => Assert.Null(PlainText.ParseInt(text));

    [Fact]
    public void LowerCaseFoldsTheKelvinSignAndTheDottedCapitalI() =>
        Assert.Equal("ik", PlainText.ToLower("\u0130\u212A"));
}

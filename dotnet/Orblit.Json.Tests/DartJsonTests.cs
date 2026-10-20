namespace Orblit.Json.Tests;

// There is no Dart package to port, so the cases are what Dart's jsonDecode and jsonEncode gave when run.
public class DartJsonTests
{
    [Theory]
    [InlineData("9223372036854775807", 9223372036854775807L)]
    [InlineData("-9223372036854775808", long.MinValue)]
    [InlineData("-0", 0L)]
    [InlineData(" 12 ", 12L)]
    public void ReadsAWholeNumberThatFitsAsALong(string text, long expected) =>
        Assert.Equal(expected, Assert.IsType<long>(DartJson.Decode(text)));

    [Theory]
    [InlineData("9223372036854775808", 9223372036854775808.0)]
    [InlineData("-9223372036854775809", -9223372036854775808.0)]
    [InlineData("12345678901234567890", 12345678901234567890.0)]
    [InlineData("1e999", double.PositiveInfinity)]
    [InlineData("-1e999", double.NegativeInfinity)]
    [InlineData("1E5", 100000.0)]
    [InlineData("0.1e1", 1.0)]
    [InlineData("-0.0", -0.0)]
    public void ReadsEveryOtherNumberAsADouble(string text, double expected) =>
        Assert.Equal(expected, Assert.IsType<double>(DartJson.Decode(text)));

    [Fact]
    public void KeepsTheSignOfNegativeZero() =>
        Assert.True(double.IsNegative(Assert.IsType<double>(DartJson.Decode("-0.0"))));

    [Theory]
    [InlineData("00")]
    [InlineData("01")]
    [InlineData("1.")]
    [InlineData(".5")]
    [InlineData("+1")]
    [InlineData("1e")]
    [InlineData("1e+")]
    [InlineData("1.e1")]
    [InlineData("- 1")]
    [InlineData("\"\\u12\"")]
    [InlineData("\"a\nb\"")]
    [InlineData("\"a\tb\"")]
    [InlineData("\"\\x\"")]
    [InlineData("\"open")]
    [InlineData("\ufeff{}")]
    [InlineData("\u000b{}")]
    [InlineData("{} x")]
    [InlineData("{\"a\":1,}")]
    [InlineData("[1,]")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("nul")]
    [InlineData("True")]
    [InlineData("{\"a\" 1}")]
    [InlineData("{'a':1}")]
    [InlineData("[1 2]")]
    [InlineData("1 2")]
    public void RefusesWhatDartRefuses(string text) =>
        Assert.Throws<FormatException>(() => DartJson.Decode(text));

    [Fact]
    public void ReadsStringsAsDartDoes()
    {
        Assert.Equal("é", DartJson.Decode("\"é\""));
        Assert.Equal("/", DartJson.Decode("\"\\/\""));
        Assert.Equal("\ud800", DartJson.Decode("\"\\ud800\""));
        Assert.Equal("a\"\\\b\f\n\r\t", DartJson.Decode("\"a\\\"\\\\\\b\\f\\n\\r\\t\""));
        Assert.Equal("\u007f ", DartJson.Decode("\"\u007f \""));
    }

    [Fact]
    public void ReadsTheLiterals()
    {
        Assert.Null(DartJson.Decode("null"));
        Assert.Equal(true, DartJson.Decode("true"));
        Assert.Equal(false, DartJson.Decode("false"));
    }

    [Fact]
    public void KeepsTheFirstPlaceAndTheLastValueOfARepeatedKey()
    {
        var map = Assert.IsType<OrderedDictionary<string, object?>>(DartJson.Decode("{\"a\":1,\"b\":2,\"a\":3}"));
        Assert.Equal(["a", "b"], map.Keys);
        Assert.Equal(3L, map["a"]);
    }

    [Fact]
    public void ReadsNestedValues()
    {
        var list = Assert.IsType<List<object?>>(DartJson.Decode("[1, [2.5, {\"k\": []}], {}]"));
        Assert.Equal(3, list.Count);
        Assert.Equal(1L, list[0]);
        Assert.Empty(Assert.IsType<OrderedDictionary<string, object?>>(list[2]));
    }

    [Fact]
    public void RefusesNestingPastTheLimitAndReadsNestingAtIt()
    {
        string Nested(int depth) => new string('[', depth) + new string(']', depth);
        Assert.NotNull(DartJson.Decode(Nested(DartJson.MaxDepth)));
        Assert.Throws<FormatException>(() => DartJson.Decode(Nested(DartJson.MaxDepth + 1)));
    }

    private static OrderedDictionary<string, object?> Sample() => new()
    {
        ["frames"] = new OrderedDictionary<string, object?>
        {
            ["a>b"] = new OrderedDictionary<string, object?> { ["x"] = 1L, ["flag"] = true, ["none"] = null },
        },
        ["list"] = new List<object?> { 1, "é\n\u0001\ud800", new List<object?>() },
        ["empty"] = new OrderedDictionary<string, object?>(),
    };

    [Fact]
    public void WritesCompactJsonWithoutEscapingWhatDartDoesNotEscape() =>
        Assert.Equal(
            "{\"frames\":{\"a>b\":{\"x\":1,\"flag\":true,\"none\":null}},\"list\":[1,\"é\\n\\u0001\\ud800\",[]],\"empty\":{}}",
            DartJson.Encode(Sample()));

    [Fact]
    public void WritesIndentedJsonLikeJsonEncoderWithIndent() =>
        Assert.Equal(
            "{\n  \"frames\": {\n    \"a>b\": {\n      \"x\": 1,\n      \"flag\": true,\n      \"none\": null\n    }\n  },\n"
            + "  \"list\": [\n    1,\n    \"é\\n\\u0001\\ud800\",\n    []\n  ],\n  \"empty\": {}\n}",
            DartJson.Encode(Sample(), "  "));

    [Fact]
    public void ReadsBackWhatItWrote()
    {
        var text = DartJson.Encode(Sample(), "  ");
        Assert.Equal(DartJson.Encode(Sample()), DartJson.Encode(DartJson.Decode(text)));
    }

    [Fact]
    public void RefusesAValueItDoesNotWrite() =>
        Assert.Throws<ArgumentException>(() => DartJson.Encode(1.5));
}

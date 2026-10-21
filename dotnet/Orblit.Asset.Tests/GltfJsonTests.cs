namespace Orblit.Asset.Tests;

// Dart's `as int?` and `as int`, as C# has to spell them.
public sealed class GltfJsonTests
{
    [Fact]
    public void ReadsWholeNumbersOfEitherWidth()
    {
        Assert.Equal(3, GltfJson.AsInt(3));
        Assert.Equal(3, GltfJson.AsInt(3L));
        Assert.Null(GltfJson.AsInt(null));
    }

    [Fact]
    public void ARealNumberIsNotAnInt() => Assert.Throws<InvalidCastException>(() => GltfJson.AsInt(2.0));

    [Fact]
    public void AStringIsNotAnInt() => Assert.Throws<InvalidCastException>(() => GltfJson.AsInt("3"));

    [Fact]
    public void ALongPastTheRangeOfAnIntIsRefusedRatherThanWrapped() =>
        Assert.Throws<OverflowException>(() => GltfJson.AsInt(1L << 40));

    [Fact]
    public void ReadsAKeyAsAnIntOrItsFallback()
    {
        var map = new GltfMap { ["a"] = 4L };

        Assert.Equal(4, GltfJson.IntAt(map, "a"));
        Assert.Null(GltfJson.IntAt(map, "b"));
        Assert.Equal(4, GltfJson.IntOr(map, "a", 9));
        Assert.Equal(9, GltfJson.IntOr(map, "b", 9));
        Assert.Equal(4, GltfJson.RequiredInt(map, "a"));
    }

    [Fact]
    public void ARequiredKeyThatIsMissingIsNamed() =>
        Assert.Equal(
            "\"a\" is not an int.",
            Assert.Throws<InvalidCastException>(() => GltfJson.RequiredInt([], "a")).Message);

    [Fact]
    public void OnlyWholeNumbersAreInts()
    {
        Assert.True(GltfJson.IsInt(1));
        Assert.True(GltfJson.IsInt(1L));
        Assert.False(GltfJson.IsInt(1.0));
        Assert.False(GltfJson.IsInt(null));
    }
}

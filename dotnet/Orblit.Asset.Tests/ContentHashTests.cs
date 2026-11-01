using System.Text;

namespace Orblit.Asset.Tests;

public class ContentHashTests
{
    private const string EmptyHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
    private const string AbcHash = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";

    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);

    // a content hash

    [Fact]
    public void IsTheSha256OfTheBytesAsPublished()
    {
        Assert.Equal(EmptyHash, ContentHash.Of([]).Hex);
        Assert.Equal(AbcHash, ContentHash.Of(Utf8("abc")).Hex);
    }

    [Fact]
    public async Task FromAStreamIsTheSameAsFromTheBytesHoweverCut()
    {
        var bytes = Utf8("the quick brown fox jumps over the lazy dog");
        var whole = ContentHash.Of(bytes);

        Assert.Equal(whole, await ContentHash.OfStreamAsync(new[] { (ReadOnlyMemory<byte>)bytes }.ToAsyncEnumerable()));
        var cut = new[]
        {
            (ReadOnlyMemory<byte>)bytes.AsMemory(0, 1),
            bytes.AsMemory(1, 16),
            ReadOnlyMemory<byte>.Empty,
            bytes.AsMemory(17),
        };
        Assert.Equal(whole, await ContentHash.OfStreamAsync(cut.ToAsyncEnumerable()));
    }

    [Fact]
    public async Task OfAnEmptyStreamIsTheHashOfNothing() =>
        Assert.Equal(EmptyHash, (await ContentHash.OfStreamAsync(Array.Empty<ReadOnlyMemory<byte>>().ToAsyncEnumerable())).Hex);

    [Fact]
    public void IsTheSameForTheSameBytesAndDifferentForDifferentOnes()
    {
        Assert.Equal(ContentHash.Of([1, 2, 3]), ContentHash.Of([1, 2, 3]));
        Assert.Equal(ContentHash.Of([1, 2, 3]).GetHashCode(), ContentHash.Of([1, 2, 3]).GetHashCode());
        Assert.NotEqual(ContentHash.Of([1, 2, 3]), ContentHash.Of([1, 2, 4]));
    }

    [Fact]
    public void ItsShardIsItsFirstTwoDigits() => Assert.Equal("ba", ContentHash.Of(Utf8("abc")).Shard);

    [Fact]
    public void ReadsBackWhatItWrites()
    {
        var hash = ContentHash.Of(Utf8("abc"));
        Assert.Equal(hash, ContentHash.Parse(hash.Hex));
        Assert.Equal(AbcHash, hash.ToString());
    }

    [Fact]
    public void UpperCaseDigitsAreReadAsTheSameHash()
    {
        var upper = ContentHash.Parse(AbcHash.ToUpperInvariant());
        Assert.Equal(AbcHash, upper.Hex);
        Assert.Equal(ContentHash.Parse(AbcHash), upper);
    }

    // a content hash is refused when

    [Fact]
    public void ItIsTheWrongLength()
    {
        var error = Assert.Throws<AssetFormatException>(() => ContentHash.Parse(AbcHash[1..]));
        Assert.Contains("63 characters", error.Message);
        Assert.Throws<AssetFormatException>(() => ContentHash.Parse(""));
    }

    [Fact]
    public void ItHasSomethingInItThatIsNotAHexadecimalDigit()
    {
        var bad = $"{AbcHash[..10]}g{AbcHash[11..]}";
        var error = Assert.Throws<AssetFormatException>(() => ContentHash.Parse(bad));
        Assert.Contains("\"g\"", error.Message);
        Assert.Equal(10, error.Offset);
        Assert.Throws<AssetFormatException>(() => ContentHash.Parse($"{AbcHash[1..]}:"));
    }
}

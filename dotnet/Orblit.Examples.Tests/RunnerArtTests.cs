using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Orblit.Json;
using Orblit.Sprite;

namespace Orblit.Examples.Tests;

/// <summary>
/// The runner's models and pictures. Every file made without a random draw is
/// pinned by hash to the bytes the Dart runner made. The bushes and the two
/// pictures draw from <see cref="Random"/>, which gives other numbers than the
/// Dart's generator, so they are pinned on what no draw changes.
/// </summary>
public sealed class RunnerArtTests
{
    private static readonly IReadOnlyDictionary<string, byte[]> Files = RunnerArt.Build();

    public static TheoryData<string> Models => new(Files.Keys.Where(name => name.EndsWith(".glb", StringComparison.Ordinal)));

    [Fact]
    public void TheFilesAreNamedByVersionSoAChangedModelIsANewName()
    {
        Assert.Equal(21, Files.Count);
        Assert.All(Files.Keys, name => Assert.StartsWith($"orblit:resource/runner/v{RunnerArt.Version}/", name, StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(Models))]
    public void AModelIsBinaryGltfThatSaysWhatItHolds(string name)
    {
        var bytes = Files[name];
        Assert.Equal(0x46546C67u, Word(bytes, 0));
        Assert.Equal(2u, Word(bytes, 4));
        Assert.Equal((uint)bytes.Length, Word(bytes, 8));

        var jsonLength = (int)Word(bytes, 12);
        Assert.Equal(0x4E4F534Au, Word(bytes, 16));
        Assert.Equal(0, jsonLength % 4);
        var json = Map(JsonText.Decode(Encoding.UTF8.GetString(bytes, 20, jsonLength)));

        var binAt = 20 + jsonLength;
        var binLength = (long)Word(bytes, binAt);
        Assert.Equal(0x004E4942u, Word(bytes, binAt + 4));
        Assert.Equal(bytes.Length, binAt + 8 + binLength);

        Assert.Equal(binLength, Map(Assert.Single(List(json["buffers"])))["byteLength"]);
        foreach (var view in List(json["bufferViews"]).Select(Map))
        {
            var at = (long)(view.GetValueOrDefault("byteOffset") ?? 0L);
            Assert.Equal(0, at % 4);
            Assert.True(at + (long)view["byteLength"]! <= binLength);
        }

        Assert.NotNull(Assert.Single(List(json["meshes"])));
    }

    [Fact]
    public void ThePicturesArePng()
    {
        foreach (var name in new[] { RunnerArt.Asphalt, RunnerArt.Grass })
        {
            Assert.Equal([137, 80, 78, 71, 13, 10, 26, 10], Files[name][..8]);
        }
    }

    [Theory]
    [InlineData("bar.glb", 16244, "fd0a02508e8eb29ca1aafdcf42823335513104dcc36e5110d700e29736f47a84")]
    [InlineData("billboard.glb", 36268, "94796d101f803e97d8d17e335b1fcf4e5eafbfd9f2e97c8525cd8b767f185881")]
    [InlineData("body.glb", 170468, "c5b9c1f0760b3900df888c374e36d0d85d81a754925da0ab4b0c520bd0f66926")]
    [InlineData("cloud.glb", 86900, "b7ea0d15b1f31a03178fd0ce6a641c0358079b917a3f57438f9ef8ed53918cd3")]
    [InlineData("coin.glb", 19172, "37873c9988c0dbb6ebcd40c35bb3f6b755c7ef70c61b77dcae51d676b01d123e")]
    [InlineData("container.glb", 50216, "352122146b5926e1e3fa1f750c51cea99a562f71d7bf36dd2b3cd615601db941")]
    [InlineData("dark_house.glb", 23496, "c916a81988816f773ecf75efd9d5fb32d8ebe19d435212d44bbb73ec40833b4f")]
    [InlineData("foot.glb", 22700, "fcc3c3eca05e8d72bb2c6e9532bcbf24d1150797b678c5ed771bdab5305bbbb6")]
    [InlineData("ground.glb", 31680, "e910efb4456b20a9332613b2ac641d3c4fe42b7f51a2073f75360fd73101a2d5")]
    [InlineData("hand.glb", 32012, "269c3448c4cf64c2421d72151eba9c1b1040f11f27e43e6ba5cb227549dbc842")]
    [InlineData("hill.glb", 50564, "a72e5a00233ad2441fb13465b8a3870eb135dbf468df4ed6ff7b0fc983be5038")]
    [InlineData("house.glb", 23488, "790c2a97a78475a7341c253bd3400cf3df0bd422f5ee691b84cd1984d2526c1d")]
    [InlineData("hurdle.glb", 16540, "93753191200eed8c61a2e4aab04b083b85dee95e40efdf8c004942f8f983db54")]
    [InlineData("oak.glb", 83664, "b139fe5246f812ef01a0e07613e3e0a13207fffa0cf2abe8a29b62d55b67757d")]
    [InlineData("pine.glb", 14636, "9ecf7b2418bcd22a6897d779a35589618f2317c9030f1496c0b0cb373d1545d1")]
    [InlineData("road.glb", 19476, "4accb3e6358bed4df51b5e7a70c1cc5d4fad6e81927453485587b24ae0699767")]
    [InlineData("rock.glb", 6996, "b2901c4352ac63bd43ccc7cce759ac0c663641b37584c1607e69cddb804d95da")]
    public void AModelWithNoRandomDrawIsTheBytesTheDartMade(string file, int length, string sha)
    {
        var bytes = Files[RunnerArt.Name(file)];

        Assert.Equal(length, bytes.Length);
        Assert.Equal(sha, Hash(bytes));
    }

    /// <summary>
    /// The leaves come first in the binary and draw nothing, so they match the
    /// Dart's bytes. The flowers fall where the draws put them, but each is the
    /// same ball, so the binary keeps its length.
    /// </summary>
    [Theory]
    [InlineData("bush.glb", "[{\"pbrMetallicRoughness\":{\"baseColorFactor\":[0.04817182422688942,0.33245153634617935,0.04091519690685319,1.0],\"metallicFactor\":0.0,\"roughnessFactor\":0.8}},{\"pbrMetallicRoughness\":{\"baseColorFactor\":[0.7454042095403874,0.052860647023180246,0.04970656598412723,1.0],\"metallicFactor\":0.0,\"roughnessFactor\":0.8}}]")]
    [InlineData("red_bush.glb", "[{\"pbrMetallicRoughness\":{\"baseColorFactor\":[0.5711248294648731,0.0295568344378088,0.02518685962736163,1.0],\"metallicFactor\":0.0,\"roughnessFactor\":0.8}},{\"pbrMetallicRoughness\":{\"baseColorFactor\":[0.8713671191987972,0.25415209433082675,0.042311410620809675,1.0],\"metallicFactor\":0.0,\"roughnessFactor\":0.8}}]")]
    public void ABushHasTheDartLeavesAndColoursWhereverItsFlowersFall(string file, string materials)
    {
        var bytes = Files[RunnerArt.Name(file)];
        var jsonLength = (int)Word(bytes, 12);
        var text = Encoding.UTF8.GetString(bytes, 20, jsonLength);
        var bin = bytes[(28 + jsonLength)..];
        var flowersAt = (int)(long)Map(List(Map(JsonText.Decode(text))["bufferViews"])[4])["byteOffset"]!;

        Assert.Equal(82008, bin.Length);
        Assert.Equal("f82f3e274e63d41d1f66366de08eba85cad4b1323dbe6be6943ec997abb83623", Hash(bin[..flowersAt]));
        Assert.Contains($"\"materials\":{materials},", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Each pixel of the asphalt read as paint, kerb or bare road by its red.
    /// The draws only shade each of those, so the map is the Dart's.
    /// </summary>
    [Fact]
    public void TheAsphaltIsPaintedWhereTheDartPaintedIt()
    {
        var picture = PngCodec.Decode(Files[RunnerArt.Asphalt]);
        var map = new StringBuilder();
        for (var i = 0; i < picture.Pixels.Length; i += 4)
        {
            var red = picture.Pixels[i];
            map.Append(red > 200 ? 'p' : red > 140 ? 'k' : 'a');
        }

        Assert.Equal((256L, 256L), (picture.Width, picture.Height));
        Assert.Equal("b25292142242690c264f85757ac3beda78284c99c4b72b4dead5370b8823e7b3", Hash(Encoding.ASCII.GetBytes(map.ToString())));
    }

    [Fact]
    public void TheGrassIsOneOpaqueRepeatSquare()
    {
        var picture = PngCodec.Decode(Files[RunnerArt.Grass]);

        Assert.Equal((256L, 256L), (picture.Width, picture.Height));
        Assert.All(picture.Pixels.Where((_, i) => i % 4 == 3), alpha => Assert.Equal(255, alpha));
    }

    /// <summary>
    /// The mottle runs from a deep green to a yellow green, and the speck on top
    /// moves a pixel by eleven at most, so no pixel leaves that range.
    /// </summary>
    [Fact]
    public void TheGrassStaysBetweenItsTwoGreens()
    {
        var pixels = PngCodec.Decode(Files[RunnerArt.Grass]).Pixels;

        Assert.All(pixels.Where((_, i) => i % 4 == 0), red => Assert.InRange(red, 81, 195));
        Assert.All(pixels.Where((_, i) => i % 4 == 1), green => Assert.InRange(green, 139, 223));
    }

    private static uint Word(byte[] bytes, int at) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at));

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static IReadOnlyDictionary<string, object?> Map(object? value) => Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(value);

    private static IReadOnlyList<object?> List(object? value) => Assert.IsAssignableFrom<IReadOnlyList<object?>>(value);
}

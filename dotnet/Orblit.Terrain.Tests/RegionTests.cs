using System.Text;
using Orblit.Json;

namespace Orblit.Terrain.Tests;

public class RegionTests
{
    private static TerrainRegion Painted()
    {
        var region = new TerrainRegion(new RegionKey(-2, 5), 4);
        for (var n = 0; n < 16; n++)
        {
            region.Heights[n] = (float)((n * 1.25) - 7);
            region.Cover[n] = Cover.Of(@base: n, overlay: 31 - n, blend: n / 15.0).Word;
        }

        region.SetColour(2, 1, GroundColour.Of(red: 90, roughness: 0.5));
        return region;
    }

    private static byte[] Padded(string text, int extra)
    {
        var header = Encoding.UTF8.GetBytes(text);
        var bytes = new byte[((header.Length + 3) & ~3) + extra];
        header.CopyTo(bytes, 0);
        return bytes;
    }

    // RegionKey

    [Fact]
    public void RoundsDownNotTowardsZero()
    {
        Assert.Equal(new RegionKey(0, 0), RegionKey.Containing(0, 0, 256));
        Assert.Equal(new RegionKey(0, 1), RegionKey.Containing(255, 256, 256));
        Assert.Equal(new RegionKey(-1, -1), RegionKey.Containing(-1, -256, 256));
        Assert.Equal(new RegionKey(-2, 0), RegionKey.Containing(-257, 0, 256));
    }

    [Fact]
    public void NamesAFileItCanBeReadBackFrom()
    {
        var key = new RegionKey(-3, 12);
        Assert.Equal("x-3_z12.oregion", key.FileName);
        Assert.Equal(key, RegionKey.FromFileName(key.FileName));
        Assert.Null(RegionKey.FromFileName("x1_z2.png"));
        Assert.Null(RegionKey.FromFileName("terrain.oterrain"));
    }

    // TerrainRegion

    [Fact]
    public void IsFlatAutomaticAndUncolouredWhenNew()
    {
        var region = new TerrainRegion(new RegionKey(0, 0), 4);
        Assert.All(region.Heights, height => Assert.Equal(0f, height));
        Assert.All(region.Cover, word => Assert.Equal(Cover.Auto.Word, word));
        Assert.Equal(GroundColour.None, region.ColourAt(3, 3));
    }

    [Fact]
    public void RefusesASizeThatIsNotAPowerOfTwo()
    {
        Assert.ThrowsAny<ArgumentException>(() => new TerrainRegion(new RegionKey(0, 0), 6));
        Assert.ThrowsAny<ArgumentException>(() => new TerrainRegion(new RegionKey(0, 0), 1));
        Assert.ThrowsAny<ArgumentException>(() => new TerrainRegion(new RegionKey(0, 0), 4, heights: new float[15]));
    }

    [Fact]
    public void StoresTexelIJAtJSizeI()
    {
        var region = new TerrainRegion(new RegionKey(0, 0), 4);
        region.SetHeight(1, 2, 5);
        Assert.Equal(5f, region.Heights[(2 * 4) + 1]);
        Assert.Equal(5, region.HeightAt(1, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => region.HeightAt(4, 0));
    }

    [Fact]
    public void ARevisionIsNewForEveryChangeAndEveryRegion()
    {
        var a = new TerrainRegion(new RegionKey(0, 0), 2);
        var b = new TerrainRegion(new RegionKey(0, 0), 2);
        Assert.NotEqual(b.Revision, a.Revision);
        var before = a.Revision;
        a.SetCover(0, 0, Cover.Plain);
        Assert.True(a.Revision > before);
        Assert.True(a.Revision > b.Revision);
    }

    [Fact]
    public void MeasuresItsHeightRangeAgainAfterATouch()
    {
        var region = new TerrainRegion(new RegionKey(0, 0), 2);
        region.SetHeight(0, 0, -3);
        region.SetHeight(1, 1, 7);
        Assert.Equal((-3.0, 7.0), (region.MinHeight, region.MaxHeight));
        region.Heights[1] = 20;
        Assert.Equal(7, region.MaxHeight);
        region.Touch();
        Assert.Equal(20, region.MaxHeight);
    }

    // region files

    [Fact]
    public void ComeBackAsTheyWent()
    {
        var region = Painted();
        var load = TerrainRegion.Decode(region.Encode());
        Assert.Empty(load.Problems);
        Assert.Equal(region.Key, load.Region.Key);
        Assert.Equal(4, load.Region.Size);
        Assert.Equal(region.Heights, load.Region.Heights);
        Assert.Equal(region.Cover, load.Region.Cover);
        Assert.Equal(region.Colour, load.Region.Colour);
    }

    [Fact]
    public void LeaveOutMapsStillAtTheirDefaults()
    {
        var bytes = new TerrainRegion(new RegionKey(0, 0), 256).Encode();
        Assert.True(bytes.Length < 128);
        var load = TerrainRegion.Decode(bytes);
        Assert.Equal(256, load.Region.Size);
        Assert.Equal(Cover.Auto.Word, load.Region.Cover[0]);
    }

    [Fact]
    public void StartWithALineSayingWhatFollowsThenAlignedMaps()
    {
        var region = new TerrainRegion(new RegionKey(0, 0), 2);
        region.SetHeight(0, 0, 1);
        var bytes = region.Encode();
        var end = Array.IndexOf(bytes, (byte)0x0A);
        var header = (OrderedDictionary<string, object?>)DartJson.Decode(Encoding.UTF8.GetString(bytes, 0, end))!;
        Assert.Equal("orblit.region", header["kind"]);
        Assert.Equal(1L, header["formatVersion"]);
        var map = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(Assert.Single((List<object?>)header["maps"]!));
        Assert.Equal("height", map["name"]);
        Assert.Equal(16L, map["bytes"]);
        var start = (end + 4) & ~3;
        Assert.Equal(0, start % 4);
        Assert.Equal(start + 16, bytes.Length);

        // 1.0 as a little-endian float.
        Assert.Equal(new byte[] { 0x00, 0x00, 0x80, 0x3F }, bytes[start..(start + 4)]);
    }

    [Fact]
    public void RefuseWhatIsNotARegion()
    {
        Assert.Throws<RegionFormatException>(() => TerrainRegion.Decode(Encoding.UTF8.GetBytes("hello")));
        Assert.Throws<RegionFormatException>(() => TerrainRegion.Decode(Encoding.UTF8.GetBytes("{\"kind\":\"orblit.clip\"}\n")));
    }

    [Fact]
    public void RefuseOneFromANewerOrblit()
    {
        const string text = "{\"kind\":\"orblit.region\",\"formatVersion\":99," +
            "\"x\":0,\"z\":0,\"size\":2,\"maps\":[]}\n";
        var error = Assert.Throws<RegionFormatException>(() => TerrainRegion.Decode(Encoding.UTF8.GetBytes(text)));
        Assert.Contains("newer", error.Message);
    }

    [Fact]
    public void RefuseAMapCutShortOrTheWrongSize()
    {
        var bytes = Painted().Encode();
        Assert.Throws<RegionFormatException>(() => TerrainRegion.Decode(bytes[..200]));
        var wrong = Padded(
            "{\"kind\":\"orblit.region\",\"formatVersion\":1," +
            "\"x\":0,\"z\":0,\"size\":4,\"maps\":[{\"name\":\"height\",\"bytes\":8}]}\n",
            8);
        Assert.Throws<RegionFormatException>(() => TerrainRegion.Decode(wrong));
    }

    [Fact]
    public void LeaveOutAMapTheyDoNotKnowWithANote()
    {
        var bytes = Padded(
            "{\"kind\":\"orblit.region\",\"formatVersion\":1," +
            "\"x\":0,\"z\":0,\"size\":2,\"maps\":[{\"name\":\"wetness\",\"bytes\":4}]}\n",
            4);
        var load = TerrainRegion.Decode(bytes);
        Assert.Contains("wetness", Assert.Single(load.Problems));
        Assert.All(load.Region.Heights, height => Assert.Equal(0f, height));
    }
}

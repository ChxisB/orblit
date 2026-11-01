using System.Buffers.Binary;

namespace Orblit.Asset.Tests;

public class NetKtx2ChainTests
{
    // reading a mip chain
    [Fact]
    public void ReadsTheHeaderAndEveryLevel()
    {
        var chain = Ktx2Chain.Read(Ktx2Files.Mipped())!;
        Assert.Equal(256, chain.Width);
        Assert.Equal(256, chain.Height);
        Assert.Equal(0, chain.Depth);
        Assert.Equal(9, chain.Levels.Count);

        // Smallest first in the file, largest first in the index.
        Assert.True(chain.Levels[0].Offset > chain.Levels[^1].Offset);
        Assert.True(chain.Levels[0].Length > chain.Levels[^1].Length);
        Assert.True(chain.Levels[^1].End < chain.Levels[0].Offset);
    }

    [Fact]
    public void ASingleLevelFileHasNothingSmallerToOffer() => Assert.Null(Ktx2Chain.Read(Ktx2Files.Mipped(levels: 1)));

    // Every level of an uncompressed file has to stay aligned to its texel block, and shortening the index
    // moves all of them.
    [Fact]
    public void AFileStoredRawIsLeftAlone() => Assert.Null(Ktx2Chain.Read(Ktx2Files.Mipped(supercompression: 0)));

    [Fact]
    public void AnythingThatIsNotAKTX2IsNotOne()
    {
        var png = new byte[2048];
        new byte[] { 0x89, 0x50, 0x4E, 0x47 }.CopyTo(png, 0);
        Assert.Null(Ktx2Chain.Read(png));
    }

    [Fact]
    public void AHeaderThatHasNotAllArrivedYetSaysSo()
    {
        var whole = Ktx2Files.Mipped();
        Assert.Null(Ktx2Chain.Read(whole.AsSpan(0, 200)));
        Assert.NotNull(Ktx2Chain.Read(whole.AsSpan(0, 80 + (9 * 24))));
    }

    // The prefix has to carry the format description with it, which only works when the description comes
    // first.
    [Fact]
    public void AFileWithItsDescriptionAfterTheLevelsIsRefused() =>
        Assert.Null(Ktx2Chain.Read(Ktx2Files.Mipped(metadataLast: true)));

    // choosing a level
    [Fact]
    public void TakesTheCoarsestLevelStillAtLeastTheSizeAskedFor()
    {
        var chain = Ktx2Chain.Read(Ktx2Files.Mipped())!;

        // 256, 128, 64, 32 ... so 64 is level 2.
        Assert.Equal(2, chain.LevelAtLeast(64));
        Assert.Equal(2, chain.LevelAtLeast(60));
        Assert.Equal(3, chain.LevelAtLeast(32));
    }

    [Fact]
    public void NeverTheWholePictureAndNeverTheLastLevel()
    {
        var chain = Ktx2Chain.Read(Ktx2Files.Mipped())!;

        // Only level 0 is 256 across, and level 0 is the whole download.
        Assert.Null(chain.LevelAtLeast(256));
        Assert.Null(chain.LevelAtLeast(4096));

        // The 1x1 level is not worth a stage of its own either, so the smallest answer is the one above it.
        Assert.Equal(7, chain.LevelAtLeast(1));
        Assert.Equal(1, Ktx2Chain.Read(Ktx2Files.Mipped(width: 512, height: 512))!.LevelAtLeast(256));
    }

    [Fact]
    public void TheBytesNeededAreTheEndOfThatLevel()
    {
        var chain = Ktx2Chain.Read(Ktx2Files.Mipped())!;
        Assert.Equal(chain.Levels[2].End, chain.BytesFor(2));
        Assert.True(chain.BytesFor(2) < chain.Levels[0].End);
    }

    // building a smaller file out of the front of a bigger one
    [Fact]
    public void SaysHowBigItIsNowAndHowManyLevelsItHas()
    {
        var whole = Ktx2Files.Mipped();
        var small = Ktx2Chain.Read(whole)!.Prefix(whole, 3)!;

        var smaller = Ktx2Chain.Read(small)!;
        Assert.Equal(32, smaller.Width);
        Assert.Equal(32, smaller.Height);
        Assert.Equal(6, smaller.Levels.Count);
        Assert.True(small.Length < whole.Length);
    }

    [Fact]
    public void TheLevelsItKeptAreByteForByteTheOnesItHad()
    {
        var whole = Ktx2Files.Mipped();
        var chain = Ktx2Chain.Read(whole)!;
        var small = chain.Prefix(whole, 3)!;
        var smaller = Ktx2Chain.Read(small)!;

        for (var i = 0; i < smaller.Levels.Count; i++)
        {
            var was = chain.Levels[3 + i];
            var now = smaller.Levels[i];
            Assert.Equal(was.Length, now.Length);
            Assert.Equal(was.UncompressedLength, now.UncompressedLength);
            Assert.Equal(whole[(int)was.Offset..(int)was.End], small[(int)now.Offset..(int)now.End]);
        }
    }

    [Fact]
    public void TheDescriptionAndTheKeyValueDataTravelWithIt()
    {
        var whole = Ktx2Files.Mipped();
        var small = Ktx2Chain.Read(whole)!.Prefix(whole, 2)!;
        var dfdAt = (int)BinaryPrimitives.ReadUInt32LittleEndian(small.AsSpan(48));
        var kvdAt = (int)BinaryPrimitives.ReadUInt32LittleEndian(small.AsSpan(56));

        Assert.Equal(44u, BinaryPrimitives.ReadUInt32LittleEndian(small.AsSpan(52)));
        Assert.Equal(20u, BinaryPrimitives.ReadUInt32LittleEndian(small.AsSpan(60)));
        Assert.All(small[dfdAt..(dfdAt + 44)], b => Assert.Equal(0xD0, b));
        Assert.All(small[kvdAt..(kvdAt + 20)], b => Assert.Equal(0xCE, b));

        // Where nothing was, nothing is: an absent part keeps its nought rather than being shifted into
        // pointing at something.
        Assert.Equal(0L, BinaryPrimitives.ReadInt64LittleEndian(small.AsSpan(64)));
    }

    [Fact]
    public void NothingOverlapsAndNothingPointsPastTheEnd()
    {
        var whole = Ktx2Files.Mipped();
        var small = Ktx2Chain.Read(whole)!.Prefix(whole, 1)!;
        var smaller = Ktx2Chain.Read(small)!;
        Assert.Equal(small.Length, smaller.Levels[0].End);
        foreach (var level in smaller.Levels)
        {
            Assert.True(level.Offset >= smaller.MetadataEnd);
            Assert.True(level.End <= small.Length);
        }
    }

    [Fact]
    public void APrefixThatHasNotAllArrivedYetBuildsNothing()
    {
        var whole = Ktx2Files.Mipped();
        var chain = Ktx2Chain.Read(whole)!;
        var needed = (int)chain.BytesFor(2);
        Assert.Null(chain.Prefix(whole.AsSpan(0, needed - 1), 2));
        Assert.NotNull(chain.Prefix(whole.AsSpan(0, needed), 2));
    }

    [Fact]
    public void LevelNoughtIsTheWholeFileAndIsNotAPrefixOfIt()
    {
        var whole = Ktx2Files.Mipped();
        var chain = Ktx2Chain.Read(whole)!;
        Assert.Null(chain.Prefix(whole, 0));
        Assert.Null(chain.Prefix(whole, 9));
    }
}

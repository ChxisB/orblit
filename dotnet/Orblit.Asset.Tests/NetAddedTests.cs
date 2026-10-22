using System.Buffers.Binary;
using Orblit.Json;

namespace Orblit.Asset.Tests;

// What the fixtures do not say, and where the port makes a choice.
public class NetAddedTests
{
    private static readonly ContentHash Hash = ContentHash.Of([1, 2, 3]);

    private static FetchRecord? Record(string fetched) =>
        FetchRecord.FromJson(JsonText.Decode($$"""{"hash":"{{Hash.Hex}}","bytes":3,"fetched":"{{fetched}}"}"""));

    // A DateTimeOffset cannot hold these times, so the time is dropped and
    // the record is kept, as for any other time that is not one.
    [Theory]
    [InlineData("0000-01-01T00:00:00.000Z")]
    [InlineData("-002026-09-20T11:30:00.000Z")]
    [InlineData("+010000-01-01T00:00:00.000Z")]
    [InlineData("12345-01-01T00:00:00.000Z")]
    public void ATimeOutsideTheYearsOneToNineThousandNineHundredNinetyNineIsDroppedAndTheRecordKept(string fetched)
    {
        var record = Record(fetched)!;
        Assert.Equal(Hash, record.Hash);
        Assert.Null(record.Fetched);
    }

    // A time with no zone is read as UTC, because a cache moves between machines.
    [Fact]
    public void ATimeWithNoZoneIsReadAsUTC()
    {
        var record = Record("2026-09-20T11:30:00.000")!;
        Assert.Equal(new DateTimeOffset(2026, 9, 20, 11, 30, 0, TimeSpan.Zero), record.Fetched);
    }

    // A lenient URI parser accepts these. They are not addresses, and a rule that allows a host has to read the same
    // address the transport connects to.
    [Theory]
    [InlineData("https://a.example.com:65536/x")]
    [InlineData("https://a.example.com:+80/x")]
    [InlineData("https://a.example.com: 80/x")]
    [InlineData("http://[fe80::1%25eth0]/x")]
    [InlineData("http://[v1.fe]/x")]
    public void AnAddressThatIsNotOneIsRefused(string text)
    {
        Assert.False(WebUrl.TryParse(text, out _));
        Assert.Throws<FormatException>(() => WebUrl.Parse(text));
    }

    [Fact]
    public void ABackslashInAPathIsASlash() =>
        Assert.Equal("/a/b", WebUrl.Parse("https://h.example.com\\a\\b").Path);

    [Fact]
    public void ARequestThatCannotBeMadeIsRefused()
    {
        var url = WebUrl.Parse("https://h.example.com/a");
        Assert.Throws<ArgumentOutOfRangeException>(() => new FetchRequest(url, from: -1));
        Assert.Throws<ArgumentException>(() => new FetchRequest(url, through: 5));
        Assert.Throws<ArgumentException>(() => new FetchRequest(url, from: 6, through: 5));
    }

    // A header that claims billions of levels must not cost seconds. The levels that are one pixel are
    // added in one step, with the same wrapped total.
    [Fact]
    public void AHeaderClaimingBillionsOfLevelsIsCountedInOneStep()
    {
        var bytes = Ktx2Files.Plain(256, 256);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(40), uint.MaxValue);

        var clock = System.Diagnostics.Stopwatch.StartNew();
        var size = PictureSize.Of(bytes)!;

        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(2), $"took {clock.Elapsed}");
        Assert.Equal(87381L + (uint.MaxValue - 9L), size.Pixels);
    }

    // With a zero delay a send starts as soon as a thread is free, so the wants may go out apart, but each is
    // answered with its own bytes.
    [Fact]
    public async Task AZeroDelayStillAnswersEveryWant()
    {
        var queue = new RangeQueue(
            (at, bytes) => Task.FromResult(new byte[bytes].Select((_, i) => (byte)(at + i)).ToArray()),
            new RangeBatching { Delay = TimeSpan.Zero });

        var replies = await Task.WhenAll(Enumerable.Range(0, 10).Select(i => queue.WantAsync(i * 100L, 10)));

        for (var i = 0; i < 10; i++)
        {
            Assert.Equal(Enumerable.Range(i * 100, 10).Select(n => (byte)n), replies[i]);
        }
    }

    [Fact]
    public async Task AFetchThatThrowsBeforeItReturnsFailsTheWantsAndTheQueueGoesOn()
    {
        var fail = true;
        var queue = new RangeQueue(
            (at, bytes) => fail ? throw new FormatException("no") : Task.FromResult(new byte[bytes]),
            new RangeBatching { Delay = TimeSpan.FromMilliseconds(5) });

        await Assert.ThrowsAsync<FormatException>(() => queue.WantAsync(0, 10));

        fail = false;
        Assert.Equal(10, (await queue.WantAsync(0, 10)).Length);
    }

    // 1.0 is the same number as 1, so a file that says so is read. Another version is not.
    [Fact]
    public void AFormatVersionWrittenAsAFractionIsReadAndAnotherVersionIsNot()
    {
        static string File(string version) =>
            "{\"formatVersion\":" + version + ",\"records\":{\"https://cdn.example.com/a\":{\"hash\":\"" + Hash.Hex + "\",\"bytes\":3}}}";

        Assert.Single(FetchRecordsCodec.Decode(File("1")));
        Assert.Single(FetchRecordsCodec.Decode(File("1.0")));
        Assert.Empty(FetchRecordsCodec.Decode(File("2.0")));
    }

    [Fact]
    public void TheDefaultGapForMergingWantsIsSixteenKilobytes() =>
        Assert.Equal(16 * 1024, new RangeBatching().MergeGap);

    [Fact]
    public void AFileCutShortInsideItsHeaderIsNotAChain() =>
        Assert.Null(Ktx2Chain.Read(Ktx2Files.Mipped().AsSpan(0, 40)));

    [Fact]
    public void ALevelThatStartsRightAfterTheIndexIsAccepted() =>
        Assert.NotNull(Ktx2Chain.Read(Ktx2Files.Mipped(describe: 0, keyValues: 0)));

    [Theory]
    [InlineData("FFD8")]
    [InlineData("FFD8FF")]
    [InlineData("FFD8FFC0")]
    [InlineData("FFD8FFFF")]
    [InlineData("FFD8FFFFFF")]
    [InlineData("FFD8FFFFFFFF")]
    public void AJpegCutShortIsNotASizeAndNotAnError(string hex) =>
        Assert.Null(PictureSize.Of(Convert.FromHexString(hex)));

    // An APP1 segment whose length of 2 counts only itself, then a SOF0 saying 64 wide and 32 high.
    [Fact]
    public void AJpegSegmentWithNothingInItIsSkipped()
    {
        var jpeg = Convert.FromHexString("FFD8" + "FFE10002" + "FFC00011" + "08" + "0020" + "0040" + "03" + "012200" + "021101" + "031101");

        Assert.Equal("64×32", PictureSize.Of(jpeg)!.ToString());
    }

    // The widest and narrowest the colons can be written, and an octet at the top of its range.
    [Theory]
    [InlineData("http://[::]/")]
    [InlineData("http://[1:2:3:4:5:6:7::]/")]
    [InlineData("http://[::1.2.3.255]/")]
    public void AnIPv6HostAtTheEdgeOfWhatIsAllowedIsAccepted(string text) => Assert.Equal(text, WebUrl.Parse(text).ToString());

    [Fact]
    public void AnIPv4TailWithAnOctetOverTwoHundredFiftyFiveIsRefused() =>
        Assert.Throws<FormatException>(() => WebUrl.Parse("http://[::1.2.3.256]/"));
}

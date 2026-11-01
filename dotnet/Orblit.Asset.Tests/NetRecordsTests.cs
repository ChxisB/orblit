namespace Orblit.Asset.Tests;

public class NetRecordsTests
{
    private static readonly ContentHash Hash = ContentHash.Of([1, 2, 3]);
    private static readonly ContentHash Other = ContentHash.Of([4, 5, 6]);
    private static readonly WebUrl Url = WebUrl.Parse("https://cdn.example.com/game/robot.glb");

    private static void AssertSame(Dictionary<WebUrl, FetchRecord> expected, Dictionary<WebUrl, FetchRecord> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        foreach (var (key, record) in expected)
        {
            Assert.Equal(record, actual[key]);
        }
    }

    // one record
    [Fact]
    public void SurvivesTheRoundTripWhole()
    {
        var when = new DateTimeOffset(2026, 9, 20, 11, 30, 0, TimeSpan.Zero);
        var record = new FetchRecord(Hash, 3, "\"v1\"", when);

        var back = FetchRecord.FromJson(record.ToJson())!;
        Assert.Equal(Hash, back.Hash);
        Assert.Equal(3, back.Bytes);
        Assert.Equal("\"v1\"", back.ETag);
        Assert.Equal(when, back.Fetched);
    }

    [Fact]
    public void KeepsTheTimeInUTCWhereverItWasWritten()
    {
        var local = new DateTimeOffset(2026, 9, 20, 13, 30, 0, TimeSpan.FromHours(2));
        var back = FetchRecord.FromJson(new FetchRecord(Hash, 1, fetched: local).ToJson())!;
        Assert.Equal(TimeSpan.Zero, back.Fetched!.Value.Offset);
        Assert.Equal(local.UtcDateTime, back.Fetched.Value.UtcDateTime);
    }

    [Fact]
    public void AConfirmationMovesTheTimeAndNothingElse()
    {
        var record = new FetchRecord(Hash, 3, "\"v1\"", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var now = new DateTimeOffset(2026, 9, 20, 0, 0, 0, TimeSpan.Zero);
        var again = record.ConfirmedAt(now);

        Assert.Equal(Hash, again.Hash);
        Assert.Equal(3, again.Bytes);
        Assert.Equal("\"v1\"", again.ETag);
        Assert.Equal(now, again.Fetched);
    }

    [Fact]
    public void ARecordWithNoTagAndNoTimeIsStillARecord()
    {
        var back = FetchRecord.FromJson(new FetchRecord(Hash, 0).ToJson());
        Assert.NotNull(back);
        Assert.Null(back.ETag);
        Assert.Null(back.Fetched);
    }

    [Fact]
    public void NonsenseIsNoRecordRatherThanACrashAtStartup()
    {
        Assert.Null(FetchRecord.FromJson(null));
        Assert.Null(FetchRecord.FromJson("a string"));
        Assert.Null(FetchRecord.FromJson(new Dictionary<string, object?>()));

        // a hash that will not parse is not one
        Assert.Null(FetchRecord.FromJson(new Dictionary<string, object?> { ["hash"] = "not a hash", ["bytes"] = 1L }));

        // a negative length is not a length
        Assert.Null(FetchRecord.FromJson(new Dictionary<string, object?> { ["hash"] = Hash.Hex, ["bytes"] = -1L }));

        // without a length there is nothing to show progress against
        Assert.Null(FetchRecord.FromJson(new Dictionary<string, object?> { ["hash"] = Hash.Hex }));
    }

    [Fact]
    public void ATimeThatWillNotParseCostsTheTimeNotTheRecord()
    {
        var back = FetchRecord.FromJson(new Dictionary<string, object?>
        {
            ["hash"] = Hash.Hex,
            ["bytes"] = 3L,
            ["fetched"] = "last Tuesday",
        });
        Assert.NotNull(back);
        Assert.Equal(Hash, back.Hash);
        Assert.Null(back.Fetched);
    }

    // the whole set
    [Fact]
    public void SurvivesTheRoundTrip()
    {
        var records = new Dictionary<WebUrl, FetchRecord>
        {
            [Url] = new(Hash, 3, "\"v1\""),
            [WebUrl.Parse("https://cdn.example.com/game/wall.ktx2")] = new(Other, 6),
        };

        AssertSame(records, FetchRecordsCodec.Decode(FetchRecordsCodec.Encode(records)));
    }

    [Fact]
    public void WritesTheSameBytesEveryTimeForTheSameRecords()
    {
        var b = WebUrl.Parse("https://cdn.example.com/b");
        var a = WebUrl.Parse("https://cdn.example.com/a");
        var one = new Dictionary<WebUrl, FetchRecord> { [b] = new(Hash, 1), [a] = new(Other, 2) };
        var two = new Dictionary<WebUrl, FetchRecord> { [a] = new(Other, 2), [b] = new(Hash, 1) };

        Assert.Equal(FetchRecordsCodec.Encode(two), FetchRecordsCodec.Encode(one));
    }

    [Fact]
    public void OneBadEntryCostsThatEntryNotTheCache()
    {
        // The whole point of a cache that reads this way: a truncated write or a single corrupt line should
        // mean one asset downloads again, not that everything the app has ever fetched does.
        var text = FetchRecordsCodec.Encode(new Dictionary<WebUrl, FetchRecord>
        {
            [Url] = new(Hash, 3),
            [WebUrl.Parse("https://cdn.example.com/game/broken.bin")] = new(Other, 6),
        }).Replace(Other.Hex, "not a hash");

        Assert.Equal([Url], FetchRecordsCodec.Decode(text).Keys);
    }

    [Fact]
    public void ANewerFormatIsNotGuessedAt()
    {
        var text = FetchRecordsCodec.Encode(new Dictionary<WebUrl, FetchRecord> { [Url] = new(Hash, 3) })
            .Replace("\"formatVersion\":1", "\"formatVersion\":2");

        Assert.Empty(FetchRecordsCodec.Decode(text));
    }

    [Fact]
    public void AnythingThatIsNotARecordSetAtAllReadsAsEmpty()
    {
        Assert.Empty(FetchRecordsCodec.Decode(""));
        Assert.Empty(FetchRecordsCodec.Decode("}{ truncated"));
        Assert.Empty(FetchRecordsCodec.Decode("[1, 2, 3]"));
        Assert.Empty(FetchRecordsCodec.Decode("{\"formatVersion\":1}"));
        Assert.Empty(FetchRecordsCodec.Decode("{\"formatVersion\":1,\"records\":7}"));
    }

    // records held in memory
    [Fact]
    public async Task RememberForgetAndClear()
    {
        var records = new MemoryFetchRecords();
        Assert.Null(await records.GetAsync(Url));

        await records.PutAsync(Url, new FetchRecord(Hash, 3));
        Assert.Equal(Hash, (await records.GetAsync(Url))!.Hash);

        // a URL has one current version, not a history
        await records.PutAsync(Url, new FetchRecord(Other, 6));
        Assert.Equal(Other, (await records.GetAsync(Url))!.Hash);

        await records.RemoveAsync(Url);
        Assert.Null(await records.GetAsync(Url));

        await records.PutAsync(Url, new FetchRecord(Hash, 3));
        await records.ClearAsync();
        Assert.Null(await records.GetAsync(Url));
    }

    [Fact]
    public async Task DoesNotHoldOnToTheMapItWasHanded()
    {
        var given = new Dictionary<WebUrl, FetchRecord> { [Url] = new(Hash, 3) };
        var records = new MemoryFetchRecords(given);
        await records.RemoveAsync(Url);

        // the caller keeps its own map
        Assert.Single(given);
    }
}

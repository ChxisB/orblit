namespace Orblit.Asset.Tests;

public sealed class NetRecordsStoreTests : IDisposable
{
    private static readonly ContentHash Hash = ContentHash.Of([1, 2, 3]);
    private static readonly ContentHash Other = ContentHash.Of([4, 5, 6]);
    private static readonly WebUrl Url = WebUrl.Parse("https://cdn.example.com/game/robot.glb");
    private static readonly WebUrl Wall = WebUrl.Parse("https://cdn.example.com/game/wall.ktx2");

    // The Dart timers fire only when the test awaits a timer, so a burst of awaited puts cannot outrun them.
    // Here a settle is a real wait, so the ones that count writes use one that only a flush can cut short.
    private static readonly TimeSpan Long = TimeSpan.FromSeconds(30);

    private readonly DirectoryInfo _temp = Directory.CreateTempSubdirectory("orblit_records_");

    public void Dispose() => _temp.Delete(recursive: true);

    private string Root => _temp.FullName;

    private string RecordsFile => Path.Combine(Root, "records.json");

    // records kept in a directory
    [Fact]
    public async Task AnEmptyDirectoryIsSimplyNothingKnown()
    {
        var records = new DirectoryFetchRecords(Root);
        Assert.Null(await records.GetAsync(Url));
    }

    [Fact]
    public async Task WhatWasWrittenIsThereOnTheNextRun()
    {
        var writing = new DirectoryFetchRecords(Root, TimeSpan.Zero);
        await writing.PutAsync(Url, new FetchRecord(Hash, 3, "\"v1\""));
        await writing.CloseAsync();

        var reading = new DirectoryFetchRecords(Root);
        var back = await reading.GetAsync(Url);
        Assert.Equal(Hash, back!.Hash);
        Assert.Equal("\"v1\"", back.ETag);
    }

    [Fact]
    public async Task ForgettingOneSurvivesTheRoundTripToo()
    {
        var writing = new DirectoryFetchRecords(Root, TimeSpan.Zero);
        await writing.PutAsync(Url, new FetchRecord(Hash, 3));
        await writing.PutAsync(Wall, new FetchRecord(Other, 6));
        await writing.RemoveAsync(Url);
        await writing.CloseAsync();

        var reading = new DirectoryFetchRecords(Root);
        Assert.Null(await reading.GetAsync(Url));
        Assert.NotNull(await reading.GetAsync(Wall));
    }

    [Fact]
    public async Task ACorruptFileCostsTheCacheNotTheLaunch()
    {
        await File.WriteAllTextAsync(RecordsFile, "{ this is not json", TestContext.Current.CancellationToken);

        var records = new DirectoryFetchRecords(Root);
        Assert.Null(await records.GetAsync(Url));

        // And it recovers: writing over it works.
        await records.PutAsync(Url, new FetchRecord(Hash, 3));
        await records.CloseAsync();
        Assert.NotNull(await new DirectoryFetchRecords(Root).GetAsync(Url));
    }

    [Fact]
    public async Task TheFileIsOnlyEverWhole()
    {
        // Written to one side and renamed into place, so a reader never sees half a set, the same reason the
        // content store does it.
        var records = new DirectoryFetchRecords(Root, TimeSpan.Zero);
        for (var at = 0; at < 50; at++)
        {
            await records.PutAsync(WebUrl.Parse($"https://cdn.example.com/game/{at}.bin"), new FetchRecord(Hash, at));
        }

        await records.CloseAsync();

        var text = await File.ReadAllTextAsync(RecordsFile, TestContext.Current.CancellationToken);
        Assert.Equal(50, FetchRecordsCodec.Decode(text).Count);
    }

    [Fact]
    public async Task AnUnwritableDirectoryDoesNotTakeTheAppDown()
    {
        var records = new DirectoryFetchRecords(Path.Combine(Root, "nothing", "here"), TimeSpan.Zero);

        // Creating it is allowed to work. What must not happen is a throw out of put, which is called from the
        // middle of a download.
        await records.PutAsync(Url, new FetchRecord(Hash, 3));
        await records.FlushAsync();
        Assert.NotNull(await records.GetAsync(Url));
    }

    // writes are coalesced
    [Fact]
    public async Task ABurstOfChangesCostsOneWrite()
    {
        var counted = new Counted(Long);
        for (var at = 0; at < 200; at++)
        {
            await counted.PutAsync(WebUrl.Parse($"https://cdn.example.com/game/{at}.bin"), new FetchRecord(Hash, at));
        }

        await counted.FlushAsync();

        // two hundred assets loading is one write, not two hundred
        Assert.Equal(1, counted.Writes);
        Assert.Equal(200, FetchRecordsCodec.Decode(counted.Text!).Count);
    }

    [Fact]
    public async Task ALaterChangeIsWrittenToo()
    {
        var counted = new Counted(Long);
        await counted.PutAsync(Url, new FetchRecord(Hash, 3));
        await counted.FlushAsync();
        await counted.PutAsync(Url, new FetchRecord(Other, 6));
        await counted.FlushAsync();

        Assert.Equal(2, counted.Writes);
        Assert.Equal(Other, FetchRecordsCodec.Decode(counted.Text!)[Url].Hash);
    }

    [Fact]
    public async Task NothingIsWrittenWhenNothingChanged()
    {
        var counted = new Counted();
        await counted.GetAsync(Url);
        await counted.FlushAsync();

        Assert.Equal(0, counted.Writes);
    }

    [Fact]
    public async Task AStoreThatCannotBeWrittenIsNotACrash()
    {
        var broken = new Broken(TimeSpan.Zero);
        await broken.PutAsync(Url, new FetchRecord(Hash, 3));
        await broken.FlushAsync();

        // Still answers from memory. The cost was the next launch, not this one.
        Assert.Equal(Hash, (await broken.GetAsync(Url))!.Hash);
    }

    [Fact]
    public async Task AStoreThatCannotBeReadStartsEmpty()
    {
        var broken = new Broken();
        Assert.Null(await broken.GetAsync(Url));
    }

    [Fact]
    public async Task ClosingStopsSchedulingButNotReading()
    {
        var counted = new Counted(TimeSpan.FromMilliseconds(20));
        await counted.PutAsync(Url, new FetchRecord(Hash, 3));
        await counted.CloseAsync();
        Assert.Equal(1, counted.Writes);

        await counted.PutAsync(Url, new FetchRecord(Other, 6));
        await Task.Delay(TimeSpan.FromMilliseconds(120), TestContext.Current.CancellationToken);
        Assert.Equal(1, counted.Writes);
        Assert.Equal(Other, (await counted.GetAsync(Url))!.Hash);
    }

    // Buffered records over a string in memory, counting the writes.
    private sealed class Counted(TimeSpan? settle = null) : BufferedFetchRecords(settle)
    {
        public string? Text { get; private set; }

        public int Writes { get; private set; }

        protected override Task<string?> ReadTextAsync() => Task.FromResult(Text);

        protected override Task WriteTextAsync(string text)
        {
            Writes++;
            Text = text;
            return Task.CompletedTask;
        }
    }

    // Buffered records over a store that does not work.
    private sealed class Broken(TimeSpan? settle = null) : BufferedFetchRecords(settle)
    {
        protected override Task<string?> ReadTextAsync() => throw new IOException("no");

        protected override Task WriteTextAsync(string text) => throw new IOException("no");
    }
}

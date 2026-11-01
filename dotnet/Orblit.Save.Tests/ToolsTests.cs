namespace Orblit.Save.Tests;

public sealed class ToolsTests : IDisposable
{
    private readonly TempDir _dir = new();
    private readonly HistoryStore _store;
    private readonly SaveTools _tools;
    private readonly HashSet<string> _live = [];
    private long _clockMs = 900000;

    public ToolsTests()
    {
        _store = HistoryStore.Files(_dir.Path);
        _tools = new SaveTools(_store, _live.Contains, () => Support.At(_clockMs++));
    }

    public void Dispose() => _dir.Dispose();

    private async Task<byte[]> BytesOfKey(string key)
    {
        var record = await _store.ReadAsync(key);
        Assert.NotNull(record);
        return record.Bytes.ToArray();
    }

    private async Task<int> Versions(string key) => (await _store.History.ListAsync(key)).Count;

    private async Task Seed(string key, byte value, long at) =>
        await _store.WriteAsync(key, Support.RecordOf([value], at: at));

    // HistoryStore

    [Fact]
    public async Task AWriteIsTheCurrentSaveAndAVersion()
    {
        await Seed("ana", 1, 1000);

        Assert.Equal(new byte[] { 1 }, await BytesOfKey("ana"));
        Assert.True(await _store.ExistsAsync("ana"));
        Assert.Equal(["ana"], await _store.KeysAsync());
        Assert.Equal(1, await Versions("ana"));
    }

    [Fact]
    public async Task AQueueWritingThroughItFillsTheHistory()
    {
        var failures = new System.Collections.Concurrent.ConcurrentQueue<string>();
        var queue = SaveQueue.Spawn(
            HistoryStore.Files(_dir.Path),
            Support.FixedClock,
            (key, error) => failures.Enqueue($"{key} failed: {error}"));
        for (byte i = 0; i < 5; i++)
        {
            queue.Save("ana", Support.Bytes(i), new Autosave());
        }

        Assert.Empty(await queue.CloseAsync());

        Assert.Empty(failures);
        Assert.Equal(new byte[] { 4 }, await BytesOfKey("ana"));
        Assert.Equal(["ana"], await _store.History.KeysAsync());
        Assert.True(await Versions("ana") >= 1);
    }

    [Fact]
    public async Task AHistoryThatFailsFailsTheWriteAfterTheSaveIsKept()
    {
        Directory.CreateDirectory(_dir.Sub("history"));
        await File.WriteAllTextAsync(_dir.Sub("history/entries"), "in the way", TestContext.Current.CancellationToken);

        await Assert.ThrowsAnyAsync<Exception>(() => _store.WriteAsync("ana", Support.RecordOf([1])));

        Assert.Equal(new byte[] { 1 }, await BytesOfKey("ana"));
    }

    // restore

    private async Task SeedRestore()
    {
        await Seed("ana", 1, 1000);
        await Seed("ana", 2, 2000);
    }

    [Fact]
    public async Task MakesAnOlderVersionTheCurrentSave()
    {
        await SeedRestore();
        await _tools.RestoreAsync("ana", Support.At(1000));

        var record = await _store.ReadAsync("ana");
        Assert.NotNull(record);
        Assert.Equal(new byte[] { 1 }, record.Bytes.ToArray());
        Assert.Equal(new Custom("restore"), record.Reason);
        Assert.Equal(900001, record.At.ToUnixTimeMilliseconds());
    }

    [Fact]
    public async Task KeepsTheSaveItReplacedAsPreRestore()
    {
        await SeedRestore();
        await _tools.RestoreAsync("ana", Support.At(1000));

        var kept = Assert.Single(await _store.History.ListAsync("ana"), v => v.Reason == new Custom("pre-restore"));
        Assert.Equal(new byte[] { 2 }, (await _store.History.LoadAsync(kept)).Bytes.ToArray());
        Assert.Equal(900000, kept.At.ToUnixTimeMilliseconds());
    }

    [Fact]
    public async Task RefusesAKeyAPlayerIsUsingAndWritesNothing()
    {
        await SeedRestore();
        _live.Add("ana");

        await Assert.ThrowsAsync<InvalidOperationException>(() => _tools.RestoreAsync("ana", Support.At(1000)));

        Assert.Equal(new byte[] { 2 }, await BytesOfKey("ana"));
        Assert.Equal(2, await Versions("ana"));
    }

    [Fact]
    public async Task RefusesATimeWithNoVersion()
    {
        await SeedRestore();

        await Assert.ThrowsAsync<InvalidOperationException>(() => _tools.RestoreAsync("ana", Support.At(1500)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _tools.RestoreAsync("bo", Support.At(1000)));

        Assert.Equal(new byte[] { 2 }, await BytesOfKey("ana"));
        Assert.Equal(2, await Versions("ana"));
    }

    [Fact]
    public async Task BringsBackASaveThatIsGone()
    {
        await SeedRestore();
        File.Delete(_dir.Sub("ana.save"));

        await _tools.RestoreAsync("ana", Support.At(2000));

        Assert.Equal(new byte[] { 2 }, await BytesOfKey("ana"));
        Assert.DoesNotContain(await _store.History.ListAsync("ana"), v => v.Reason == new Custom("pre-restore"));
    }

    // restore all

    private async Task SeedRestoreAll()
    {
        await Seed("ana", 1, 1000);
        await Seed("ana", 2, 3000);
        await Seed("bo", 5, 1000);
        await Seed("bo", 6, 3000);
        await Seed("cy", 7, 1000);
        await Seed("dee", 8, 3000);
    }

    [Fact]
    public async Task ThePlanListsWhatWouldChangeAndWritesNothing()
    {
        await SeedRestoreAll();

        var plan = await _tools.RestorePlanAsync(Support.At(2000));

        Assert.Equal(["ana", "bo"], plan.Changes.Select(e => e.Key).Order(StringComparer.Ordinal));
        Assert.All(plan.Changes, e => Assert.Equal(Support.At(1000), e.At));
        Assert.Empty(plan.Live);
        Assert.Equal(new byte[] { 2 }, await BytesOfKey("ana"));
        Assert.Equal(new byte[] { 6 }, await BytesOfKey("bo"));
        Assert.Equal(2, await Versions("ana"));
        Assert.Equal(2, await Versions("bo"));
    }

    [Fact]
    public async Task LeavesOutASaveAlreadyAtThatVersionAndOneWithNoneOldEnough()
    {
        await SeedRestoreAll();

        var plan = await _tools.RestorePlanAsync(Support.At(2000));

        Assert.DoesNotContain(plan.Changes, e => e.Key == "cy");
        Assert.DoesNotContain(plan.Changes, e => e.Key == "dee");
    }

    [Fact]
    public async Task ReportsAKeyInUseAndDoesNotPlanIt()
    {
        await SeedRestoreAll();
        _live.Add("bo");

        var plan = await _tools.RestorePlanAsync(Support.At(2000));

        Assert.Equal(["bo"], plan.Live);
        Assert.Equal(["ana"], plan.Changes.Select(e => e.Key));
    }

    [Fact]
    public async Task ApplyingAPlanWritesEveryChange()
    {
        await SeedRestoreAll();

        await _tools.ApplyRestoreAsync(await _tools.RestorePlanAsync(Support.At(2000)));

        Assert.Equal(new byte[] { 1 }, await BytesOfKey("ana"));
        Assert.Equal(new byte[] { 5 }, await BytesOfKey("bo"));
        Assert.Equal(new byte[] { 7 }, await BytesOfKey("cy"));
        Assert.Equal(new byte[] { 8 }, await BytesOfKey("dee"));
    }

    [Fact]
    public async Task AKeyThatWentLiveAfterThePlanStopsItAndStaysAsItWas()
    {
        await SeedRestoreAll();
        var plan = await _tools.RestorePlanAsync(Support.At(2000));
        _live.UnionWith(["ana", "bo"]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _tools.ApplyRestoreAsync(plan));

        Assert.Equal(new byte[] { 2 }, await BytesOfKey("ana"));
        Assert.Equal(new byte[] { 6 }, await BytesOfKey("bo"));
    }

    [Fact]
    public async Task PicksTheNewestVersionAtOrBeforeTheTime()
    {
        await SeedRestoreAll();
        await Seed("fay", 1, 1000);
        await Seed("fay", 2, 2000);
        await Seed("fay", 3, 3000);

        var plan = await _tools.RestorePlanAsync(Support.At(2000));

        var fay = Assert.Single(plan.Changes, e => e.Key == "fay");
        Assert.Equal(Support.At(2000), fay.At);
    }

    [Fact]
    public async Task ATimeBeforeEveryVersionPlansNothing()
    {
        await SeedRestoreAll();

        var plan = await _tools.RestorePlanAsync(Support.At(10));

        Assert.Empty(plan.Changes);
        Assert.Empty(plan.Live);
    }

    // edit

    [Fact]
    public async Task SavesWhatTheChangeMakesOfTheBytesForTheReason()
    {
        await _store.WriteAsync("ana", Support.RecordOf([1, 2, 3], at: 1000));

        await _tools.EditAsync("ana", bytes => [.. bytes.Select(b => (byte)(b * 2))], new Custom("gm fix"));

        var record = await _store.ReadAsync("ana");
        Assert.NotNull(record);
        Assert.Equal(new byte[] { 2, 4, 6 }, record.Bytes.ToArray());
        Assert.Equal(new Custom("gm fix"), record.Reason);
        Assert.Equal(900000, record.At.ToUnixTimeMilliseconds());
        Assert.Equal(2, await Versions("ana"));
    }

    [Fact]
    public async Task RefusesAKeyAPlayerIsUsing()
    {
        await _store.WriteAsync("ana", Support.RecordOf([1, 2, 3], at: 1000));
        _live.Add("ana");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _tools.EditAsync("ana", _ => Support.Bytes(9), new Custom("x")));

        Assert.Equal(new byte[] { 1, 2, 3 }, await BytesOfKey("ana"));
    }

    [Fact]
    public async Task RefusesAKeyWithNoSave()
    {
        await _store.WriteAsync("ana", Support.RecordOf([1, 2, 3], at: 1000));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _tools.EditAsync("bo", _ => Support.Bytes(9), new Custom("x")));
        Assert.False(await _store.ExistsAsync("bo"));
    }

    [Fact]
    public async Task AChangeThatThrowsLeavesTheSaveAsItWas()
    {
        await _store.WriteAsync("ana", Support.RecordOf([1, 2, 3], at: 1000));

        await Assert.ThrowsAsync<FormatException>(
            () => _tools.EditAsync("ana", _ => throw new FormatException("no"), new Custom("x")));

        Assert.Equal(new byte[] { 1, 2, 3 }, await BytesOfKey("ana"));
        Assert.Equal(1, await Versions("ana"));
    }
}

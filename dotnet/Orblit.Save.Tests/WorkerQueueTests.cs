using System.Collections.Concurrent;

namespace Orblit.Save.Tests;

public sealed class WorkerQueueTests : IDisposable
{
    private readonly ConcurrentQueue<string> _failures = new();
    private readonly TempDir _dir = new();
    private readonly FileSaveStore _files;

    public WorkerQueueTests() => _files = new FileSaveStore(_dir.Sub("saves"));

    public void Dispose() => _dir.Dispose();

    private SaveQueue Spawn() => SaveQueue.Spawn(_files, Support.FixedClock, (key, _) => _failures.Enqueue(key));

    private async Task<byte[]> BytesOf(string key)
    {
        var record = await _files.ReadAsync(key);
        Assert.NotNull(record);
        return record.Bytes.ToArray();
    }

    [Fact]
    public async Task WritesASaveToTheFileStore()
    {
        var queue = Spawn();
        queue.Save("ana", Support.Bytes(1, 2, 3), new Custom("first"));

        Assert.Empty(await queue.CloseAsync());

        var record = await _files.ReadAsync("ana");
        Assert.NotNull(record);
        Assert.Equal(new byte[] { 1, 2, 3 }, record.Bytes.ToArray());
        Assert.Equal(new Custom("first"), record.Reason);
        Assert.Equal(Support.FixedClock(), record.At);
        Assert.True(queue.WorkerStarts == 1, "one worker, and it ended");
    }

    [Fact]
    public async Task AQueueClosedBeforeItsWorkerIsUpStillEndsIt()
    {
        var queue = Spawn();

        Assert.Empty(await queue.CloseAsync());

        Assert.Equal(1, queue.WorkerStarts);
    }

    [Fact]
    public async Task AReadStraightAfterSaveReturnsTheNewBytes()
    {
        var queue = Spawn();
        queue.Save("ana", Support.Bytes(5), new Autosave());

        var seen = await queue.ReadAsync("ana");
        Assert.NotNull(seen);
        Assert.Equal(new byte[] { 5 }, seen.Bytes.ToArray());

        await queue.CloseAsync();
    }

    [Fact]
    public async Task TenSavesAllLandWhenTheWorkerEndsFirst()
    {
        var queue = Spawn();
        queue.KillWorker();
        for (byte i = 0; i < 10; i++)
        {
            queue.Save($"p{i}", Support.Bytes(i), new Autosave());
        }

        Assert.Empty(await queue.CloseAsync());

        for (byte i = 0; i < 10; i++)
        {
            Assert.True((await BytesOf($"p{i}")).SequenceEqual(new[] { i }), $"p{i}");
        }
    }

    [Fact]
    public async Task TenSavesAllLandWhenTheWorkerEndsPartWay()
    {
        var queue = Spawn();
        for (byte i = 0; i < 5; i++)
        {
            queue.Save($"p{i}", Support.Bytes(i), new Autosave());
        }

        await Task.Delay(50, TestContext.Current.CancellationToken);
        queue.KillWorker();
        for (byte i = 5; i < 10; i++)
        {
            queue.Save($"p{i}", Support.Bytes(i), new Autosave());
        }

        Assert.Empty(await queue.CloseAsync());

        Assert.Equal(10, (await _files.KeysAsync()).Count);
    }

    [Fact]
    public async Task AWorkerThatEndsTwiceIsStartedTwice()
    {
        var queue = Spawn();
        queue.Save("a", Support.Bytes(1), new Autosave());
        await queue.FlushAsync();

        Assert.Equal(1, queue.WorkerStarts);
        queue.KillWorker();
        await Task.Delay(100, TestContext.Current.CancellationToken);
        queue.KillWorker();
        await Task.Delay(100, TestContext.Current.CancellationToken);
        queue.Save("b", Support.Bytes(2), new Autosave());

        Assert.Empty(await queue.CloseAsync());
        Assert.Equal(["a", "b"], (await _files.KeysAsync()).Order(StringComparer.Ordinal));
        Assert.True(queue.WorkerStarts == 3, "two restarts, none after close");
    }

    [Fact]
    public async Task TheNewestOfManySavesOfOneKeyIsWhatLands()
    {
        var queue = Spawn();
        for (byte i = 0; i < 50; i++)
        {
            queue.Save("ana", Support.Bytes(i), new Autosave());
        }

        await queue.CloseAsync();

        Assert.Equal(new byte[] { 49 }, await BytesOf("ana"));
    }

    [Fact]
    public async Task AWriteThatFailsIsReportedAndKept()
    {
        var queue = SaveQueue.Spawn(
            new FileSaveStore(_dir.Sub("file-not-dir/inside")),
            Support.FixedClock,
            (key, _) => _failures.Enqueue(key));
        Directory.CreateDirectory(_dir.Path);
        await File.WriteAllTextAsync(_dir.Sub("file-not-dir"), "in the way", TestContext.Current.CancellationToken);
        queue.Save("ana", Support.Bytes(1), new Autosave());

        Assert.True((await queue.FlushAsync()).SetEquals(["ana"]));

        Assert.NotEmpty(_failures);
        var seen = await queue.ReadAsync("ana");
        Assert.NotNull(seen);
        Assert.Equal(new byte[] { 1 }, seen.Bytes.ToArray());
        await queue.CloseAsync();
    }
}

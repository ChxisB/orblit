using System.Collections.Concurrent;

namespace Orblit.Save.Tests;

/// <summary>Tests the port adds, for what its types guarantee without a fixture.</summary>
public sealed class AddedTests
{
    /// <summary>A store that notes whether a write started inside a call to <c>SaveQueue.Save</c>.</summary>
    private sealed class Probe(ConcurrentQueue<bool> inSave) : ISaveStore
    {
        public Task<SaveRecord?> ReadAsync(string key) => Task.FromResult<SaveRecord?>(null);

        public Task WriteAsync(string key, SaveRecord record)
        {
            inSave.Enqueue(Environment.StackTrace.Contains("SaveQueue.Save(", StringComparison.Ordinal));
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(string key) => Task.FromResult(false);

        public Task<IReadOnlyList<string>> KeysAsync() => Task.FromResult<IReadOnlyList<string>>([]);
    }

    [Fact]
    public async Task TheInlineQueueWritesOffTheCallersStack()
    {
        var inSave = new ConcurrentQueue<bool>();
        var queue = SaveQueue.Inline(new Probe(inSave), Support.FixedClock, (_, _) => { });

        queue.Save("ana", Support.Bytes(1), new Autosave());
        await queue.FlushAsync();

        Assert.Equal([false], inSave);
    }

    [Fact]
    public async Task ClosingTwiceReturnsAtOnce()
    {
        var queue = SaveQueue.Spawn(new MemorySaveStore(), Support.FixedClock, (_, _) => { });
        queue.Save("ana", Support.Bytes(1), new Autosave());

        Assert.Empty(await queue.CloseAsync());
        Assert.Empty(await queue.CloseAsync());
    }

    [Fact]
    public void ARecordCopiesItsBytesAndKeepsMilliseconds()
    {
        var bytes = Support.Bytes(1, 2);
        var at = new DateTimeOffset(2026, 10, 20, 1, 2, 3, TimeSpan.FromHours(2)).AddTicks(7);

        var record = new SaveRecord(bytes, new Autosave(), at);
        bytes[0] = 9;

        Assert.Equal(new byte[] { 1, 2 }, record.Bytes.ToArray());
        Assert.Equal(at.ToUnixTimeMilliseconds(), record.At.ToUnixTimeMilliseconds());
        Assert.Equal(TimeSpan.Zero, record.At.Offset);
        Assert.Equal(0, record.At.Ticks % TimeSpan.TicksPerMillisecond);
    }

    [Fact]
    public async Task AKeyWithATrailingNewLineIsRefused()
    {
        var store = new MemorySaveStore();

        await Assert.ThrowsAnyAsync<ArgumentException>(() => store.WriteAsync("ana\n", Support.RecordOf([1])));
    }

    [Fact]
    public async Task TheToolsRefuseAKeyThatIsNotASaveKey()
    {
        var tools = new SaveTools(HistoryStore.Files(Path.Combine(Path.GetTempPath(), "orblit_save_unused")), _ => false, Support.FixedClock);

        await Assert.ThrowsAnyAsync<ArgumentException>(() => tools.RestoreAsync("../x", Support.At(1000)));
    }
}

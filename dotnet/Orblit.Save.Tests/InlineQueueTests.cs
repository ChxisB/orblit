using System.Collections.Concurrent;

namespace Orblit.Save.Tests;

public sealed class InlineQueueTests
{
    private readonly ConcurrentQueue<string> _failures = new();

    private SaveQueue Inline(ISaveStore store) =>
        SaveQueue.Inline(store, Support.FixedClock, (key, _) => _failures.Enqueue(key));

    [Fact]
    public async Task AReadStraightAfterSaveReturnsTheNewBytes()
    {
        var store = new Gated();
        var queue = Inline(store);
        queue.Save("ana", Support.Bytes(1), new Autosave());
        var done = queue.FlushAsync();

        queue.Save("ana", Support.Bytes(2), new Leave());
        var seen = await queue.ReadAsync("ana");

        Assert.NotNull(seen);
        Assert.Equal(new byte[] { 2 }, seen.Bytes.ToArray());
        Assert.Equal(new Leave(), seen.Reason);
        Assert.Null(await store.Inner.ReadAsync("ana"));
        Assert.True(await queue.ExistsAsync("ana"));

        store.Open();
        await done;
        await queue.FlushAsync();
        var written = await store.Inner.ReadAsync("ana");
        Assert.NotNull(written);
        Assert.Equal(new byte[] { 2 }, written.Bytes.ToArray());
    }

    [Fact]
    public async Task AnAnswerForAnOlderSaveDoesNotHideANewerOne()
    {
        var store = new Gated();
        var queue = Inline(store);
        queue.Save("ana", Support.Bytes(1), new Autosave());
        queue.Save("ana", Support.Bytes(2), new Autosave());

        await Support.Until(() => store.GateCount == 1, "the first write to start");
        store.ReleaseFirst();
        await Support.Until(() => queue.Pending == 1, "the first answer");

        var first = await store.Inner.ReadAsync("ana");
        Assert.NotNull(first);
        Assert.Equal(new byte[] { 1 }, first.Bytes.ToArray());
        var seen = await queue.ReadAsync("ana");
        Assert.NotNull(seen);
        Assert.Equal(new byte[] { 2 }, seen.Bytes.ToArray());
        Assert.Equal(1, queue.Pending);

        store.Open();
        await queue.FlushAsync();
        var last = await store.Inner.ReadAsync("ana");
        Assert.NotNull(last);
        Assert.Equal(new byte[] { 2 }, last.Bytes.ToArray());
    }

    [Fact]
    public async Task AFlushDoesNotWriteASaveThatIsAlreadyOnItsWay()
    {
        var store = new Gated();
        var queue = Inline(store);
        queue.Save("ana", Support.Bytes(1), new Autosave());
        var done = queue.FlushAsync();
        await Support.Until(() => store.GateCount == 1, "the write to start");

        store.Open();
        await done;
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Equal(1, store.Written);
    }

    [Fact]
    public async Task WritesHappenOneAtATimeInTheOrderTheyWereSaved()
    {
        var store = new Gated();
        var queue = Inline(store);
        queue.Save("a", Support.Bytes(1), new Autosave());
        queue.Save("b", Support.Bytes(2), new Autosave());
        await Support.Until(() => store.GateCount == 1, "the first write to start");
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.True(store.GateCount == 1, "b waits for a");

        store.ReleaseFirst();
        await Support.Until(() => store.GateCount == 2, "b to start");

        Assert.True(store.GateCount == 2, "b starts once a is done");
        Assert.Equal(1, store.Written);

        store.Open();
        await queue.FlushAsync();
    }

    [Fact]
    public async Task TheBytesAreCopiedWhenSaved()
    {
        var queue = Inline(new MemorySaveStore());
        var bytes = Support.Bytes(1, 2);
        queue.Save("ana", bytes, new Autosave());
        bytes[0] = 99;

        await queue.FlushAsync();

        var seen = await queue.ReadAsync("ana");
        Assert.NotNull(seen);
        Assert.Equal(new byte[] { 1, 2 }, seen.Bytes.ToArray());
    }

    [Fact]
    public async Task TheRecordCarriesTheClockAndTheReason()
    {
        var store = new MemorySaveStore();
        var queue = Inline(store);
        queue.Save("ana", Support.Bytes(1), new Shutdown());
        await queue.FlushAsync();

        var record = await store.ReadAsync("ana");

        Assert.NotNull(record);
        Assert.Equal(Support.FixedClock(), record.At);
        Assert.Equal(new Shutdown(), record.Reason);
    }

    [Fact]
    public async Task TheNewestOfTwoSavesIsWhatLands()
    {
        var store = new Gated();
        var queue = Inline(store);
        queue.Save("ana", Support.Bytes(1), new Autosave());
        queue.Save("ana", Support.Bytes(2), new Autosave());
        Assert.Equal(2, queue.Pending);

        store.Open();
        Assert.Empty(await queue.FlushAsync());

        Assert.Equal(0, queue.Pending);
        var record = await store.Inner.ReadAsync("ana");
        Assert.NotNull(record);
        Assert.Equal(new byte[] { 2 }, record.Bytes.ToArray());
    }

    [Fact]
    public async Task AFailedWriteIsReportedKeptAndTriedAgainByFlush()
    {
        var store = new Gated { Fail = true };
        store.Open();
        var queue = Inline(store);
        queue.Save("ana", Support.Bytes(1), new Autosave());

        Assert.True((await queue.FlushAsync()).SetEquals(["ana"]));
        Assert.Equal(["ana"], _failures);
        var seen = await queue.ReadAsync("ana");
        Assert.NotNull(seen);
        Assert.Equal(new byte[] { 1 }, seen.Bytes.ToArray());
        Assert.False(await store.Inner.ExistsAsync("ana"));

        Assert.True((await queue.FlushAsync()).SetEquals(["ana"]), "it tried again");
        Assert.Equal(["ana", "ana"], _failures);

        store.Fail = false;
        Assert.Empty(await queue.FlushAsync());
        var record = await store.Inner.ReadAsync("ana");
        Assert.NotNull(record);
        Assert.Equal(new byte[] { 1 }, record.Bytes.ToArray());
    }

    [Fact]
    public async Task ANewerSaveThatWorksReplacesAnOlderOneThatFailed()
    {
        var store = new Gated { Fail = true };
        store.Open();
        var queue = Inline(store);
        queue.Save("ana", Support.Bytes(1), new Autosave());
        await queue.FlushAsync();

        store.Fail = false;
        queue.Save("ana", Support.Bytes(2), new Autosave());

        Assert.Empty(await queue.FlushAsync());
        var record = await store.Inner.ReadAsync("ana");
        Assert.NotNull(record);
        Assert.Equal(new byte[] { 2 }, record.Bytes.ToArray());
    }

    [Fact]
    public void AKeyThatIsNotAPlainNameIsRefusedAtSave()
    {
        var queue = Inline(new MemorySaveStore());

        Assert.ThrowsAny<ArgumentException>(() => queue.Save("../x", Support.Bytes(1), new Autosave()));
        Assert.Equal(0, queue.Pending);
    }

    [Fact]
    public async Task FlushWithNothingQueuedReturnsAtOnce()
    {
        var queue = Inline(new MemorySaveStore());

        Assert.Empty(await queue.FlushAsync());
    }

    [Fact]
    public async Task ReadsFallThroughToTheStoreForAKeyNeverQueued()
    {
        var store = new MemorySaveStore();
        await store.WriteAsync("ana", Support.RecordOf([7]));
        var queue = Inline(store);

        var seen = await queue.ReadAsync("ana");
        Assert.NotNull(seen);
        Assert.Equal(new byte[] { 7 }, seen.Bytes.ToArray());
        Assert.True(await queue.ExistsAsync("ana"));
        Assert.Null(await queue.ReadAsync("bo"));
        Assert.False(await queue.ExistsAsync("bo"));
    }

    [Fact]
    public async Task ListsTheKeysOnTheStoreAndTheOnesStillHeld()
    {
        var store = new Gated();
        await store.Inner.WriteAsync("ana", Support.RecordOf([7]));
        var queue = Inline(store);
        queue.Save("bo", Support.Bytes(1), new Autosave());
        try
        {
            Assert.True((await queue.KeysAsync()).SetEquals(["ana", "bo"]));
        }
        finally
        {
            store.Open();
            await queue.CloseAsync();
        }
    }
}

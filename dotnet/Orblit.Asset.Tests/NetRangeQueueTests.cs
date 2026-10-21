namespace Orblit.Asset.Tests;

public class NetRangeQueueTests
{
    // A file whose byte at offset n is n & 0xff, so a reply can be checked against where it came from.
    private static byte[] BytesAt(long at, long bytes) => [.. Enumerable.Range(0, (int)bytes).Select(i => (byte)(at + i))];

    // Answers every range at once, or holds each until a test lets it go.
    private sealed class Host(bool hold = false)
    {
        private readonly List<(long At, long Bytes)> _calls = [];
        private readonly List<TaskCompletionSource> _gates = [];

        public (long At, long Bytes)[] Calls
        {
            get
            {
                lock (_calls)
                {
                    return [.. _calls];
                }
            }
        }

        public long TotalBytes => Calls.Sum(call => call.Bytes);

        public async Task<byte[]> Fetch(long at, long bytes)
        {
            Task? gate = null;
            lock (_calls)
            {
                _calls.Add((at, bytes));
                if (hold)
                {
                    var open = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    _gates.Add(open);
                    gate = open.Task;
                }
            }

            if (gate is not null)
            {
                await gate;
            }

            return BytesAt(at, bytes);
        }

        // Lets the nth request (from nought) be answered.
        public void Release(int n)
        {
            lock (_calls)
            {
                _gates[n].SetResult();
            }
        }

        public Task AwaitCalls(int count) => Waits.Until(() => Calls.Length >= count, $"{count} requests");
    }

    // A real delay long enough for the wants of one loop to join, since .NET has no "after this turn".
    private static RangeQueue QueueOver(Host host, long mergeGap = 16, long maxBatch = 1 << 20, int parallel = 4) =>
        new(
            host.Fetch,
            new RangeBatching
            {
                MergeGap = mergeGap,
                MaxBatch = maxBatch,
                Parallel = parallel,
                Delay = TimeSpan.FromMilliseconds(50),
            });

    private static Task<byte[]>[] Want(RangeQueue queue, int count, Func<int, (long At, long Bytes)> range) =>
        [.. Enumerable.Range(0, count).Select(i => queue.WantAsync(range(i).At, range(i).Bytes))];

    [Fact]
    public async Task SendsFortyAdjacentBlobsAsOneRequest()
    {
        var host = new Host();
        var queue = QueueOver(host);

        var replies = await Task.WhenAll(Want(queue, 40, i => (100 + (i * 50), 50)));

        Assert.Equal([(100L, 2000L)], host.Calls);
        for (var i = 0; i < 40; i++)
        {
            Assert.Equal(BytesAt(100 + (i * 50), 50), replies[i]);
        }
    }

    [Fact]
    public async Task JoinsRangesCloserThanTheGapAndFetchesTheGapToo()
    {
        var host = new Host();
        var queue = QueueOver(host, mergeGap: 16);

        var replies = await Task.WhenAll(queue.WantAsync(0, 100), queue.WantAsync(116, 100));

        Assert.Equal([(0L, 216L)], host.Calls);
        Assert.Equal(100 + 100 + 16, host.TotalBytes);
        Assert.Equal(BytesAt(0, 100), replies[0]);
        Assert.Equal(BytesAt(116, 100), replies[1]);
    }

    [Fact]
    public async Task KeepsRangesFurtherApartThanTheGapSeparate()
    {
        var host = new Host();
        var queue = QueueOver(host, mergeGap: 16);

        await Task.WhenAll(queue.WantAsync(0, 100), queue.WantAsync(117, 100));

        Assert.Equal([(0L, 100L), (117L, 100L)], host.Calls);
    }

    [Fact]
    public async Task SendsOverlappingRangesAsOneRequest()
    {
        var host = new Host();
        var queue = QueueOver(host, mergeGap: 0);

        var replies = await Task.WhenAll(queue.WantAsync(0, 100), queue.WantAsync(50, 100), queue.WantAsync(60, 10));

        Assert.Equal([(0L, 150L)], host.Calls);
        Assert.Equal(BytesAt(0, 100), replies[0]);
        Assert.Equal(BytesAt(50, 100), replies[1]);
        Assert.Equal(BytesAt(60, 10), replies[2]);
    }

    [Fact]
    public async Task MeasuresTheGapFromTheFurthestEndSoFar()
    {
        var host = new Host();
        var queue = QueueOver(host, mergeGap: 5);

        await Task.WhenAll(queue.WantAsync(0, 100), queue.WantAsync(60, 10), queue.WantAsync(104, 10));

        Assert.Equal([(0L, 114L)], host.Calls);
    }

    [Fact]
    public async Task KeepsTwoWantsFromTheSamePlaceApartWhenTheyDiffer()
    {
        var host = new Host();
        var queue = QueueOver(host);

        var replies = await Task.WhenAll(queue.WantAsync(0, 10), queue.WantAsync(0, 20));

        Assert.Equal(BytesAt(0, 10), replies[0]);
        Assert.Equal(BytesAt(0, 20), replies[1]);
    }

    [Fact]
    public async Task NeverSendsARequestOverTheBatchSize()
    {
        var host = new Host();
        var queue = QueueOver(host, maxBatch: 1000);

        var replies = await Task.WhenAll(Want(queue, 40, i => (i * 100, 100)));

        Assert.Equal(4, host.Calls.Length);
        Assert.All(host.Calls, call => Assert.True(call.Bytes <= 1000));
        Assert.Equal(4000, host.TotalBytes);
        for (var i = 0; i < 40; i++)
        {
            Assert.Equal(BytesAt(i * 100, 100), replies[i]);
        }
    }

    [Fact]
    public async Task SendsOneBlobOverTheBatchSizeWhole()
    {
        var host = new Host();
        var queue = QueueOver(host, maxBatch: 1000);

        var reply = await queue.WantAsync(0, 5000);

        Assert.Equal([(0L, 5000L)], host.Calls);
        Assert.Equal(BytesAt(0, 5000), reply);
    }

    [Fact]
    public async Task StartsANewRequestForAWantThatComesAfterTheWindow()
    {
        var host = new Host();
        var queue = QueueOver(host);

        await queue.WantAsync(0, 10);
        await queue.WantAsync(10, 10);

        Assert.Equal([(0L, 10L), (10L, 10L)], host.Calls);
    }

    [Fact]
    public async Task HandsEachAskerItsOwnCopyOfTheBytes()
    {
        var host = new Host();
        var queue = QueueOver(host, mergeGap: 0);
        var replies = await Task.WhenAll(queue.WantAsync(0, 100), queue.WantAsync(50, 100));

        replies[0][60] = 0xAA;

        Assert.Equal(60, replies[1][10]);
    }

    [Fact]
    public async Task HandsTwoAskersForTheSameBytesSeparateCopies()
    {
        var host = new Host();
        var queue = QueueOver(host);

        var replies = await Task.WhenAll(queue.WantAsync(0, 10), queue.WantAsync(0, 10));

        Assert.Single(host.Calls);
        replies[0][0] = 0xAA;
        Assert.Equal(0, replies[1][0]);
    }

    [Fact]
    public async Task KeepsOneRequestFreeForAnUrgentWant()
    {
        var host = new Host(hold: true);
        var queue = QueueOver(host, mergeGap: 0, parallel: 2);
        var lower = new[]
        {
            queue.WantAsync(0, 10, RangePriority.Later),
            queue.WantAsync(1000, 10, RangePriority.Later),
        };
        await host.AwaitCalls(1);
        await Waits.Settle();
        Assert.Equal([(0L, 10L)], host.Calls);

        var urgent = queue.WantAsync(5000, 10);
        await host.AwaitCalls(2);
        Assert.Equal((5000L, 10L), host.Calls[^1]);

        host.Release(0);
        host.Release(1);
        await host.AwaitCalls(3);
        host.Release(2);
        await Task.WhenAll([.. lower, urgent]);
        Assert.Equal(3, host.Calls.Length);
    }

    [Fact]
    public async Task LetsAWantThatCanWaitUseTheOnlyRequestThereIs()
    {
        var host = new Host();
        var queue = QueueOver(host, parallel: 1);

        var reply = await queue.WantAsync(0, 10, RangePriority.Later);

        Assert.Equal(BytesAt(0, 10), reply);
    }

    [Fact]
    public async Task RunsNoMoreRequestsAtOnceThanParallelAllows()
    {
        var host = new Host(hold: true);
        var queue = QueueOver(host, mergeGap: 0, parallel: 2);
        var replies = Want(queue, 4, i => (i * 1000, 10));

        await host.AwaitCalls(2);
        await Waits.Settle();
        Assert.Equal(2, host.Calls.Length);

        host.Release(0);
        await host.AwaitCalls(3);
        await Waits.Settle();
        Assert.Equal(3, host.Calls.Length);

        host.Release(1);
        host.Release(2);
        await host.AwaitCalls(4);
        host.Release(3);
        Assert.Equal(4, (await Task.WhenAll(replies)).Length);
    }

    [Fact]
    public async Task AnswersAnUrgentWantBeforeTheOnesThatCanWait()
    {
        var host = new Host(hold: true);
        var queue = QueueOver(host, mergeGap: 0, parallel: 1);
        var first = queue.WantAsync(0, 10);
        await host.AwaitCalls(1);
        var replies = new[]
        {
            queue.WantAsync(1000, 10, RangePriority.Later),
            queue.WantAsync(2000, 10, RangePriority.Later),
            queue.WantAsync(5000, 10),
        };
        await Waits.Settle();
        Assert.Equal([(0L, 10L)], host.Calls);

        host.Release(0);
        await host.AwaitCalls(2);
        Assert.Equal((5000L, 10L), host.Calls[^1]);
        host.Release(1);
        await host.AwaitCalls(3);
        host.Release(2);
        await host.AwaitCalls(4);
        host.Release(3);

        await Task.WhenAll([first, .. replies]);
        Assert.Equal([0L, 5000L, 1000L, 2000L], host.Calls.Select(call => call.At));
    }

    [Fact]
    public async Task RunsAWantThatCanWaitWhenNothingUrgentDoes()
    {
        var host = new Host();
        var queue = QueueOver(host);

        var reply = await queue.WantAsync(40, 10, RangePriority.Later);

        Assert.Equal(BytesAt(40, 10), reply);
        Assert.Equal([(40L, 10L)], host.Calls);
    }

    [Fact]
    public async Task MovesAWantUpWhenSomeoneThenNeedsTheSameBytes()
    {
        var host = new Host(hold: true);
        var queue = QueueOver(host, mergeGap: 0, parallel: 1);
        var first = queue.WantAsync(0, 10);
        await host.AwaitCalls(1);
        var lower = queue.WantAsync(1000, 10, RangePriority.Later);
        var later = queue.WantAsync(2000, 10, RangePriority.Later);
        var promoted = queue.WantAsync(2000, 10);
        await Waits.Settle();

        host.Release(0);
        await host.AwaitCalls(2);
        Assert.Equal((2000L, 10L), host.Calls[^1]);
        host.Release(1);
        await host.AwaitCalls(3);
        host.Release(2);

        Assert.Equal(BytesAt(2000, 10), await promoted);
        Assert.Equal(BytesAt(2000, 10), await later);
        await Task.WhenAll(first, lower);
        Assert.Equal(3, host.Calls.Length);
    }

    [Fact]
    public async Task KeepsASecondAskAtLowPriorityFromJumpingTheQueue()
    {
        var host = new Host(hold: true);
        var queue = QueueOver(host, mergeGap: 0, parallel: 1);
        var first = queue.WantAsync(0, 10);
        await host.AwaitCalls(1);
        var urgent = queue.WantAsync(3000, 10);
        var lower = queue.WantAsync(1000, 10, RangePriority.Later);
        var again = queue.WantAsync(1000, 10, RangePriority.Later);
        await Waits.Settle();

        host.Release(0);
        await host.AwaitCalls(2);
        host.Release(1);
        await host.AwaitCalls(3);
        host.Release(2);

        await Task.WhenAll(first, urgent, lower, again);
        Assert.Equal([0L, 3000L, 1000L], host.Calls.Select(call => call.At));
    }

    [Fact]
    public async Task FailsEveryWantInARequestThatFailedAndThenRecovers()
    {
        var fail = true;
        var calls = new List<long>();
        var queue = new RangeQueue(
            (at, bytes) =>
            {
                calls.Add(at);
                return fail
                    ? Task.FromException<byte[]>(new FormatException("the connection was lost"))
                    : Task.FromResult(BytesAt(at, bytes));
            },
            new RangeBatching { Delay = TimeSpan.FromMilliseconds(50), Parallel = 1 });

        var failed = new[] { queue.WantAsync(0, 10), queue.WantAsync(10, 10) };
        foreach (var want in failed)
        {
            await Assert.ThrowsAsync<FormatException>(() => want);
        }

        Assert.Equal([0L], calls);

        fail = false;
        Assert.Equal(BytesAt(20, 10), await queue.WantAsync(20, 10));
    }

    [Fact]
    public async Task FailsAWantWhenTheReplyIsShortRatherThanHanging()
    {
        var queue = new RangeQueue(
            (at, bytes) => Task.FromResult(new byte[bytes - 1]),
            new RangeBatching { Delay = TimeSpan.FromMilliseconds(50) });

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => queue.WantAsync(0, 10));
    }

    [Fact]
    public void RefusesSettingsThatCannotWork()
    {
        static RangeQueue Make(RangeBatching batching) => new((at, bytes) => Task.FromResult(new byte[bytes]), batching);

        Assert.ThrowsAny<ArgumentException>(() => Make(new RangeBatching { MergeGap = -1 }));
        Assert.ThrowsAny<ArgumentException>(() => Make(new RangeBatching { MaxBatch = 0 }));
        Assert.ThrowsAny<ArgumentException>(() => Make(new RangeBatching { Parallel = 0 }));
        Assert.ThrowsAny<ArgumentException>(() => Make(new RangeBatching { Delay = TimeSpan.FromMilliseconds(-1) }));
        Assert.NotNull(Make(new RangeBatching { MergeGap = 0 }));
        Assert.NotNull(Make(new RangeBatching { MaxBatch = 1 }));
    }

    [Fact]
    public async Task FailsARangeThatCannotExist()
    {
        var queue = QueueOver(new Host());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => queue.WantAsync(-1, 10));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => queue.WantAsync(0, 0));
    }
}

namespace Orblit.Asset;

/// <summary>Asks for byte ranges of one file through a fetch function, as fewer and larger requests.</summary>
/// <remarks>
/// A want waits for <see cref="RangeBatching.Delay"/>, so wants made close together can join. Wants whose gap
/// is within <see cref="RangeBatching.MergeGap"/> are sent as one request, cut at
/// <see cref="RangeBatching.MaxBatch"/>. At most <see cref="RangeBatching.Parallel"/> requests are out. The
/// most urgent wait is sent first, and a want that can wait is sent only when no urgent one is queued, and
/// never in the last free request.
/// <para>
/// A want for the same bytes as one still queued is the same want. Its askers share the answer, and it takes
/// the more urgent of the two priorities. One already sent is not looked for, so the same bytes can be
/// fetched twice.
/// </para>
/// <para>
/// shortcut: a want inside a request that went out for another is not answered from it, and the bytes between
/// two joined wants are thrown away once the wants are answered. Upgrade to a small cache of recent ranges
/// when a pack is read with a lot of overlap.
/// </para>
/// <para>
/// shortcut: a request starts at the lowest offset of its priority, so under a steady stream of low offsets a
/// want at a high one waits. Wants of the two priorities are never joined. The queue is scanned in full for
/// each want and each request, which is fine for hundreds of wants at once. Upgrade to starting from the
/// oldest want, and to an ordered structure, when a pack is read that hard.
/// </para>
/// </remarks>
internal sealed class RangeQueue
{
    private readonly Func<long, long, Task<byte[]>> _fetch;
    private readonly object _gate = new();
    private readonly List<Want> _waiting = [];
    private bool _armed;
    private int _running;

    public RangeQueue(Func<long, long, Task<byte[]>> fetch, RangeBatching? batching = null)
    {
        Batching = batching ?? new RangeBatching();
        ArgumentOutOfRangeException.ThrowIfNegative(Batching.MergeGap, nameof(Batching.MergeGap));
        ArgumentOutOfRangeException.ThrowIfLessThan(Batching.MaxBatch, 1, nameof(Batching.MaxBatch));
        ArgumentOutOfRangeException.ThrowIfLessThan(Batching.Parallel, 1, nameof(Batching.Parallel));
        ArgumentOutOfRangeException.ThrowIfLessThan(Batching.Delay, TimeSpan.Zero, nameof(Batching.Delay));
        _fetch = fetch;
    }

    public RangeBatching Batching { get; }

    /// <summary>
    /// <paramref name="bytes"/> bytes from <paramref name="at"/>. Fails with an
    /// <see cref="ArgumentOutOfRangeException"/> for a range that cannot exist.
    /// </summary>
    public Task<byte[]> WantAsync(long at, long bytes, RangePriority priority = RangePriority.Now)
    {
        if (at < 0 || bytes < 1)
        {
            return Task.FromException<byte[]>(new ArgumentOutOfRangeException(null, $"Cannot want {bytes} bytes at {at}."));
        }

        Task<byte[]> reply;
        bool arm;
        lock (_gate)
        {
            if (FindQueued(at, bytes) is { } queued)
            {
                if (priority == RangePriority.Now)
                {
                    queued.Priority = priority;
                }

                return CopyOfAsync(queued.Reply.Task);
            }

            var want = new Want(at, bytes, priority);
            _waiting.Add(want);
            reply = want.Reply.Task;
            arm = !_armed;
            _armed = true;
        }

        if (arm)
        {
            _ = FireAfterDelayAsync();
        }

        return reply;
    }

    private static async Task<byte[]> CopyOfAsync(Task<byte[]> reply) => [.. await reply.ConfigureAwait(false)];

    // Caller holds the gate.
    private Want? FindQueued(long at, long bytes) => _waiting.Find(want => want.At == at && want.Bytes == bytes);

    private async Task FireAfterDelayAsync()
    {
        await Task.Delay(Batching.Delay).ConfigureAwait(false);
        lock (_gate)
        {
            _armed = false;
        }

        Pump();
    }

    // Requests are started under the gate, so they go out in the order they were taken off the queue.
    private void Pump()
    {
        lock (_gate)
        {
            while (_waiting.Count > 0)
            {
                var priority = _waiting.Exists(want => want.Priority == RangePriority.Now) ? RangePriority.Now : RangePriority.Later;
                if (_running >= SlotsFor(priority))
                {
                    return;
                }

                var batch = TakeBatch(priority);
                _running++;
                _ = AnswerAsync(batch);
            }
        }
    }

    // A want that can wait never takes the last free request, so a want made urgent later has somewhere to
    // go. With one request allowed there is nothing to keep back.
    private int SlotsFor(RangePriority priority) =>
        priority == RangePriority.Now ? Batching.Parallel : Math.Max(1, Batching.Parallel - 1);

    // The next wants of the priority to send as one request, taken off the queue. Caller holds the gate.
    private List<Want> TakeBatch(RangePriority priority)
    {
        var pool = _waiting.Where(want => want.Priority == priority).OrderBy(want => want.At).ToList();
        var batch = new List<Want> { pool[0] };
        var end = pool[0].End;
        foreach (var want in pool.Skip(1))
        {
            if (want.At - end > Batching.MergeGap)
            {
                break;
            }

            var grown = Math.Max(end, want.End);
            if (grown - batch[0].At > Batching.MaxBatch)
            {
                break;
            }

            batch.Add(want);
            end = grown;
        }

        _waiting.RemoveAll(batch.Contains);
        return batch;
    }

    private async Task AnswerAsync(List<Want> batch)
    {
        var at = batch[0].At;
        var end = batch.Max(want => want.End);
        var pending = StartFetch(at, end - at);

        // A reply that is already there must not run the next pump from inside this one.
        await Task.Yield();
        try
        {
            var got = await pending.ConfigureAwait(false);
            var parts = batch.Select(want => Slice(got, want.At - at, want.End - at)).ToList();
            for (var i = 0; i < batch.Count; i++)
            {
                batch[i].Reply.TrySetResult(parts[i]);
            }
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            foreach (var want in batch)
            {
                want.Reply.TrySetException(error);
            }
        }
        finally
        {
            lock (_gate)
            {
                _running--;
            }

            Pump();
        }
    }

    // A fetch that throws before it returns fails the wants the same way as one that fails later.
    private Task<byte[]> StartFetch(long at, long bytes)
    {
        try
        {
            return _fetch(at, bytes);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            return Task.FromException<byte[]>(error);
        }
    }

    private static byte[] Slice(byte[] got, long start, long end) =>
        end > got.Length
            ? throw new ArgumentOutOfRangeException(null, $"The host sent {got.Length} bytes where the range needed {end}.")
            : got.AsSpan((int)start, (int)(end - start)).ToArray();

    private sealed class Want(long at, long bytes, RangePriority priority)
    {
        public long At { get; } = at;

        public long Bytes { get; } = bytes;

        public RangePriority Priority { get; set; } = priority;

        public TaskCompletionSource<byte[]> Reply { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public long End => At + Bytes;
    }
}

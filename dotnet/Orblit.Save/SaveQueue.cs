namespace Orblit.Save;

/// <summary>
/// Takes saves from the tick and writes them to an <see cref="ISaveStore"/> somewhere else.
/// <see cref="Save"/> returns at once. A read sees the newest save before it reaches the
/// store. A write that fails is reported to the caller's <c>onFailure</c> and the save is
/// kept in memory, so reads stay right and <see cref="FlushAsync"/> tries again.
/// </summary>
public sealed class SaveQueue
{
    private readonly Lock _gate = new();
    private readonly ISaveStore _store;
    private readonly Func<DateTimeOffset> _clock;
    private readonly Action<string, Exception> _onFailure;
    private readonly ISaveSink _sink;

    /// <summary>Saves sent and not answered, oldest first.</summary>
    private readonly OrderedDictionary<long, QueueEntry> _open = [];

    /// <summary>The newest save of each key that the store may not hold yet: sent, or failed.</summary>
    private readonly Dictionary<string, QueueEntry> _held = new(StringComparer.Ordinal);

    private TaskCompletionSource? _idle;
    private long _nextId;

    private SaveQueue(
        ISaveStore store,
        Func<DateTimeOffset> clock,
        Action<string, Exception> onFailure,
        Func<SaveQueue, ISaveSink> sinkFor)
    {
        _store = store;
        _clock = clock;
        _onFailure = onFailure;
        _sink = sinkFor(this);
    }

    /// <summary>How many saves are waiting for the store.</summary>
    public int Pending
    {
        get
        {
            lock (_gate)
            {
                return _open.Count;
            }
        }
    }

    /// <summary>How many worker tasks have been started. For tests.</summary>
    public int WorkerStarts => _sink.WorkerStarts;

    /// <summary>
    /// A queue whose writes happen on a worker task. A worker that ends early starts again
    /// and writes the saves that had no answer. <paramref name="onFailure"/> runs on the worker
    /// with the queue's lock held, so it must be short and must not wait on another thread.
    /// </summary>
    public static SaveQueue Spawn(ISaveStore store, Func<DateTimeOffset> clock, Action<string, Exception> onFailure) =>
        new(store, clock, onFailure, queue => new WorkerSink(queue._gate, store, queue.Answer, queue.OpenEntries));

    /// <summary>
    /// A queue whose writes happen one at a time on the thread pool, with no worker to lose.
    /// For tests and for a store that does no heavy work.
    /// </summary>
    public static SaveQueue Inline(ISaveStore store, Func<DateTimeOffset> clock, Action<string, Exception> onFailure) =>
        new(store, clock, onFailure, queue => new InlineSink(store, queue.Answer));

    /// <summary>Queues <paramref name="bytes"/> under <paramref name="key"/>. The bytes are copied.</summary>
    public void Save(string key, ReadOnlySpan<byte> bytes, SaveReason reason)
    {
        SaveKey.Check(key);
        var record = new SaveRecord(bytes, reason, _clock());
        lock (_gate)
        {
            Send(new QueueEntry(_nextId++, key, record));
        }
    }

    /// <summary>The newest save under <paramref name="key"/>, including one not yet written.</summary>
    public async Task<SaveRecord?> ReadAsync(string key)
    {
        lock (_gate)
        {
            if (_held.TryGetValue(key, out var held))
            {
                return held.Record;
            }
        }

        return await _store.ReadAsync(key).ConfigureAwait(false);
    }

    /// <summary>Whether <paramref name="key"/> has a save, including one not yet written.</summary>
    public async Task<bool> ExistsAsync(string key)
    {
        lock (_gate)
        {
            if (_held.ContainsKey(key))
            {
                return true;
            }
        }

        return await _store.ExistsAsync(key).ConfigureAwait(false);
    }

    /// <summary>Every key with a save, including one not yet written.</summary>
    public async Task<IReadOnlySet<string>> KeysAsync()
    {
        HashSet<string> keys;
        lock (_gate)
        {
            keys = new HashSet<string>(_held.Keys, StringComparer.Ordinal);
        }

        keys.UnionWith(await _store.KeysAsync().ConfigureAwait(false));
        return keys;
    }

    /// <summary>
    /// Writes again every save that failed, waits until none is waiting, and returns the
    /// keys that are still not on the store.
    /// </summary>
    public async Task<IReadOnlySet<string>> FlushAsync()
    {
        Task? wait = null;
        lock (_gate)
        {
            foreach (var entry in _held.Values.ToList().Where(e => !_open.ContainsKey(e.Id)))
            {
                Send(entry);
            }

            if (_open.Count > 0)
            {
                wait = (_idle ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).Task;
            }
        }

        if (wait is not null)
        {
            await wait.ConfigureAwait(false);
        }

        lock (_gate)
        {
            return new HashSet<string>(_held.Keys, StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// Flushes, then stops the worker and waits for it to end. The queue is not used after
    /// this.
    /// </summary>
    public async Task<IReadOnlySet<string>> CloseAsync()
    {
        var stuck = await FlushAsync().ConfigureAwait(false);
        await _sink.CloseAsync().ConfigureAwait(false);
        return stuck;
    }

    /// <summary>Ends the worker task as a crash would, between writes. For tests.</summary>
    public void KillWorker() => _sink.Kill();

    // Called with the lock held.
    private void Send(QueueEntry entry)
    {
        _open[entry.Id] = entry;
        _held[entry.Key] = entry;
        _sink.Send(entry);
    }

    // Called with the lock held, by the worker that restarts.
    private List<QueueEntry> OpenEntries() => [.. _open.Values];

    private void Answer(long id, Exception? error)
    {
        lock (_gate)
        {
            if (!_open.Remove(id, out var entry))
            {
                return; // answered twice after a restart
            }

            if (error is not null)
            {
                // Before the idle signal, so a flush that returns has seen the failure.
                _onFailure(entry.Key, error);
            }
            else if (_held.TryGetValue(entry.Key, out var held) && held.Id <= id)
            {
                _held.Remove(entry.Key);
            }

            if (_open.Count == 0)
            {
                _idle?.TrySetResult();
                _idle = null;
            }
        }
    }
}

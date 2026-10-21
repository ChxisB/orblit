namespace Orblit.Asset;

/// <summary>
/// Records kept as one blob somewhere that is slow to write. Every persistent store here wants the same two
/// things and neither is about where the bytes end up, so they are written once: the whole set is read on
/// first use and kept in memory, and changes are coalesced into one write <see cref="Settle"/> after the
/// last of them.
/// </summary>
/// <remarks>
/// The coalescing is the point. A scene loading five hundred assets produces five hundred records within a
/// second or two of each other, and a store that wrote on every change would rewrite a file five hundred
/// times to answer a question that is only asked at startup.
/// <para>
/// Losing a write costs a revalidation, not a correct answer: the bytes live in an
/// <see cref="IContentStore"/> under their hash, and a record that never landed means the next launch
/// downloads rather than asking whether it needs to.
/// </para>
/// </remarks>
public abstract class BufferedFetchRecords(TimeSpan? settle = null) : IFetchRecords
{
    private readonly object _gate = new();
    private Task<Dictionary<WebUrl, FetchRecord>>? _reading;
    private Dictionary<WebUrl, FetchRecord>? _records;
    private long _generation;
    private Task _writing = Task.CompletedTask;
    private bool _changed;
    private bool _closed;

    /// <summary>How long after the last change the set is written.</summary>
    public TimeSpan Settle { get; } = settle ?? TimeSpan.FromMilliseconds(250);

    public async Task<FetchRecord?> GetAsync(WebUrl url)
    {
        var records = await LoadAsync().ConfigureAwait(false);
        lock (_gate)
        {
            return records.GetValueOrDefault(url);
        }
    }

    public Task PutAsync(WebUrl url, FetchRecord record) => ChangeAsync(records => records[url] = record);

    public Task RemoveAsync(WebUrl url) => ChangeAsync(records => records.Remove(url));

    public Task ClearAsync() => ChangeAsync(records => records.Clear());

    /// <summary>
    /// Writes now, if anything is pending, and waits for it. A set nothing has changed is not written again.
    /// Closing a fetcher that only ever read should cost nothing.
    /// </summary>
    public async Task FlushAsync()
    {
        Task writing;
        lock (_gate)
        {
            Cancel();
            if (_changed)
            {
                Schedule();
            }

            writing = _writing;
        }

        await writing.ConfigureAwait(false);
    }

    /// <summary>Writes what is pending and stops scheduling. Reading still works.</summary>
    public async Task CloseAsync()
    {
        await FlushAsync().ConfigureAwait(false);
        lock (_gate)
        {
            _closed = true;
        }
    }

    /// <summary>
    /// The stored text, or null when nothing has been stored yet. Throwing is treated as nothing stored: this
    /// is a cache, and a store that cannot be read should cost downloads rather than a startup failure.
    /// </summary>
    protected abstract Task<string?> ReadTextAsync();

    /// <summary>Replaces the stored text. Must not leave a half-written blob behind for the next read to find.</summary>
    protected abstract Task WriteTextAsync(string text);

    private async Task ChangeAsync(Action<Dictionary<WebUrl, FetchRecord>> change)
    {
        var records = await LoadAsync().ConfigureAwait(false);
        lock (_gate)
        {
            change(records);
            Touch();
        }
    }

    private Task<Dictionary<WebUrl, FetchRecord>> LoadAsync()
    {
        lock (_gate)
        {
            return _reading ??= Task.Run(FirstAsync);
        }
    }

    private async Task<Dictionary<WebUrl, FetchRecord>> FirstAsync()
    {
        Dictionary<WebUrl, FetchRecord> records;
        try
        {
            var text = await ReadTextAsync().ConfigureAwait(false);
            records = text is null ? [] : FetchRecordsCodec.Decode(text);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            records = [];
        }

        lock (_gate)
        {
            _records = records;
        }

        return records;
    }

    // Caller holds the gate.
    private void Touch()
    {
        if (_closed)
        {
            return;
        }

        _changed = true;
        Cancel();
        _ = WriteAfterSettleAsync(_generation);
    }

    // Caller holds the gate. Bumping the generation retires a wait that is already running, so a change
    // restarts the settle time without anything to dispose.
    private void Cancel() => _generation++;

    private async Task WriteAfterSettleAsync(long generation)
    {
        await Task.Delay(Settle).ConfigureAwait(false);
        lock (_gate)
        {
            if (generation == _generation)
            {
                Schedule();
            }
        }
    }

    // Caller holds the gate. Queues a write behind whatever is already writing, chained rather than
    // started at once, so that two bursts cannot overlap and leave the later set underneath the earlier one.
    private void Schedule()
    {
        _changed = false;
        _writing = WriteAfterAsync(_writing, FetchRecordsCodec.Encode(_records ?? []));
    }

    private async Task WriteAfterAsync(Task before, string text)
    {
        await before.ConfigureAwait(false);
        try
        {
            await WriteTextAsync(text).ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            // A cache that cannot be written is a cache that does not help, which is not a reason to take
            // an app down. The next change tries again.
        }
    }
}

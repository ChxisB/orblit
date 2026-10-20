using System.Threading.Channels;

namespace Orblit.Save;

/// <summary>One save on its way to the store. The id orders entries and matches an answer to its entry.</summary>
internal sealed record QueueEntry(long Id, string Key, SaveRecord Record)
{
    /// <summary>Writes this entry to <paramref name="store"/> and reports how it went to <paramref name="answer"/>.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Any failure of a write is reported to the caller and the save is kept.")]
    public async Task WriteAsync(ISaveStore store, Answer answer)
    {
        try
        {
            await store.WriteAsync(Key, Record).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            answer(Id, e);
            return;
        }

        answer(Id, null);
    }
}

/// <summary>Reports the end of a write: null for success, or what went wrong.</summary>
internal delegate void Answer(long id, Exception? error);

/// <summary>Where a <see cref="SaveQueue"/> sends the saves it has taken.</summary>
internal interface ISaveSink
{
    int WorkerStarts { get; }

    void Send(QueueEntry entry);

    Task CloseAsync();

    void Kill();
}

/// <summary>Writes on the thread pool, one at a time, in the order sent.</summary>
internal sealed class InlineSink(ISaveStore store, Answer answer) : ISaveSink
{
    private readonly Lock _gate = new();
    private Task _tail = Task.CompletedTask;

    public int WorkerStarts => 0;

    public void Send(QueueEntry entry)
    {
        lock (_gate)
        {
            _tail = WriteAfterAsync(_tail, entry);
        }
    }

    public Task CloseAsync() => Task.CompletedTask;

    public void Kill()
    {
    }

    private async Task WriteAfterAsync(Task before, QueueEntry entry)
    {
        // Leave the caller's stack first. Save holds the queue's lock, and an answer
        // that came back inside Save would run onFailure under it before Save returned.
        await Task.Yield();
        await before.ConfigureAwait(false);
        await entry.WriteAsync(store, answer).ConfigureAwait(false);
    }
}

/// <summary>
/// Writes on a worker task. A worker that ends early starts again and takes the
/// saves that had no answer. The sink shares the queue's lock, so a save sent while
/// a worker is being replaced is either in the snapshot or goes to the new worker.
/// </summary>
internal sealed class WorkerSink : ISaveSink
{
    private readonly Lock _gate;
    private readonly ISaveStore _store;
    private readonly Answer _answer;
    private readonly Func<IReadOnlyList<QueueEntry>> _open;
    private readonly TaskCompletionSource _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Worker _worker;
    private bool _closing;

    public WorkerSink(Lock gate, ISaveStore store, Answer answer, Func<IReadOnlyList<QueueEntry>> open)
    {
        _gate = gate;
        _store = store;
        _answer = answer;
        _open = open;
        lock (_gate)
        {
            _worker = Start();
        }
    }

    public int WorkerStarts { get; private set; }

    public void Send(QueueEntry entry)
    {
        lock (_gate)
        {
            _worker.Inbox.Writer.TryWrite(entry);
        }
    }

    public Task CloseAsync()
    {
        lock (_gate)
        {
            _closing = true;
            _worker.Inbox.Writer.TryComplete();
        }

        return _exited.Task;
    }

    public void Kill()
    {
        lock (_gate)
        {
            _worker.Dead = true;
            _worker.Inbox.Writer.TryComplete();
        }
    }

    // Called with the lock held, so the open list is the same one Send sees.
    private Worker Start()
    {
        WorkerStarts++;
        var worker = new Worker();
        foreach (var entry in _open())
        {
            worker.Inbox.Writer.TryWrite(entry);
        }

        _ = Task.Run(() => WorkAsync(worker));
        return worker;
    }

    private async Task WorkAsync(Worker worker)
    {
        try
        {
            await foreach (var entry in worker.Inbox.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                // A killed worker stops between writes and leaves the rest unanswered.
                if (worker.Dead)
                {
                    break;
                }

                await entry.WriteAsync(_store, _answer).ConfigureAwait(false);
            }
        }
        finally
        {
            Ended();
        }
    }

    private void Ended()
    {
        lock (_gate)
        {
            if (!_closing)
            {
                _worker = Start();
                return;
            }
        }

        _exited.TrySetResult();
    }

    private sealed class Worker
    {
        public Channel<QueueEntry> Inbox { get; } = Channel.CreateUnbounded<QueueEntry>(new UnboundedChannelOptions { SingleReader = true });

        public volatile bool Dead;
    }
}

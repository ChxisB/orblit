using System.Collections.Concurrent;

namespace Orblit.Remote;

/// <summary>
/// A queue a game's loop drains once a frame, for a game that has no synchronization context of
/// its own. Hand it to <see cref="RemoteServer"/> and call <see cref="Pump"/> from the loop
/// thread, so every verb touches the world there.
/// </summary>
/// <remarks>
/// <see cref="SynchronizationContext.Send"/> is not overridden. It runs the callback on the
/// calling thread, as the base class does, and the server never calls it.
/// </remarks>
public sealed class GameLoopContext : SynchronizationContext
{
    private readonly ConcurrentQueue<(SendOrPostCallback Run, object? State)> _queue = new();

    public override void Post(SendOrPostCallback d, object? state) => _queue.Enqueue((d, state));

    /// <summary>
    /// Runs the callbacks queued so far, with this context current so that an <c>await</c> in one
    /// of them resumes here on a later pump. Work queued while it runs waits for the next frame,
    /// so a callback that keeps queuing itself cannot hold the loop.
    /// </summary>
    public void Pump()
    {
        var before = Current;
        SetSynchronizationContext(this);
        try
        {
            for (var left = _queue.Count; left > 0 && _queue.TryDequeue(out var work); left--)
            {
                work.Run(work.State);
            }
        }
        finally
        {
            SetSynchronizationContext(before);
        }
    }
}

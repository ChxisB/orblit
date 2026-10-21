using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Orblit.Asset.Tests;

// A jitter that does not jitter, so a wait is the wait that was asked for.
internal sealed class Steady : Random
{
    public override double NextDouble() => 0.5;
}

internal static class Transports
{
    // The last path segment of a request, which is the name a test gave the asset.
    public static string NameOf(FetchRequest request) => request.Url.Path[(request.Url.Path.LastIndexOf('/') + 1)..];
}

// A transport whose replies finish when the test says so.
internal sealed class HeldTransport(byte[] bytes) : IAssetTransport
{
    private readonly Lock _gate = new();
    private readonly List<FetchRequest> _asked = [];
    private readonly Dictionary<string, Channel<byte[]>> _open = [];

    public FetchRequest[] Asked
    {
        get
        {
            lock (_gate)
            {
                return [.. _asked];
            }
        }
    }

    public Task<FetchReply> SendAsync(FetchRequest request, CancellationToken cancellation = default)
    {
        var tap = Channel.CreateUnbounded<byte[]>();
        lock (_gate)
        {
            _asked.Add(request);
            _open[Transports.NameOf(request)] = tap;
        }

        return Task.FromResult(new FetchReply { Status = 200, Body = Chunks(tap.Reader, cancellation), Length = bytes.Length });
    }

    public void Finish(string name, byte[] sending)
    {
        Channel<byte[]> tap;
        lock (_gate)
        {
            tap = _open[name];
            _open.Remove(name);
        }

        tap.Writer.TryWrite(sending);
        tap.Writer.Complete();
    }

    public void Dispose()
    {
        lock (_gate)
        {
            foreach (var tap in _open.Values)
            {
                tap.Writer.TryComplete();
            }

            _open.Clear();
        }
    }

    private static async IAsyncEnumerable<byte[]> Chunks(
        ChannelReader<byte[]> reader, [EnumeratorCancellation] CancellationToken cancellation = default)
    {
        await foreach (var chunk in reader.ReadAllAsync(cancellation).ConfigureAwait(false))
        {
            yield return chunk;
        }
    }
}

// A transport that answers 200 to a resumed request, as a server with no range support does.
internal sealed class IgnoresRanges(byte[] bytes, int stopAfter) : IAssetTransport
{
    private int? _stopAfter = stopAfter;

    public Task<FetchReply> SendAsync(FetchRequest request, CancellationToken cancellation = default)
    {
        var cut = _stopAfter;
        _stopAfter = null;
        return Task.FromResult(new FetchReply
        {
            Status = 200,
            Length = bytes.Length,
            Body = cut is { } after ? Short(after) : Whole(),
        });
    }

    public void Dispose()
    {
    }

    private async IAsyncEnumerable<byte[]> Whole()
    {
        await Task.Yield();
        yield return bytes;
    }

    private async IAsyncEnumerable<byte[]> Short(int after)
    {
        await Task.Yield();
        yield return bytes[..after];
        throw new SocketFailureException("dropped");
    }
}

// A transport that declares no length and counts what was actually pulled.
internal sealed class Undeclared(byte[] bytes, int cut) : IAssetTransport
{
    public int Handed { get; private set; }

    public Task<FetchReply> SendAsync(FetchRequest request, CancellationToken cancellation = default) =>
        Task.FromResult(new FetchReply { Status = 200, Body = Feed() });

    public void Dispose()
    {
    }

    private async IAsyncEnumerable<byte[]> Feed()
    {
        for (var at = 0; at < bytes.Length; at += cut)
        {
            await Task.Yield();
            var end = Math.Min(at + cut, bytes.Length);
            Handed = end;
            yield return bytes[at..end];
        }
    }
}

// A transport that answers some names at once and holds the rest back until the test lets them go.
internal sealed class Staggered(IReadOnlyDictionary<string, byte[]> quick, IReadOnlyDictionary<string, byte[]> held) : IAssetTransport
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, Channel<byte[]>> _open = [];

    public Task<FetchReply> SendAsync(FetchRequest request, CancellationToken cancellation = default)
    {
        var name = Transports.NameOf(request);
        if (quick.TryGetValue(name, out var atOnce))
        {
            return Task.FromResult(new FetchReply { Status = 200, Body = Single(atOnce), Length = atOnce.Length });
        }

        if (!held.TryGetValue(name, out var waiting))
        {
            return Task.FromResult(FetchReply.Empty(404));
        }

        var tap = Channel.CreateUnbounded<byte[]>();
        lock (_gate)
        {
            _open[name] = tap;
        }

        return Task.FromResult(new FetchReply { Status = 200, Body = Chunks(tap.Reader, cancellation), Length = waiting.Length });
    }

    public bool IsHolding(string name)
    {
        lock (_gate)
        {
            return _open.ContainsKey(name);
        }
    }

    public void Release(string name)
    {
        Channel<byte[]> tap;
        lock (_gate)
        {
            tap = _open[name];
            _open.Remove(name);
        }

        tap.Writer.TryWrite(held[name]);
        tap.Writer.Complete();
    }

    public void Dispose()
    {
        lock (_gate)
        {
            foreach (var tap in _open.Values)
            {
                tap.Writer.TryComplete();
            }

            _open.Clear();
        }
    }

    private static async IAsyncEnumerable<byte[]> Single(byte[] bytes)
    {
        await Task.Yield();
        yield return bytes;
    }

    private static async IAsyncEnumerable<byte[]> Chunks(
        ChannelReader<byte[]> reader, [EnumeratorCancellation] CancellationToken cancellation = default)
    {
        await foreach (var chunk in reader.ReadAllAsync(cancellation).ConfigureAwait(false))
        {
            yield return chunk;
        }
    }
}

// Holds every request until the test opens the gate, so a test can ask for several things before any answer.
// A download that finishes between two calls would otherwise change what the test is about.
internal sealed class Gated(IAssetTransport inner) : IAssetTransport
{
    private readonly TaskCompletionSource _open = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Open() => _open.TrySetResult();

    public async Task<FetchReply> SendAsync(FetchRequest request, CancellationToken cancellation = default)
    {
        await _open.Task.WaitAsync(cancellation).ConfigureAwait(false);
        return await inner.SendAsync(request, cancellation).ConfigureAwait(false);
    }

    public void Dispose() => inner.Dispose();
}

// Lets the first bytes through at once and holds back each chunk after that, so the front of a file is
// there long before the rest.
internal sealed class Slowed(IAssetTransport inner, long fast, TimeSpan between) : IAssetTransport
{
    public async Task<FetchReply> SendAsync(FetchRequest request, CancellationToken cancellation = default)
    {
        var reply = await inner.SendAsync(request, cancellation).ConfigureAwait(false);
        return new FetchReply { Status = reply.Status, Body = Paced(reply.Body, cancellation), ETag = reply.ETag, Length = reply.Length, From = reply.From };
    }

    public void Dispose() => inner.Dispose();

    private async IAsyncEnumerable<byte[]> Paced(
        IAsyncEnumerable<byte[]> chunks, [EnumeratorCancellation] CancellationToken cancellation = default)
    {
        var seen = 0L;
        await foreach (var chunk in chunks.WithCancellation(cancellation).ConfigureAwait(false))
        {
            if (seen >= fast)
            {
                await Task.Delay(between, cancellation).ConfigureAwait(false);
            }

            seen += chunk.Length;
            yield return chunk;
        }
    }
}

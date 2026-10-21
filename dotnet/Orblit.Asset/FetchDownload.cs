using System.Runtime.CompilerServices;

namespace Orblit.Asset;

/// <summary>
/// One download, shared by every caller that asked for the same URL. It owns the queue state, the callers
/// waiting on it and the single attempt loop that serves them all. All shared state is guarded by the
/// fetcher's gate. Nothing that waits, such as the transport, the store or a caller's callback, runs under it.
/// </summary>
internal sealed class FetchDownload : IDisposable
{
    private readonly AssetFetcher _fetcher;
    private readonly List<FetchJob> _jobs = [];
    private readonly CancellationTokenSource _cancel = new();
    private int? _rough;
    private bool _roughSent;
    private Ktx2Chain? _chain;
    private bool _chainRead;
    private int? _roughLevel;
    private long? _roughEnd;
    private bool _over;
    private bool _disposed;

    // What the attempt loop carries from one attempt to the next. Only the loop touches these.
    private FetchRecord? _held;
    private Resume _resume = new(null);
    private object? _last;

    internal FetchDownload(AssetFetcher fetcher, WebUrl url, AssetId? id, long arrived)
    {
        _fetcher = fetcher;
        Url = url;
        Id = id;
        Arrived = arrived;
    }

    internal WebUrl Url { get; }

    internal AssetId? Id { get; }

    internal long Arrived { get; }

    internal FetchUrgency Urgency { get; private set; } = FetchUrgency.Eventually;

    private bool Queued { get; set; }

    private bool Started { get; set; }

    private ContentHash? Wanted => Id is { } id && _fetcher.Manifest is { } manifest && manifest.Entries.TryGetValue(id, out var entry)
        ? entry.Hash
        : null;

    private CancellationToken Token => _cancel.Token;

    internal bool IsOver
    {
        get
        {
            lock (_fetcher.Gate)
            {
                return _over;
            }
        }
    }

    /// <summary>Adds a caller. Called under the fetcher's gate.</summary>
    internal void Join(FetchJob job, int? roughSize)
    {
        _jobs.Add(job);

        // The smallest anybody asked for wins. A stand-in is worth having early, and this is the level that
        // arrives first, and it still beats the nothing a caller who wanted a sharper one would be looking at.
        if (roughSize is { } size && (_rough is not { } want || size < want))
        {
            _rough = size;
        }
    }

    /// <summary>Called under the fetcher's gate.</summary>
    internal void WantedBy(FetchUrgency urgency)
    {
        var sooner = urgency < Urgency;
        if (sooner)
        {
            Urgency = urgency;
        }

        if (!Started && !Queued)
        {
            _fetcher.Queue(this);
        }
        else if (Queued && sooner)
        {
            // Already in line, and now wanted more: reorder rather than requeue, which would lose the place
            // it earned by being asked for first.
            _fetcher.Sort();
            _fetcher.Pump();
        }
    }

    /// <summary>Called under the fetcher's gate.</summary>
    internal void MarkQueued() => Queued = true;

    /// <summary>
    /// Called under the fetcher's gate as the download leaves the queue. False when it was abandoned while it
    /// waited, in which case it must not run.
    /// </summary>
    internal bool TryStart()
    {
        Queued = false;
        if (_over)
        {
            return false;
        }

        Started = true;
        return true;
    }

    /// <summary>Takes a caller out. The last one leaving abandons the download.</summary>
    internal void Drop(FetchJob job)
    {
        lock (_fetcher.Gate)
        {
            _jobs.Remove(job);

            // The last caller has gone. Stop reading, which is what closes the connection, and let the slot
            // go to something wanted.
            if (_jobs.Count == 0 && !_over)
            {
                AbandonLocked();
            }
        }
    }

    /// <summary>Stops the download and fails every caller still waiting.</summary>
    internal void Abandon()
    {
        lock (_fetcher.Gate)
        {
            AbandonLocked();
        }
    }

    private void AbandonLocked()
    {
        if (_over)
        {
            return;
        }

        _over = true;

        // Out of the map at once, or the next caller for this URL joins a download that has given up and
        // waits for bytes that will never come.
        _fetcher.ForgetLocked(this);
        var jobs = _jobs.ToArray();
        _jobs.Clear();
        foreach (var job in jobs)
        {
            job.GiveUp();
        }

        // The run is told to stop so that it gives its slot back. Cancelling after the run has ended and the
        // source is disposed is a no-op by the flag.
        if (!_disposed)
        {
            _ = _cancel.CancelAsync();
        }
    }

    internal async Task RunAsync()
    {
        // A fetch that is already satisfiable would otherwise run its store and transport work under the
        // gate, on the thread that asked for it.
        await Task.Yield();
        if (IsOver)
        {
            return;
        }

        try
        {
            await AttemptsAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (Token.IsCancellationRequested)
        {
            // Abandoned. Everyone waiting was told when it was.
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            // The records or the store failed outside an attempt. Dart would leave the callers hanging.
            Fail(e);
        }
    }

    public void Dispose()
    {
        lock (_fetcher.Gate)
        {
            _disposed = true;
        }

        _cancel.Dispose();
    }

    private void Finish(byte[] bytes)
    {
        FetchJob[] jobs;
        lock (_fetcher.Gate)
        {
            if (_over)
            {
                return;
            }

            _over = true;
            _fetcher.ForgetLocked(this);
            jobs = [.. _jobs];
            _jobs.Clear();
        }

        foreach (var job in jobs)
        {
            job.Complete(bytes);
        }
    }

    private void Fail(Exception error)
    {
        FetchJob[] jobs;
        lock (_fetcher.Gate)
        {
            if (_over)
            {
                return;
            }

            _over = true;
            _fetcher.ForgetLocked(this);
            jobs = [.. _jobs];
            _jobs.Clear();
        }

        foreach (var job in jobs)
        {
            job.Fail(error);
        }
    }

    private void Tell(long received, long? total)
    {
        FetchJob[] jobs;
        lock (_fetcher.Gate)
        {
            jobs = [.. _jobs];
        }

        var progress = new FetchProgress(received, total);
        foreach (var job in jobs)
        {
            job.OnProgress?.Invoke(progress);
        }
    }

    private async Task AttemptsAsync()
    {
        var known = await _fetcher.Records.GetAsync(Url).ConfigureAwait(false);
        if (await FromStoreAsync(known).ConfigureAwait(false))
        {
            return;
        }

        // Starts as what was on disk and is cleared if it turns out to be no use, so that a later attempt
        // does not offer the server a tag we have already learned we cannot honour.
        _held = known;
        _resume = new Resume(known?.ETag);
        for (var attempt = 1; attempt <= _fetcher.Attempts; attempt++)
        {
            if (!await WaitedAsync(attempt).ConfigureAwait(false) || await AttemptAsync(attempt).ConfigureAwait(false))
            {
                return;
            }
        }

        if (!IsOver)
        {
            await ExhaustedAsync().ConfigureAwait(false);
        }
    }

    // True when the download is still going and the attempt may begin.
    private async Task<bool> WaitedAsync(int attempt)
    {
        if (IsOver)
        {
            return false;
        }

        if (attempt > 1)
        {
            await Task.Delay(_fetcher.WaitBefore(attempt - 1), _fetcher.Clock, CancellationToken.None).ConfigureAwait(false);
        }

        return !IsOver;
    }

    /// <summary>
    /// True if the download is over before it began: the manifest names a hash and the store already holds
    /// bytes that match it. A hash the manifest names is a promise about the bytes, not about the URL, so a
    /// copy that matches it needs no server at all, not even a round trip to ask.
    /// </summary>
    private async Task<bool> FromStoreAsync(FetchRecord? known)
    {
        if (Wanted is not { } wanted || await _fetcher.Store.GetAsync(wanted).ConfigureAwait(false) is not { } bytes)
        {
            return false;
        }

        await _fetcher.Records.PutAsync(
            Url, new FetchRecord(wanted, bytes.Length, known?.ETag, _fetcher.Clock.GetUtcNow())).ConfigureAwait(false);
        Finish(bytes);
        return true;
    }

    // True when the attempt settled the download or it was abandoned. False asks for another attempt.
    private async Task<bool> AttemptAsync(int attempt)
    {
        try
        {
            // Only on a first, whole request: asking a server both to resume and to tell us whether the file
            // changed is asking two questions whose answers contradict each other.
            var reply = await _fetcher.Transport.SendAsync(
                new FetchRequest(
                    Url,
                    ifNoneMatch: _resume.Resuming ? null : _held?.ETag,
                    from: _resume.Resuming ? _resume.Length : null),
                Token).ConfigureAwait(false);
            if (IsOver)
            {
                await reply.DiscardAsync().ConfigureAwait(false);
                return true;
            }

            return await AnswerAsync(reply, attempt).ConfigureAwait(false);
        }
        catch (Exception e) when (e is FetchCorruptException or FetchRefusedException)
        {
            // Not retried. See FetchCorruptException.
            Fail(e);
            return true;
        }
        catch (OperationCanceledException) when (Token.IsCancellationRequested)
        {
            return true;
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            _last = e;
            return false;
        }
    }

    private async Task<bool> AnswerAsync(FetchReply reply, int attempt)
    {
        if (reply.IsNotModified)
        {
            return await NotModifiedAsync(reply).ConfigureAwait(false);
        }

        // A reply carrying bytes is the last one either way: Keep finishes the download, throws, or returns
        // having been abandoned.
        if (reply.Status is 200 or 206)
        {
            await KeepAsync(reply).ConfigureAwait(false);
            return true;
        }

        await reply.DiscardAsync().ConfigureAwait(false);
        if (!reply.MayWorkLater)
        {
            Fail(new FetchFailedException(Url, attempt, reply.Status));
            return true;
        }

        _last = reply.Status;
        return false;
    }

    private async Task<bool> NotModifiedAsync(FetchReply reply)
    {
        if (await ConfirmedAsync().ConfigureAwait(false))
        {
            return true;
        }

        // The server says the copy we named is current and we no longer have it. The system emptied the
        // cache, most likely. Forget the record, here and in memory, so that the attempt that follows asks
        // for the whole thing instead of being told 304 all over again.
        await _fetcher.Records.RemoveAsync(Url).ConfigureAwait(false);
        _held = null;
        _last = reply.Status;
        _resume.Tag = null;
        _resume.Restart();
        return false;
    }

    // True if a 304 can be honoured: the record the server confirmed still names bytes that are held.
    private async Task<bool> ConfirmedAsync()
    {
        if (_held is not { } held || await _fetcher.Store.GetAsync(held.Hash).ConfigureAwait(false) is not { } cached)
        {
            return false;
        }

        await _fetcher.Records.PutAsync(Url, held.ConfirmedAt(_fetcher.Clock.GetUtcNow())).ConfigureAwait(false);
        Finish(cached);
        return true;
    }

    // A server that ignored the range, or one whose copy changed under us, answers 200 to a resumed
    // request. Either way what is held is the start of a different file.
    private void TakeTagOf(FetchReply reply)
    {
        if (_resume.Resuming && reply.Status != 206)
        {
            _resume.Restart();
        }

        if (reply.ETag is not null && _resume.Tag is not null && reply.ETag != _resume.Tag)
        {
            _resume.Restart();
        }

        _resume.Tag = reply.ETag ?? _resume.Tag;
    }

    /// <summary>
    /// Reads a reply that carries bytes, checks them against whatever the manifest promised, and stores them.
    /// </summary>
    private async Task KeepAsync(FetchReply reply)
    {
        var policy = _fetcher.Origin.Policy;
        TakeTagOf(reply);

        var total = reply.Length;
        if (total > policy.MaxBytes)
        {
            await reply.DiscardAsync().ConfigureAwait(false);
            throw new FetchRefusedException(
                Url.ToString(), $"it is {total} bytes and the policy allows {policy.MaxBytes}");
        }

        var bytes = await ReadBodyAsync(reply, total, policy).ConfigureAwait(false);
        if (IsOver)
        {
            return;
        }

        var hash = ContentHash.Of(bytes);
        if (Wanted is { } promised && hash != promised)
        {
            throw new FetchCorruptException(Url, promised, hash);
        }

        await _fetcher.Store.PutAsync(bytes).ConfigureAwait(false);
        await _fetcher.Records.PutAsync(
            Url, new FetchRecord(hash, bytes.Length, _resume.Tag, _fetcher.Clock.GetUtcNow())).ConfigureAwait(false);
        Finish(bytes);
    }

    /// <summary>
    /// Out of attempts. An old copy is better than a blank screen, and being offline is the ordinary reason
    /// to be here.
    /// </summary>
    private async Task ExhaustedAsync()
    {
        if (_held is { } held && await _fetcher.Store.GetAsync(held.Hash).ConfigureAwait(false) is { } cached)
        {
            Finish(cached);
            return;
        }

        Fail(new FetchFailedException(Url, _fetcher.Attempts, _last ?? "no answer"));
    }

    /// <summary>
    /// Reads a reply's body into the bytes held so far, stopping early where it can. Two limits are enforced
    /// while the bytes are still arriving rather than after. A length that passes what the policy allows
    /// stops the download at the byte that passes it. A picture whose header says it is bigger than the
    /// policy allows stops it as soon as the header has arrived, which for every format here is within the
    /// first few dozen bytes. A sixteen-gigapixel PNG is a small download and an enormous decode, so the only
    /// useful place to catch it is before the rest of it is even sent.
    /// </summary>
    private async Task<byte[]> ReadBodyAsync(FetchReply reply, long? total, FetchPolicy policy)
    {
        var partial = _resume.Bytes;
        var looked = false;
        await foreach (var chunk in reply.Body.WithCancellation(Token).ConfigureAwait(false))
        {
            // A transport that ignores the enumerator's token still stops being read here.
            Token.ThrowIfCancellationRequested();
            partial.Write(chunk);
            if (partial.Length > policy.MaxBytes)
            {
                throw new FetchRefusedException(
                    Url.ToString(), $"it passed the {policy.MaxBytes} bytes the policy allows");
            }

            if (!looked && partial.Length >= 64)
            {
                looked = true;
                CheckPictureSize(partial, policy);
            }

            OfferRough(partial, total);
            Tell(partial.Length, total);
        }

        return partial.ToArray();
    }

    private void CheckPictureSize(MemoryStream partial, FetchPolicy policy)
    {
        var size = PictureSize.Of(partial.GetBuffer().AsSpan(0, (int)partial.Length));
        if (size is not null && size.Pixels > policy.MaxPixels)
        {
            throw new FetchRefusedException(
                Url.ToString(),
                $"it is a {size} picture, which is {size.Pixels} pixels and the policy allows {policy.MaxPixels}");
        }
    }

    /// <summary>
    /// Hands every caller a smaller version of the texture, part way through. KTX2 stores its mip levels
    /// smallest first, so the beginning of the file is a complete set of small ones: the first eight per cent
    /// of a 1254-pixel texture is every level down to 313 pixels. Those bytes are arriving anyway on the way
    /// to the rest, so this costs no extra request, no extra byte and nothing at all for a file that turns
    /// out not to be a mipped texture. It is the same download, read twice. Once only, and never when the
    /// whole thing has already arrived: a stand-in for a texture that is already here is a wasted upload and
    /// a visible flash.
    /// </summary>
    private void OfferRough(MemoryStream partial, long? total)
    {
        if (RoughSizeFor(partial, total) is not { } size)
        {
            return;
        }

        var seen = partial.GetBuffer().AsSpan(0, (int)partial.Length);
        if (_chain is null && !ReadChain(seen, size))
        {
            return;
        }

        if (_roughEnd is not { } end || partial.Length < end)
        {
            return;
        }

        var jobs = TakeJobsForRough();
        if (_chain!.Prefix(seen, _roughLevel!.Value) is not { } small)
        {
            return;
        }

        foreach (var job in jobs)
        {
            job.OnRough?.Invoke(small);
        }
    }

    // Null when no one asked, when it went already, or when the whole file is here.
    private int? RoughSizeFor(MemoryStream partial, long? total)
    {
        lock (_fetcher.Gate)
        {
            if (_rough is not { } wanted || _roughSent)
            {
                return null;
            }

            return total is { } whole && partial.Length >= whole ? null : wanted;
        }
    }

    private FetchJob[] TakeJobsForRough()
    {
        lock (_fetcher.Gate)
        {
            _roughSent = true;
            return [.. _jobs];
        }
    }

    // False when there is nothing to offer yet, or ever. The header is read once, after a kilobyte, because
    // that is enough for any real KTX2 header and index and a file that is not one is not worth a second look.
    private bool ReadChain(ReadOnlySpan<byte> seen, int size)
    {
        if (_chainRead || seen.Length < 1024)
        {
            return false;
        }

        _chainRead = true;
        _chain = Ktx2Chain.Read(seen);
        if (_chain is null)
        {
            return false;
        }

        if (_chain.LevelAtLeast(size) is not { } level)
        {
            _chain = null;
            return false;
        }

        _roughLevel = level;
        _roughEnd = _chain.BytesFor(level);
        return true;
    }

    /// <summary>What an interrupted attempt left behind: the bytes read so far, and which copy they came from.</summary>
    private sealed class Resume(string? tag)
    {
        /// <summary>The entity tag the bytes were read from, so the next attempt can ask whether it still serves that copy.</summary>
        public string? Tag { get; set; } = tag;

        public MemoryStream Bytes { get; private set; } = new();

        public long Length => Bytes.Length;

        public bool Resuming => Bytes.Length > 0;

        /// <summary>Forgets what was read. Whatever comes next is a whole file, not a continuation of this one.</summary>
        public void Restart() => Bytes = new MemoryStream();
    }
}

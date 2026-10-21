namespace Orblit.Asset;

/// <summary>
/// One pack being fetched into a <c>.part</c> file and promoted when it is whole. This is not
/// <see cref="AssetFetcher"/>, and not by oversight. That keeps what it fetches in memory and files it
/// under its hash in a content store, which is right for a texture and wrong for a pack of hundreds of
/// megabytes. A pack is written to disk as it arrives, so a download that dies with the process can carry on
/// from the file, and it is checked as a pack and not as bytes.
/// </summary>
// F2: a download is described by what it fetches and how, all named at the call.
internal sealed class PackDownload
{
    public required PackOffer Offer { get; init; }

    public required IAssetTransport Transport { get; init; }

    public required AssetOrigin Origin { get; init; }

    /// <summary>
    /// Where the bytes go until they are known to be the pack. The finished pack is this file's sibling,
    /// named by the hash.
    /// </summary>
    public required string Part { get; init; }

    public required int Attempts { get; init; }

    public required TimeSpan Backoff { get; init; }

    public Action<FetchProgress>? OnProgress { get; init; }

    public Random Jitter { get; init; } = Random.Shared;

    /// <summary>Fetches, retries and checks. Answers the pack, filed under its name.</summary>
    public async Task<AssetPack> RunAsync()
    {
        Origin.RequireAllowed(Offer.Url);
        object? last = null;
        for (var attempt = 1; attempt <= Attempts; attempt++)
        {
            if (attempt > 1)
            {
                await Task.Delay(WaitBefore(attempt - 1)).ConfigureAwait(false);
            }

            try
            {
                return await AttemptAsync(attempt).ConfigureAwait(false);
            }
            catch (Exception error) when (MayWorkLater(error))
            {
                last = error is TryAgainException again ? again.Cause : error;
            }
        }

        throw new FetchFailedException(Offer.Url, Attempts, last ?? "no answer");
    }

    // Waits, longer each time, with a little noise. The noise matters more than the doubling. A hundred
    // devices that lost the same server come back at the same moment without it.
    internal TimeSpan WaitBefore(int attempt)
    {
        var grown = Backoff * Math.Pow(2, attempt - 1);
        var noise = 1 + ((Jitter.NextDouble() - 0.5) * 0.4);
        return TimeSpan.FromMicroseconds(Math.Round(grown.TotalMicroseconds * noise, MidpointRounding.AwayFromZero));
    }

    // One request, from wherever the part file ends.
    private async Task<AssetPack> AttemptAsync(int attempt)
    {
        var have = LengthOfPart();
        var reply = await Transport.SendAsync(new FetchRequest(Offer.Url, from: have > 0 ? have : null)).ConfigureAwait(false);
        if (reply.Status == 416 && have > 0)
        {
            // The server has no byte there. Either the part is the whole file and the rename never
            // happened, or the file on the server is shorter than the part and the part is of no use.
            await reply.DiscardAsync().ConfigureAwait(false);
            return await VerifyAsync(resumed: true).ConfigureAwait(false);
        }

        if (reply.Status is not (200 or 206))
        {
            await reply.DiscardAsync().ConfigureAwait(false);
            throw reply.MayWorkLater ? new TryAgainException(reply.Status) : new FetchFailedException(Offer.Url, attempt, reply.Status);
        }

        var start = await StartOfAsync(reply, have).ConfigureAwait(false);
        await WriteAsync(reply, start).ConfigureAwait(false);
        return await VerifyAsync(resumed: start > 0).ConfigureAwait(false);
    }

    // Where in the file this reply's bytes go. Nought when the server sent the whole file, which it does to a
    // resumed request when it cannot or will not honour a range. A server that resumes from some other byte
    // is not trusted with the part, and the part goes.
    private async Task<long> StartOfAsync(FetchReply reply, long have)
    {
        if (reply.Status == 200)
        {
            return 0;
        }

        if (reply.From == have)
        {
            return have;
        }

        await reply.DiscardAsync().ConfigureAwait(false);
        PackFolder.Delete(Part);
        throw new TryAgainException($"the server resumed at {reply.From?.ToString() ?? "null"}, not at {have}");
    }

    // Writes the reply's bytes to the part file and checks it came to the length the server gave. The part is
    // kept when the reply ended short, since that is what the next attempt resumes from. It is dropped when
    // the reply was too big, which no resume can mend.
    private async Task WriteAsync(FetchReply reply, long start)
    {
        var limit = Origin.Policy.MaxBytes;
        var total = reply.Length;
        if (total > limit)
        {
            await reply.DiscardAsync().ConfigureAwait(false);
            throw TooBig(total.Value, limit);
        }

        var written = await ReceiveAsync(reply, start, limit).ConfigureAwait(false);
        if (written > limit)
        {
            PackFolder.Delete(Part);
            throw TooBig(written, limit);
        }

        if (total is { } expected && written != expected)
        {
            if (written > expected)
            {
                PackFolder.Delete(Part);
            }

            throw new TryAgainException($"the reply ended at {written} of {expected} bytes");
        }
    }

    // Appends the body to the part file, stopping once it is past limit, and answers how long the file is then.
    private async Task<long> ReceiveAsync(FetchReply reply, long start, long limit)
    {
        // Unbuffered, so that what the process wrote is in the file if it dies, which is what a resume starts from.
        var file = new FileStream(
            Part, start > 0 ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.Read, 0, FileOptions.Asynchronous);
        var written = start;
        await using (file.ConfigureAwait(false))
        {
            await foreach (var chunk in reply.Body.ConfigureAwait(false))
            {
                written += chunk.Length;

                // Leaving the loop cancels the request, which is what stops a server that sends more than
                // it said.
                if (written > limit)
                {
                    break;
                }

                await file.WriteAsync(chunk).ConfigureAwait(false);
                OnProgress?.Invoke(new FetchProgress(written, reply.Length));
            }
        }

        return written;
    }

    private FetchRefusedException TooBig(long bytes, long limit) =>
        new(Offer.Url.ToString(), $"it is {bytes} bytes and the policy allows {limit}");

    // Checks the part as a pack, and files it under its name when it is. A part that was resumed may be the
    // start of a different file, as when the server's copy changed between two runs. It is thrown away and
    // fetched again whole. A whole download that is wrong is the server's answer, and asking again would get
    // the same one.
    private async Task<AssetPack> VerifyAsync(bool resumed)
    {
        try
        {
            await CheckAsync().ConfigureAwait(false);
        }
        catch (Exception error) when (error is FormatException or FetchCorruptException)
        {
            PackFolder.Delete(Part);
            if (!resumed)
            {
                throw;
            }

            throw new TryAgainException(error);
        }

        var done = Path.Join(Path.GetDirectoryName(Part), Offer.Hash.Hex + ".opack");
        File.Move(Part, done, overwrite: true);
        return await PackFolder.OpenFileAsync(done).ConfigureAwait(false);
    }

    // Throws when the part is not the pack that was offered, whole.
    private async Task CheckAsync()
    {
        var bytes = new FilePackBytes(Part);
        await using (bytes.ConfigureAwait(false))
        {
            var pack = await AssetPack.OpenAsync(bytes).ConfigureAwait(false);
            if (pack.Hash != Offer.Hash)
            {
                throw new FetchCorruptException(Offer.Url, Offer.Hash, pack.Hash);
            }

            var damaged = await pack.FindCorruptAsync().ConfigureAwait(false);
            if (damaged.Count > 0)
            {
                throw new FormatException(
                    $"{damaged.Count} assets in the download from {Offer.Url} do not match the hashes in its directory, starting with {damaged[0]}");
            }
        }
    }

    private long LengthOfPart() => File.Exists(Part) ? new FileInfo(Part).Length : 0;

    // False for what asking again cannot change: a refusal, a pack that is not the one offered, an answer such
    // as 404, a full disk and a mistake in the program. A dropped connection and a busy server are the rest.
    private static bool MayWorkLater(Exception error) => !IsMistake(error) && !IsAnswer(error);

    // What Dart calls an Error: the program did something it should not have.
    internal static bool IsMistake(Exception error) =>
        error is ArgumentException or InvalidOperationException or NotSupportedException or NotImplementedException
            or NullReferenceException or IndexOutOfRangeException or InvalidCastException or OutOfMemoryException;

    private static bool IsAnswer(Exception error) =>
        error is FetchRefusedException or FetchCorruptException or FetchFailedException or FormatException
            or IOException or UnauthorizedAccessException;

    // A reason to try again, so that a retry reads as one in RunAsync and nothing else does.
    private sealed class TryAgainException(object cause) : Exception($"Try again: {cause}")
    {
        public object Cause { get; } = cause;
    }
}

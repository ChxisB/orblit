using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Orblit.Asset;

/// <summary>One thing to draw while an asset loads, or the asset itself.</summary>
/// <param name="Bytes">The stage's bytes.</param>
/// <param name="Id">
/// Which asset these bytes are: the stand-in, or the one that was asked for. Worth knowing, because the two
/// are rarely the same size and a renderer swapping one for the other has to say so. A stage that is not the
/// last and names the asset that was asked for is that asset's coarse mip levels, the same picture, smaller.
/// </param>
/// <param name="IsFinal">Whether anything better is still coming.</param>
public sealed record AssetStage(byte[] Bytes, AssetId Id, bool IsFinal);

/// <summary>Loading something in stages, so that there is always something to draw.</summary>
public static class ProgressiveFetch
{
    /// <summary>
    /// Hands over a stand-in while <paramref name="id"/> loads, then <paramref name="id"/> itself. At most two
    /// stages, and often one. The stand-in is only handed over if it has arrived, <paramref name="standInAfter"/>
    /// has passed, and the real asset has not turned up in the meantime, which on a warm cache it will have,
    /// so a second launch shows no stand-in at all rather than flashing one for a frame. That flash is the
    /// reason the delay exists: substituting a blurry texture for a sharp one is worth it while the wait is
    /// long, and worse than nothing when the wait is three frames.
    /// </summary>
    /// <remarks>
    /// A stand-in that fails to load is not a failure. It is the thing that was there to make a failure
    /// bearable. The real asset failing ends the stream with that error. Ending the enumeration early cancels
    /// both fetches, which is what makes this safe to start for everything on screen and drop when the scene
    /// changes. Nothing is fetched until the first item is asked for.
    ///
    /// A third stage arrives on its own for a mipped texture: the coarse mip levels, built out of the front
    /// of the file while the rest of it is still arriving. It costs no extra request and no extra byte, and
    /// it is the same asset rather than a substitute for it, so it supersedes the stand-in and is never
    /// followed by one. <paramref name="roughSize"/> is the longest side worth waiting for. A texture with no
    /// mip chain never produces the stage.
    /// </remarks>
    public static async IAsyncEnumerable<AssetStage> FetchInStagesAsync(
        this AssetFetcher fetcher,
        AssetId id,
        AssetId? standIn = null,
        FetchUrgency urgency = FetchUrgency.OnScreen,
        TimeSpan? standInAfter = null,
        int roughSize = 256,
        Action<FetchProgress>? onProgress = null,
        [EnumeratorCancellation] CancellationToken cancellation = default)
    {
        using var staging = new Staging(fetcher, id, standIn, standInAfter ?? TimeSpan.FromMilliseconds(100));
        staging.Start(urgency, roughSize, onProgress);
        await foreach (var stage in staging.Stages.ReadAllAsync(cancellation).ConfigureAwait(false))
        {
            yield return stage;
        }
    }

    // The state one staged fetch shares between the fetcher's callbacks, the stand-in's timer and the
    // consumer. One lock guards it. The fetcher never calls back while it holds its own gate, so taking this
    // lock first and the fetcher's second is the only order there is.
    private sealed class Staging(AssetFetcher fetcher, AssetId id, AssetId? standIn, TimeSpan standInAfter) : IDisposable
    {
        private readonly Lock _gate = new();
        private readonly Channel<AssetStage> _out = Channel.CreateUnbounded<AssetStage>(
            new UnboundedChannelOptions { SingleReader = true });

        private FetchJob? _rough;
        private FetchJob? _wanted;
        private Timer? _waiting;
        private byte[]? _roughBytes;
        private bool _arrived;
        private bool _shown;
        private bool _stopped;

        public ChannelReader<AssetStage> Stages => _out.Reader;

        public void Start(FetchUrgency urgency, int roughSize, Action<FetchProgress>? onProgress)
        {
            FetchJob? rough = null;
            FetchJob wanted;
            lock (_gate)
            {
                _wanted = wanted = fetcher.Fetch(id, urgency, onProgress, OnRough, roughSize);
                if (standIn is not null)
                {
                    // Created under the lock so that a zero delay cannot run before the field is set.
                    _waiting = new Timer(_ => WaitOver(), null, standInAfter, Timeout.InfiniteTimeSpan);
                    _rough = rough = fetcher.Fetch(standIn, FetchUrgency.OnScreen);
                }
            }

            _ = WantedAsync(wanted);
            if (rough is not null)
            {
                _ = StandInAsync(rough);
            }
        }

        // Called when the consumer stops reading, whether it finished, gave up or was cancelled.
        public void Dispose()
        {
            lock (_gate)
            {
                _stopped = true;
                StopWaiting();
                _rough?.Cancel();
                _wanted?.Cancel();
            }

            _out.Writer.TryComplete();
        }

        private void StopWaiting()
        {
            _waiting?.Dispose();
            _waiting = null;
        }

        private void OnRough(byte[] bytes)
        {
            lock (_gate)
            {
                if (_arrived || _stopped)
                {
                    return;
                }

                // The asset's own coarse levels beat any stand-in, so the stand-in is called off rather than
                // drawn over the top of them.
                _shown = true;
                StopWaiting();
                _rough?.Cancel();
                _out.Writer.TryWrite(new AssetStage(bytes, id, false));
            }
        }

        private void WaitOver()
        {
            lock (_gate)
            {
                _waiting = null;
                OfferStandIn();
            }
        }

        // Only once both the bytes and the moment have arrived, whichever of the two is later.
        private void OfferStandIn()
        {
            if (_arrived || _shown || _stopped || _waiting is not null || _roughBytes is not { } bytes || standIn is null)
            {
                return;
            }

            _shown = true;
            _out.Writer.TryWrite(new AssetStage(bytes, standIn, false));
        }

        private async Task StandInAsync(FetchJob job)
        {
            byte[] bytes;
            try
            {
                bytes = await job.Bytes.ConfigureAwait(false);
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                // Nothing to draw in the meantime, which is the ordinary state of affairs and not worth
                // ending the load over.
                return;
            }

            lock (_gate)
            {
                _roughBytes = bytes;
                OfferStandIn();
            }
        }

        private async Task WantedAsync(FetchJob job)
        {
            byte[]? bytes = null;
            Exception? error = null;
            try
            {
                bytes = await job.Bytes.ConfigureAwait(false);
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                error = e;
            }

            lock (_gate)
            {
                _arrived = true;
                StopWaiting();
                _rough?.Cancel();
                if (_stopped)
                {
                    return;
                }

                if (bytes is not null)
                {
                    _out.Writer.TryWrite(new AssetStage(bytes, id, true));
                }

                _out.Writer.TryComplete(error);
            }
        }
    }
}

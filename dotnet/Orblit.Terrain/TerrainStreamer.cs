using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace Orblit.Terrain;

/// <summary>
/// Where a streamed terrain's regions are kept.
/// Declared here, beside the <see cref="TerrainStreamer"/> that calls it, and implemented by
/// whatever holds the files: a folder, a pack, a server, or a function that makes ground from noise.
/// </summary>
public interface IRegionSource
{
    /// <summary>The regions there are. One that is not listed is never asked for.</summary>
    IEnumerable<RegionKey> Keys { get; }

    /// <summary>The bytes of the region's file, as <see cref="TerrainRegion.Decode"/> reads them.</summary>
    Task<byte[]> ReadAsync(RegionKey key);
}

/// <summary>
/// Keeps the regions of a <see cref="Terrain"/> that are near a point and lets go of the rest.
/// The renderer draws at most 256 regions, so a world of more cannot be handed over whole. A
/// streamer holds the ones within <see cref="LoadRadius"/> of the focus and puts them in and out
/// of the terrain as the focus moves. A renderer and a <see cref="ScatterPlacer"/> read the
/// terrain's regions as they are each frame, so they follow without being told.
/// Regions that were in the terrain before the streamer, and regions that were edited after it
/// loaded them, are the caller's. They are never removed, and they count against
/// <see cref="MaxResident"/>.
/// A terrain is not safe to share between threads. The streamer changes it on the synchronization
/// context that <see cref="FollowAsync"/> was called on, so a caller on a UI thread reads and
/// draws it from the same thread. With no context, call <see cref="FollowAsync"/> from the thread
/// that reads the terrain and read it only between runs.
/// </summary>
public sealed class TerrainStreamer
{
    private readonly object _gate = new();
    private readonly IRegionSource _source;
    private readonly List<RegionKey> _keys;

    // The regions this streamer put in, each with the revision it had when it went in. A revision
    // that has moved is an edit, and the region is then the caller's.
    private readonly Dictionary<RegionKey, long> _streamed = [];
    private readonly HashSet<RegionKey> _loading = [];
    private readonly OrderedDictionary<RegionKey, string> _failed = [];

    private double _x;
    private double _z;
    private bool _draining;
    private Task _running = Task.CompletedTask;

    // F2: the two required values and the four settings are always called by name.
    public TerrainStreamer(
        Terrain terrain,
        IRegionSource source,
        double loadRadius = 512,
        double? keepRadius = null,
        int maxResident = 256,
        int concurrency = 2)
    {
        var keep = keepRadius ?? (loadRadius + (terrain.RegionSize * terrain.Spacing));
        Check(loadRadius, keep, maxResident, concurrency);
        Terrain = terrain;
        LoadRadius = loadRadius;
        KeepRadius = keep;
        MaxResident = maxResident;
        Concurrency = concurrency;
        _keys = [.. source.Keys];
        _source = source;
    }

    public Terrain Terrain { get; }

    /// <summary>How far from the focus, in metres, a region's nearest edge may be and still be loaded.</summary>
    public double LoadRadius { get; }

    /// <summary>
    /// How far a loaded region may drift before it is let go of. Above <see cref="LoadRadius"/>, so
    /// a focus pacing a boundary does not load and drop the same region every frame.
    /// </summary>
    public double KeepRadius { get; }

    /// <summary>The most regions the terrain may hold, the caller's own included. The renderer's limit is 256.</summary>
    public int MaxResident { get; }

    /// <summary>How many reads are out at once.</summary>
    public int Concurrency { get; }

    /// <summary>The regions this streamer is holding.</summary>
    public IReadOnlySet<RegionKey> Resident
    {
        get
        {
            lock (_gate)
            {
                return _streamed.Keys.ToFrozenSet();
            }
        }
    }

    /// <summary>How many reads are out.</summary>
    public int Loading
    {
        get
        {
            lock (_gate)
            {
                return _loading.Count;
            }
        }
    }

    /// <summary>
    /// The regions that could not be read or were not a region, with why. They are not asked for
    /// again until <see cref="RetryFailed"/>.
    /// </summary>
    public IReadOnlyDictionary<RegionKey, string> Failures
    {
        get
        {
            lock (_gate)
            {
                return new OrderedDictionary<RegionKey, string>(_failed);
            }
        }
    }

    /// <summary>
    /// Moves the focus to world (<paramref name="x"/>, <paramref name="z"/>), lets go of what is out
    /// of range and starts reading what is in it, nearest first.
    /// Completes when everything within range is in or has failed. Calling it again while it runs
    /// moves the focus and returns the same run, so a frame loop may call it every frame without
    /// awaiting.
    /// </summary>
    public Task FollowAsync(double x, double z)
    {
        if (!double.IsFinite(x) || !double.IsFinite(z))
        {
            throw new ArgumentException($"The focus must be a finite point: ({x}, {z}).");
        }

        lock (_gate)
        {
            _x = x;
            _z = z;
            LetGo();
            if (_draining)
            {
                return _running;
            }

            _draining = true;
            return _running = Drain();
        }
    }

    /// <summary>Makes the regions that failed eligible to be read again at the next <see cref="FollowAsync"/>.</summary>
    public void RetryFailed()
    {
        lock (_gate)
        {
            _failed.Clear();
        }
    }

    private static void Check(double loadRadius, double keepRadius, int maxResident, int concurrency)
    {
        if (!(loadRadius > 0) || !double.IsFinite(loadRadius))
        {
            throw new ArgumentOutOfRangeException(nameof(loadRadius), loadRadius, "Must be above 0.");
        }

        if (!(keepRadius >= loadRadius) || !double.IsFinite(keepRadius))
        {
            throw new ArgumentOutOfRangeException(
                nameof(keepRadius),
                keepRadius,
                $"Must be at least the load radius, {loadRadius}, or a region would be let go of as soon as it was loaded.");
        }

        if (maxResident < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxResident), maxResident, "Must be 1 or more.");
        }

        if (concurrency < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(concurrency), concurrency, "Must be 1 or more.");
        }
    }

    private async Task Drain()
    {
        var reads = new List<Task>();
        try
        {
            while (true)
            {
                Task[] pending;
                lock (_gate)
                {
                    reads.RemoveAll(read => read.IsCompleted);
                    foreach (var key in Missing().Take(Concurrency - reads.Count))
                    {
                        reads.Add(Load(key));
                    }

                    if (reads.Count == 0)
                    {
                        // Cleared here, in the same hold of the lock as the decision to stop, so a
                        // focus that arrives next starts a new run rather than joining this one.
                        _draining = false;
                        return;
                    }

                    pending = [.. reads];
                }

                await Task.WhenAny(pending);
            }
        }
        catch
        {
            lock (_gate)
            {
                _draining = false;
            }

            throw;
        }
    }

    // A source can fail in ways of its own: a dropped connection, a missing file. Each is one
    // region's failure, not the stream's.
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A source can fail in any way.")]
    private async Task Load(RegionKey key)
    {
        lock (_gate)
        {
            _loading.Add(key);
        }

        try
        {
            var bytes = await _source.ReadAsync(key);
            lock (_gate)
            {
                Accept(key, TerrainRegion.Decode(bytes).Region);
            }
        }
        catch (Exception error)
        {
            lock (_gate)
            {
                _failed[key] = error.Message;
            }
        }
        finally
        {
            lock (_gate)
            {
                _loading.Remove(key);
            }
        }
    }

    private void Accept(RegionKey key, TerrainRegion region)
    {
        if (region.Key != key)
        {
            _failed[key] = $"The file for {key} holds the region at {region.Key}.";
        }
        else if (region.Size != Terrain.RegionSize)
        {
            _failed[key] = $"The file for {key} is {region.Size} texels across and this terrain's regions are {Terrain.RegionSize}.";
        }
        else if (Terrain.RegionAt(key) is null && Target().Contains(key))
        {
            // A read that lands after the focus has moved on is dropped, and a region put in by
            // someone else meanwhile is left alone.
            Terrain.PutRegion(region);
            _streamed[key] = region.Revision;
        }
    }

    // What the terrain should hold, nearest the focus first: everything in load range, then what
    // is already held and still in keep range, cut to the room left once the caller's own regions
    // are counted.
    private List<RegionKey> Target()
    {
        var room = Math.Max(0, MaxResident - CallersRegions());
        return
        [
            .. _keys
                .Where(key => !_failed.ContainsKey(key))
                .Select(key => (Distance: DistanceTo(key), Key: key))
                .Where(near => near.Distance <= (_streamed.ContainsKey(near.Key) ? KeepRadius : LoadRadius))
                .OrderBy(near => near.Distance)
                .ThenBy(near => near.Key.X)
                .ThenBy(near => near.Key.Z)
                .Take(room)
                .Select(near => near.Key),
        ];
    }

    private int CallersRegions() => Terrain.Regions.Count(region => !_streamed.ContainsKey(region.Key));

    // What is wanted and neither held nor being read.
    private List<RegionKey> Missing() =>
    [
        .. Target().Where(key => !_streamed.ContainsKey(key) && !_loading.Contains(key) && Terrain.RegionAt(key) is null),
    ];

    private void LetGo()
    {
        var keep = Target().ToHashSet();
        foreach (var key in _streamed.Keys.ToList())
        {
            var region = Terrain.RegionAt(key);
            if (region is not null && keep.Contains(key))
            {
                continue;
            }

            // Gone already, or edited: either way it is not the streamer's to remove any more.
            if (region is not null && region.Revision == _streamed[key])
            {
                Terrain.RemoveRegion(key);
            }

            _streamed.Remove(key);
        }
    }

    // Metres from the focus to the nearest point of the region's square.
    private double DistanceTo(RegionKey key)
    {
        var across = Terrain.RegionSize * Terrain.Spacing;
        var dx = Outside(_x, key.X * across, (key.X + 1) * across);
        var dz = Outside(_z, key.Z * across, (key.Z + 1) * across);
        return Math.Sqrt((dx * dx) + (dz * dz));
    }

    private static double Outside(double at, double low, double high) =>
        at < low ? low - at : (at > high ? at - high : 0);
}

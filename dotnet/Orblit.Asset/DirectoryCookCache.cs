using System.Globalization;
using Orblit.Json;

namespace Orblit.Asset;

/// <summary>
/// A cook cache in a directory, shared by every build on the machine.
/// </summary>
/// <remarks>
/// Laid out as an index of what was cooked and a content store of the bytes:
/// <code>
/// index.json          key -> outputs, and when each was last wanted;
///                     and the keys that would not cook, with why
/// index.lock          held while the index is read and written
/// content/&lt;shard&gt;/...   the bytes, filed by hash
/// </code>
/// The split is what makes it cheap. Bytes are content-addressed, so two targets whose cooks happen
/// to agree share one copy, and writing bytes needs no lock at all: the name of an entry is its
/// contents, so two writers racing write the same thing. Only the index is mutable, and only the
/// index is locked.
/// <para>
/// The lock is a file lock, so it holds across processes, and Dart's cache over the same directory
/// takes the same lock. Inside one process it is backed up by a queue shared by every cache on the
/// same directory, because a POSIX file lock belongs to the process rather than to whoever took it.
/// </para>
/// </remarks>
public sealed class DirectoryCookCache : ICookCache
{
    /// <summary>
    /// Bumped when the index's shape changes. An index this cannot read is treated as an empty one,
    /// so an older build meeting a newer cache misses and recooks rather than misreading.
    /// </summary>
    public const int FormatVersion = 1;

    // How many remembered failures to keep, newest first. A bound rather than a limit in bytes,
    // because the thing worth stopping is a build loop that fails on ten thousand assets a night.
    private const int KeptFailures = 4096;

    private static readonly TimeSpan UsedAtResolution = TimeSpan.FromHours(1);
    private static readonly TimeSpan LockRetry = TimeSpan.FromMilliseconds(10);

    // One queue for each directory, shared by every cache on it in this process. Keyed on the
    // absolute path, so a relative spelling and an absolute one queue together.
    private static readonly Dictionary<string, Task> Turns = [];
    private static int _writes;

    private readonly Func<DateTimeOffset> _clock;
    private readonly DirectoryContentStore _content;
    private readonly string _queueKey;
    private readonly long? _limitBytes;

    public DirectoryCookCache(string rootPath, long? limitBytes = null, Func<DateTimeOffset>? clock = null)
    {
        if (limitBytes is <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(limitBytes),
                limitBytes,
                "A cache limit of nothing is a cache that evicts every entry as it writes it. Leave it out for no limit.");
        }

        RootPath = rootPath;
        _limitBytes = limitBytes;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
        _content = new DirectoryContentStore(Path.Combine(rootPath, "content"));
        _queueKey = Path.GetFullPath(rootPath);
    }

    /// <summary>The directory the cache lives in. It need not exist yet.</summary>
    public string RootPath { get; }

    private string IndexPath => Path.Combine(RootPath, "index.json");

    private string LockPath => Path.Combine(RootPath, "index.lock");

    public Task<CookedAsset?> LookUpAsync(CookKey key) => GuardedAsync(() => LookUpLockedAsync(key));

    public async Task<CookedAsset> StoreAsync(CookKey key, IReadOnlyDictionary<string, byte[]> outputs)
    {
        // Outside the lock on purpose. An entry's name is its contents, so two writers storing the
        // same bytes write the same file, and a long cook holding the index shut would serialise
        // every build on the machine behind it.
        var stored = new List<CookOutput>();
        foreach (var (name, bytes) in outputs)
        {
            var hash = await _content.PutAsync(bytes).ConfigureAwait(false);
            stored.Add(new CookOutput(name, hash, bytes.Length));
        }

        var asset = new CookedAsset(stored);
        await GuardedAsync(() => FileAwayAsync(key, asset)).ConfigureAwait(false);
        return asset;
    }

    public Task<byte[]?> ReadAsync(ContentHash hash) => _content.GetAsync(hash);

    public Task<CookFailure?> LookUpFailureAsync(CookKey key) =>
        GuardedAsync(async () => (await ReadIndexAsync().ConfigureAwait(false)).Failures.GetValueOrDefault(key.Hash.Hex));

    public Task RecordFailureAsync(CookKey key, string reason) => GuardedAsync(async () =>
    {
        var index = await ReadIndexAsync().ConfigureAwait(false);
        index.Failures[key.Hash.Hex] = new CookFailure(reason, _clock());
        Forget(index.Failures, keeping: key.Hash.Hex);
        await WriteIndexAsync(index).ConfigureAwait(false);
        return true;
    });

    private async Task<CookedAsset?> LookUpLockedAsync(CookKey key)
    {
        var index = await ReadIndexAsync().ConfigureAwait(false);
        var hex = key.Hash.Hex;
        if (!index.Entries.TryGetValue(hex, out var entry))
        {
            return null;
        }

        foreach (var output in entry.Asset.Outputs)
        {
            if (!await _content.ContainsAsync(output.Hash).ConfigureAwait(false))
            {
                // The bytes have been evicted, or removed by something that is not this cache. The
                // entry is a promise the cache can no longer keep, so it goes, and the caller cooks.
                index.Entries.Remove(hex);
                await WriteIndexAsync(index).ConfigureAwait(false);
                return null;
            }
        }

        var now = _clock();
        if (now - entry.UsedAt > UsedAtResolution)
        {
            index.Entries[hex] = entry with { UsedAt = now };
            await WriteIndexAsync(index).ConfigureAwait(false);
        }

        return entry.Asset;
    }

    private async Task<bool> FileAwayAsync(CookKey key, CookedAsset asset)
    {
        var index = await ReadIndexAsync().ConfigureAwait(false);
        var hex = key.Hash.Hex;
        index.Entries[hex] = new Entry(asset, _clock());
        // A key that cooks is a key that no longer failed: the tool that was missing has been
        // installed, or the disk has been cleared.
        index.Failures.Remove(hex);
        await EvictAsync(index.Entries, keeping: hex).ConfigureAwait(false);
        await WriteIndexAsync(index).ConfigureAwait(false);
        return true;
    }

    // Drops the oldest failures once there are more than KeptFailures, never the one just written.
    private static void Forget(OrderedDictionary<string, CookFailure> failures, string keeping)
    {
        foreach (var candidate in failures.OrderBy(pair => pair.Value.At).ToList())
        {
            if (failures.Count <= KeptFailures)
            {
                break;
            }

            if (candidate.Key != keeping)
            {
                failures.Remove(candidate.Key);
            }
        }
    }

    // Drops entries, oldest first, until what is held is within the limit. Sizes are counted per
    // distinct hash, because that is what the disk holds: two entries sharing one cooked texture share
    // one file. Bytes are only removed once no surviving entry names them.
    private async Task EvictAsync(OrderedDictionary<string, Entry> entries, string keeping)
    {
        if (_limitBytes is not { } limit || Held(entries) <= limit)
        {
            return;
        }

        var dropped = new HashSet<ContentHash>();
        foreach (var candidate in entries.OrderBy(pair => pair.Value.UsedAt).ToList())
        {
            if (candidate.Key == keeping)
            {
                continue;
            }

            if (Held(entries) <= limit)
            {
                break;
            }

            dropped.UnionWith(candidate.Value.Asset.Outputs.Select(output => output.Hash));
            entries.Remove(candidate.Key);
        }

        dropped.ExceptWith(entries.Values.SelectMany(entry => entry.Asset.Outputs).Select(output => output.Hash));
        foreach (var hash in dropped)
        {
            await _content.RemoveAsync(hash).ConfigureAwait(false);
        }
    }

    private static long Held(OrderedDictionary<string, Entry> entries)
    {
        var sizes = new Dictionary<ContentHash, long>();
        foreach (var output in entries.Values.SelectMany(entry => entry.Asset.Outputs))
        {
            sizes[output.Hash] = output.Bytes;
        }

        return sizes.Values.Sum();
    }

    // Runs body with the index locked, against other processes and against this object's own
    // concurrent callers.
    private async Task<T> GuardedAsync<T>(Func<Task<T>> body)
    {
        var ours = new TaskCompletionSource();
        Task queue;
        lock (Turns)
        {
            queue = Turns.GetValueOrDefault(_queueKey) ?? Task.CompletedTask;
            Turns[_queueKey] = ours.Task;
        }

        await queue.ConfigureAwait(false);
        try
        {
            return await LockedAsync(body).ConfigureAwait(false);
        }
        finally
        {
            ours.SetResult();
        }
    }

    private async Task<T> LockedAsync<T>(Func<Task<T>> body)
    {
        Directory.CreateDirectory(RootPath);
        var file = await HoldAsync(LockPath).ConfigureAwait(false);
        await using (file.ConfigureAwait(false))
        {
            return await body().ConfigureAwait(false);
        }
    }

    // Takes the lock by opening the lock file with no sharing, which .NET backs with a file lock on
    // Unix and a share mode on Windows, and waits for it rather than failing when it is held, so a
    // second build cooking at the same moment takes its turn instead of crashing. Closing the file
    // lets go.
    // shortcut: polls every 10 ms and never gives up, upgrade when a stuck lock needs a timeout.
    private static async Task<FileStream> HoldAsync(string path)
    {
        while (true)
        {
            try
            {
                return new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.None);
            }
            catch (IOException error) when (error is not (FileNotFoundException or DirectoryNotFoundException))
            {
                await Task.Delay(LockRetry).ConfigureAwait(false);
            }
        }
    }

    // The index, or an empty one when there is nothing readable there. Every failure lands here: no
    // file yet, a half-written file from a machine that lost power, a newer format, a hash that is
    // not one. All of them mean the cache cannot say what was cooked, and the answer is to cook
    // again. A corrupt cache is a slow build and never a broken one.
    private async Task<Index> ReadIndexAsync()
    {
        object? parsed;
        try
        {
            var bytes = await File.ReadAllBytesAsync(IndexPath).ConfigureAwait(false);
            parsed = DartJson.Decode(DartText.Decode(bytes));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or FormatException)
        {
            return new Index();
        }

        return parsed is IReadOnlyDictionary<string, object?> map && IsFormat(map.GetValueOrDefault("formatVersion"))
            && map.GetValueOrDefault("entries") is IReadOnlyDictionary<string, object?> entries
            ? ParseIndex(entries, map.GetValueOrDefault("failures"))
            : new Index();
    }

    // A number equals 1 whether it was written 1 or 1.0, as in Dart.
    private static bool IsFormat(object? version) => version switch
    {
        long whole => whole == FormatVersion,
        double number => number == FormatVersion,
        _ => false,
    };

    private static Index ParseIndex(IReadOnlyDictionary<string, object?> entries, object? failures)
    {
        var index = new Index();
        foreach (var (key, value) in entries)
        {
            if (ReadEntry(value) is { } read)
            {
                index.Entries[key] = read;
            }
        }

        // Absent for an index written before failures were recorded, and for one written by a build
        // that does not know about them. No failures remembered is the state every cache starts in.
        if (failures is IReadOnlyDictionary<string, object?> remembered)
        {
            foreach (var (key, value) in remembered)
            {
                if (ReadFailure(value) is { } read)
                {
                    index.Failures[key] = read;
                }
            }
        }

        return index;
    }

    private static CookFailure? ReadFailure(object? value) =>
        value is IReadOnlyDictionary<string, object?> map
            && map.GetValueOrDefault("reason") is string reason
            && TryTime(map.GetValueOrDefault("at"), out var at)
            ? new CookFailure(reason, at)
            : null;

    // Reads one entry, or gives null when it cannot be trusted whole. A single unreadable entry costs
    // one recook. An entry half-read would be a hit that hands back some of a cook, so anything wrong
    // with any output throws the entry away.
    private static Entry? ReadEntry(object? value)
    {
        if (value is not IReadOnlyDictionary<string, object?> map
            || !TryTime(map.GetValueOrDefault("usedAt"), out var usedAt)
            || map.GetValueOrDefault("outputs") is not List<object?> outputs)
        {
            return null;
        }

        var read = new List<CookOutput>();
        foreach (var output in outputs)
        {
            if (ReadOutput(output) is not { } one)
            {
                return null;
            }

            read.Add(one);
        }

        return new Entry(new CookedAsset(read), usedAt);
    }

    private static CookOutput? ReadOutput(object? value)
    {
        if (value is not IReadOnlyDictionary<string, object?> map
            || map.GetValueOrDefault("name") is not string { Length: > 0 } name
            || map.GetValueOrDefault("bytes") is not long bytes
            || bytes < 0
            || map.GetValueOrDefault("hash") is not string text)
        {
            return null;
        }

        try
        {
            return new CookOutput(name, ContentHash.Parse(text), bytes);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static bool TryTime(object? value, out DateTimeOffset at)
    {
        at = default;
        return value is string text && DartText.TryParseTime(text, out at);
    }

    // Writes the index where a reader sees all of it or none of it. The temporary file is named
    // uniquely, so that a hole in the lock costs a stale index rather than a crashed build.
    private async Task WriteIndexAsync(Index index)
    {
        var json = new OrderedDictionary<string, object?>
        {
            ["formatVersion"] = (long)FormatVersion,
            ["entries"] = Ordered(index.Entries, entry => entry.ToJson()),
            ["failures"] = Ordered(index.Failures, failure => new OrderedDictionary<string, object?>
            {
                ["reason"] = failure.Reason,
                ["at"] = DartText.Iso(failure.At),
            }),
        };
        var temporary = $"{IndexPath}.{Environment.ProcessId}.{Interlocked.Increment(ref _writes) - 1}.{Random.Shared.NextInt64(1L << 32):x}.incoming";
        var text = DartJson.Encode(json, "  ") + "\n";
        await AtomicFile.WriteAsync(temporary, IndexPath, System.Text.Encoding.UTF8.GetBytes(text)).ConfigureAwait(false);
    }

    private static OrderedDictionary<string, object?> Ordered<T>(
        OrderedDictionary<string, T> items, Func<T, object?> toJson)
    {
        var ordered = new OrderedDictionary<string, object?>();
        foreach (var key in items.Keys.Order(StringComparer.Ordinal))
        {
            ordered[key] = toJson(items[key]);
        }

        return ordered;
    }

    // What index.json holds: the keys that cooked, and the keys that would not. One file and one lock
    // for both, because they are written together.
    private sealed class Index
    {
        public OrderedDictionary<string, Entry> Entries { get; } = [];

        public OrderedDictionary<string, CookFailure> Failures { get; } = [];
    }

    // One line of the index: what a key cooked to, and when it was last wanted.
    private sealed record Entry(CookedAsset Asset, DateTimeOffset UsedAt)
    {
        public OrderedDictionary<string, object?> ToJson() => new()
        {
            ["usedAt"] = DartText.Iso(UsedAt),
            ["outputs"] = Asset.Outputs
                .Select(output => (object?)new OrderedDictionary<string, object?>
                {
                    ["name"] = output.Name,
                    ["hash"] = output.Hash.Hex,
                    ["bytes"] = output.Bytes,
                })
                .ToList(),
        };
    }
}

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Dir = System.IO.Directory;

namespace Orblit.Save;

/// <summary>
/// The versions of every save, in a directory. Bytes are kept once for each
/// distinct content, gzipped under their SHA-256, and an entry for each version
/// names the blob, so an identical save adds an entry and no copy.
/// </summary>
public sealed class SaveHistory(string directory)
{
    private const int EntryFormat = 1;
    private const string EntrySuffix = ".json";
    private const string BlobSuffix = ".gz";

    /// <summary>
    /// A blob younger than this is kept even when no entry names it, because the write
    /// that made it may not have written its entry yet.
    /// </summary>
    private static readonly TimeSpan BlobGrace = TimeSpan.FromMinutes(1);

    /// <summary>The directory the history is in.</summary>
    public string Directory { get; } = directory;

    /// <summary>Keeps <paramref name="record"/> as a version of <paramref name="key"/>.</summary>
    public async Task RecordAsync(string key, SaveRecord record)
    {
        var hash = HashOf(record.Bytes.Span);
        var blob = BlobPath(hash);
        // shortcut: a prune that deletes this old, unnamed copy after this check
        // leaves the new version without bytes, which load reports. Run prune on
        // a timer and not per save, and add a lock if that ever races.
        if (!File.Exists(blob))
        {
            await WholeFile.WriteAsync(blob, Gzip(record.Bytes.Span)).ConfigureAwait(false);
        }

        var entry = Encoding.UTF8.GetBytes(
            $"{{\"format\":{EntryFormat},\"at\":{record.At.ToUnixTimeMilliseconds()},"
            + $"\"reason\":{SaveJson.Quote(record.Reason.Label)},\"hash\":{SaveJson.Quote(hash)}}}");
        await WholeFile.WriteAsync(EntryPath(key, record.At, hash), entry).ConfigureAwait(false);
    }

    /// <summary>The keys that have a version.</summary>
    public Task<IReadOnlyList<string>> KeysAsync()
    {
        var entries = $"{Directory}/entries";
        if (!Dir.Exists(entries))
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }

        return Task.FromResult<IReadOnlyList<string>>([.. Dir.EnumerateDirectories(entries).Select(path => Path.GetFileName(path))]);
    }

    /// <summary>
    /// The versions of <paramref name="key"/>, newest first. Throws
    /// <see cref="FormatException"/> naming the file when an entry is damaged.
    /// </summary>
    public async Task<IReadOnlyList<HistoryEntry>> ListAsync(string key)
    {
        var dir = EntryDir(key);
        if (!Dir.Exists(dir))
        {
            return [];
        }

        var entries = new List<HistoryEntry>();
        foreach (var file in Dir.EnumerateFiles(dir).Where(path => path.EndsWith(EntrySuffix, StringComparison.Ordinal)))
        {
            entries.Add(await ReadEntryAsync(key, file).ConfigureAwait(false));
        }

        // The order is total, so an unstable sort gives the same list as Dart's.
        entries.Sort((a, b) => a.At != b.At ? b.At.CompareTo(a.At) : string.CompareOrdinal(a.Hash, b.Hash));
        return entries;
    }

    /// <summary>
    /// The version <paramref name="entry"/> names. Throws <see cref="FormatException"/> when
    /// its blob is missing or does not hold what its name says.
    /// </summary>
    public async Task<SaveRecord> LoadAsync(HistoryEntry entry)
    {
        byte[] bytes;
        try
        {
            bytes = Gunzip(await File.ReadAllBytesAsync(BlobPath(entry.Hash)).ConfigureAwait(false));
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new FormatException($"The copy {entry.Hash} is missing");
        }
        catch (InvalidDataException e)
        {
            throw new FormatException($"The copy {entry.Hash} is damaged: {e.Message}", e);
        }

        if (HashOf(bytes) != entry.Hash)
        {
            throw new FormatException($"The copy {entry.Hash} is damaged: wrong hash");
        }

        return new SaveRecord(bytes, entry.Reason, entry.At);
    }

    /// <summary>
    /// Removes the versions <paramref name="spacing"/> drops, then the copies nothing
    /// names. Returns how many versions it removed.
    /// </summary>
    public async Task<int> PruneAsync(Spacing spacing, DateTimeOffset now)
    {
        var removed = 0;
        var named = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in await KeysAsync().ConfigureAwait(false))
        {
            HistoryEntry? kept = null;
            foreach (var entry in await ListAsync(key).ConfigureAwait(false))
            {
                var gap = spacing(now - entry.At);
                if (gap is { } least && (kept is null || kept.At - entry.At >= least))
                {
                    kept = entry;
                    named.Add(entry.Hash);
                }
                else
                {
                    File.Delete(EntryPath(entry.Key, entry.At, entry.Hash));
                    removed++;
                }
            }
        }

        RemoveUnnamed(named, now);
        return removed;
    }

    /// <summary>The SHA-256 of <paramref name="bytes"/> as hex, which names a stored copy.</summary>
    internal static string HashOf(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private string EntryDir(string key)
    {
        SaveKey.Check(key);
        return $"{Directory}/entries/{key}";
    }

    private string BlobPath(string hash) => $"{Directory}/blobs/{hash}{BlobSuffix}";

    private string EntryPath(string key, DateTimeOffset at, string hash) =>
        $"{EntryDir(key)}/{at.ToUnixTimeMilliseconds()}-{hash[..12]}{EntrySuffix}";

    private static async Task<HistoryEntry> ReadEntryAsync(string key, string path)
    {
        try
        {
            var json = SaveJson.Parse(await File.ReadAllBytesAsync(path).ConfigureAwait(false));
            if (SaveJson.HasNumber(json, "format", EntryFormat)
                && SaveJson.TryWhole(json, "at", out var at)
                && SaveJson.TryString(json, "reason", out var reason)
                && SaveJson.TryString(json, "hash", out var hash))
            {
                return new HistoryEntry(key, DateTimeOffset.FromUnixTimeMilliseconds(at), SaveReason.Parse(reason), hash);
            }

            throw new FormatException("not a history entry");
        }
        catch (FormatException e)
        {
            throw new FormatException($"The history entry {path} is damaged: {e.Message}", e);
        }
    }

    private void RemoveUnnamed(HashSet<string> named, DateTimeOffset now)
    {
        var blobs = $"{Directory}/blobs";
        if (!Dir.Exists(blobs))
        {
            return;
        }

        foreach (var file in Dir.EnumerateFiles(blobs).ToList())
        {
            if (IsUnnamedAndOld(file, named, now))
            {
                File.Delete(file);
            }
        }
    }

    private static bool IsUnnamedAndOld(string file, HashSet<string> named, DateTimeOffset now)
    {
        var name = Path.GetFileName(file);
        if (!name.EndsWith(BlobSuffix, StringComparison.Ordinal))
        {
            return false;
        }

        var hash = name[..^BlobSuffix.Length];
        return !named.Contains(hash) && now - File.GetLastWriteTimeUtc(file) >= BlobGrace;
    }

    private static byte[] Gzip(ReadOnlySpan<byte> bytes)
    {
        using var output = new MemoryStream();
        using (var zip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            zip.Write(bytes);
        }

        return output.ToArray();
    }

    private static byte[] Gunzip(byte[] bytes)
    {
        using var zip = new GZipStream(new MemoryStream(bytes), CompressionMode.Decompress);
        using var output = new MemoryStream();
        zip.CopyTo(output);
        return output.ToArray();
    }
}

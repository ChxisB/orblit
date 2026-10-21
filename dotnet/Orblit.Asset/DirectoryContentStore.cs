namespace Orblit.Asset;

/// <summary>
/// A content store in a directory on disk, laid out as <c>&lt;shard&gt;/&lt;hash&gt;</c>.
/// </summary>
/// <remarks>
/// Safe for several writers at once, in one process or several, because no entry is ever written
/// where it is read. <see cref="PutAsync"/> writes into <c>.incoming</c> in the same directory and
/// renames the finished file into place: a reader sees the whole entry or no entry, never half of
/// one. The temporary file lives under the store rather than in the system's temporary directory
/// because that is often a different file system, and a rename across two is a copy.
/// <para>
/// A process killed mid-write leaves its temporary file in <c>.incoming</c>. Nothing ever reads from
/// there, so it is untidy rather than wrong.
/// </para>
/// </remarks>
public sealed class DirectoryContentStore(string rootPath) : IContentStore
{
    private static int _written;

    /// <summary>The directory the store lives in. It need not exist until something is put.</summary>
    public string RootPath { get; } = rootPath;

    /// <summary>
    /// The bytes under <paramref name="hash"/>, checked against it on the way out. A file on disk can
    /// be changed by things that are not this store, and bytes that no longer match their hash are
    /// worse than none, because everything above trusts the hash. An entry that fails is deleted and
    /// reported as absent, which lets the next put of the right bytes write it back.
    /// </summary>
    public async Task<byte[]?> GetAsync(ContentHash hash)
    {
        var path = PathOf(hash);
        byte[] bytes;
        try
        {
            bytes = await File.ReadAllBytesAsync(path).ConfigureAwait(false);
        }
        catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }

        if (ContentHash.Of(bytes) == hash)
        {
            return bytes;
        }

        // Something else may have noticed first and already taken it away.
        await RemoveAsync(hash).ConfigureAwait(false);
        return null;
    }

    /// <summary>
    /// Files <paramref name="bytes"/> under their hash. Bytes already stored are not written again:
    /// the name of an entry is its contents, so a file already under that name already holds them.
    /// Two puts of the same bytes racing each other are fine, and a second rename that fails, as it
    /// can on Windows while the entry is open for reading, is not an error when the entry is there.
    /// </summary>
    public async Task<ContentHash> PutAsync(ReadOnlyMemory<byte> bytes)
    {
        var hash = ContentHash.Of(bytes.Span);
        var target = PathOf(hash);
        if (File.Exists(target))
        {
            return hash;
        }

        var incoming = Path.Combine(RootPath, ".incoming");
        Directory.CreateDirectory(incoming);
        // Unique between processes by the process id and between puts by the count. The random part
        // is for two stores that each start counting from zero in one process.
        var name = $"{hash.Hex}.{Environment.ProcessId}.{Interlocked.Increment(ref _written)}.{Random.Shared.NextInt64(1L << 32):x}";
        try
        {
            await AtomicFile.WriteAsync(Path.Combine(incoming, name), target, bytes).ConfigureAwait(false);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException && File.Exists(target))
        {
            // The entry being there is all that was wanted.
        }

        return hash;
    }

    /// <summary>
    /// Whether there is an entry under <paramref name="hash"/>. Not checked against the hash the way
    /// <see cref="GetAsync"/> is, since that means reading all of it. An entry this says is there can
    /// still turn out damaged when it is read.
    /// </summary>
    public Task<bool> ContainsAsync(ContentHash hash) => Task.FromResult(File.Exists(PathOf(hash)));

    public Task RemoveAsync(ContentHash hash)
    {
        try
        {
            File.Delete(PathOf(hash));
        }
        catch (DirectoryNotFoundException)
        {
            // Already not there, which is what was asked for.
        }

        return Task.CompletedTask;
    }

    private string PathOf(ContentHash hash) => Path.Combine(RootPath, hash.Shard, hash.Hex);
}

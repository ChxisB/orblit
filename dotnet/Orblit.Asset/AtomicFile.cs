namespace Orblit.Asset;

/// <summary>
/// Writes a file so that a reader sees all of it or none of it: into a temporary file, then a
/// rename, which within one file system is atomic where a write over a live file is not.
/// </summary>
internal static class AtomicFile
{
    public static async Task WriteAsync(string temporary, string target, ReadOnlyMemory<byte> bytes)
    {
        try
        {
            if (Path.GetDirectoryName(target) is { Length: > 0 } folder)
            {
                Directory.CreateDirectory(folder);
            }

            var stream = new FileStream(
                temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous);
            await using (stream.ConfigureAwait(false))
            {
                await stream.WriteAsync(bytes).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporary, target, overwrite: true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Discard(temporary);
            throw;
        }
    }

    /// <summary>
    /// <see cref="WriteAsync"/> through a temporary file beside <paramref name="target"/> with a random name,
    /// so a file already there under the temporary name is never written through or destroyed.
    /// </summary>
    public static Task WriteBesideAsync(string target, ReadOnlyMemory<byte> bytes) =>
        WriteAsync($"{target}.{Random.Shared.NextInt64(1L << 32):x}.partial", target, bytes);

    private static void Discard(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Never created, or already moved away.
        }
    }
}

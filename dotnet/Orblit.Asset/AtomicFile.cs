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
            var stream = new FileStream(
                temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous);
            await using (stream.ConfigureAwait(false))
            {
                await stream.WriteAsync(bytes).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            if (Path.GetDirectoryName(target) is { Length: > 0 } folder)
            {
                Directory.CreateDirectory(folder);
            }

            File.Move(temporary, target, overwrite: true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Discard(temporary);
            throw;
        }
    }

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

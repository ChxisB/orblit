namespace Orblit.Save;

internal static class WholeFile
{
    /// <summary>
    /// Writes <paramref name="bytes"/> to <paramref name="path"/> whole or not
    /// at all. The bytes go to a temporary file beside it, are flushed, and
    /// replace the real file by a rename, so a crash leaves the old file.
    /// </summary>
    // shortcut: the directory is not synced after the rename, so a power cut can
    // lose the newest write and keep the one before. Upgrade with a native fsync.
    internal static async Task WriteAsync(string path, ReadOnlyMemory<byte> bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!); // a full path always has a parent
        var temp = path + ".tmp";
        var file = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous);
        await using (file.ConfigureAwait(false))
        {
            await file.WriteAsync(bytes).ConfigureAwait(false);
            await file.FlushAsync().ConfigureAwait(false);
            file.Flush(flushToDisk: true);
        }

        File.Move(temp, path, overwrite: true);
    }
}

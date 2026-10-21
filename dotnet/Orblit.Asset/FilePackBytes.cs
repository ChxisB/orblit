using Microsoft.Win32.SafeHandles;

namespace Orblit.Asset;

/// <summary>
/// A pack read from a file, or from a range of one. <c>start</c> and <c>bytes</c> describe a pack that
/// sits inside another file, which is how an APK holds an uncompressed asset. Offsets a reader asks
/// for are relative to <c>start</c>.
/// </summary>
/// <remarks>
/// One handle serves every read and the reads run one after another. A game asking for a thousand
/// assets at once would run out of handles if each read opened its own.
/// </remarks>
public sealed class FilePackBytes : IPackBytes, IAsyncDisposable
{
    private readonly SemaphoreSlim _turn = new(1, 1);
    private SafeFileHandle? _handle;

    public FilePackBytes(string path, long start = 0, long? bytes = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        if (bytes is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bytes), bytes, "A length cannot be negative.");
        }

        Path = path;
        Start = start;
        Bytes = bytes;
    }

    public string Path { get; }

    /// <summary>Where the pack begins in the file.</summary>
    public long Start { get; }

    /// <summary>How long the pack is, or null for the rest of the file.</summary>
    public long? Bytes { get; }

    public async Task<long> LengthAsync()
    {
        await _turn.WaitAsync().ConfigureAwait(false);
        try
        {
            var available = RandomAccess.GetLength(Handle()) - Start;
            return available < 0 ? 0 : Bytes is { } wanted && wanted <= available ? wanted : available;
        }
        finally
        {
            _turn.Release();
        }
    }

    public async Task<byte[]> ReadAsync(long at, long bytes)
    {
        await _turn.WaitAsync().ConfigureAwait(false);
        try
        {
            return await ReadFromAsync(Handle(), at, bytes).ConfigureAwait(false);
        }
        finally
        {
            _turn.Release();
        }
    }

    /// <summary>Closes the file. A later read opens it again.</summary>
    public async Task CloseAsync()
    {
        await _turn.WaitAsync().ConfigureAwait(false);
        try
        {
            _handle?.Dispose();
            _handle = null;
        }
        finally
        {
            _turn.Release();
        }
    }

    /// <summary>Closes the file for good. After this a read throws.</summary>
    public async ValueTask DisposeAsync()
    {
        await CloseAsync().ConfigureAwait(false);
        _turn.Dispose();
    }

    private SafeFileHandle Handle() =>
        _handle ??= File.OpenHandle(Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, FileOptions.Asynchronous);

    // Reads past the end of the pack but not past the end of the file, as the Dart twin does.
    private async Task<byte[]> ReadFromAsync(SafeFileHandle file, long at, long bytes)
    {
        var from = Start + at;
        if (at < 0 || bytes < 0 || bytes > Array.MaxLength || from + bytes > RandomAccess.GetLength(file))
        {
            throw new ArgumentOutOfRangeException(
                nameof(bytes), $"Cannot read {bytes} bytes at {at} from \"{Path}\", which is {RandomAccess.GetLength(file)} bytes long.");
        }

        var data = new byte[bytes];
        var read = 0;
        while (read < data.Length)
        {
            var step = await RandomAccess.ReadAsync(file, data.AsMemory(read), from + read).ConfigureAwait(false);
            if (step == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bytes), $"Cannot read {bytes} bytes at {at} from \"{Path}\", which gave {read}.");
            }

            read += step;
        }

        return data;
    }
}

namespace Orblit.Asset;

/// <summary>
/// Where a pack's bytes come from. Two questions: how long is it, and what are these bytes. A file, a
/// range of a file, a download held in memory and a pack inside a larger file all answer them, and the
/// pack does not care which.
/// </summary>
public interface IPackBytes
{
    /// <summary>How many bytes there are.</summary>
    Task<long> LengthAsync();

    /// <summary><paramref name="bytes"/> bytes from <paramref name="at"/>. Throws when fewer than that exist.</summary>
    Task<byte[]> ReadAsync(long at, long bytes);
}

/// <summary>
/// A pack held in memory: for tests, for a pack that arrived whole, and for a small pack loaded from a
/// resource. Each read is a copy, as every other asset source hands back fresh bytes.
/// </summary>
public sealed class MemoryPackBytes(byte[] data) : IPackBytes
{
    public Task<long> LengthAsync() => Task.FromResult((long)data.Length);

    public Task<byte[]> ReadAsync(long at, long bytes)
    {
        // The sum wraps, so a range that wraps is refused by the last test.
        var end = unchecked(at + bytes);
        return at < 0 || bytes < 0 || end > data.Length || end < at
            ? Task.FromException<byte[]>(new ArgumentOutOfRangeException(
                nameof(bytes), $"Cannot read {bytes} bytes at {at} from a pack of {data.Length}."))
            : Task.FromResult(data.AsSpan((int)at, (int)bytes).ToArray());
    }
}

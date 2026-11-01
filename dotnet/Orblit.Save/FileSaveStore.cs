using System.Buffers.Binary;
using System.Text;
using Dir = System.IO.Directory;

namespace Orblit.Save;

/// <summary>
/// One file for each key in a directory. A write goes to a temporary file, is
/// flushed, and replaces the real file by a rename, so a crash leaves the old
/// save whole.
/// </summary>
public sealed class FileSaveStore(string directory) : ISaveStore
{
    private const int FileFormat = 1;
    private const string Suffix = ".save";
    private const int PrefixLength = 8;

    private static readonly byte[] Magic = [0x4F, 0x53, 0x41, 0x56]; // OSAV

    /// <summary>The directory the saves are in.</summary>
    public string Directory { get; } = directory;

    /// <inheritdoc />
    public async Task<SaveRecord?> ReadAsync(string key)
    {
        byte[] bytes;
        try
        {
            bytes = await File.ReadAllBytesAsync(PathOf(key)).ConfigureAwait(false);
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }

        try
        {
            return Decode(bytes);
        }
        catch (FormatException e)
        {
            throw new FormatException($"The save \"{key}\" is damaged: {e.Message}", e);
        }
    }

    /// <inheritdoc />
    public Task WriteAsync(string key, SaveRecord record) => WholeFile.WriteAsync(PathOf(key), Encode(record));

    /// <inheritdoc />
    public Task<bool> ExistsAsync(string key) => Task.FromResult(File.Exists(PathOf(key)));

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> KeysAsync()
    {
        if (!Dir.Exists(Directory))
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }

        return Task.FromResult<IReadOnlyList<string>>([
            .. Dir.EnumerateFiles(Directory)
                .Select(path => Path.GetFileName(path))
                .Where(name => name.EndsWith(Suffix, StringComparison.Ordinal))
                .Select(name => name[..^Suffix.Length]),
        ]);
    }

    private string PathOf(string key)
    {
        SaveKey.Check(key);
        return $"{Directory}/{key}{Suffix}";
    }

    private static byte[] Encode(SaveRecord record)
    {
        var header = Encoding.UTF8.GetBytes(
            $"{{\"format\":{FileFormat},\"reason\":{SaveJson.Quote(record.Reason.Label)},\"at\":{record.At.ToUnixTimeMilliseconds()}}}");
        var output = new byte[PrefixLength + header.Length + record.Bytes.Length];
        Magic.CopyTo(output, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(4), (uint)header.Length);
        header.CopyTo(output, PrefixLength);
        record.Bytes.CopyTo(output.AsMemory(PrefixLength + header.Length));
        return output;
    }

    private static SaveRecord Decode(byte[] file)
    {
        if (file.Length < PrefixLength || !file.AsSpan(0, Magic.Length).SequenceEqual(Magic))
        {
            throw new FormatException("not a save file");
        }

        var headerLength = (long)BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(4));
        if (PrefixLength + headerLength > file.Length)
        {
            throw new FormatException("the header is cut short");
        }

        var header = SaveJson.Parse(file.AsMemory(PrefixLength, (int)headerLength));
        if (SaveJson.HasNumber(header, "format", FileFormat)
            && SaveJson.TryString(header, "reason", out var reason)
            && SaveJson.TryWhole(header, "at", out var at))
        {
            return new SaveRecord(
                file.AsSpan(PrefixLength + (int)headerLength),
                SaveReason.Parse(reason),
                DateTimeOffset.FromUnixTimeMilliseconds(at));
        }

        throw new FormatException("the header is not one this version wrote");
    }
}

namespace Orblit.Asset;

/// <summary>
/// Records kept in one <c>records.json</c> in a directory. Written to a file of its own in
/// <c>.incoming</c> and renamed into place, so a reader never sees half a set, which is why the content
/// store does it too.
/// </summary>
public sealed class DirectoryFetchRecords(string rootPath, TimeSpan? settle = null) : BufferedFetchRecords(settle)
{
    private const string Name = "records.json";

    /// <summary>The directory the file lives in. It need not exist until something is written.</summary>
    public string RootPath { get; } = rootPath;

    private string FilePath => Path.Combine(RootPath, Name);

    protected override async Task<string?> ReadTextAsync()
    {
        try
        {
            return TextRules.Decode(await File.ReadAllBytesAsync(FilePath).ConfigureAwait(false));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or FormatException)
        {
            // No file yet, or one that cannot be read. Either way nothing is known about any URL, which is
            // what no text says.
            return null;
        }
    }

    protected override async Task WriteTextAsync(string text)
    {
        var incoming = Directory.CreateDirectory(Path.Combine(RootPath, ".incoming")).FullName;
        var temporary = Path.Combine(incoming, $"{Name}.{Environment.ProcessId}.{Random.Shared.NextInt64(1L << 32):x}.tmp");
        await AtomicFile.WriteAsync(temporary, FilePath, System.Text.Encoding.UTF8.GetBytes(text)).ConfigureAwait(false);
    }
}

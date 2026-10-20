namespace Orblit.Save;

/// <summary>
/// A store that also keeps each write in a <see cref="SaveHistory"/>. Reads go
/// to the store, so a history that is lost loses no current save.
/// </summary>
public sealed class HistoryStore(ISaveStore store, SaveHistory history) : ISaveStore
{
    /// <summary>The store that holds the current saves.</summary>
    public ISaveStore Store { get; } = store;

    /// <summary>The history each write is kept in.</summary>
    public SaveHistory History { get; } = history;

    /// <summary>Saves in <paramref name="directory"/> and their history in <c>directory/history</c>.</summary>
    public static HistoryStore Files(string directory) =>
        new(new FileSaveStore(directory), new SaveHistory($"{directory}/history"));

    /// <inheritdoc />
    public Task<SaveRecord?> ReadAsync(string key) => Store.ReadAsync(key);

    /// <summary>
    /// Writes the save, then its version. A failure of either fails the write, and writing
    /// again is safe, so a queue keeps the save and retries.
    /// </summary>
    public async Task WriteAsync(string key, SaveRecord record)
    {
        await Store.WriteAsync(key, record).ConfigureAwait(false);
        await History.RecordAsync(key, record).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(string key) => Store.ExistsAsync(key);

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> KeysAsync() => Store.KeysAsync();
}

namespace Orblit.Save;

/// <summary>
/// Saves in memory, for tests. A worker writes through it from another thread,
/// so every member takes a lock.
/// </summary>
public sealed class MemorySaveStore : ISaveStore
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, SaveRecord> _records = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public Task<SaveRecord?> ReadAsync(string key)
    {
        SaveKey.Check(key);
        lock (_gate)
        {
            return Task.FromResult(_records.GetValueOrDefault(key));
        }
    }

    /// <inheritdoc />
    public Task WriteAsync(string key, SaveRecord record)
    {
        SaveKey.Check(key);
        lock (_gate)
        {
            _records[key] = record;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(string key)
    {
        SaveKey.Check(key);
        lock (_gate)
        {
            return Task.FromResult(_records.ContainsKey(key));
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> KeysAsync()
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<string>>([.. _records.Keys]);
        }
    }
}

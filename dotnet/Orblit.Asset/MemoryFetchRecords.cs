namespace Orblit.Asset;

/// <summary>
/// Records held in memory, which is the right answer for a tool that runs once and for every test that is
/// not about persistence.
/// </summary>
public sealed class MemoryFetchRecords(IReadOnlyDictionary<WebUrl, FetchRecord>? records = null) : IFetchRecords
{
    // A fetcher asks from several tasks at once, where the Dart one asks from one.
    private readonly Dictionary<WebUrl, FetchRecord> _records = records is null ? [] : new(records);

    public Task<FetchRecord?> GetAsync(WebUrl url)
    {
        lock (_records)
        {
            return Task.FromResult(_records.GetValueOrDefault(url));
        }
    }

    public Task PutAsync(WebUrl url, FetchRecord record)
    {
        lock (_records)
        {
            _records[url] = record;
        }

        return Task.CompletedTask;
    }

    public Task RemoveAsync(WebUrl url)
    {
        lock (_records)
        {
            _records.Remove(url);
        }

        return Task.CompletedTask;
    }

    public Task ClearAsync()
    {
        lock (_records)
        {
            _records.Clear();
        }

        return Task.CompletedTask;
    }
}

namespace Orblit.Asset;

/// <summary>Which bytes each URL was, last anybody looked.</summary>
public interface IFetchRecords
{
    /// <summary>What is known about <paramref name="url"/>, or null when nothing is.</summary>
    Task<FetchRecord?> GetAsync(WebUrl url);

    Task PutAsync(WebUrl url, FetchRecord record);

    Task RemoveAsync(WebUrl url);

    /// <summary>Forgets everything. For a "clear cache" button, and for tests.</summary>
    Task ClearAsync();
}

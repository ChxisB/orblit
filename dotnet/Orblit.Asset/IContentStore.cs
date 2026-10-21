namespace Orblit.Asset;

/// <summary>
/// Bytes filed by their hash. Nothing is stored under a name somebody chose, only under what the
/// bytes are, so putting the same bytes twice stores them once and an entry never needs updating.
/// </summary>
public interface IContentStore
{
    /// <summary>
    /// The bytes stored under <paramref name="hash"/>, or null when there are none. A store is
    /// asked speculatively, so a miss is an ordinary answer.
    /// </summary>
    Task<byte[]?> GetAsync(ContentHash hash);

    /// <summary>Stores <paramref name="bytes"/> and gives the hash they are now filed under.</summary>
    Task<ContentHash> PutAsync(ReadOnlyMemory<byte> bytes);

    /// <summary>Whether anything is stored under <paramref name="hash"/>.</summary>
    Task<bool> ContainsAsync(ContentHash hash);

    /// <summary>Forgets whatever is stored under <paramref name="hash"/>. Nothing there is not an error.</summary>
    Task RemoveAsync(ContentHash hash);
}

using System.Collections.ObjectModel;

namespace Orblit.Asset;

/// <summary>
/// One opened <c>.opack</c> file. A pack is one cooked bundle for one target. The blobs come first and
/// the directory last, so a writer streams and a reader needs only the header and the directory to know
/// what is inside. A pack is an <see cref="IAssetSource"/>, so nothing that reads assets changes when
/// the assets move into one.
/// </summary>
/// <remarks>
/// Blob bytes are not hashed on every read. They are checked when the pack is written and once after a
/// download. <see cref="FindCorruptAsync"/> checks them on demand. A pack read over a network can be
/// given a store. A blob is then filed under its hash the first time it is read and checked against
/// that hash on the way in, and later reads, in this session or the next, come from the store. A
/// rebuilt pack shares the hashes of what did not change, so a returning player fetches only the blobs
/// that did.
/// </remarks>
public sealed class AssetPack : IAssetSource
{
    private readonly IPackBytes _bytes;
    private readonly PackDirectory _directory;
    private readonly IContentStore? _cache;

    private AssetPack(IPackBytes bytes, PackDirectory directory, ContentHash hash, IContentStore? cache)
    {
        _bytes = bytes;
        _directory = directory;
        _cache = cache;
        Hash = hash;
        Manifest = new AssetManifest(new ReadOnlyDictionary<AssetId, AssetEntry>(Ordered.Copy(
            directory.Assets.Select(e => KeyValuePair.Create(e.Key, new AssetEntry(e.Value, directory.Blobs[e.Value].Bytes))))));
    }

    /// <summary>
    /// The hash of the directory. It holds every blob's hash and place, so it stands for the whole pack,
    /// and a patch names its base by it.
    /// </summary>
    public ContentHash Hash { get; }

    /// <summary>Every asset in this pack and which bytes it is.</summary>
    public AssetManifest Manifest { get; }

    /// <summary>The cook target this pack was made for.</summary>
    public string Target => _directory.Target;

    /// <summary>The hash of the pack a patch was built against, or null.</summary>
    public ContentHash? Base => _directory.Base;

    /// <summary>Ids this pack takes away from the packs under it.</summary>
    public IReadOnlyList<AssetId> Removed => _directory.Removed;

    /// <summary>
    /// Opens a pack, throwing a <see cref="FormatException"/> that says what is wrong with it when it
    /// cannot be read whole. With a <paramref name="cache"/>, a blob read from <paramref name="bytes"/>
    /// is kept there by its hash.
    /// </summary>
    public static async Task<AssetPack> OpenAsync(IPackBytes bytes, IContentStore? cache = null)
    {
        var total = await bytes.LengthAsync().ConfigureAwait(false);
        if (total < PackHeader.Size)
        {
            throw new FormatException(
                $"This is not an Orblit pack. It is {total} bytes long and a pack header alone is {PackHeader.Size}.");
        }

        var header = PackHeader.Read(await bytes.ReadAsync(0, PackHeader.Size).ConfigureAwait(false));
        if (header.DirectoryAt < 0 || header.DirectoryBytes < 0)
        {
            throw new FormatException("The header of this pack places its directory past what any file can hold.");
        }

        // Two sizes of 62 bits wrap in 64, so the sum is taken in 128.
        var end = (Int128)header.DirectoryAt + header.DirectoryBytes;
        if (end > total)
        {
            throw new FormatException(
                $"This pack is short by {end - total} bytes. It was probably cut off while it was being copied or downloaded.");
        }

        if (header.DirectoryAt < PackHeader.Size)
        {
            throw new FormatException($"The directory of this pack starts at byte {header.DirectoryAt}, inside the header.");
        }

        var directoryBytes = await bytes.ReadAsync(header.DirectoryAt, header.DirectoryBytes).ConfigureAwait(false);
        return new AssetPack(
            bytes, PackDirectory.Decode(directoryBytes, header.DirectoryAt), ContentHash.Of(directoryBytes), cache);
    }

    /// <summary>
    /// Lets go of the file behind the pack, if it has one. A later read opens it again. Windows will not
    /// delete or replace a file that is open, so a caller that is done with a pack for now closes it.
    /// </summary>
    public Task CloseAsync() => _bytes.CloseAsync();

    public async Task<byte[]> ReadAsync(AssetId id)
    {
        if (!_directory.Assets.TryGetValue(id, out var hash))
        {
            throw new AssetNotFoundException(id);
        }

        var blob = _directory.Blobs[hash];
        return _cache is null
            ? await _bytes.ReadAsync(blob.At, blob.Bytes).ConfigureAwait(false)
            : await ReadThroughAsync(_cache, id, hash, blob).ConfigureAwait(false);
    }

    public Task<bool> ExistsAsync(AssetId id) => Task.FromResult(_directory.Assets.ContainsKey(id));

    /// <summary>
    /// The ids whose bytes no longer match their hash, in order. Reads and hashes every blob. A flipped
    /// byte in a download or on a disk shows up here and nowhere else.
    /// </summary>
    public async Task<IReadOnlyList<AssetId>> FindCorruptAsync()
    {
        var bad = new HashSet<ContentHash>();
        foreach (var (hash, blob) in _directory.Blobs)
        {
            var bytes = await _bytes.ReadAsync(blob.At, blob.Bytes).ConfigureAwait(false);
            if (ContentHash.Of(bytes) != hash)
            {
                bad.Add(hash);
            }
        }

        return Ordered.ById(_directory.Assets.Where(e => bad.Contains(e.Value)).Select(e => e.Key));
    }

    private async Task<byte[]> ReadThroughAsync(IContentStore cache, AssetId id, ContentHash hash, PackBlob blob)
    {
        var held = await cache.GetAsync(hash).ConfigureAwait(false);
        if (held is not null)
        {
            return held;
        }

        var bytes = await _bytes.ReadAsync(blob.At, blob.Bytes).ConfigureAwait(false);

        // Putting hashes the bytes, so its answer is the check and the bytes are hashed once. Bytes that
        // are not the blob's go in under their own hash, which is harmless in a store keyed by contents.
        var stored = await cache.PutAsync(bytes).ConfigureAwait(false);
        return stored != hash
            ? throw new FormatException(
                $"The bytes of {id} do not match the hash in this pack. It wants {hash.Hex} and got {stored.Hex}.")
            : bytes;
    }
}

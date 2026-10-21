namespace Orblit.Asset;

/// <summary>
/// Writes the assets of a manifest as one pack. <c>read</c> gives the bytes of an asset, and what it
/// gives is checked against the manifest's hash and size on the way. A cooked bundle and a kit both
/// write through here, because the layout rules change together.
/// </summary>
internal static class PackWriter
{
    /// <summary>
    /// Writes <paramref name="manifest"/> at <paramref name="outPath"/> for <paramref name="target"/>.
    /// The pack is written beside <paramref name="outPath"/> and renamed into place, so a failure
    /// leaves no half of one behind. <paramref name="added"/> is how many of the assets a base did not
    /// serve, every asset when it is null.
    /// </summary>
    public static async Task<PackSummary> WriteAsync(
        AssetManifest manifest,
        string outPath,
        string target,
        Func<AssetId, Task<byte[]>> read,
        ContentHash? @base = null,
        IReadOnlyList<AssetId>? removed = null,
        int? added = null)
    {
        removed ??= [];
        var layout = Layout.Of(manifest, target, @base, removed);
        var temporary = outPath + ".partial";
        try
        {
            await CopyBlobsAsync(temporary, layout, read).ConfigureAwait(false);
            File.Move(temporary, outPath, overwrite: true);
        }
        catch
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }

            throw;
        }

        var assets = manifest.Entries.Count;
        return new PackSummary
        {
            Hash = ContentHash.Of(layout.DirectoryBytes),
            Bytes = layout.DirectoryAt + layout.DirectoryBytes.Length,
            Assets = assets,
            Added = added ?? assets,
            Replaced = assets - (added ?? assets),
            Blobs = layout.FirstUse.Count,
            SharedBytes = layout.SharedBytes,
            Removed = removed.Count,
        };
    }

    private static async Task CopyBlobsAsync(string temporary, Layout layout, Func<AssetId, Task<byte[]>> read)
    {
        var header = new PackHeader(layout.DirectoryAt, layout.DirectoryBytes.Length);
        await using var sink = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 1, useAsync: true);
        await sink.WriteAsync(header.Encode()).ConfigureAwait(false);
        foreach (var (hash, id) in layout.FirstUse)
        {
            var blob = layout.Blobs[hash];
            await sink.WriteAsync(await ReadCheckedAsync(read, id, hash, blob.Bytes).ConfigureAwait(false)).ConfigureAwait(false);
            var padding = PackFormat.AlignedOffset(blob.End) - blob.End;
            if (padding > 0)
            {
                await sink.WriteAsync(new byte[padding]).ConfigureAwait(false);
            }
        }

        await sink.WriteAsync(layout.DirectoryBytes).ConfigureAwait(false);
        await sink.FlushAsync().ConfigureAwait(false);
    }

    private static async Task<byte[]> ReadCheckedAsync(Func<AssetId, Task<byte[]>> read, AssetId id, ContentHash expected, long expectedBytes)
    {
        var bytes = await read(id).ConfigureAwait(false);
        return bytes.Length != expectedBytes || ContentHash.Of(bytes) != expected
            ? throw new FormatException($"\"{id}\" is not what its manifest says.")
            : bytes;
    }

    // Where every blob goes, worked out from the manifest alone. The sizes and hashes are in the
    // manifest, so the header and the directory are known before a byte is copied and the file is
    // written front to back.
    private sealed class Layout
    {
        private Layout()
        {
        }

        // The first id, in id order, that uses each blob, in file order.
        public required OrderedDictionary<ContentHash, AssetId> FirstUse { get; init; }

        public required OrderedDictionary<ContentHash, PackBlob> Blobs { get; init; }

        public required long SharedBytes { get; init; }

        public required long DirectoryAt { get; init; }

        public required byte[] DirectoryBytes { get; init; }

        public static Layout Of(AssetManifest manifest, string target, ContentHash? @base, IReadOnlyList<AssetId> removed)
        {
            var blobs = new OrderedDictionary<ContentHash, PackBlob>();
            var firstUse = new OrderedDictionary<ContentHash, AssetId>();
            long at = PackHeader.Size;
            long shared = 0;
            foreach (var id in Ordered.ById(manifest.Entries.Keys))
            {
                var entry = manifest.Entries[id];
                if (blobs.ContainsKey(entry.Hash))
                {
                    shared = unchecked(shared + entry.Bytes);
                    continue;
                }

                blobs[entry.Hash] = new PackBlob(at, entry.Bytes);
                firstUse[entry.Hash] = id;
                at = PackFormat.AlignedOffset(unchecked(at + entry.Bytes));
            }

            var assets = manifest.Entries.Select(e => KeyValuePair.Create(e.Key, e.Value.Hash));
            var directory = new PackDirectory(target, @base, removed, Ordered.Copy(assets), blobs, at);
            return new Layout
            {
                FirstUse = firstUse,
                Blobs = blobs,
                SharedBytes = shared,
                DirectoryAt = at,
                DirectoryBytes = directory.Encode(),
            };
        }
    }
}

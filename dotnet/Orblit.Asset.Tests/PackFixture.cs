using System.Text;

namespace Orblit.Asset.Tests;

/// <summary>A pack built for a test, as the file it is, as an opened pack and as the summary the writer gave.</summary>
internal sealed record BuiltPack(byte[] Bytes, AssetPack Pack, PackSummary Summary)
{
    public ContentHash Hash => Pack.Hash;
}

/// <summary>Real packs, made by <see cref="PackBundle"/> in a temporary folder.</summary>
internal sealed class PackFixture : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("orblit_pack_fixture_");

    public void Dispose() => _directory.Delete(recursive: true);

    public string At(string relative) => Path.Combine([_directory.FullName, .. relative.Split('/')]);

    /// <summary>Writes a bundle folder of <paramref name="files"/>, each a path and its text, with the manifest that goes with it.</summary>
    public Task<string> BundleAsync(string folder, IReadOnlyDictionary<string, string> files) =>
        BundleAsync(folder, files.ToDictionary(file => file.Key, file => Encoding.UTF8.GetBytes(file.Value)));

    /// <summary><see cref="BundleAsync(string, IReadOnlyDictionary{string, string})"/> for files that are bytes and not text.</summary>
    public async Task<string> BundleAsync(string folder, IReadOnlyDictionary<string, byte[]> files)
    {
        var token = TestContext.Current.CancellationToken;
        Directory.CreateDirectory(At(folder));
        var entries = new Dictionary<AssetId, AssetEntry>();
        foreach (var (key, bytes) in files)
        {
            var file = At($"{folder}/{key}");
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            await File.WriteAllBytesAsync(file, bytes, token);
            entries[AssetId.Parse(key)] = new AssetEntry(ContentHash.Of(bytes), bytes.Length);
        }

        await File.WriteAllTextAsync(At($"{folder}/manifest.json"), new AssetManifest(entries).Encode(), token);
        return At(folder);
    }

    /// <summary>
    /// Packs <paramref name="files"/> as <c>&lt;name&gt;.opack</c>. With <paramref name="base"/>, the pack
    /// is a patch on those packs and holds only what differs from them.
    /// </summary>
    public async Task<BuiltPack> BuildAsync(
        string name,
        IReadOnlyDictionary<string, string> files,
        IReadOnlyList<AssetPack>? @base = null,
        string target = "android")
    {
        var folder = await BundleAsync($"bundle_{name}", files);
        return await PackFolderAsync(folder, name, @base, target);
    }

    /// <summary>Packs a folder <see cref="BundleAsync"/> wrote as <c>&lt;name&gt;.opack</c>.</summary>
    public async Task<BuiltPack> PackFolderAsync(
        string folder, string name, IReadOnlyList<AssetPack>? @base, string target)
    {
        var summary = await PackBundle.PackAsync(folder, At($"{name}.opack"), CookTargets.Find(target)!, @base);
        var packed = await File.ReadAllBytesAsync(At($"{name}.opack"), TestContext.Current.CancellationToken);
        return new BuiltPack(packed, await AssetPack.OpenAsync(new MemoryPackBytes(packed)), summary);
    }

    /// <summary><see cref="BuildAsync"/> for a test that wants only the opened pack.</summary>
    public async Task<AssetPack> PackAsync(
        string name,
        IReadOnlyDictionary<string, string> files,
        IReadOnlyList<AssetPack>? @base = null,
        string target = "android") =>
        (await BuildAsync(name, files, @base, target)).Pack;
}

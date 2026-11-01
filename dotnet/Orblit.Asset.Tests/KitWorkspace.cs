using System.Text.Json.Nodes;

namespace Orblit.Asset.Tests;

/// <summary>A temporary folder for a test that packs kits, with the folders and projects it fills.</summary>
internal sealed class KitWorkspace : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("orblit_kit_test_");
    private int _made;

    public void Dispose() => _directory.Delete(recursive: true);

    public string At(string name, string? file = null) =>
        file is null ? Path.Combine(_directory.FullName, name) : Path.Combine(_directory.FullName, name, file);

    /// <summary>A name for a file or folder that no earlier call gave.</summary>
    public int Next() => _made++;

    /// <summary>The manifest an author writes, with the three fields a pack adds left out.</summary>
    public static JsonObject Authored() => new()
    {
        ["slug"] = "crate-pack",
        ["publisher"] = "brass-lamp",
        ["name"] = "Crate pack",
        ["summary"] = "Twelve crates.",
        ["version"] = "1.0.0",
        ["licence"] = new JsonObject { ["spdx"] = "MIT", ["url"] = "https://example.com/l" },
        ["engine"] = new JsonObject { ["from"] = "0.30.0", ["to"] = "0.40.0" },
        ["tags"] = new JsonArray(),
        ["previews"] = new JsonArray(),
    };

    /// <summary>Writes <paramref name="files"/>, each a path and its text, under <paramref name="root"/>.</summary>
    public static async Task WriteAsync(string root, Dictionary<string, string> files)
    {
        foreach (var (name, text) in files)
        {
            var file = Path.Combine(root, name);
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            await File.WriteAllTextAsync(file, text, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>A kit folder holding <paramref name="files"/>, each a path and its text, and a <c>kit.json</c>.</summary>
    public async Task<string> FolderAsync(Dictionary<string, string> files, JsonObject? manifest = null)
    {
        var name = At($"kit_{_made++}");
        await WriteAsync(name, files);
        await File.WriteAllTextAsync(Path.Combine(name, "kit.json"), (manifest ?? Authored()).ToJsonString(), TestContext.Current.CancellationToken);
        return name;
    }

    /// <summary>A project folder holding <paramref name="files"/>.</summary>
    public async Task<string> ProjectAsync(Dictionary<string, string> files)
    {
        var root = At($"project_{_made++}");
        Directory.CreateDirectory(root);
        await WriteAsync(root, files);
        return root;
    }

    /// <summary>Packs <paramref name="source"/> and opens the file as a kit.</summary>
    public async Task<Kit> PackedAsync(string source)
    {
        var path = At($"out_{_made++}.opack");
        await KitPacking.PackAsync(source, path);
        return await Kit.OpenAsync(await AssetPack.OpenAsync(new MemoryPackBytes(await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken))));
    }

    /// <summary>A kit holding <paramref name="files"/>.</summary>
    public async Task<Kit> KitOfAsync(Dictionary<string, string> files) => await PackedAsync(await FolderAsync(files));
}

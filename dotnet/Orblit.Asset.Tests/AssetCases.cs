using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Orblit.Json;
using Orblit.Parity;

namespace Orblit.Asset.Tests;

// Reads the specs the fixture exporter wrote, and writes what the port gives in the shape the exporter used.
internal static class AssetCases
{
    // The clock every cache run starts on, and the one the fixture exporter used.
    private static readonly DateTimeOffset Epoch = new(2026, 10, 21, 10, 0, 0, 123, TimeSpan.Zero);

    public static JsonElement In(JsonElement c, int index) => c.GetProperty("in")[index];

    public static string Text(JsonElement c, int index) => In(c, index).GetString()!;

    public static JsonObject IdParts(string text)
    {
        var id = AssetId.Parse(text);
        return new JsonObject
        {
            ["path"] = id.ToString(),
            ["name"] = id.Name,
            ["directory"] = id.Directory,
            ["extension"] = id.Extension,
            ["same"] = AssetId.TryParse(text)?.ToString(),
        };
    }

    public static JsonObject ManifestOut(string text)
    {
        var load = AssetManifest.Read(text);
        return new JsonObject
        {
            ["encoded"] = load.Manifest.Encode(),
            ["problems"] = Dump.Arr(load.Problems, Dump.Text),
            ["count"] = load.Manifest.Entries.Count,
        };
    }

    public static JsonObject SettingsOut(string text)
    {
        var settings = ImportSettings.Parse(text, AssetId.Parse("textures/wall.png.import.json"));
        return new JsonObject
        {
            ["importer"] = settings.Importer,
            ["values"] = Dump.Tree(settings.Values),
            ["encoded"] = settings.Encode(),
        };
    }

    public static JsonObject SettingsNames(string text)
    {
        var id = AssetId.Parse(text);
        return new JsonObject
        {
            ["file"] = ImportSettings.FileFor(id).ToString(),
            ["isSettings"] = ImportSettings.IsSettingsFile(id),
            ["asset"] = ImportSettings.AssetFor(id)?.ToString(),
        };
    }

    // Cook keys.

    public static JsonObject KeyOut(JsonElement spec)
    {
        var key = new CookKey(
            spec.GetProperty("importer").GetString()!,
            spec.GetProperty("version").GetInt32(),
            Hash(spec.GetProperty("source").GetString()!),
            dependencies: spec.GetProperty("deps").EnumerateObject().ToDictionary(
                dep => AssetId.Parse(dep.Name), dep => Hash(dep.Value.GetString()!)),
            settings: Plain(spec.GetProperty("settings")),
            target: Plain(spec.GetProperty("target")));
        return new JsonObject { ["recipe"] = key.Recipe, ["hash"] = key.Hash.Hex };
    }

    // An object as the decoder gives it: whole numbers as long, anything written with a point or an exponent as double.
    private static IReadOnlyDictionary<string, object?> Plain(JsonElement element) =>
        (IReadOnlyDictionary<string, object?>)JsonText.Decode(element.GetRawText())!;

    private static ContentHash Hash(string text) => ContentHash.Of(Encoding.UTF8.GetBytes(text));

    // A cook cache run.

    public static CookKey CacheKey(string name, string os) =>
        new("texture", 1, Hash(name), target: new Dictionary<string, object?> { ["os"] = os });

    // The clock a run reads, moved by the script.
    private sealed class Ticking
    {
        public DateTimeOffset Now { get; set; } = Epoch;
    }

    public static async Task<JsonNode?> CacheRun(JsonElement c)
    {
        var limit = In(c, 0).ValueKind == JsonValueKind.Null ? (long?)null : In(c, 0).GetInt64();
        var root = Directory.CreateTempSubdirectory("orblit_parity_cache_");
        try
        {
            var path = Path.Combine(root.FullName, "cache");
            var clock = new Ticking();
            var cache = new DirectoryCookCache(path, limit, () => clock.Now);
            var stored = new OrderedDictionary<string, CookKey>();
            foreach (var step in In(c, 1).EnumerateArray())
            {
                await Step(cache, stored, step, clock);
            }

            // Looking at an entry marks it used, so the files are read after the looking.
            var hits = new JsonObject();
            var failures = new JsonObject();
            foreach (var (name, key) in stored)
            {
                hits[name] = HitOut(await cache.LookUpAsync(key));
                failures[name] = (await cache.LookUpFailureAsync(key))?.Reason;
            }

            return new JsonObject { ["files"] = Files(path), ["hits"] = hits, ["failures"] = failures };
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    public static JsonNode? HitOut(CookedAsset? hit) => hit is null
        ? null
        : new JsonObject(hit.Outputs.Select(output => KeyValuePair.Create(
            output.Name, (JsonNode?)new JsonArray(output.Hash.Hex, Dump.Big(output.Bytes)))));

    private static async Task Step(
        DirectoryCookCache cache, OrderedDictionary<string, CookKey> stored, JsonElement step, Ticking clock)
    {
        var verb = step[0].GetString();
        if (verb == "tick")
        {
            clock.Now = clock.Now.AddSeconds(step[1].GetInt64());
            return;
        }

        var key = CacheKey(step[1].GetString()!, step[2].GetString()!);
        if (verb == "use")
        {
            await cache.LookUpAsync(key);
            return;
        }

        stored[$"{step[1].GetString()}/{step[2].GetString()}"] = key;
        if (verb == "fail")
        {
            await cache.RecordFailureAsync(key, step[3].GetString()!);
            return;
        }

        await cache.StoreAsync(key, step[3].EnumerateObject().ToDictionary(
            output => output.Name, output => Encoding.UTF8.GetBytes(output.Value.GetString()!)));
    }

    private static JsonObject Files(string root)
    {
        var files = new JsonObject();
        if (!Directory.Exists(root))
        {
            return files;
        }

        var found = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(file => (Name: Path.GetRelativePath(root, file).Replace('\\', '/'), File: file))
            .OrderBy(file => file.Name, StringComparer.Ordinal);
        foreach (var (name, file) in found)
        {
            files[name] = Convert.ToBase64String(File.ReadAllBytes(file));
        }

        return files;
    }
}

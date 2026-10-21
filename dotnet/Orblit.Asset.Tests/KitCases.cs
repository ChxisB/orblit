using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Orblit.Parity;

namespace Orblit.Asset.Tests;

// Reads the kit specs the Dart exporter wrote, and writes what the port gives in the shape the exporter used.
internal static class KitCases
{
    public static JsonObject ManifestOut(byte[] bytes, string mode)
    {
        var manifest = mode switch
        {
            "read" => KitManifest.Read(bytes),
            "authored" => KitManifest.ReadAuthored(bytes, containsScripts: false),
            _ => KitManifest.ReadAuthored(bytes, containsScripts: true),
        };
        return new JsonObject { ["hex"] = Convert.ToHexStringLower(manifest.Encode()), ["scripts"] = manifest.ContainsScripts };
    }

    // A valid manifest followed by spaces up to length bytes, so the size limit is met at its edge.
    public static byte[] Padded(int length) =>
        Encoding.UTF8.GetBytes(KitManifestJson().PadRight(length));

    private static string KitManifestJson() =>
        "{\"kind\":\"orblit.kit\",\"formatVersion\":1,\"containsScripts\":false,\"slug\":\"crate-pack\","
        + "\"publisher\":\"brass-lamp\",\"name\":\"Crate pack\",\"summary\":\"Twelve crates.\",\"version\":\"1.2.0\","
        + "\"licence\":{\"spdx\":\"CC-BY-4.0\",\"url\":\"https://example.com/licence\"},"
        + "\"engine\":{\"from\":\"0.30.0\",\"to\":\"0.40.0\"},\"tags\":[\"props\"],\"previews\":[]}";

    public static JsonNode RulesOut(JsonElement c)
    {
        var options = c.GetProperty("in")[1];
        var scripts = options.GetProperty("scripts").GetBoolean();
        var previews = options.TryGetProperty("previews", out var named)
            ? [.. named.EnumerateArray().Select(name => AssetId.Parse(name.GetString()!))]
            : new List<AssetId>();
        var limit = c.GetProperty("in")[2];
        var kit = new KitManifest(
            "crate-pack",
            "brass-lamp",
            "Crate pack",
            "Twelve crates.",
            "1.0.0",
            new KitLicence("MIT", "https://example.com/l"),
            new KitEngine("0.30.0", "0.40.0"),
            scripts,
            previews: previews);
        var problems = limit.ValueKind == JsonValueKind.Null
            ? KitRules.Problems(kit, Files(c.GetProperty("in")[0]))
            : KitRules.Problems(kit, Files(c.GetProperty("in")[0]), Size(limit));
        return Dump.Arr(problems, Dump.Text);
    }

    // A list of files: a name, a name and a size, or one object that asks for that many small files.
    private static Dictionary<AssetId, long> Files(JsonElement spec)
    {
        var files = new Dictionary<AssetId, long>();
        if (spec.GetArrayLength() == 1 && spec[0].ValueKind == JsonValueKind.Object)
        {
            for (var i = 0; i < spec[0].GetProperty("count").GetInt32(); i++)
            {
                files[AssetId.Parse($"f/{i}.bin")] = 1;
            }

            return files;
        }

        foreach (var entry in spec.EnumerateArray())
        {
            if (entry.ValueKind == JsonValueKind.String)
            {
                files[AssetId.Parse(entry.GetString()!)] = 10;
            }
            else
            {
                files[AssetId.Parse(entry[0].GetString()!)] = Size(entry[1]);
            }
        }

        return files;
    }

    // A size as the exporter's big() wrote it: a number, or text past 2^53.
    private static long Size(JsonElement value) =>
        value.ValueKind == JsonValueKind.String
            ? long.Parse(value.GetString()!, CultureInfo.InvariantCulture)
            : value.GetInt64();

    public static JsonNode? PlaceProblemOut(JsonElement c) =>
        JsonValue.Create(KitRules.PlaceProblem(
            c.GetProperty("in")[0].GetString()!,
            c.GetProperty("in")[1].EnumerateArray().Select(name => AssetId.Parse(name.GetString()!)).ToList()));

    public static JsonNode PlaceOut(JsonElement c) =>
        JsonValue.Create(KitRules.Place(AssetId.Parse(c.GetProperty("in")[0].GetString()!), c.GetProperty("in")[1].GetString()!).ToString())!;

    public static JsonNode FitsOut(JsonElement c) =>
        JsonValue.Create(new KitEngine(
            c.GetProperty("in")[0].GetString()!, c.GetProperty("in")[1].GetString()!).Fits(c.GetProperty("in")[2].GetString()!));

    public static JsonNode ScriptsOut(string name)
    {
        var id = AssetId.Parse(name);
        return new JsonObject { ["script"] = KitRules.IsScript(id), ["carries"] = KitRules.CarriesScripts([id]) };
    }

    public static JsonNode UpdateOut(JsonElement c)
    {
        var plan = KitUpdatePlan.Plan(
            Hashes(c.GetProperty("in")[0]),
            Hashes(c.GetProperty("in")[1]),
            c.GetProperty("in")[2].EnumerateObject().ToDictionary(
                e => AssetId.Parse(e.Name),
                e => e.Value.ValueKind == JsonValueKind.Null ? null : (ContentHash?)Hash(e.Value.GetString()!)));
        return new JsonObject
        {
            ["replace"] = Names(plan.Replace),
            ["add"] = Names(plan.Add),
            ["remove"] = Names(plan.Remove),
            ["kept"] = Names(plan.Kept),
            ["empty"] = plan.IsEmpty,
        };
    }

    private static Dictionary<AssetId, ContentHash> Hashes(JsonElement map) =>
        map.EnumerateObject().ToDictionary(e => AssetId.Parse(e.Name), e => Hash(e.Value.GetString()!));

    private static ContentHash Hash(string text) => ContentHash.Of(Encoding.UTF8.GetBytes(text));

    private static JsonArray Names(IEnumerable<AssetId> ids) => Dump.Arr(ids, id => Dump.Text(id.ToString()));
}

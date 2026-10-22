using System.Text.Json;
using System.Text.Json.Nodes;
using Orblit.Json;
using Orblit.Parity;
using static Orblit.Asset.Tests.AssetCases;

namespace Orblit.Asset.Tests;

// Writes what the port gives for the net exporter's cases, in the shape the exporter used.
internal static class NetCases
{
    public static JsonNode UrlOut(WebUrl url) => new JsonObject
    {
        ["scheme"] = url.Scheme,
        ["hasAuthority"] = url.HasAuthority,
        ["userInfo"] = url.UserInfo,
        ["host"] = url.Host,
        ["port"] = url.Port ?? -1,
        ["path"] = url.Path,
        ["hasQuery"] = url.Query is not null,
        ["query"] = url.Query ?? "",
        ["hasFragment"] = url.Fragment is not null,
        ["fragment"] = url.Fragment ?? "",
        ["text"] = url.ToString(),
    };

    public static JsonNode? ParseOut(JsonElement c) => UrlOut(WebUrl.Parse(Text(c, 0)));

    public static JsonNode? ResolveOut(JsonElement c) =>
        UrlOut(WebUrl.Parse(Text(c, 0)).Resolve(WebUrl.Parse(Text(c, 1))));

    public static JsonNode? EncodeOut(JsonElement c) => TextRules.EncodeComponent(Text(c, 0));

    // The fixture reasons name members in camel case, so the two names that differ only in case are compared as the fixture spells them.
    public static string AsFixtureSpells(string reason) =>
        reason.Replace("FetchPolicy.Hosts", "FetchPolicy.hosts").Replace("AllowInsecure", "allowInsecure");

    public static JsonNode? Origin(JsonElement c, Func<AssetOrigin, JsonNode?> use)
    {
        var options = In(c, 1);
        var policy = new FetchPolicy(
            options.GetProperty("hosts").EnumerateArray().Select(h => h.GetString()!),
            options.GetProperty("insecure").GetBoolean());
        try
        {
            return use(AssetOrigin.Parse(Text(c, 0), policy));
        }
        catch (FetchRefusedException refused)
        {
            return new JsonObject { ["refused"] = AsFixtureSpells(refused.Reason) };
        }
    }

    public static JsonNode? OriginParseOut(JsonElement c) => Origin(c, o => new JsonObject
    {
        ["base"] = o.Base.ToString(),
        ["host"] = o.Base.Host,
        ["path"] = o.Base.Path,
    });

    public static JsonNode? OriginUrlOut(JsonElement c) =>
        Origin(c, o => new JsonObject { ["url"] = o.UrlOf(AssetId.Parse(Text(c, 2))).ToString() });

    public static JsonNode? OriginBesideOut(JsonElement c) =>
        Origin(c, o => new JsonObject { ["url"] = o.Beside(WebUrl.Parse(Text(c, 2)), Text(c, 3)).ToString() });

    public static JsonNode? OriginAllowsOut(JsonElement c) =>
        Origin(c, o => new JsonObject { ["allows"] = o.Allows(WebUrl.Parse(Text(c, 2))) });

    public static JsonNode? OriginRequireOut(JsonElement c) =>
        Origin(c, o =>
        {
            o.RequireAllowed(WebUrl.Parse(Text(c, 2)));
            return new JsonObject { ["allowed"] = true };
        });

    // A record is written as the fixture exporter did: nothing known is an empty object.
    public static JsonNode RecordOut(FetchRecord? record) => record is null
        ? new JsonObject()
        : new JsonObject
        {
            ["hash"] = record.Hash.Hex,
            ["bytes"] = Dump.Big(record.Bytes),
            ["etag"] = record.ETag,
            ["fetched"] = record.Fetched is { } at ? TextRules.Iso(at) : null,
            ["micros"] = record.Fetched is { } when ? Dump.Big((when.UtcTicks - DateTime.UnixEpoch.Ticks) / 10) : null,
            ["utc"] = record.Fetched is { } zoned ? zoned.Offset == TimeSpan.Zero : null,
        };

    public static JsonNode? RecordParseOut(JsonElement c) => RecordOut(FetchRecord.FromJson(JsonText.Decode(Text(c, 0))));

    private static Dictionary<WebUrl, FetchRecord> RecordsOf(JsonElement spec) =>
        spec.EnumerateArray().ToDictionary(
            entry => WebUrl.Parse(entry[0].GetString()!),
            entry => RecordOf(entry[1]));

    private static FetchRecord RecordOf(JsonElement spec) => new(
        ContentHash.Parse(spec.GetProperty("hash").GetString()!),
        spec.GetProperty("bytes").GetInt64(),
        spec.TryGetProperty("etag", out var etag) ? etag.GetString() : null,
        spec.TryGetProperty("fetched", out var fetched) && fetched.ValueKind == JsonValueKind.String
            ? FetchedAt(fetched.GetString()!)
            : null);

    private static DateTimeOffset FetchedAt(string text) =>
        TextRules.TryParseTime(text, out var at) ? at : throw new FormatException(text);

    public static JsonNode? RecordsEncodeOut(JsonElement c) => FetchRecordsCodec.Encode(RecordsOf(In(c, 0)));

    public static JsonNode? RecordsDecodeOut(JsonElement c) =>
        Dump.Arr(FetchRecordsCodec.Decode(Text(c, 0)), pair => new JsonArray(pair.Key.ToString(), RecordOut(pair.Value)));

    public static JsonNode? RecordsReencodeOut(JsonElement c) =>
        FetchRecordsCodec.Encode(FetchRecordsCodec.Decode(Text(c, 0)));

    public static JsonNode? PictureOut(JsonElement c) =>
        PictureSize.Of(Convert.FromBase64String(Text(c, 0))) is not { } size
            ? new JsonObject()
            : new JsonObject
            {
                ["width"] = Dump.Big(size.Width),
                ["height"] = Dump.Big(size.Height),
                ["depth"] = Dump.Big(size.Depth),
                ["layers"] = Dump.Big(size.Layers),
                ["faces"] = Dump.Big(size.Faces),
                ["levels"] = Dump.Big(size.Levels),
                ["pixels"] = Dump.Big(size.Pixels),
                ["text"] = size.ToString(),
            };

    private static readonly long[] AskedSizes = [0, 1, 2, 3, 4, 8, 16, 31, 32, 33, 60, 64, 65, 100, 128, 255, 256, 257, 512, 4096];

    public static JsonNode? Ktx2Out(JsonElement c)
    {
        var bytes = Convert.FromBase64String(Text(c, 1));
        if (Ktx2Chain.Read(bytes) is not { } chain)
        {
            return new JsonObject();
        }

        var asked = new JsonObject();
        foreach (var size in AskedSizes)
        {
            asked[size.ToString(System.Globalization.CultureInfo.InvariantCulture)] = chain.LevelAtLeast(size);
        }

        return new JsonObject
        {
            ["width"] = Dump.Big(chain.Width),
            ["height"] = Dump.Big(chain.Height),
            ["depth"] = Dump.Big(chain.Depth),
            ["supercompression"] = Dump.Big(chain.Supercompression),
            ["metadataEnd"] = Dump.Big(chain.MetadataEnd),
            ["levels"] = Dump.Arr(
                chain.Levels,
                level => new JsonArray(
                    Dump.Big(level.Offset), Dump.Big(level.Length), Dump.Big(level.UncompressedLength), Dump.Big(level.End))),
            ["levelAtLeast"] = asked,
            ["bytesFor"] = Dump.Arr(Enumerable.Range(0, chain.Levels.Count + 1), level => Dump.Big(chain.BytesFor(level))),
            ["prefixes"] = Dump.Arr(
                Enumerable.Range(-1, chain.Levels.Count + 3),
                level => chain.Prefix(bytes, level) is { } cut
                    ? new JsonArray(Dump.Big(cut.Length), ContentHash.Of(cut).Hex)
                    : null),
        };
    }

    // The file the queue script reads from: a byte that depends on where it is.
    public static byte[] FileBytes(long at, long bytes) =>
        [.. Enumerable.Range(0, (int)bytes).Select(i => (byte)(((at + i) * 7) + 3))];

    // A script with no delay runs with the 2 ms the others use, because a send cannot wait for the loop that
    // made the wants to end. A zero delay has its own tests.
    private static RangeBatching BatchingOf(JsonElement spec)
    {
        var delay = spec.GetProperty("delay").GetInt32();
        return new RangeBatching
        {
            MergeGap = spec.GetProperty("gap").GetInt64(),
            MaxBatch = spec.GetProperty("batch").GetInt64(),
            Parallel = spec.GetProperty("parallel").GetInt32(),
            Delay = TimeSpan.FromMilliseconds(delay == 0 ? 2 : delay),
        };
    }

    public static JsonNode? QueueNewOut(JsonElement c)
    {
        _ = new RangeQueue((at, bytes) => Task.FromResult(new byte[bytes]), BatchingOf(In(c, 0)));
        return true;
    }

    public static async Task<JsonNode?> QueueOut(JsonElement c)
    {
        var calls = new List<(long At, long Bytes)>();
        var failing = In(c, 2).EnumerateArray().Select(index => index.GetInt32()).ToList();
        var queue = new RangeQueue(
            (at, bytes) =>
            {
                lock (calls)
                {
                    calls.Add((at, bytes));
                    return failing.Contains(calls.Count - 1)
                        ? Task.FromException<byte[]>(new InvalidOperationException("no"))
                        : Task.FromResult(FileBytes(at, bytes));
                }
            },
            BatchingOf(In(c, 0)));

        var wants = In(c, 1).EnumerateArray().ToList();
        var pending = wants.Select(want => queue.WantAsync(
            want[0].GetInt64(), want[1].GetInt64(), want[2].GetString() == "later" ? RangePriority.Later : RangePriority.Now)).ToList();
        var got = new List<byte[]?>();
        foreach (var reply in pending)
        {
            got.Add(await Answer(reply));
        }

        return new JsonObject
        {
            ["calls"] = Dump.Arr(calls, call => new JsonArray(call.At, call.Bytes)),
            ["answers"] = Dump.Arr(got, bytes => bytes is null ? "failed" : new JsonArray(bytes.Length, ContentHash.Of(bytes).Hex)),
            ["matches"] = Dump.Arr(
                Enumerable.Range(0, wants.Count),
                i => got[i] is { } bytes && bytes.AsSpan().SequenceEqual(FileBytes(wants[i][0].GetInt64(), wants[i][1].GetInt64()))),
        };
    }

    private static async Task<byte[]?> Answer(Task<byte[]> reply)
    {
        try
        {
            return await reply;
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            return null;
        }
    }

    // The transport scripts: one page of a hundred bytes counting up, and requests sent to it in order.
    private static readonly byte[] Page = [.. Enumerable.Range(0, 100).Select(i => (byte)i)];

    private static readonly WebUrl PageUrl = WebUrl.Parse("https://e.example.com/a.bin");

    public static async Task<JsonNode?> BodyOut(FetchReply reply)
    {
        var chunks = new JsonArray();
        var all = new List<byte>();
        string? failure = null;
        try
        {
            await foreach (var chunk in reply.Body)
            {
                chunks.Add(chunk.Length);
                all.AddRange(chunk);
            }
        }
        catch (SocketFailureException error)
        {
            failure = error.Reason;
        }

        return new JsonObject
        {
            ["status"] = reply.Status,
            ["etag"] = reply.ETag,
            ["length"] = reply.Length,
            ["from"] = reply.From,
            ["notModified"] = reply.IsNotModified,
            ["mayWorkLater"] = reply.MayWorkLater,
            ["chunks"] = chunks,
            ["hash"] = ContentHash.Of([.. all]).Hex,
            ["size"] = all.Count,
            ["failure"] = failure,
        };
    }

    public static async Task<JsonNode?> TransportOut(JsonElement c)
    {
        var spec = In(c, 0);
        using var transport = new MapTransport(new Dictionary<WebUrl, TransportPage>
        {
            [PageUrl] = new TransportPage(
                Page,
                etag: spec.TryGetProperty("etag", out var etag) ? etag.GetString() : null,
                cut: spec.TryGetProperty("cut", out var cut) ? cut.GetInt32() : 0,
                stopAfter: spec.TryGetProperty("stopAfter", out var stop) ? stop.GetInt32() : null,
                faults: [.. FaultsOf(spec)]),
        });
        var replies = new JsonArray();
        foreach (var request in In(c, 1).EnumerateArray())
        {
            replies.Add(await Send(transport, request));
        }

        return new JsonObject
        {
            ["replies"] = replies,
            ["sent"] = Dump.Arr(transport.Sent, s => new JsonArray(s.Url.ToString(), s.IfNoneMatch, s.From, s.Through)),
        };
    }

    private static IEnumerable<Fault> FaultsOf(JsonElement spec) =>
        spec.TryGetProperty("faults", out var faults)
            ? faults.EnumerateArray().Select(f => f.ValueKind == JsonValueKind.Number ? Fault.OfStatus(f.GetInt32()) : Fault.Thrown())
            : [];

    private static async Task<JsonNode?> Send(MapTransport transport, JsonElement request)
    {
        var target = request[0].GetString() == "missing" ? WebUrl.Parse("https://e.example.com/missing.bin") : PageUrl;
        try
        {
            return await BodyOut(await transport.SendAsync(new FetchRequest(
                target,
                ifNoneMatch: request[1].GetString(),
                from: request[2].ValueKind == JsonValueKind.Null ? null : request[2].GetInt64(),
                through: request[3].ValueKind == JsonValueKind.Null ? null : request[3].GetInt64())));
        }
        catch (SocketFailureException error)
        {
            return new JsonObject { ["threw"] = error.Reason };
        }
    }
}

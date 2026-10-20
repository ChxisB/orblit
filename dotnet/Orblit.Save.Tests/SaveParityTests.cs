using System.IO.Compression;
using System.Text.Json;
using Orblit.Parity;

namespace Orblit.Save.Tests;

public sealed class SaveParityTests : IDisposable
{
    private static readonly Fixture Dart = Fixture.Load("orblit_save", "save");

    private readonly TempDir _dir = new();

    public void Dispose() => _dir.Dispose();

    private static List<JsonElement> Cases(string op)
    {
        var cases = Dart.Cases.Where(c => c.GetProperty("op").GetString() == op).ToList();
        Assert.True(cases.Count > 0, $"the fixture has no {op} cases");
        return cases;
    }

    private static string Why(JsonElement c, string what)
    {
        var text = c.GetRawText();
        return $"{what}, case {text[..Math.Min(text.Length, 160)]}, from Dart commit {Dart.DartCommit}";
    }

    // Text arrives as UTF-16 code units, so a lone surrogate survives the fixture.
    private static string Units(JsonElement units) =>
        new([.. units.EnumerateArray().Select(u => (char)u.GetInt32())]);

    private static byte[] Hex(JsonElement text) => Convert.FromHexString(text.GetString()!);

    private static string HexOf(byte[] bytes) => Convert.ToHexStringLower(bytes);

    private static string Kind(SaveReason reason) => reason switch
    {
        Autosave => "autosave",
        Leave => "leave",
        Disconnect => "disconnect",
        Shutdown => "shutdown",
        Failed => "failed",
        Custom => "custom",
        _ => throw new InvalidOperationException($"unknown reason {reason}"),
    };

    // The words Dart's classifier looks for. The C# messages say the same things.
    private static string Classify(Exception e) => e switch
    {
        FormatException f when f.Message.Contains("not a save file", StringComparison.Ordinal) => "notSave",
        FormatException f when f.Message.Contains("cut short", StringComparison.Ordinal) => "cutShort",
        FormatException f when f.Message.Contains("not one this version wrote", StringComparison.Ordinal) => "header",
        FormatException f when f.Message.Contains("Not a save reason", StringComparison.Ordinal) => "reasonLabel",
        FormatException => "json",
        ArgumentException => "argument",
        _ => throw new InvalidOperationException($"unexpected {e}"),
    };

    private static string ResultOf(Action act)
    {
        try
        {
            act();
            return "ok";
        }
        catch (FormatException)
        {
            return "format";
        }
        catch (ArgumentException)
        {
            return "argument";
        }
    }

    [Fact]
    public void ReasonParseMatchesDart()
    {
        foreach (var c in Cases("reasonParse"))
        {
            var label = Units(c.GetProperty("label"));
            SaveReason? reason = null;
            var result = ResultOf(() => reason = SaveReason.Parse(label));

            Assert.True(c.GetProperty("result").GetString() == result, Why(c, $"result was {result}"));
            if (result != "ok")
            {
                continue;
            }

            Assert.NotNull(reason);
            Assert.True(c.GetProperty("kind").GetString() == Kind(reason), Why(c, "kind"));
            var text = c.GetProperty("text");
            var expected = text.ValueKind == JsonValueKind.Null ? null : Units(text);
            Assert.True(expected == (reason as Custom)?.Text, Why(c, "text"));
            Assert.True(Units(c.GetProperty("roundTrip")) == reason.Label, Why(c, "label"));
        }
    }

    [Fact]
    public void ReasonCustomMatchesDart()
    {
        foreach (var c in Cases("reasonCustom"))
        {
            var text = Units(c.GetProperty("text"));
            Custom? custom = null;
            var result = ResultOf(() => custom = new Custom(text));

            Assert.True(c.GetProperty("result").GetString() == result, Why(c, $"result was {result}"));
            if (result == "ok")
            {
                Assert.NotNull(custom);
                Assert.True(Units(c.GetProperty("label")) == custom.Label, Why(c, "label"));
            }
        }
    }

    [Fact]
    public void KeyCheckMatchesDart()
    {
        foreach (var c in Cases("key"))
        {
            var key = Units(c.GetProperty("key"));

            var result = ResultOf(() => SaveKey.Check(key));

            Assert.True(c.GetProperty("result").GetString() == result, Why(c, $"result was {result}"));
        }
    }

    [Fact]
    public async Task FileWritesAreByteIdenticalToDartsAndReadBack()
    {
        var store = new FileSaveStore(_dir.Sub("saves"));
        foreach (var c in Cases("file"))
        {
            var key = c.GetProperty("key").GetString()!;
            var reason = SaveReason.Parse(Units(c.GetProperty("reason")));
            var at = Support.At(c.GetProperty("at").GetInt64());
            var bytes = Hex(c.GetProperty("bytes"));

            await store.WriteAsync(key, new SaveRecord(bytes, reason, at));

            var path = $"{_dir.Sub("saves")}/{key}.save";
            var written = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
            Assert.True(c.GetProperty("file").GetString() == HexOf(written), Why(c, "the file"));
            var back = await store.ReadAsync(key);
            Assert.NotNull(back);
            Assert.True(bytes.SequenceEqual(back.Bytes.ToArray()), Why(c, "bytes back"));
            Assert.True(reason == back.Reason && at == back.At, Why(c, "reason and time back"));
            File.Delete(path);
        }
    }

    [Fact]
    public async Task FileReadsClassifyLikeDart()
    {
        var store = new FileSaveStore(_dir.Sub("saves"));
        Directory.CreateDirectory(_dir.Sub("saves"));
        var path = $"{_dir.Sub("saves")}/ana.save";
        foreach (var c in Cases("fileRead"))
        {
            await File.WriteAllBytesAsync(path, Hex(c.GetProperty("file")), TestContext.Current.CancellationToken);

            string result;
            SaveRecord? record = null;
            try
            {
                record = await store.ReadAsync("ana");
                result = "ok";
            }
            catch (Exception e) when (e is FormatException or ArgumentException)
            {
                result = Classify(e);
            }

            Assert.True(c.GetProperty("result").GetString() == result, Why(c, $"result was {result}"));
            if (result == "ok")
            {
                Assert.NotNull(record);
                Assert.True(c.GetProperty("bytes").GetString() == HexOf(record.Bytes.ToArray()), Why(c, "bytes"));
                Assert.True(Units(c.GetProperty("reason")) == record.Reason.Label, Why(c, "reason"));
                Assert.True(c.GetProperty("at").GetInt64() == record.At.ToUnixTimeMilliseconds(), Why(c, "time"));
            }
        }
    }

    [Fact]
    public async Task HistoryRecordsMatchDartsAndDartsLoad()
    {
        var n = 0;
        foreach (var c in Cases("record"))
        {
            var key = c.GetProperty("key").GetString()!;
            var reason = SaveReason.Parse(Units(c.GetProperty("reason")));
            var at = Support.At(c.GetProperty("at").GetInt64());
            var bytes = Hex(c.GetProperty("bytes"));
            var plain = c.GetProperty("plain").GetString()!;
            var root = _dir.Sub($"c{n++}");

            await new SaveHistory($"{root}/history").RecordAsync(key, new SaveRecord(bytes, reason, at));

            var entry = Assert.Single(Directory.GetFiles($"{root}/history/entries/{key}"));
            var blob = Assert.Single(Directory.GetFiles($"{root}/history/blobs"));
            Assert.True(c.GetProperty("entryName").GetString() == Path.GetFileName(entry), Why(c, "entry name"));
            Assert.True(
                c.GetProperty("entryText").GetString() == await File.ReadAllTextAsync(entry, TestContext.Current.CancellationToken),
                Why(c, "entry text"));
            Assert.True(c.GetProperty("blobName").GetString() == Path.GetFileName(blob), Why(c, "blob name"));
            Assert.True(plain == HexOf(Gunzip(await File.ReadAllBytesAsync(blob, TestContext.Current.CancellationToken))), Why(c, "our blob"));
            Assert.True(plain == HexOf(Gunzip(Hex(c.GetProperty("blob")))), Why(c, "Dart's blob"));

            await LoadDartsAsync(c, key, new SaveRecord(bytes, reason, at), _dir.Sub($"d{n}"));
        }
    }

    private static async Task LoadDartsAsync(JsonElement c, string key, SaveRecord expected, string root)
    {
        Directory.CreateDirectory($"{root}/history/entries/{key}");
        Directory.CreateDirectory($"{root}/history/blobs");
        await File.WriteAllTextAsync(
            $"{root}/history/entries/{key}/{c.GetProperty("entryName").GetString()}",
            c.GetProperty("entryText").GetString(),
            TestContext.Current.CancellationToken);
        await File.WriteAllBytesAsync(
            $"{root}/history/blobs/{c.GetProperty("blobName").GetString()}",
            Hex(c.GetProperty("blob")),
            TestContext.Current.CancellationToken);
        var history = new SaveHistory($"{root}/history");

        var loaded = await history.LoadAsync(Assert.Single(await history.ListAsync(key)));

        Assert.True(expected.Bytes.Span.SequenceEqual(loaded.Bytes.Span), Why(c, "Dart's bytes load"));
        Assert.True(expected.Reason == loaded.Reason && expected.At == loaded.At, Why(c, "Dart's reason and time load"));
    }

    private static byte[] Gunzip(byte[] packed)
    {
        using var zip = new GZipStream(new MemoryStream(packed), CompressionMode.Decompress);
        using var output = new MemoryStream();
        zip.CopyTo(output);
        return output.ToArray();
    }

    [Fact]
    public async Task EntryReadsMatchDart()
    {
        var history = new SaveHistory(_dir.Sub("history"));
        var folder = _dir.Sub("history/entries/ana");
        Directory.CreateDirectory(folder);
        foreach (var c in Cases("entry"))
        {
            var file = $"{folder}/1000-aaaaaaaaaaaa.json";
            await File.WriteAllTextAsync(file, c.GetProperty("text").GetString(), TestContext.Current.CancellationToken);

            string result;
            HistoryEntry? entry = null;
            try
            {
                entry = Assert.Single(await history.ListAsync("ana"));
                result = "ok";
            }
            catch (FormatException)
            {
                result = "format";
            }
            catch (ArgumentException)
            {
                result = "argument";
            }

            Assert.True(c.GetProperty("result").GetString() == result, Why(c, $"result was {result}"));
            if (result == "ok")
            {
                Assert.NotNull(entry);
                Assert.True(c.GetProperty("at").GetInt64() == entry.At.ToUnixTimeMilliseconds(), Why(c, "time"));
                Assert.True(Units(c.GetProperty("reason")) == entry.Reason.Label, Why(c, "reason"));
                Assert.True(c.GetProperty("hash").GetString() == entry.Hash, Why(c, "hash"));
            }

            File.Delete(file);
        }
    }

    [Fact]
    public void SpacingMatchesDart()
    {
        foreach (var c in Cases("spacing"))
        {
            var gap = Spacings.Default(TimeSpan.FromMilliseconds(c.GetProperty("age").GetInt64()));

            var expected = c.GetProperty("gap");
            if (expected.ValueKind == JsonValueKind.Null)
            {
                Assert.True(gap is null, Why(c, "no gap"));
            }
            else
            {
                Assert.True(gap is { } found && (long)found.TotalMilliseconds == expected.GetInt64(), Why(c, "gap"));
            }
        }
    }

    [Fact]
    public void AutosaveClaimsAndDrawsMatchDart()
    {
        foreach (var c in Cases("autosave"))
        {
            var random = new Scripted(c.GetProperty("seed").GetInt32());
            var auto = new AutoSaver(
                c.GetProperty("perTick").GetInt32(),
                c.GetProperty("every").GetInt32(),
                c.GetProperty("jitter").GetInt32(),
                random);
            var ticks = c.GetProperty("ticks").EnumerateArray().Select(t => t.GetInt64()).ToList();
            var lives = c.GetProperty("live").EnumerateArray().ToList();
            var claims = c.GetProperty("claims").EnumerateArray().ToList();

            for (var i = 0; i < ticks.Count; i++)
            {
                var live = lives[i].EnumerateArray().Select(k => k.GetString()!).ToList();

                var picked = auto.Claim(live, ticks[i]);

                var expected = claims[i].EnumerateArray().Select(k => k.GetString()!).ToList();
                Assert.True(expected.SequenceEqual(picked), Why(c, $"claims at tick {ticks[i]}"));
            }

            var draws = c.GetProperty("draws").EnumerateArray().Select(d => d.GetInt32()).ToList();
            Assert.True(draws.SequenceEqual(random.Draws), Why(c, "draws"));
        }
    }

    [Fact]
    public void AutosaveRefusalsMatchDart()
    {
        foreach (var c in Cases("autosaveRefuse"))
        {
            var result = ResultOf(() => _ = new AutoSaver(
                c.GetProperty("perTick").GetInt32(),
                c.GetProperty("every").GetInt32(),
                c.GetProperty("jitter").GetInt32(),
                new Scripted(1)));

            Assert.True(c.GetProperty("result").GetString() == result, Why(c, $"result was {result}"));
        }
    }

    /// <summary>The random numbers of the Dart exporter: the same generator, and every bound recorded.</summary>
    private sealed class Scripted(int seed) : Random
    {
        private long _seed = seed;

        public List<int> Draws { get; } = [];

        public override int Next(int maxValue)
        {
            Draws.Add(maxValue);
            _seed = ((_seed * 1103515245L) + 12345L) & 0x7fffffffL;
            return (int)((_seed >> 8) % maxValue);
        }
    }
}

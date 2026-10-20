using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Orblit.Parity;
using Orblit.VectorMath;
using static Orblit.Parity.Dump;
using static Orblit.Sprite.Tests.SpriteDump;

namespace Orblit.Sprite.Tests;

public class SpriteParityTests
{
    private static void Check(string op, Func<JsonElement, JsonNode?> run) =>
        Fixture.Load("orblit_sprite", "sprite").Check(op, run);

    private static long Long(JsonElement value) =>
        value.ValueKind == JsonValueKind.String ? long.Parse(value.GetString()!, CultureInfo.InvariantCulture) : value.GetInt64();

    private static double Double(JsonElement value) => Fixture.Number(value);

    private static IEnumerable<double> Doubles(JsonElement list) => list.EnumerateArray().Select(Double);

    private static string? NullableText(JsonElement value) =>
        value.ValueKind == JsonValueKind.Null ? null : value.GetString();

    private static Region RegionOf(JsonElement r) => new(
        r.GetProperty("name").GetString()!,
        Long(r.GetProperty("x")),
        Long(r.GetProperty("y")),
        Long(r.GetProperty("width")),
        Long(r.GetProperty("height")),
        r.GetProperty("rotated").GetBoolean(),
        r.GetProperty("trimmed").GetBoolean(),
        Long(r.GetProperty("offsetX")),
        Long(r.GetProperty("offsetY")),
        Long(r.GetProperty("sourceWidth")),
        Long(r.GetProperty("sourceHeight")));

    private static Atlas AtlasOfNames(IEnumerable<string> names)
    {
        var regions = new OrderedDictionary<string, Region>();
        var index = 0;
        foreach (var name in names)
        {
            regions[name] = new Region(name, index++, 0, 1, 1);
        }

        return new Atlas("a.png", regions);
    }

    private static Region Named(string name) => new(name, 0, 0, 1, 1);

    // the atlas

    [Fact]
    public void AtlasReadMatchesTheDartTwin() => Check("atlasRead", c =>
        Attempt(() => Atlas(Orblit.Sprite.Atlas.Read(c.GetProperty("text").GetString()!, NullableText(c.GetProperty("image"))))));

    [Fact]
    public void SequenceMatchesTheDartTwin() => Check("sequence", c =>
    {
        var atlas = AtlasOfNames(c.GetProperty("names").EnumerateArray().Select(n => n.GetString()!));
        return Arr(atlas.Sequence(c.GetProperty("prefix").GetString()!), r => Text(r.Name));
    });

    [Fact]
    public void GridMatchesTheDartTwin() => Check("grid", c => Attempt(() => Atlas(Orblit.Sprite.Atlas.Grid(
        c.GetProperty("image").GetString()!,
        Long(c.GetProperty("imageWidth")),
        Long(c.GetProperty("imageHeight")),
        Long(c.GetProperty("cellWidth")),
        Long(c.GetProperty("cellHeight")),
        c.GetProperty("name").GetString()!,
        c.GetProperty("count").GetInt32(),
        Long(c.GetProperty("spacing")),
        Long(c.GetProperty("margin"))))));

    [Fact]
    public void RegionMatchesTheDartTwin() => Check("region", c =>
    {
        var region = RegionOf(c.GetProperty("region"));
        var uv = region.Uv(c.GetProperty("imageWidth").GetInt64(), c.GetProperty("imageHeight").GetInt64());
        return new JsonObject
        {
            ["placedWidth"] = Big(region.PlacedSize.Width),
            ["placedHeight"] = Big(region.PlacedSize.Height),
            ["u0"] = Num(uv.U0),
            ["v0"] = Num(uv.V0),
            ["u1"] = Num(uv.U1),
            ["v1"] = Num(uv.V1),
        };
    });

    [Fact]
    public void WriteAtlasMatchesTheDartTwin() => Check("writeAtlas", c =>
    {
        var regions = new OrderedDictionary<string, Region>();
        var names = c.GetProperty("names").EnumerateArray().Select(n => n.GetString()!).ToList();
        for (var i = 0; i < names.Count; i++)
        {
            regions[names[i]] = new Region(names[i], i, i * 2, 3, 4, i % 2 == 0, i % 3 == 0, i, -i, 7, 9);
        }

        var atlas = new Atlas(c.GetProperty("image").GetString()!, regions, Long(c.GetProperty("width")), Long(c.GetProperty("height")));
        return Text(AtlasPacker.WriteAtlas(atlas));
    });

    // tiles

    [Fact]
    public void TilesetMatchesTheDartTwin() => Check("tileset", c =>
    {
        var set = new Tileset(
            c.GetProperty("name").GetString()!,
            "x.png",
            Long(c.GetProperty("firstId")),
            Long(c.GetProperty("tileWidth")),
            Long(c.GetProperty("tileHeight")),
            Long(c.GetProperty("columns")),
            Long(c.GetProperty("count")),
            Long(c.GetProperty("spacing")),
            Long(c.GetProperty("margin")));
        return Arr(
            c.GetProperty("ids").EnumerateArray().Select(Long),
            id => new JsonObject { ["has"] = set.Has(id), ["rect"] = Rect(set.RectOf(id)) });
    });

    [Fact]
    public void TileMapMatchesTheDartTwin() => Check("tileMap", c => Attempt(() => TileMapDump(c)));

    private static JsonObject? TileMapDump(JsonElement c)
    {
        var map = TileMap.Read(c.GetProperty("text").GetString()!);
        if (map is null)
        {
            return null;
        }

        var probes = ProbesFor(map, c.GetProperty("extraProbes"));
        return new JsonObject
        {
            ["width"] = Big(map.Width),
            ["height"] = Big(map.Height),
            ["tileWidth"] = Big(map.TileWidth),
            ["tileHeight"] = Big(map.TileHeight),
            ["properties"] = Tree(map.Properties),
            ["probes"] = Arr(probes, p => Arr(p, Big)),
            ["layers"] = Arr(map.Layers, LayerDump),
            ["tilesets"] = Arr(map.Tilesets, SetDump),
            ["layerAt"] = Arr(map.Layers, layer => Arr(probes, p => Attempt(() => CellDump(layer, p)))),
            ["layerIndex"] = Arr(map.Layers.Select(l => l.Name).Append("missing"), name => Big(IndexOf(map, name))),
            ["layerMissing"] = map.Layer("missing") is null,
            ["setFor"] = Arr(c.GetProperty("ids").EnumerateArray().Select(Long), id => map.SetFor(id) is { } set ? Text(set.Name) : null),
            ["placeOf"] = Arr(c.GetProperty("ids").EnumerateArray().Select(Long), id => Attempt(() => Rect(map.PlaceOf(id, 3, -2)))),
            ["cellAt"] = Arr(c.GetProperty("doubles").EnumerateArray(), p => Attempt(() => CellAtDump(map, p))),
            ["solid"] = Arr(c.GetProperty("onlys").EnumerateArray(), only => Arr(probes, p => Attempt(() => map.IsSolidAt(p[0], p[1], OnlyOf(only))))),
        };
    }

    private static List<long[]> ProbesFor(TileMap map, JsonElement extra)
    {
        var probes = extra.EnumerateArray().Select(p => new[] { p[0].GetInt64(), p[1].GetInt64() }).ToList();
        foreach (var l in map.Layers)
        {
            probes.Add([l.Left, l.Top]);
            probes.Add([l.Left + l.Width - 1, l.Top + l.Height - 1]);
            probes.Add([l.Left + l.Width, l.Top + l.Height]);
            probes.Add([l.Left - 1, l.Top - 1]);
        }

        return probes;
    }

    private static HashSet<string>? OnlyOf(JsonElement only) =>
        only.ValueKind == JsonValueKind.Null ? null : only.EnumerateArray().Select(n => n.GetString()!).ToHashSet();

    // The first layer of that name, or the first layer, as Dart's indexOf finds it by identity.
    private static long IndexOf(TileMap map, string name)
    {
        var found = map.Layer(name) ?? map.Layers[0];
        for (var i = 0; i < map.Layers.Count; i++)
        {
            if (ReferenceEquals(map.Layers[i], found))
            {
                return i;
            }
        }

        throw new InvalidOperationException("the layer is not in the map");
    }

    private static JsonObject CellDump(TileLayer layer, long[] p)
    {
        var flips = layer.FlipsAt(p[0], p[1]);
        return new JsonObject
        {
            ["at"] = Big(layer.At(p[0], p[1])),
            ["empty"] = layer.IsEmptyAt(p[0], p[1]),
            ["h"] = flips.Horizontal,
            ["v"] = flips.Vertical,
            ["d"] = flips.Diagonal,
        };
    }

    private static JsonArray CellAtDump(TileMap map, JsonElement p)
    {
        var cell = map.CellAt(Double(p[0]), Double(p[1]));
        return new JsonArray(Big(cell.X), Big(cell.Y));
    }

    private static JsonObject LayerDump(TileLayer l) => new()
    {
        ["name"] = l.Name,
        ["width"] = Big(l.Width),
        ["height"] = Big(l.Height),
        ["left"] = Big(l.Left),
        ["top"] = Big(l.Top),
        ["visible"] = l.Visible,
        ["opacity"] = Num(l.Opacity),
        ["tiles"] = Arr(l.Tiles, Big),
    };

    private static JsonObject SetDump(Tileset s) => new()
    {
        ["name"] = s.Name,
        ["image"] = s.Image,
        ["firstId"] = Big(s.FirstId),
        ["tileWidth"] = Big(s.TileWidth),
        ["tileHeight"] = Big(s.TileHeight),
        ["columns"] = Big(s.Columns),
        ["count"] = Big(s.Count),
        ["spacing"] = Big(s.Spacing),
        ["margin"] = Big(s.Margin),
    };

    // animation

    private static JsonObject AnimationDump(SpriteAnimation a, IReadOnlyList<double> times) => new()
    {
        ["length"] = a.Length,
        ["isEmpty"] = a.IsEmpty,
        ["duration"] = Num(a.Duration),
        ["cycle"] = Num(a.Cycle),
        ["frames"] = Arr(a.Frames, f => new JsonObject { ["name"] = f.Region.Name, ["seconds"] = Num(f.Seconds) }),
        ["samples"] = Arr(times, t => new JsonObject
        {
            ["index"] = a.IndexAt(t),
            ["frame"] = a.At(t) is { } region ? Text(region.Name) : null,
            ["done"] = a.DoneBy(t),
        }),
    };

    [Fact]
    public void AnimationMatchesTheDartTwin() => Check("animation", c =>
    {
        var seconds = Doubles(c.GetProperty("seconds")).ToList();
        var frames = seconds.Select((s, i) => new Frame(Named($"f{i}"), s)).ToList();
        var animation = new SpriteAnimation(frames, c.GetProperty("loop").GetBoolean(), c.GetProperty("pingPong").GetBoolean());
        return AnimationDump(animation, [.. Doubles(c.GetProperty("times"))]);
    });

    [Fact]
    public void AnimationFromAnAtlasMatchesTheDartTwin() => Check("animationFrom", c =>
    {
        var atlas = AtlasOfNames(c.GetProperty("names").EnumerateArray().Select(n => n.GetString()!));
        var animation = SpriteAnimation.From(
            atlas,
            c.GetProperty("prefix").GetString()!,
            Double(c.GetProperty("fps")),
            c.GetProperty("loop").GetBoolean(),
            c.GetProperty("pingPong").GetBoolean());
        return AnimationDump(animation, [.. Doubles(c.GetProperty("times"))]);
    });

    [Fact]
    public void FlipbookMatchesTheDartTwin() => Check("flipbook", c =>
    {
        var clips = new OrderedDictionary<string, SpriteAnimation>();
        foreach (var clip in c.GetProperty("clips").EnumerateArray())
        {
            var name = clip.GetProperty("name").GetString()!;
            var frames = Doubles(clip.GetProperty("seconds")).Select((s, i) => new Frame(Named($"{name}_{i}"), s)).ToList();
            clips[name] = new SpriteAnimation(frames, clip.GetProperty("loop").GetBoolean(), clip.GetProperty("pingPong").GetBoolean());
        }

        var book = new Flipbook(clips, NullableText(c.GetProperty("playing")));
        var states = new JsonArray(FlipbookState(book));
        foreach (var step in c.GetProperty("steps").EnumerateArray())
        {
            if (step.GetProperty("do").GetString() == "advance")
            {
                book.Advance(Double(step.GetProperty("seconds")));
            }
            else
            {
                book.Play(step.GetProperty("name").GetString()!, step.TryGetProperty("restart", out var restart) && restart.GetBoolean());
            }

            states.Add(FlipbookState(book));
        }

        return states;
    });

    private static JsonObject FlipbookState(Flipbook book) => new()
    {
        ["playing"] = book.Playing,
        ["since"] = Num(book.Since),
        ["frame"] = book.Frame is { } frame ? Text(frame.Name) : null,
        ["done"] = book.Done,
    };

    // parallax and the view

    [Fact]
    public void ParallaxMatchesTheDartTwin() => Check("parallax", c =>
    {
        var layers = c.GetProperty("layers").EnumerateArray().Select(l => new Layer(
            "x",
            Double(l.GetProperty("depth")),
            Double(l.GetProperty("size")),
            Double(l.GetProperty("offset")),
            Double(l.GetProperty("drift")))).ToList();
        var parallax = new Parallax(layers);
        var points = c.GetProperty("points").EnumerateArray().Select(p => (Camera: Double(p[0]), Seconds: Double(p[1]))).ToList();
        var across = Doubles(c.GetProperty("across")).ToList();
        return new JsonObject
        {
            ["length"] = parallax.Length,
            ["at"] = Arr(points, p => Arr(parallax.At(p.Camera, p.Seconds), Num)),
            ["placeOf"] = Arr(points, p => Arr(Enumerable.Range(-1, layers.Count + 2), i => Num(parallax.PlaceOf(i, p.Camera, p.Seconds)))),
            ["copies"] = Arr(layers, l => Arr(across, a => Attempt(() => JsonValue.Create(Parallax.CopiesFor(l, a))))),
        };
    });

    [Fact]
    public void ViewMatchesTheDartTwin() => Check("view", c =>
    {
        var bounds = c.GetProperty("bounds");
        var limits = bounds.ValueKind == JsonValueKind.Null
            ? null
            : (ViewBounds?)new ViewBounds(
                new Vector2(Double(bounds[0]), Double(bounds[1])),
                new Vector2(Double(bounds[2]), Double(bounds[3])));
        var view = new View2(Double(c.GetProperty("width")), Double(c.GetProperty("height")), limits);
        var states = new JsonArray(Place(view));
        foreach (var step in c.GetProperty("steps").EnumerateArray())
        {
            states.Add(Attempt(() => Step(view, step)));
        }

        return states;
    });

    private static JsonArray Place(View2 view) => new JsonArray(Num(view.At.X), Num(view.At.Y));

    private static JsonArray Step(View2 view, JsonElement step)
    {
        var target = new Vector2(Double(step.GetProperty("x")), Double(step.GetProperty("y")));
        if (step.GetProperty("do").GetString() == "jump")
        {
            view.JumpTo(target);
        }
        else
        {
            view.Follow(target, Double(step.GetProperty("seconds")), step.TryGetProperty("ease", out var ease) ? Double(ease) : 6);
        }

        return Place(view);
    }

    // the packer

    private static AtlasPackOptions OptionsOf(JsonElement o) => new(
        maxPageSize: Option(o, "maxPageSize", 2048),
        minPageSize: Option(o, "minPageSize", 64),
        padding: Option(o, "padding", 2),
        border: Option(o, "border", 0),
        allowRotation: Flag(o, "allowRotation", false),
        trim: Flag(o, "trim", true),
        trimAlphaThreshold: Option(o, "trimAlphaThreshold", 0),
        extrude: Option(o, "extrude", 1),
        powerOfTwo: Flag(o, "powerOfTwo", true),
        square: Flag(o, "square", false),
        mergeDuplicates: Flag(o, "mergeDuplicates", true),
        heuristic: o.TryGetProperty("heuristic", out var h) ? Enum.Parse<MaxRectsHeuristic>(h.GetString()!, ignoreCase: true) : null);

    private static long Option(JsonElement o, string name, long fallback) =>
        o.TryGetProperty(name, out var value) ? value.GetInt64() : fallback;

    private static bool Flag(JsonElement o, string name, bool fallback) =>
        o.TryGetProperty(name, out var value) ? value.GetBoolean() : fallback;

    private static string Lower(MaxRectsHeuristic heuristic) =>
        char.ToLowerInvariant(heuristic.ToString()[0]) + heuristic.ToString()[1..];

    [Fact]
    public void PackAtlasMatchesTheDartTwin() => Check("pack", c =>
    {
        var sprites = c.GetProperty("sprites").EnumerateArray().Select(s => new AtlasSprite(
            s.GetProperty("name").GetString()!,
            s.GetProperty("width").GetInt64(),
            s.GetProperty("height").GetInt64(),
            Convert.FromBase64String(s.GetProperty("pixels").GetString()!))).ToList();
        var options = c.GetProperty("options");
        var result = AtlasPacker.PackAtlas(sprites, OptionsOf(options));
        return new JsonObject
        {
            ["heuristic"] = Lower(result.Heuristic),
            ["problems"] = Arr(result.Problems, p => new JsonObject { ["name"] = p.Name, ["reason"] = p.Reason }),
            ["imageNames"] = Arr(result.ToAtlasSet().Pages, a => Text(a.Image)),
            ["pages"] = Arr(result.Pages.Select((page, i) => (page, i)), PageDump),
        };
    });

    private static JsonObject PageDump((AtlasPackPage Page, int Index) entry)
    {
        var (page, index) = entry;
        return new JsonObject
        {
            ["width"] = Big(page.Width),
            ["height"] = Big(page.Height),
            ["fillRatio"] = Num(page.FillRatio),
            ["pixels"] = Base64(page.Pixels),
            ["regions"] = Arr(page.Regions.Values, r => SpriteDump.Region(r)),
            ["atlasText"] = AtlasPacker.WriteAtlas(page.ToAtlas($"p{index}.png")),
            ["extracted"] = Arr(
                page.Regions.Values,
                r => Text(Base64(AtlasPacker.ExtractRegionPixels(page.Width, page.Height, page.Pixels, r)))),
        };
    }

    // PNG

    [Fact]
    public void PngDecodeMatchesTheDartTwin() => Check("pngDecode", c => Attempt(() =>
    {
        var decoded = PngCodec.Decode(Convert.FromBase64String(c.GetProperty("png").GetString()!));
        return new JsonObject
        {
            ["width"] = Big(decoded.Width),
            ["height"] = Big(decoded.Height),
            ["pixels"] = Base64(decoded.Pixels),
        };
    }));

    [Fact]
    public void PngEncodeMatchesTheDartTwin() => Check("pngEncode", c =>
    {
        var png = PngCodec.Encode(
            c.GetProperty("width").GetInt64(),
            c.GetProperty("height").GetInt64(),
            Convert.FromBase64String(c.GetProperty("rgba").GetString()!));
        return Structure(png);
    });

    [Fact]
    public void PngEncodeRefusesTheSameInputsAsTheDartTwin() => Check("pngEncodeRefused", c => Attempt(() =>
        Text(Base64(PngCodec.Encode(
            c.GetProperty("width").GetInt64(),
            c.GetProperty("height").GetInt64(),
            new byte[c.GetProperty("length").GetInt32()])))));

    // The compressed bytes are the library's own, so what is compared is the header, the
    // inflated rows and the checksums around them.
    private static JsonObject Structure(byte[] png)
    {
        var types = new List<string>();
        var idat = new MemoryStream();
        byte[] ihdr = [];
        byte[] iend = [];
        var offset = 8;
        while (offset + 8 <= png.Length)
        {
            var length = (int)System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(offset));
            var type = System.Text.Encoding.ASCII.GetString(png, offset + 4, 4);
            var whole = png.AsSpan(offset, 12 + length).ToArray();
            types.Add(type);
            switch (type)
            {
                case "IHDR":
                    ihdr = whole;
                    break;
                case "IDAT":
                    idat.Write(png, offset + 8, length);
                    break;
                case "IEND":
                    iend = whole;
                    break;
            }

            offset += 12 + length;
        }

        idat.Position = 0;
        var raw = new MemoryStream();
        using (var inflate = new ZLibStream(idat, CompressionMode.Decompress))
        {
            inflate.CopyTo(raw);
        }

        return new JsonObject
        {
            ["chunks"] = Arr(types, Text),
            ["ihdr"] = Base64(ihdr),
            ["raw"] = Base64(raw.ToArray()),
            ["iend"] = Base64(iend),
        };
    }
}

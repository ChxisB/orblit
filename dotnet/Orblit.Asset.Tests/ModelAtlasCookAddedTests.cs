using Orblit.Sprite;
using static Orblit.Asset.Tests.ModelAtlasImporterTests;

namespace Orblit.Asset.Tests;

/// <summary>
/// The cook half of the model atlas, run through a stand-in for <c>orblit_texture_cook</c>. The ported tests only
/// reach it through the real tool, which most machines do not have built.
/// </summary>
public sealed class ModelAtlasCookAddedTests : NativeToolWork
{
    private static readonly int[] Green = [40, 200, 40, 255];
    private static readonly double[] Tiled = [0, 0, 2, 0, 2, 2, 0, 2];
    private static readonly AssetId Model = AssetId.Parse("models/thing.glb");

    // Writes <stem>.ktx2 and <stem>.bc.ktx2 for whatever it was asked to cook, and when a decode is wanted
    // writes `decoded` there instead. `decoded` is printf octal, so a test can hand back any bytes it likes.
    private NativeTool Cooker(string? decoded = null, bool echo = false, bool once = false)
    {
        List<string> lines =
        [
            $"record='{Record}'",
            "for a in \"$@\"; do printf '%s\\n' \"$a\"; done | { cat; printf '\\n'; } >> \"$record\"",
        ];
        if (decoded is not null)
        {
            lines.Add("prev=''");
            lines.Add("for a in \"$@\"; do");
            var guard = once ? $"[ ! -e \"{Record}.once\" ] && {{ printf '{decoded}' > \"$a\"; : > \"{Record}.once\"; }}" : $"printf '{decoded}' > \"$a\"";
            lines.Add($"  if [ \"$prev\" = \"--decode\" ]; then {guard}; exit 0; fi");
            lines.Add("  prev=$a");
            lines.Add("done");
        }

        lines.Add("name=$(basename \"$1\")");
        lines.Add("stem=${name%.*}");
        lines.Add("mkdir -p \"$2\"");
        lines.Add(echo
            ? "cp \"$1\" \"$2/$stem.ktx2\""
            : "printf 'cooked %s' \"$name\" > \"$2/$stem.ktx2\"");
        lines.Add("printf 'cooked %s' \"$name\" > \"$2/$stem.bc.ktx2\"");
        return Script("cooker.sh", string.Join('\n', lines), "cooker");
    }

    private static string Octal(params byte[] bytes) =>
        string.Concat(bytes.Select(value => $"\\{Convert.ToString(value, 8).PadLeft(3, '0')}"));

    private static byte[] Png(int[] rgba)
    {
        var pixels = new byte[8 * 8 * 4];
        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = (byte)rgba[i % 4];
        }

        return PngCodec.Encode(8, 8, pixels);
    }

    private static ImportRequest Request(
        byte[] bytes,
        CookTarget? target = null,
        IReadOnlyDictionary<AssetId, byte[]>? assets = null,
        IReadOnlyDictionary<string, object?>? settings = null) =>
        new()
        {
            Id = Model,
            Bytes = bytes,
            Settings = settings ?? AtlasSettings(),
            Target = target ?? CookTargets.Macos,
            Source = new MemoryAssetSource(assets),
        };

    private static CookTarget Target(long? size, params string[] families)
    {
        var properties = new Dictionary<string, object?>();
        if (size is { } limit)
        {
            properties["maxTextureSize"] = limit;
        }

        if (families.Length > 0)
        {
            properties["textureFamilies"] = families;
        }

        return new CookTarget("test", properties);
    }

    private static async Task<GltfDocument> ReadAsync(ImportResult result) =>
        await GltfDocument.ReadAsync(Model, result.Outputs["glb"], new MemoryAssetSource());

    /// <summary>Two quads that pack, and a third drawn with tiling coordinates, which the atlas must leave alone.</summary>
    private static QuadModel WithATiledMaterial(Func<QuadModel, GltfMap> material)
    {
        var model = TwoMaterials();
        model.Quad(model.Material(material(model)), x: 4, uv: Tiled);
        return model;
    }

    private static GltfMap Image(QuadModel model, int index) => ((List<object?>)model.Json["images"]!)[index] as GltfMap
        ?? throw new InvalidOperationException("no such image");

    private static void Detach(GltfMap image, string? uri, string? name = null, string? mimeType = null)
    {
        image.Remove("bufferView");
        image.Remove("mimeType");
        if (uri is not null)
        {
            image["uri"] = uri;
        }

        if (name is not null)
        {
            image["name"] = name;
        }

        if (mimeType is not null)
        {
            image["mimeType"] = mimeType;
        }
    }

    [ShellFact]
    public async Task CooksEachPageWithTheFlagsItsRoleCallsFor()
    {
        var result = await new GltfImporter(new ModelAtlasCook(Cooker())).ImportAsync(Request(TwoMaterials().ToGlb()));

        var run = OnlyRun();
        Assert.EndsWith("atlas0.basecolour.png", run[0], StringComparison.Ordinal);
        Assert.EndsWith("cooked" + Path.DirectorySeparatorChar, run[1], StringComparison.Ordinal);
        Assert.Equal(
            ["--targets", "basis,bc", "--srgb", "--max-size", "16384", "--uastc", "2", "--zstd", "19", "--quiet"],
            run.Skip(2));
        Assert.Equal(["atlas0.basecolour.bc.ktx2", "atlas0.basecolour.ktx2", "glb"], result.Outputs.Keys.Order());
        Assert.Equal("cooked atlas0.basecolour.png", System.Text.Encoding.UTF8.GetString(result.Outputs["atlas0.basecolour.ktx2"]));

        // The two source images were swallowed, so only the page is left and the model names it.
        var document = await ReadAsync(result);
        var image = Assert.Single(document.List("images"));
        Assert.Equal("atlas0.basecolour.ktx2", image["uri"]);
        Assert.Single(document.List("textures"));
    }

    [ShellFact]
    public async Task CooksANormalPageAsLinearWithTheNormalFlag()
    {
        var model = new QuadModel();
        foreach (var (colour, x) in new[] { (Red, 0), (Blue, 2) })
        {
            var material = BaseColour(model.Texture(colour));
            material["normalTexture"] = new GltfMap { ["index"] = model.Texture(Green) };
            model.Quad(model.Material(material), x: x);
        }

        await new GltfImporter(new ModelAtlasCook(Cooker())).ImportAsync(Request(model.ToGlb()));

        var runs = Runs().ToDictionary(run => Path.GetFileName(run[0]));
        Assert.Contains("--srgb", runs["atlas0.basecolour.png"]);
        Assert.DoesNotContain("--normal", runs["atlas0.basecolour.png"]);
        Assert.Contains("--linear", runs["atlas0.normal.png"]);
        Assert.Contains("--normal", runs["atlas0.normal.png"]);
        Assert.DoesNotContain("--srgb", runs["atlas0.normal.png"]);
    }

    [ShellFact]
    public async Task CooksAnOcclusionPageAsSingleChannel()
    {
        var model = new QuadModel();
        foreach (var (colour, x) in new[] { (Red, 0), (Blue, 2) })
        {
            var material = BaseColour(model.Texture(colour));
            material["occlusionTexture"] = new GltfMap { ["index"] = model.Texture(Green) };
            model.Quad(model.Material(material), x: x);
        }

        await new GltfImporter(new ModelAtlasCook(Cooker())).ImportAsync(Request(model.ToGlb()));

        var occlusion = Runs().Single(run => Path.GetFileName(run[0]).Contains("occlusion", StringComparison.Ordinal));
        Assert.Contains("--single-channel", occlusion);
        Assert.Contains("--linear", occlusion);
        Assert.DoesNotContain("--normal", occlusion);
    }

    private Task<ImportResult> Run(QuadModel model, NativeTool? tool = null, IReadOnlyDictionary<AssetId, byte[]>? assets = null) =>
        new GltfImporter(new ModelAtlasCook(tool ?? Cooker())).ImportAsync(Request(model.ToGlb(), assets: assets));

    [ShellFact]
    public async Task CooksWhatTheAtlasLeftAloneAndPointsTheModelAtTheResult()
    {
        var result = await Run(WithATiledMaterial(model => BaseColour(model.Texture(Green))));

        // The page first, then the texture the page did not take.
        Assert.Equal(["atlas0.basecolour.png", "image0.png"], Runs().Select(run => Path.GetFileName(run[0])));
        Assert.Equal(
            ["atlas0.basecolour.bc.ktx2", "atlas0.basecolour.ktx2", "glb", "image0.bc.ktx2", "image0.ktx2"],
            result.Outputs.Keys.Order());

        var document = await ReadAsync(result);
        var images = document.List("images");
        Assert.Equal(2, images.Count);
        Assert.Equal("image0.ktx2", images[0]["uri"]);
        Assert.Equal("image/ktx2", images[0]["mimeType"]);
        Assert.False(images[0].ContainsKey("bufferView"));

        var textures = document.List("textures");
        Assert.False(textures[0].ContainsKey("source"));
        var basis = (GltfMap)((GltfMap)textures[0]["extensions"]!)["KHR_texture_basisu"]!;
        Assert.Equal(0L, basis["source"]);
    }

    [ShellFact]
    public async Task CooksALeftoverNormalMapAsLinear()
    {
        var model = WithATiledMaterial(model =>
        {
            var material = BaseColour(model.Texture(Green));
            material["normalTexture"] = new GltfMap { ["index"] = model.Texture(Blue) };
            return material;
        });

        await Run(model);

        var leftovers = Runs().Skip(1).ToList();
        Assert.Equal(2, leftovers.Count);
        Assert.Contains("--srgb", leftovers[0]);
        Assert.Contains("--linear", leftovers[1]);
        Assert.Contains("--normal", leftovers[1]);
    }

    [ShellFact]
    public async Task CooksATextureOnlyAnExtensionNamesAsColourAndKeepsItsIndexTrue()
    {
        // clearcoatTexture is a texture reference the atlas does not understand, so the material is left unpacked
        // and the texture has to survive the clean-up, renumbered, with the reference following it.
        var model = WithATiledMaterial(model => new GltfMap
        {
            ["extensions"] = new GltfMap
            {
                ["KHR_materials_clearcoat"] = new GltfMap
                {
                    ["clearcoatTexture"] = new GltfMap { ["index"] = model.Texture(Green) },
                },
            },
        });

        var result = await Run(model);

        Assert.Contains("--srgb", Runs()[1]);
        var document = await ReadAsync(result);
        var clearcoat = (GltfMap)((GltfMap)((GltfMap)document.List("materials")[2]["extensions"]!)["KHR_materials_clearcoat"]!)["clearcoatTexture"]!;
        Assert.Equal(0L, clearcoat["index"]);
    }

    [ShellFact]
    public async Task DropsATextureNothingUsesWithoutCookingIt()
    {
        var model = TwoMaterials();
        model.Texture(Green);

        var result = await Run(model);

        Assert.Single(Runs());
        var document = await ReadAsync(result);
        Assert.Single(document.List("textures"));
        Assert.Single(document.List("images"));
    }

    [ShellFact]
    public async Task KeepsAnImageOnlyAnExtensionNamesAndRenumbersTheExtension()
    {
        var model = WithATiledMaterial(model => BaseColour(model.Texture(Green)));
        var textures = (List<object?>)model.Json["textures"]!;
        var kept = (GltfMap)textures[2]!;
        kept.Remove("source");
        kept["extensions"] = new GltfMap { ["EXT_texture_webp"] = new GltfMap { ["source"] = 2 } };

        var result = await Run(model);

        var document = await ReadAsync(result);
        var webp = (GltfMap)((GltfMap)document.List("textures")[0]["extensions"]!)["EXT_texture_webp"]!;
        Assert.Equal(0L, webp["source"]);
        Assert.Equal(2, document.List("images").Count);
        Assert.Single(Runs());
    }

    [ShellFact]
    public async Task HoldsPagesToWhatTheTargetSamplesAndSaysSo()
    {
        var result = await new GltfImporter(new ModelAtlasCook(Cooker())).ImportAsync(
            Request(TwoMaterials().ToGlb(), Target(256, TextureFamily.Astc)));

        Assert.Contains("pages held to 256px, which is what this target samples", result.Notes);
        var run = OnlyRun();
        AssertInOrder(run, "--targets", "basis,astc", "--max-size", "256");
    }

    [ShellFact]
    public async Task SaysNothingWhenThePageSizeFitsTheTarget()
    {
        var result = await new GltfImporter(new ModelAtlasCook(Cooker())).ImportAsync(
            Request(TwoMaterials().ToGlb(), Target(512)));

        Assert.DoesNotContain(result.Notes, note => note.StartsWith("pages held", StringComparison.Ordinal));
        AssertInOrder(OnlyRun(), "--max-size", "512");
    }

    [ShellFact]
    public async Task NeverHoldsAPageBelowSixtyFourPixels()
    {
        var settings = AtlasSettings();
        settings["maxPageSize"] = 32L;
        var cook = new ModelAtlasCook(Cooker());

        var result = await cook.RunAsync(Request(TwoMaterials().ToGlb(), Target(1024)), settings);

        Assert.Contains("pages held to 64px, which is what this target samples", result.Notes);
    }

    [ShellFact]
    public async Task RefusesATargetThatSamplesLessThanTheSmallestPage()
    {
        // A target that cannot hold a 64 pixel page cannot hold any page, and clamping to it would be a lie.
        await Assert.ThrowsAsync<ArgumentException>(() => new GltfImporter(new ModelAtlasCook(Cooker()))
            .ImportAsync(Request(TwoMaterials().ToGlb(), Target(32))));
    }

    [ShellFact]
    public async Task AcceptsATargetThatSamplesExactlyTheSmallestPage()
    {
        var result = await new GltfImporter(new ModelAtlasCook(Cooker()))
            .ImportAsync(Request(TwoMaterials().ToGlb(), Target(64)));

        Assert.Contains("pages held to 64px, which is what this target samples", result.Notes);
    }

    [ShellFact]
    public async Task CooksEveryFamilyButRawWhenTheTargetNamesNone()
    {
        await new GltfImporter(new ModelAtlasCook(Cooker())).ImportAsync(Request(TwoMaterials().ToGlb(), CookTarget.Any));

        var targets = OnlyRun()[3];
        Assert.Equal(
            string.Join(',', new[] { TextureFamily.Basis }.Concat(TextureFamily.All.Where(f => f != TextureFamily.Basis && f != TextureFamily.Raw))),
            targets);
    }

    [ShellFact]
    public async Task LeavesRawOutAndNamesBasisOnceWhateverTheTargetAsks()
    {
        await new GltfImporter(new ModelAtlasCook(Cooker()))
            .ImportAsync(Request(TwoMaterials().ToGlb(), Target(null, TextureFamily.Raw, TextureFamily.Basis, TextureFamily.Bc)));

        Assert.Equal("basis,bc", OnlyRun()[3]);
    }

    [ShellFact]
    public async Task PassesTheCookersOwnFailureThrough()
    {
        var error = await Assert.ThrowsAsync<ImportFailureException>(
            () => Run(TwoMaterials(), Fake("exit=1;say=no encoder here")));

        Assert.Contains("no encoder here", error.Message, StringComparison.Ordinal);
    }

    [ShellFact]
    public async Task ReadsAnImageOffTheSourceAndDecodesItThroughTheCookTool()
    {
        var (model, assets) = JpegModel();
        var pixels = Enumerable.Repeat<byte>(200, 16).ToArray();
        var decoded = Octal(2, 0, 0, 0, 2, 0, 0, 0) + Octal(pixels);

        var result = await Run(model, Cooker(decoded), assets);

        var decodes = Runs().Where(run => run.Contains("--decode")).ToList();
        Assert.Equal(2, decodes.Count);
        Assert.EndsWith("decode.jpg", decodes[0][0], StringComparison.Ordinal);
        Assert.EndsWith("decode.raw", decodes[0][3], StringComparison.Ordinal);
        Assert.Equal(["--decode", decodes[0][3], "--quiet"], decodes[0].Skip(2));
        Assert.Contains("atlas0.basecolour.ktx2", result.Outputs.Keys);
    }

    [ShellFact]
    public async Task LeavesAnImageUndecodedWhenTheToolsAnswerIsNotWhole()
    {
        var header = Octal(2, 0, 0, 0, 2, 0, 0, 0);
        (string Why, string Raw)[] broken =
        [
            ("seven bytes cannot hold a header", Octal(2, 0, 0, 0, 2, 0, 0)),
            ("a pixel byte short", header + Octal(Enumerable.Repeat<byte>(200, 15).ToArray())),
            ("a pixel byte over", header + Octal(Enumerable.Repeat<byte>(200, 17).ToArray())),
            ("a width no image can have", Octal(255, 255, 255, 255, 0, 0, 0, 0)),
            ("a height no image can have", Octal(0, 0, 0, 0, 255, 255, 255, 255)),
        ];
        foreach (var (why, raw) in broken)
        {
            var (model, assets) = JpegModel();
            var bytes = model.ToGlb();

            var result = await new GltfImporter(new ModelAtlasCook(Cooker(raw)))
                .ImportAsync(Request(bytes, assets: assets));

            Assert.True(
                result.Notes.Any(note => note.Contains("could not be decoded here", StringComparison.Ordinal)),
                why);
            Assert.Same(bytes, result.Outputs["glb"]);
        }
    }

    [ShellFact]
    public async Task LeavesAnImageUndecodedWhenTheToolWritesNothing()
    {
        var (model, assets) = JpegModel();

        var result = await Run(model, Fake(""), assets);

        Assert.Contains(result.Notes, note => note.Contains("could not be decoded here", StringComparison.Ordinal));
    }

    [ShellFact]
    public async Task LeavesAnImageUndecodedWhenThereIsNoToolToDecodeIt()
    {
        var (model, assets) = JpegModel();

        var result = await Run(model, new NativeTool("orblit_missing_tool_for_test"), assets);

        Assert.Contains(result.Notes, note => note.Contains("could not be decoded here", StringComparison.Ordinal));
        Assert.Empty(Runs());
    }

    [ShellFact]
    public async Task FailsWhenTheToolCannotDecodeAnImage()
    {
        var (model, assets) = JpegModel();

        var error = await Assert.ThrowsAsync<ImportFailureException>(
            () => Run(model, Fake("exit=2;say=bad jpeg"), assets));

        Assert.Contains("bad jpeg", error.Message, StringComparison.Ordinal);
    }

    private static (QuadModel Model, Dictionary<AssetId, byte[]> Assets) JpegModel()
    {
        var model = TwoMaterials();
        Detach(Image(model, 0), "tex/a.jpg");
        Detach(Image(model, 1), "tex/b.jpg");
        return (model, new Dictionary<AssetId, byte[]>
        {
            [AssetId.Parse("models/tex/a.jpg")] = "jpeg a"u8.ToArray(),
            [AssetId.Parse("models/tex/b.jpg")] = "jpeg b"u8.ToArray(),
        });
    }

    private static QuadModel LeftoverWith(string? uri, string? name = null, string? mimeType = null)
    {
        var model = WithATiledMaterial(model => BaseColour(model.Texture(Green)));
        if (uri is null)
        {
            // Bytes left in the buffer, where a GLB keeps them, with only the names changed.
            var image = Image(model, 2);
            image["name"] = name;
            if (mimeType is not null)
            {
                image["mimeType"] = mimeType;
            }
        }
        else
        {
            Detach(Image(model, 2), uri, name, mimeType);
        }

        return model;
    }

    private static Dictionary<AssetId, byte[]> Beside(string uri) =>
        new() { [AssetId.Parse("models/" + Uri.UnescapeDataString(uri))] = Png(Green) };

    [ShellFact]
    public async Task FilesACookedLeftoverUnderANameAndExtensionTheCookCanUse()
    {
        (string? Uri, string? Name, string? Mime, string Expected)[] cases =
        [
            ("tex/rock.png", null, null, "rock.png"),
            ("flat.png", null, null, "flat.png"),
            ("tex/deep/sub/rock.png", null, null, "rock.png"),
            ("tex/Rock%20Tile.JPG", null, null, "Rock_Tile.jpg"),
            ("tex/hero.v2.png", null, null, "hero.v2.png"),
            ("tex/a-b_c.png", null, null, "a-b_c.png"),
            (".jpg", null, null, ".jpg.png"),
            ("tex/plain", null, null, "plain.png"),
            ("tex/pic.x", null, null, "pic.png"),
            ("tex/pic.ab", null, null, "pic.ab"),
            ("tex/pic.abcde", null, null, "pic.abcde"),
            ("tex/pic.abcdef", null, null, "pic.png"),
            ("tex/pic.png", null, "image/jpeg", "pic.jpg"),
            ("tex/pic.png", null, "image/ktx2", "pic.ktx2"),
            ("tex/pic.jpg", null, "image/png", "pic.png"),
            ("tex/pic.webp", null, "image/webp", "pic.webp"),
            ("tex/pic.png", null, "image/webp", "pic.png"),
            (null, "Rock Wall!", null, "Rock_Wall_.png"),
            (null, "!!", null, "__.png"),
            (null, null, null, "image0.png"),
            (null, "", null, "image0.png"),
        ];
        foreach (var (uri, name, mime, expected) in cases)
        {
            var model = LeftoverWith(uri, name, mime);
            var assets = uri is null ? null : Beside(uri);

            var result = await Run(model, Cooker(), assets);

            var made = Path.GetFileName(Runs().Last()[0]);
            var stem = Path.GetFileNameWithoutExtension(expected);
            Assert.True(expected == made, $"{uri} {name} {mime}: expected {expected} but cooked {made}");
            Assert.Contains($"{stem}.ktx2", result.Outputs.Keys);
            Assert.DoesNotContain(result.Notes, note => note.Contains("could not be read", StringComparison.Ordinal));
        }
    }

    [ShellFact]
    public async Task GivesTwoLeftoversThatShareAStemDifferentFiles()
    {
        var model = TwoMaterials();
        foreach (var x in new[] { 4, 6, 8 })
        {
            model.Quad(model.Material(BaseColour(model.Texture(Green))), x: x, uv: Tiled);
        }

        var assets = new Dictionary<AssetId, byte[]>();
        foreach (var (index, folder) in new[] { (2, "a"), (3, "b"), (4, "c") })
        {
            Detach(Image(model, index), $"{folder}/rock.png");
            assets[AssetId.Parse($"models/{folder}/rock.png")] = Png(Green);
        }

        var result = await Run(model, assets: assets);

        Assert.Equal(["rock.png", "rock_.png", "rock__.png"], Runs().Skip(1).Select(run => Path.GetFileName(run[0])));
        var document = await ReadAsync(result);
        Assert.Equal(
            ["rock.ktx2", "rock_.ktx2", "rock__.ktx2", "atlas0.basecolour.ktx2"],
            document.List("images").Select(image => image["uri"]));
    }

    [ShellFact]
    public async Task CooksTheBytesAnImageNamesWhereverGltfLetsItBePut()
    {
        var png = Png(Green);
        var inline = $"data:image/png;base64,{Convert.ToBase64String(png)}";
        foreach (var (uri, key) in new[] { (inline, "image0.ktx2"), ("tex/a.png", "a.ktx2") })
        {
            var model = LeftoverWith(uri);
            var assets = uri.StartsWith("data:", StringComparison.Ordinal) ? null : Beside(uri);

            var result = await Run(model, Cooker(echo: true), assets);

            Assert.Equal(png, result.Outputs[key]);
        }
    }

    [ShellFact]
    public async Task SaysWhichImageItCouldNotReadAndLeavesItAlone()
    {
        string?[] unreadable =
        [
            "tex/missing.png",
            "https://example.com/a.png",
            "../../../outside.png",
            "",
            "data:image/png,not-base64",
            "data:image/png;base64",
        ];
        foreach (var uri in unreadable)
        {
            var result = await Run(LeftoverWith(uri));
            await AssertUnreadable(result, uri);
        }

        var bare = LeftoverWith("x.png");
        Detach(Image(bare, 2), null);
        Image(bare, 2).Remove("uri");
        await AssertUnreadable(await Run(bare), null);
    }

    private static async Task AssertUnreadable(ImportResult result, string? uri)
    {
        Assert.Contains("image 0 could not be read, so the material using it will draw untextured", result.Notes);
        var document = await ReadAsync(result);
        Assert.Equal(uri, document.List("images")[0].GetValueOrDefault("uri"));
        Assert.DoesNotContain("image0.ktx2", result.Outputs.Keys);
    }

    private static GltfMap TextureAt(QuadModel model, int index) =>
        (GltfMap)((List<object?>)model.Json["textures"]!)[index]!;

    [ShellFact]
    public async Task LeavesATextureAloneWhenItsImageIsAlreadyNamedAsKtx2()
    {
        var model = WithATiledMaterial(model => BaseColour(model.Texture(Green)));
        TextureAt(model, 2)["extensions"] = new GltfMap { ["KHR_texture_basisu"] = new GltfMap { ["source"] = 2 } };

        var result = await Run(model);

        Assert.Single(Runs());
        Assert.DoesNotContain("image0.ktx2", result.Outputs.Keys);
    }

    [ShellFact]
    public async Task CooksAnImageTwoTexturesShareOnlyOnce()
    {
        var model = WithATiledMaterial(model => BaseColour(model.Texture(Green)));
        model.Quad(model.Material(BaseColour(model.Texture(Green))), x: 6, uv: Tiled);
        TextureAt(model, 3)["source"] = 2;

        await Run(model);

        Assert.Equal(["atlas0.basecolour.png", "image0.png"], Runs().Select(run => Path.GetFileName(run[0])));
    }

    [ShellFact]
    public async Task ChoosesTheFlagsByWhatTheTextureIsNotByWhichImageItNames()
    {
        // The normal texture comes first in the images and second in the textures, so a cook that looked up
        // the role by image number would swap the flags.
        var model = WithATiledMaterial(model =>
        {
            var material = BaseColour(model.Texture(Green));
            material["normalTexture"] = new GltfMap { ["index"] = model.Texture(Blue) };
            return material;
        });
        TextureAt(model, 2)["source"] = 3;
        TextureAt(model, 3)["source"] = 2;
        Detach(Image(model, 2), "normal.png");
        Detach(Image(model, 3), "base.png");
        var assets = new Dictionary<AssetId, byte[]>
        {
            [AssetId.Parse("models/normal.png")] = Png(Blue),
            [AssetId.Parse("models/base.png")] = Png(Green),
        };

        await Run(model, assets: assets);

        var runs = Runs().ToDictionary(run => Path.GetFileName(run[0]));
        Assert.Contains("--srgb", runs["base.png"]);
        Assert.DoesNotContain("--normal", runs["base.png"]);
        Assert.Contains("--normal", runs["normal.png"]);
        Assert.Contains("--linear", runs["normal.png"]);
    }

    [ShellFact]
    public async Task KeepsTheExtensionsATextureAlreadyHadWhenItIsRepointed()
    {
        var model = WithATiledMaterial(model => BaseColour(model.Texture(Green)));
        TextureAt(model, 2)["extensions"] = new GltfMap { ["VENDOR_texture_note"] = new GltfMap { ["kept"] = true } };

        var result = await Run(model);

        var extensions = (GltfMap)(await ReadAsync(result)).List("textures")[0]["extensions"]!;
        Assert.Equal(["KHR_texture_basisu", "VENDOR_texture_note"], extensions.Keys.Order());
    }

    [ShellFact]
    public async Task SkipsATextureWhoseSourceNamesNoImage()
    {
        foreach (var source in new[] { 99, -1, 3 })
        {
            var model = WithATiledMaterial(model => BaseColour(model.Texture(Green)));
            Assert.Equal(3, ((List<object?>)model.Json["images"]!).Count);
            TextureAt(model, 2)["source"] = source;
            var before = Runs().Count;

            await Run(model);

            Assert.Equal(1, Runs().Count - before);
        }
    }

    [ShellFact]
    public async Task LeavesAnImageUndecodedWhenItLooksLikeAPngAndIsNot()
    {
        var model = TwoMaterials();
        Detach(Image(model, 0), "tex/a.png");
        Detach(Image(model, 1), "tex/b.png");
        byte[] damaged = [137, 80, 78, 71, 13, 10, 26, 10, 0];
        var assets = new Dictionary<AssetId, byte[]>
        {
            [AssetId.Parse("models/tex/a.png")] = damaged,
            [AssetId.Parse("models/tex/b.png")] = damaged,
        };
        var decoded = Octal(2, 0, 0, 0, 2, 0, 0, 0) + Octal(Enumerable.Repeat<byte>(200, 16).ToArray());

        var result = await Run(model, Cooker(decoded), assets);

        Assert.Contains(result.Notes, note => note.Contains("could not be decoded here", StringComparison.Ordinal));
        Assert.Empty(Runs());
    }

    [ShellFact]
    public async Task SendsABareEightByteSignatureToTheToolBecauseItIsNotAPng()
    {
        var model = TwoMaterials();
        Detach(Image(model, 0), "tex/a.png");
        Detach(Image(model, 1), "tex/b.png");
        byte[] signature = [137, 80, 78, 71, 13, 10, 26, 10];
        var assets = new Dictionary<AssetId, byte[]>
        {
            [AssetId.Parse("models/tex/a.png")] = signature,
            [AssetId.Parse("models/tex/b.png")] = signature,
        };
        var decoded = Octal(2, 0, 0, 0, 2, 0, 0, 0) + Octal(Enumerable.Repeat<byte>(200, 16).ToArray());

        var result = await Run(model, Cooker(decoded), assets);

        Assert.Contains("atlas0.basecolour.ktx2", result.Outputs.Keys);
    }

    [ShellFact]
    public async Task DoesNotReadAnEarlierImagesPixelsAsALaterOnes()
    {
        var (model, assets) = JpegModel();
        var decoded = Octal(2, 0, 0, 0, 2, 0, 0, 0) + Octal(Enumerable.Repeat<byte>(200, 16).ToArray());

        var result = await Run(model, Cooker(decoded, once: true), assets);

        Assert.Contains(result.Notes, note => note.Contains("could not be decoded here", StringComparison.Ordinal));
    }

    [ShellFact]
    public async Task LeavesAnImageUndecodedWhenItsSizeWrapsAround()
    {
        var (model, assets) = JpegModel();
        var wraps = Octal(0, 0, 1, 0, 0, 0, 1, 0);

        var result = await Run(model, Cooker(wraps), assets);

        Assert.Contains(result.Notes, note => note.Contains("could not be decoded here", StringComparison.Ordinal));
    }

    [ShellFact]
    public async Task KeepsATextureAMaterialNamesInsideAList()
    {
        var model = WithATiledMaterial(model => new GltfMap
        {
            ["extensions"] = new GltfMap
            {
                ["VENDOR_layers"] = new GltfMap
                {
                    ["layers"] = new List<object?> { new GltfMap { ["index"] = model.Texture(Green) } },
                },
            },
        });

        var result = await Run(model);

        var document = await ReadAsync(result);
        var layer = (GltfMap)((List<object?>)((GltfMap)((GltfMap)document.List("materials")[2]["extensions"]!)["VENDOR_layers"]!)["layers"]!)[0]!;
        Assert.Equal(0L, layer["index"]);
        Assert.Equal(2, document.List("images").Count);
    }

    [ShellFact]
    public async Task HoldsPagesToEightThousandWhenTheTargetNamesNoLimit()
    {
        var settings = AtlasSettings();
        settings["maxPageSize"] = 16384L;

        var result = await new GltfImporter(new ModelAtlasCook(Cooker())).ImportAsync(
            Request(TwoMaterials().ToGlb(), Target(null), settings: settings));

        Assert.Contains("pages held to 8192px, which is what this target samples", result.Notes);
        AssertInOrder(OnlyRun(), "--max-size", "8192");
    }

    // How many red texels the page holds and where the first blue one is, which is how a page shows its
    // extrusion, its padding and the size its cells were held to. Every texel of a page is opaque, so alpha
    // says nothing.
    private async Task<(int Red, int BlueColumn)> PageWith(Action<Dictionary<string, object?>> change)
    {
        var settings = AtlasSettings();
        change(settings);
        var result = await new GltfImporter(new ModelAtlasCook(Cooker(echo: true))).ImportAsync(
            Request(TwoMaterials().ToGlb(), settings: settings));
        var page = PngCodec.Decode(result.Outputs["atlas0.basecolour.ktx2"]);
        var texels = Enumerable.Range(0, (int)(page.Width * page.Height)).ToList();
        bool Is(int texel, int[] colour) => colour.Select((channel, i) => page.Pixels[(texel * 4) + i] == channel).All(same => same);
        return (texels.Count(texel => Is(texel, Red)), texels.First(texel => Is(texel, Blue)) % (int)page.Width);
    }

    [ShellFact]
    public async Task PacksWithThePaddingAndTheExtrusionItWasGiven()
    {
        var page = await PageWith(settings =>
        {
            settings["padding"] = 8L;
            settings["extrude"] = 0L;
        });

        Assert.Equal((64, 16), page);
    }

    [ShellFact]
    public async Task CapsACellAtTheSizeItWasGiven()
    {
        var page = await PageWith(settings =>
        {
            settings["padding"] = 0L;
            settings["extrude"] = 0L;
            settings["maxCellSize"] = 4L;
        });

        Assert.Equal((16, 4), page);
    }

    [ShellFact]
    public async Task MergesPrimitivesUpToTheVertexLimitItWasGiven()
    {
        foreach (var (limit, expected) in new[] { (65536L, 1), (4L, 2) })
        {
            var settings = AtlasSettings();
            settings["maxMergedVertices"] = limit;

            var result = await new GltfImporter(new ModelAtlasCook(Cooker())).ImportAsync(
                Request(TwoMaterials().ToGlb(), settings: settings));

            var mesh = (GltfMap)(await ReadAsync(result)).List("meshes")[0];
            Assert.Equal(expected, ((List<object?>)mesh["primitives"]!).Count);
        }
    }

    [ShellFact]
    public async Task SkipsATextureWhoseSourceIsTheNumberAfterTheLastImage()
    {
        // The leftover textures reuse the two images the page took, so nothing is dropped and the page is the
        // third image. A texture naming image 3 is then one past the end.
        var model = TwoMaterials();
        var textures = (List<object?>)model.Json["textures"]!;
        foreach (var (image, x) in new[] { (0, 4), (1, 6), (3, 8) })
        {
            textures.Add(new GltfMap { ["source"] = image });
            model.Quad(model.Material(BaseColour(textures.Count - 1)), x: x, uv: Tiled);
        }

        await Run(model);

        Assert.Equal(
            ["atlas0.basecolour.png", "image0.png", "image1.png"],
            Runs().Select(run => Path.GetFileName(run[0])));
    }

    private sealed class CountingSource(IAssetSource inner) : IAssetSource
    {
        public List<AssetId> Reads { get; } = [];

        public Task<byte[]> ReadAsync(AssetId id)
        {
            Reads.Add(id);
            return inner.ReadAsync(id);
        }

        public Task<bool> ExistsAsync(AssetId id) => inner.ExistsAsync(id);
    }

    [ShellFact]
    public async Task ReadsAnImageOnceWhenThePageAndALeftoverTextureBothNeedIt()
    {
        var model = TwoMaterials();
        Detach(Image(model, 0), "tex/a.png");
        Detach(Image(model, 1), "tex/b.png");
        var textures = (List<object?>)model.Json["textures"]!;
        textures.Add(new GltfMap { ["source"] = 0 });
        model.Quad(model.Material(BaseColour(textures.Count - 1)), x: 4, uv: Tiled);
        var source = new CountingSource(new MemoryAssetSource(new Dictionary<AssetId, byte[]>
        {
            [AssetId.Parse("models/tex/a.png")] = Png(Red),
            [AssetId.Parse("models/tex/b.png")] = Png(Blue),
        }));
        var request = new ImportRequest
        {
            Id = Model,
            Bytes = model.ToGlb(),
            Settings = AtlasSettings(),
            Target = CookTargets.Macos,
            Source = source,
        };

        await new GltfImporter(new ModelAtlasCook(Cooker())).ImportAsync(request);

        Assert.Equal(["models/tex/a.png", "models/tex/b.png"], source.Reads.Select(id => id.ToString()).Order());
    }

    [ShellFact]
    public async Task NamesAFileAfterAnUriWhoseEscapeIsBadWhenTheBytesComeFromTheBuffer()
    {
        var model = WithATiledMaterial(model => BaseColour(model.Texture(Green)));
        Image(model, 2)["uri"] = "tex/bad%zz.png";

        await Run(model);

        Assert.Equal("bad_zz.png", Path.GetFileName(Runs().Last()[0]));
    }

    [ShellFact]
    public async Task TakesAnImageTheToolDecodedToNothingAtAll()
    {
        var (model, assets) = JpegModel();

        var result = await Run(model, Cooker(Octal(0, 0, 0, 0, 0, 0, 0, 0)), assets);

        Assert.Contains("packed 2 materials onto 1 page of basecolour", result.Notes);
    }
}

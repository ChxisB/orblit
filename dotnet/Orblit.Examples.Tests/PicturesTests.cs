using System.Security.Cryptography;
using Orblit.Sprite;

namespace Orblit.Examples.Tests;

/// <summary>
/// The pictures the material examples draw. The fixture's scenes name none,
/// so these check what the gallery sees once they are kept.
/// </summary>
public sealed class PicturesTests
{
    [Fact]
    public void AKeptPictureReadsBackAsTheSamePixels()
    {
        var example = $"test_{Guid.NewGuid():N}";
        var rgba = Enumerable.Range(0, 2 * 2 * 4).Select(i => (byte)(i * 15)).ToArray();
        try
        {
            var path = new TempPictureFolder().Keep(example, "square.png", 2, rgba);

            Assert.NotNull(path);
            Assert.Equal(new DecodedPng(2, 2, rgba), PngCodec.Decode(File.ReadAllBytes(path)), new PngComparer());
        }
        finally
        {
            Directory.Delete(Path.Combine(Path.GetTempPath(), $"orblit_gallery_{example}"), recursive: true);
        }
    }

    /// <summary>
    /// The pixels of every picture drawn, pinned by hash. The Dart gallery's
    /// materials and decals pictures matched these on every opaque pixel and
    /// every alpha, and its sprite sheet on every byte. Blending seeds a
    /// different generator, so its stones are only pinned to stay the same
    /// from run to run.
    /// </summary>
    [Fact]
    public void EachPictureIsDrawnTheSameEveryRun()
    {
        var folder = new Recording();

        _ = EngineExamples.All(folder);

        Assert.Equal(
            new Dictionary<string, string>
            {
                ["blend/cobbles.png"] = "726b9c31912a4201425b8f6c91bdfec15b51341fe3ab4b32eb39cdb0ad737555",
                ["blend/grass.png"] = "b26a03cf6504476d2cd9f7e79d37aed95d3396a0ef2ab753b506b247bcbbffef",
                ["blend/height.png"] = "ebc35b745102317d81eb6c7982d648f402fd83a0ba666062531278f1b555ec7b",
                ["decals/poster.png"] = "cffed888c0c478bd43cfd037a822de824de62422048021a2a2403f937bef7b01",
                ["decals/puddle.png"] = "384f0f3cc1cdf5d8fc660d23878035eb437d3c081536fdfd0594ca056a43a99a",
                ["decals/scorch.png"] = "25ac247cfdd9ef20224860f2c674df6b0586ec6905d8a900561d2d0d0967b84e",
                ["decals/splash.png"] = "f3a0154ac468043d6f90081c5fa8292a950f199c3bd8b87df001c27e67c6bd35",
                ["decals/stripes.png"] = "fc91f32ce2026a0a0f4fa7ed0bb306fa527e523b134a9c6dc130d8a8fd25dd5d",
                ["materials/albedo.png"] = "e1a98b31fce73c44599db838e11771d4c6a7780e4926f7506439ca8c29aec3c5",
                ["materials/cutout.png"] = "202d148e7a6260ee5de6131215b40013c00037707949c220b51021d9485f1534",
                ["materials/normals.png"] = "d8848d6e5953e0b5b5fed97dd60d8e055ba3e3add18cbaa6ae7fc7b88f38e5e5",
                ["materials/packed.png"] = "23f384112d9ea387d804878891997033dfa63f8de98f8a5df793c1c44d5e7d7b",
                ["sprites/sheet.png"] = "76b0cfc778c382d1270591fb4138b26193a6999c13e86a6c31703984e6b36813",
            },
            folder.Kept.ToDictionary(kept => kept.Key, kept => Convert.ToHexStringLower(SHA256.HashData(kept.Value))));
    }

    [Theory]
    [InlineData("Materials", "materials", 4)]
    [InlineData("Blending", "blend", 3)]
    [InlineData("Decals", "decals", 5)]
    [InlineData("Sprites", "sprites", 1)]
    public void TheSceneNamesEveryPictureItKept(string name, string folderName, int pictures)
    {
        var folder = new Recording();
        var example = EngineExamples.All(folder).Single(example => example.Name == name);

        var scene = example.Scene(new ViewPoint().ToRenderCamera(), 1);

        var kept = folder.Kept.Keys.Where(path => path.StartsWith($"{folderName}/", StringComparison.Ordinal)).ToList();
        Assert.Equal(pictures, kept.Count);
        var named = scene.Materials.SelectMany(Maps)
            .Concat(scene.Decals.Select(decal => decal.Texture?.Path))
            .Concat(scene.Sprites.Select(layer => layer.Image?.Path));
        Assert.All(kept, path => Assert.Contains(path, named));
    }

    private static IEnumerable<string?> Maps(Orblit.Filament.RenderMaterial material) =>
    [
        material.BaseColourMap?.Path,
        material.NormalMap?.Path,
        material.MetallicRoughnessMap?.Path,
        material.BlendBaseColourMap?.Path,
        material.BlendMaskMap?.Path,
    ];

    /// <summary>A folder that keeps each picture in memory and names it by its example and file name.</summary>
    private sealed class Recording : IPictureFolder
    {
        public Dictionary<string, byte[]> Kept { get; } = [];

        public string? Keep(string example, string name, int size, byte[] rgba)
        {
            var path = $"{example}/{name}";
            Kept[path] = rgba;
            return path;
        }
    }

    private sealed class PngComparer : IEqualityComparer<DecodedPng>
    {
        public bool Equals(DecodedPng? x, DecodedPng? y) =>
            x is not null && y is not null && x.Width == y.Width && x.Height == y.Height && x.Pixels.SequenceEqual(y.Pixels);

        public int GetHashCode(DecodedPng obj) => HashCode.Combine(obj.Width, obj.Height);
    }
}

using Orblit.Filament;

namespace Orblit.Examples.Tests;

/// <summary>
/// What the environment tells the textures example. The fixture ran with
/// none of it set and no device, so these cover what the gallery sees on a
/// machine that has the files.
/// </summary>
public sealed class TexturesTests
{
    private static readonly RenderCamera Camera = new ViewPoint().ToRenderCamera();

    [Fact]
    public void FilesAreTrimmedAndLaidOutInRowsOfFour()
    {
        var example = new TexturesExample(Environment(("ORBLIT_TEXTURE_FILES", " a.png, ,b.png,c.png,d.png,e.png")));

        var scene = example.Scene(Camera, 0);

        Assert.Equal([10L, 11, 12, 13, 14], scene.Objects.Select(panel => panel.Key));
        Assert.Equal(1.15, scene.Objects[0].Transform.GetTranslation().Y, 12);
        Assert.Equal(-1.15, scene.Objects[4].Transform.GetTranslation().Y, 12);
        Assert.Equal(new Caption("0 of 5 named, from ../orblit/assets/bistro/Textures"), example.Settings()[1]);
    }

    [Fact]
    public void ASizeCapsTheTexturesAndNoSizeLeavesThemAlone()
    {
        var capped = new TexturesExample(Environment(("ORBLIT_TEXTURE_SIZE", "512"))).Scene(Camera, 0);
        var uncapped = new TexturesExample(Environment()).Scene(Camera, 0);

        Assert.Equal(512, capped.Pipeline.Textures.MaxSize);
        Assert.Null(uncapped.Pipeline.Textures.MaxSize);
    }

    [Fact]
    public void OnceTheDeviceIsKnownAFileInTheFolderIsNamed()
    {
        var folder = Directory.CreateTempSubdirectory("orblit_textures_").FullName;
        try
        {
            File.WriteAllBytes(Path.Combine(folder, "wall_Normal.png"), []);
            var example = new TexturesExample(
                Environment(("ORBLIT_TEXTURES", folder), ("ORBLIT_TEXTURE_FILES", "wall_Normal.png,gone.png")));

            Assert.All(example.Scene(Camera, 0).Materials, material => Assert.Null(material.BaseColourMap));
            example.Device = new DeviceProfile();
            var named = example.Scene(Camera, 0).Materials;

            Assert.Equal(new Texture($"{folder}/wall_Normal.png", Srgb: false), named[0].BaseColourMap);
            Assert.Null(named[1].BaseColourMap);
            Assert.Equal(new Caption($"1 of 2 named, from {folder}"), example.Settings()[1]);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private static Func<string, string?> Environment(params (string Name, string Value)[] set) =>
        name => set.SingleOrDefault(pair => pair.Name == name).Value;
}

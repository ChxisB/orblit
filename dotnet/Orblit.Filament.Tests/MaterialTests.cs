using Orblit.VectorMath;

namespace Orblit.Filament.Tests;

public sealed class MaterialTests
{
    private static RenderScene SceneOf(IReadOnlyList<RenderObject> objects, IReadOnlyList<RenderMaterial> materials) =>
        SceneBuilders.Of(objects, SceneBuilders.CameraAt(0, 2, 6)) with { Materials = materials };

    private static RenderObject ObjectOn(long key, long? material = null) =>
        SceneBuilders.Plain(key) with { Material = material };

    private static float[] Packed(RenderMaterial material)
    {
        var packed = new float[RenderMaterial.Stride];
        material.Pack(packed, 0);
        return packed;
    }

    private static RenderScene VideoScene(params Video[] videos) =>
        SceneBuilders.Of([], SceneBuilders.CameraAt(0, 0, 5)) with { Videos = videos };

    [Fact]
    public void AMaterialWithNothingSaidAboutItStillDrawsSomething()
    {
        var material = new RenderMaterial { Key = 1 };

        Assert.Equal(1.0, material.BaseColour.W);
        Assert.Equal(0.0, material.Metallic);
        Assert.True(material.Roughness > 0.0, "a perfectly smooth surface flickers");
        Assert.Equal(1.0, material.Tiling.X);
        Assert.All(material.Maps, map => Assert.Null(map));
    }

    [Fact]
    public void FlagsSeparateWhatNeedsARebuildFromWhatDoesNot()
    {
        var opaque = new RenderMaterial { Key = 1 };
        var faded = new RenderMaterial { Key = 1, Blend = MaterialBlend.Fade };
        var twoSided = new RenderMaterial { Key = 1, DoubleSided = true };

        Assert.Equal((int)MaterialShading.Lit, opaque.Flags & 3);
        Assert.Equal((int)MaterialBlend.Opaque, (opaque.Flags >> 2) & 15);
        Assert.Equal((int)MaterialBlend.Fade, (faded.Flags >> 2) & 15);
        Assert.Equal(1, (twoSided.Flags >> 8) & 1);
        Assert.Equal(0, (opaque.Flags >> 8) & 1);
        // Depth write is on by default.
        Assert.Equal(1, (opaque.Flags >> 9) & 1);
    }

    [Fact]
    public void ALitScreenIsAVideoWithBit14SetAndOnlyThen()
    {
        var screen = new RenderMaterial { Key = 1, Shading = MaterialShading.Video, LitScreen = true };
        var plain = new RenderMaterial { Key = 1, Shading = MaterialShading.Video };

        Assert.Equal((int)MaterialShading.Video, screen.Flags & 3);
        Assert.Equal(1, (screen.Flags >> 14) & 1);
        Assert.Equal(0, (plain.Flags >> 14) & 1);
        Assert.True((screen with { Roughness = 0.2 }).LitScreen);
        Assert.Equal(plain.Flags, (screen with { LitScreen = false }).Flags);
    }

    [Fact]
    public void ASurfaceThatSaysNothingAboutWindDoesNotMove()
    {
        // The vertex stage is compiled into every lit surface, so a material that never mentions
        // wind has to take the early return. Both the speed and the compliance are nought, and
        // either one alone is enough to stop it.
        var material = new RenderMaterial { Key = 1 };
        Assert.Equal(Wind.None, material.Wind);
        Assert.False(material.Wind.Moves);

        var packed = Packed(material);
        Assert.Equal(0f, packed[35]);
        Assert.Equal(0f, packed[36]);
    }

    [Fact]
    public void StillAirAndARigidSurfaceBothFoldToNought()
    {
        // The shader has one early return rather than two, so both ways of saying "does not move"
        // have to arrive as zero.
        var gale = new RenderMaterial { Key = 1, Wind = new Wind { Bearing = 90, Speed = 20, Strength = 0 } };
        var calm = new RenderMaterial { Key = 2, Wind = new Wind { Bearing = 90, Speed = 0, Strength = 1 } };

        Assert.Equal(0f, Packed(gale)[35]);
        Assert.Equal(0f, Packed(calm)[36]);
    }

    [Fact]
    public void ABearingBecomesADirectionOnTheGround()
    {
        // North is negative Z in a right-handed Y-up world, the same convention the weather uses.
        // The two have to agree or a scene's rain and its trees blow in different directions.
        var north = new Wind { Bearing = 0, Speed = 1 };
        Assert.Equal(0.0, north.Direction.X, 1e-9);
        Assert.Equal(1.0, north.Direction.Y, 1e-9);

        var east = new Wind { Bearing = 90, Speed = 1 };
        Assert.Equal(1.0, east.Direction.X, 1e-9);
        Assert.Equal(0.0, east.Direction.Y, 1e-9);
    }

    [Fact]
    public void WindPacksWhereTheRendererReadsIt()
    {
        var packed = Packed(new RenderMaterial { Key = 1, Wind = new Wind { Bearing = 90, Speed = 6, Strength = 0.75 } });

        SceneBuilders.Near(1.0, packed[33]);
        SceneBuilders.Near(0, packed[34]);
        SceneBuilders.Near(6.0, packed[35]);
        SceneBuilders.Near(0.75, packed[36]);
    }

    [Fact]
    public void WithCarriesWind()
    {
        var material = new RenderMaterial { Key = 1 };
        var swaying = material with { Wind = new Wind { Bearing = 180, Speed = 3 } };

        Assert.True(swaying.Wind.Moves);
        Assert.Equal(3.0, swaying.Wind.Speed);
        Assert.False(material.Wind.Moves);
    }

    [Fact]
    public void ASurfaceThatAskedForNoLobesCarriesNone()
    {
        // The coat, the grain and the sheen are compiled into every lit surface. If their defaults
        // were anything but inert, every material in every scene would quietly gain a varnish.
        var material = new RenderMaterial { Key = 1 };
        Assert.Equal(0.0, material.ClearCoat);
        Assert.Equal(0.0, material.Anisotropy);
        Assert.Equal(Vector3.Zero(), material.SheenColour);

        var packed = Packed(material);
        Assert.Equal(0f, packed[26]);
        Assert.Equal(0f, packed[28]);
        Assert.Equal(0f, packed[29]);
        Assert.Equal(0f, packed[30]);
        Assert.Equal(0f, packed[31]);
    }

    [Fact]
    public void EachLobePacksWhereTheRendererReadsIt()
    {
        var packed = Packed(
            new RenderMaterial
            {
                Key = 1,
                ClearCoat = 0.8,
                ClearCoatRoughness = 0.05,
                Anisotropy = -0.6,
                SheenColour = new Vector3(0.2, 0.3, 0.4),
                SheenRoughness = 0.55,
            });

        SceneBuilders.Near(0.8, packed[26]);
        SceneBuilders.Near(0.05, packed[27]);
        // Negative is a direction, not a mistake: the grain runs the other way.
        SceneBuilders.Near(-0.6, packed[28]);
        SceneBuilders.Near(0.2, packed[29]);
        SceneBuilders.Near(0.3, packed[30]);
        SceneBuilders.Near(0.4, packed[31]);
        SceneBuilders.Near(0.55, packed[32]);
    }

    [Fact]
    public void ASecondMaterialStartsWhereTheFirstOneEnds()
    {
        // The row grew once and an offset did not follow, so everything after the first row read
        // the row before it. Packing two and checking the second is the cheapest way to notice.
        var packed = new float[RenderMaterial.Stride * 2];
        new RenderMaterial { Key = 1, ClearCoat = 1.0 }.Pack(packed, 0);
        new RenderMaterial { Key = 2, ClearCoat = 0.25 }.Pack(packed, RenderMaterial.Stride);

        SceneBuilders.Near(1.0, packed[26]);
        SceneBuilders.Near(0.25, packed[RenderMaterial.Stride + 26]);
    }

    [Fact]
    public void WithCarriesTheLobes()
    {
        var material = new RenderMaterial { Key = 1 };
        var coated = material with { ClearCoat = 0.5, Anisotropy = 0.9 };

        Assert.Equal(0.5, coated.ClearCoat);
        Assert.Equal(0.9, coated.Anisotropy);
        Assert.Equal(material.Roughness, coated.Roughness);
        Assert.Equal(Vector3.Zero(), coated.SheenColour);
    }

    [Fact]
    public void TheNumbersPackInTheOrderTheRendererReadsThem()
    {
        var packed = Packed(
            new RenderMaterial
            {
                Key = 1,
                BaseColour = new Vector4(0.1, 0.2, 0.3, 0.4),
                Metallic = 0.5,
                Roughness = 0.6,
                Reflectance = 0.7,
                Emissive = new Vector3(0.8, 0.9, 1.0),
                EmissiveIntensity = 2.0,
                AmbientOcclusion = 0.25,
                NormalScale = 1.5,
                Tiling = new Vector2(3, 4),
                Offset = new Vector2(0.05, 0.06),
                MaskThreshold = 0.75,
            });

        SceneBuilders.Near(0.1, packed[0]);
        SceneBuilders.Near(0.4, packed[3]);
        SceneBuilders.Near(0.5, packed[4]);
        SceneBuilders.Near(0.7, packed[6]);
        SceneBuilders.Near(2.0, packed[10]);
        SceneBuilders.Near(3.0, packed[13]);
        SceneBuilders.Near(0.06, packed[16]);
        SceneBuilders.Near(0.75, packed[17]);
    }

    [Fact]
    public void AnObjectPointsAtAMaterialByItsPlaceInTheList()
    {
        var scene = SceneOf(
            [ObjectOn(1, 70), ObjectOn(2), ObjectOn(3, 60)],
            [new RenderMaterial { Key = 60 }, new RenderMaterial { Key = 70 }]);

        var indices = SceneBuilders.Read<int[]>(scene.ToMessage(), "objectMaterials");

        // Key 70 is second in the list, nothing is named for the second object.
        Assert.Equal([1, -1, 0], indices);
    }

    [Fact]
    public void NamingAMaterialTheSceneDoesNotListFallsBackRatherThanFails()
    {
        var scene = SceneOf([ObjectOn(1, 999)], []);

        Assert.Equal(-1, SceneBuilders.Read<int[]>(scene.ToMessage(), "objectMaterials")[0]);
    }

    [Fact]
    public void AnImageOnSeveralMaterialsTravelsOnce()
    {
        var shared = new Texture("/tmp/one.png");
        var scene = SceneOf(
            [ObjectOn(1, 1)],
            [
                new RenderMaterial { Key = 1, BaseColourMap = shared, EmissiveMap = shared },
                new RenderMaterial { Key = 2, BaseColourMap = shared },
            ]);

        var message = scene.ToMessage();
        var maps = SceneBuilders.Read<int[]>(message, "materialMaps");

        Assert.Equal(["/tmp/one.png"], SceneBuilders.Read<string[]>(message, "texturePaths"));
        Assert.Equal(0, maps[0]);
        // The emissive map is the same entry.
        Assert.Equal(0, maps[4]);
        Assert.Equal(-1, maps[1]);
        Assert.Equal(0, maps[RenderMaterial.MapCount]);
    }

    [Fact]
    public void TheSameFileInTwoColourSpacesIsTwoTextures()
    {
        var scene = SceneOf(
            [ObjectOn(1, 1)],
            [
                new RenderMaterial
                {
                    Key = 1,
                    BaseColourMap = new Texture("/tmp/one.png"),
                    NormalMap = new Texture("/tmp/one.png", Srgb: false),
                },
            ]);

        var message = scene.ToMessage();

        Assert.Equal(2, SceneBuilders.Read<string[]>(message, "texturePaths").Length);
        Assert.Equal([1, 0], SceneBuilders.Read<int[]>(message, "textureSrgb"));
    }

    [Fact]
    public void ASceneWithNoMaterialsStillSaysSo()
    {
        var message = SceneOf([ObjectOn(1)], []).ToMessage();

        Assert.Empty(SceneBuilders.Read<long[]>(message, "materialKeys"));
        Assert.Empty(SceneBuilders.Read<float[]>(message, "materialParams"));
        Assert.Equal(-1, SceneBuilders.Read<int[]>(message, "objectMaterials").Single());
    }

    [Fact]
    public void EveryMaterialContributesItsOwnStrideInListOrder()
    {
        var scene = SceneOf(
            [ObjectOn(1, 2)],
            [new RenderMaterial { Key = 1, Roughness = 0.11 }, new RenderMaterial { Key = 2, Roughness = 0.22 }]);

        var packed = SceneBuilders.Read<float[]>(scene.ToMessage(), "materialParams");

        Assert.Equal(2 * RenderMaterial.Stride, packed.Length);
        SceneBuilders.Near(0.11, packed[5]);
        SceneBuilders.Near(0.22, packed[RenderMaterial.Stride + 5]);
    }

    [Fact]
    public void AScreenPointsAtAVideoByItsPlaceInTheList()
    {
        var scene = SceneBuilders.Of([ObjectOn(1, 10)], SceneBuilders.CameraAt(0, 0, 5)) with
        {
            Materials =
            [
                new RenderMaterial { Key = 10, Shading = MaterialShading.Video, Video = 7 },
                new RenderMaterial { Key = 11, Shading = MaterialShading.Video, Video = 999 },
                new RenderMaterial { Key = 12 },
            ],
            Videos =
            [
                new Video { Key = 5, Path = "/tmp/a.mp4" },
                new Video { Key = 7, Path = "/tmp/b.mp4" },
            ],
        };

        var message = scene.ToMessage();
        var screens = SceneBuilders.Read<int[]>(message, "materialVideos");

        Assert.Equal(1, screens[0]);
        // A video the scene does not list, and a material that is not a screen at all.
        Assert.Equal(-1, screens[1]);
        Assert.Equal(-1, screens[2]);
        Assert.Equal(["/tmp/a.mp4", "/tmp/b.mp4"], SceneBuilders.Read<string[]>(message, "videoPaths"));
    }

    [Fact]
    public void ASeekOnlyCountsWhenItsTokenMoves()
    {
        var still = SceneBuilders.Read<float[]>(VideoScene(new Video { Key = 1, Path = "/tmp/a.mp4" }).ToMessage(), "videoParams");
        // No seek asked for.
        Assert.Equal(-1f, still[2]);
        Assert.Equal(0f, still[3]);

        var jumped = VideoScene(new Video { Key = 1, Path = "/tmp/a.mp4", SeekTo = 12, SeekToken = 3 });
        var moved = SceneBuilders.Read<float[]>(jumped.ToMessage(), "videoParams");
        Assert.Equal(12f, moved[2]);
        Assert.Equal(3f, moved[3]);
    }

    [Fact]
    public void APausedLoopingVideoSaysSoInItsFlags()
    {
        var video = new Video { Key = 1, Path = "/a", Playing = false, Loop = true };

        Assert.Equal(0, video.Flags & 1);
        Assert.Equal(2, video.Flags & 2);
    }

    [Fact]
    public void WithKeepsTheMapsAndTheKey()
    {
        var material = new RenderMaterial { Key = 4, BaseColourMap = new Texture("/tmp/a.png"), Roughness = 0.3 };
        var rougher = material with { Roughness = 0.9 };

        Assert.Equal(4, rougher.Key);
        Assert.Equal(0.9, rougher.Roughness);
        Assert.Equal("/tmp/a.png", rougher.BaseColourMap?.Path);
    }

    [Fact]
    public void ASecondSurfaceBlendedIntoTheFirstReachesTheMessageWithItsModeAmountAndTiling()
    {
        var scene = SceneOf(
            [ObjectOn(1, 9)],
            [
                new RenderMaterial
                {
                    Key = 9,
                    BlendMode = MaterialBlendMode.MaskedDepth,
                    BlendAmount = 0.4,
                    BlendSharpness = 12,
                    BlendTiling = new Vector2(8, 8),
                    BlendOffset = new Vector2(0.25, 0.5),
                },
            ]);

        var packed = SceneBuilders.Read<float[]>(scene.ToMessage(), "materialParams");

        Assert.Equal((float)MaterialBlendMode.MaskedDepth, packed[19]);
        SceneBuilders.Near(0.4, packed[20]);
        SceneBuilders.Near(12, packed[21]);
        SceneBuilders.Near(8, packed[22]);
        SceneBuilders.Near(8, packed[23]);
        SceneBuilders.Near(0.25, packed[24]);
        SceneBuilders.Near(0.5, packed[25]);
    }

    [Fact]
    public void TheSecondSurfacesMapsSitAfterTheFirstSurfaces()
    {
        var scene = SceneOf(
            [ObjectOn(1, 9)],
            [
                new RenderMaterial
                {
                    Key = 9,
                    BaseColourMap = new Texture("/ground/cobbles.png"),
                    BlendBaseColourMap = new Texture("/ground/grass.png"),
                    BlendMaskMap = new Texture("/ground/height.png"),
                },
            ]);

        var message = scene.ToMessage();
        var paths = SceneBuilders.Read<string[]>(message, "texturePaths");
        var maps = SceneBuilders.Read<int[]>(message, "materialMaps");

        // The order the renderer reads them back in. Getting this wrong swaps a mask for a colour
        // map and shows up as a surface that is somehow the wrong material rather than as anything
        // that looks like an index.
        Assert.Equal(RenderMaterial.MapCount, maps.Length);
        Assert.Equal("/ground/cobbles.png", paths[maps[0]]);
        Assert.Equal("/ground/grass.png", paths[maps[5]]);
        Assert.Equal("/ground/height.png", paths[maps[6]]);
        // Untouched slots stay empty rather than pointing at something.
        Assert.Equal(-1, maps[1]);
        Assert.Equal(-1, maps[4]);
    }

    [Fact]
    public void AMaterialThatDoesNotBlendCostsNoMapsAndNoMode()
    {
        var message = SceneOf([ObjectOn(1, 9)], [new RenderMaterial { Key = 9 }]).ToMessage();
        var packed = SceneBuilders.Read<float[]>(message, "materialParams");
        var maps = SceneBuilders.Read<int[]>(message, "materialMaps");

        Assert.Equal((float)MaterialBlendMode.None, packed[19]);
        Assert.Equal(-1, maps[5]);
        Assert.Equal(-1, maps[6]);
        Assert.Empty(SceneBuilders.Read<string[]>(message, "texturePaths"));
    }

    [Fact]
    public void TheBlendLayerTilesWithTheFirstWhenItIsNotToldOtherwise()
    {
        // Two surfaces the same size of thing is the ordinary case, and having to restate the
        // tiling for it would be a trap: forget, and the second layer silently tiles once across
        // a field.
        var material = new RenderMaterial { Key = 9, BlendMode = MaterialBlendMode.Linear };
        var tiled = new RenderMaterial { Key = 9, Tiling = new Vector2(16, 16), BlendMode = MaterialBlendMode.Linear };

        Assert.Equal(material.Tiling, material.BlendTiling);
        Assert.Equal(new Vector2(16, 16), tiled.BlendTiling);
    }
}

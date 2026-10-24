namespace Orblit.Filament.Tests;

public sealed class StrideAddedTests
{
    [RendererFact]
    public void EveryWidthThePackersWriteIsTheWidthTheRendererReads()
    {
        (NativeMethods.Stride Which, uint Width)[] widths =
        [
            (NativeMethods.Stride.Transform, 16),
            (NativeMethods.Stride.Colour, 3),
            (NativeMethods.Stride.Material, RenderMaterial.Stride),
            (NativeMethods.Stride.MaterialMaps, RenderMaterial.MapCount),
            (NativeMethods.Stride.Video, Video.Stride),
            (NativeMethods.Stride.Decal, Decal.Stride),
            (NativeMethods.Stride.Outline, Outline.Stride),
            (NativeMethods.Stride.GodRays, GodRays.Stride),
            (NativeMethods.Stride.Distortion, Distortion.Stride),
            (NativeMethods.Stride.Light, RenderLight.Stride),
            (NativeMethods.Stride.Sky, Sky.Stride),
            (NativeMethods.Stride.Fog, Fog.Stride),
            (NativeMethods.Stride.Probe, Probe.Stride),
            (NativeMethods.Stride.Field, IrradianceField.Stride),
            (NativeMethods.Stride.Environment, RenderEnvironment.Stride),
            (NativeMethods.Stride.Pass, RenderGraph.PassStride),
            (NativeMethods.Stride.Target, RenderGraph.TargetStride),
            (NativeMethods.Stride.Pipeline, Pipeline.Stride),
            (NativeMethods.Stride.Precipitation, Precipitation.Stride),
            (NativeMethods.Stride.PoseInts, ModelAnimation.IntStride),
            (NativeMethods.Stride.Pose, ModelAnimation.Stride),
            (NativeMethods.Stride.PopulationBounds, Population.BoundsStride),
            (NativeMethods.Stride.Splat, SplatCloud.Stride),
            (NativeMethods.Stride.SplatRecordBytes, SplatCloud.RecordBytes),
            (NativeMethods.Stride.SpriteLayer, SpriteLayer.Stride),
            (NativeMethods.Stride.Sprite, LayerSprite.Stride),
            (NativeMethods.Stride.TerrainInts, RenderTerrain.HeaderInts),
            (NativeMethods.Stride.TerrainRegionInts, RenderTerrain.RegionInts),
            (NativeMethods.Stride.Terrain, RenderTerrain.Stride),
            (NativeMethods.Stride.TerrainSet, TerrainSet.Stride),
        ];

        foreach (var (which, width) in widths)
        {
            Assert.True(NativeMethods.StrideOf(which) == width, $"{which}: the packer writes {width}, the renderer reads {NativeMethods.StrideOf(which)}.");
        }
    }
}

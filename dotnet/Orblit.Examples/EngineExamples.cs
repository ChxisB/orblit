namespace Orblit.Examples;

/// <summary>Every example the gallery shows, in the order it lists them.</summary>
public static class EngineExamples
{
    /// <summary>
    /// A fresh set each call, because each example keeps its own settings. The
    /// examples that draw their own textures keep them in <paramref name="pictures"/>.
    /// The ones that can be pointed at other files look them up in
    /// <paramref name="environment"/>. The gallery passes the process
    /// environment, and a test passes an empty one.
    /// </summary>
    public static IReadOnlyList<Example> All(IPictureFolder pictures, Func<string, string?> environment) =>
    [
        new SurfaceExample(),
        new SceneFilesExample(),
        new CamerasExample(),
        new LightsExample(),
        new PanelShadowExample(),
        new ShadowsExample(),
        new ProbesExample(),
        new BounceExample(),
        new FieldExample(),
        new MaterialsExample(pictures),
        new TexturesExample(environment),
        new BlendExample(pictures),
        new DecalsExample(pictures),
        new DayAndNightExample(),
        new GodRaysExample(),
        new WeatherExample(),
        new VolumesExample(),
        new PostExample(),
        new MotionBlurExample(),
        new DistortionExample(),
        new OutlineExample(),
        new MeshesExample(environment),
        new ImportedExample(environment, new DiskSamples()),
        new SplatsExample(environment),
        new SpritesExample(pictures),
        new VideoExample(environment, File.Exists),
        new ManyExample(),
        new BatchingExample(),
        new CrowdExample(),
        new OverdrawExample(),
        new PipelineExample(environment),
        new BenchmarkExample(),
        new TerrainExample(),
        new VoxelExample(),
        new BistroExteriorExample(environment, File.Exists, File.ReadAllText),
        new BistroInteriorExample(environment, File.Exists, File.ReadAllText),
    ];
}

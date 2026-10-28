namespace Orblit.Examples;

/// <summary>Every example the gallery shows, in the order it lists them.</summary>
public static class EngineExamples
{
    /// <summary>
    /// A fresh set each call, because each example keeps its own settings. The
    /// examples that draw their own textures keep them in <paramref name="pictures"/>.
    /// </summary>
    public static IReadOnlyList<Example> All(IPictureFolder pictures) =>
    [
        new SurfaceExample(),
        new LightsExample(),
        new PanelShadowExample(),
        new ShadowsExample(),
        new ProbesExample(),
        new BounceExample(),
        new FieldExample(),
        new MaterialsExample(pictures),
        new TexturesExample(Environment.GetEnvironmentVariable),
        new BlendExample(pictures),
        new DecalsExample(pictures),
        new DayAndNightExample(),
        new GodRaysExample(),
        new WeatherExample(),
        new VolumesExample(),
    ];
}

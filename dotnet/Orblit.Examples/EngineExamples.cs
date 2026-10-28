namespace Orblit.Examples;

/// <summary>Every example the gallery shows, in the order it lists them.</summary>
public static class EngineExamples
{
    /// <summary>A fresh set each call, because each example keeps its own settings.</summary>
    public static IReadOnlyList<Example> All() =>
    [
        new SurfaceExample(),
        new BounceExample(),
    ];
}

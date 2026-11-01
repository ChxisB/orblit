using Orblit.VectorMath;

namespace Orblit.Filament.Avalonia.Tests;

internal static class Scenes
{
    /// <summary>One box in front of the camera, which is enough to draw.</summary>
    internal static RenderScene Box() => new()
    {
        Objects = [new RenderObject { Key = 1, Transform = Matrix4.Identity(), Colour = new Vector3(1, 1, 1) }],
        Camera = new RenderCamera { Position = new Vector3(0, 2, 8), Target = Vector3.Zero() },
    };

    /// <summary>Six boxes in a row, which the renderer merges into one draw.</summary>
    internal static RenderScene Row() => new()
    {
        Objects =
        [
            .. Enumerable.Range(0, 6).Select(i => new RenderObject
            {
                Key = 100 + i,
                Transform = Matrix4.TranslationValues(i, 0, 0),
                Colour = new Vector3(0.5, 0.4, 0.3),
            }),
        ],
        Camera = new RenderCamera { Position = new Vector3(0, 2, 8), Target = Vector3.Zero() },
    };
}

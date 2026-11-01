using Orblit.Filament;
using Orblit.VectorMath;

namespace Orblit.Stage.Tests;

internal static class Scenes
{
    public static RenderScene One() => new()
    {
        Objects = [new RenderObject { Key = 1, Transform = Matrix4.Identity(), Colour = new Vector3(1, 1, 1) }],
        Camera = new RenderCamera { Position = new Vector3(0, 0, 5), Target = Vector3.Zero() },
    };
}

namespace Orblit.Core;

/// <summary>
/// The transform components the core provides.
/// A scene graph is a tree and an archetype store is flat, so the hierarchy is a
/// component rather than a structure: an entity names its parent and the engine
/// derives world space. Reparenting is a write, not a move through a graph.
/// </summary>
/// <param name="Local">Ten floats: translation, rotation as a quaternion, scale.</param>
/// <param name="World">
/// Sixteen floats, column-major, as glTF and Filament both expect. Derived:
/// writing it is overwritten by the next propagation.
/// </param>
/// <param name="Parent">The parent's handle. Absent means the entity is a root.</param>
public sealed record TransformComponents(ComponentType Local, ComponentType World, ComponentType Parent);

public static class Transforms
{
    /// <summary>
    /// Builds a local transform without pulling in a maths package for three
    /// translations and an identity rotation.
    /// </summary>
    public static float[] Local(
        float x = 0,
        float y = 0,
        float z = 0,
        float rotationX = 0,
        float rotationY = 0,
        float rotationZ = 0,
        float rotationW = 1,
        float scaleX = 1,
        float scaleY = 1,
        float scaleZ = 1) =>
        [x, y, z, rotationX, rotationY, rotationZ, rotationW, scaleX, scaleY, scaleZ];
}

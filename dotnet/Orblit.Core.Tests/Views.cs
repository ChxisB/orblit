namespace Orblit.Core.Tests;

internal static class Views
{
    /// <summary>One entity's float32 component as an array, or null if it does not have it.</summary>
    public static float[]? Float32Of(this World world, ulong entity, ComponentType type) =>
        world.TryGetFloat32(entity, type, out var value) ? value.ToArray() : null;
}

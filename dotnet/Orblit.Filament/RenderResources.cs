namespace Orblit.Filament;

/// <summary>
/// Bytes handed to the renderer by name, looked for before the disk.
/// </summary>
/// <remarks>
/// A scene names its assets as paths: an object's mesh, a material's textures, an environment, a
/// decal, a splat capture. On a desktop reading them off disk is fine. Almost everywhere else it is
/// not: a browser has no disk, a mobile application's assets are inside its package, and anything
/// fetched over a network or out of a cache is already bytes in memory. <see cref="Provide"/> puts
/// bytes under a name, and from then on every renderer in the process looks there first, for the
/// files a <c>.gltf</c> names beside itself as well.
/// <para>
/// A name stands for bytes that do not change. Providing it again replaces what the next load sees,
/// but a renderer does not load again what it already has, so the way to change an asset is to give
/// the new bytes a new name. A content hash in it is the natural one. A name looked for before it
/// was provided is looked for again once it has been, so a scene may arrive before its bytes. It
/// draws without them until they do, so provide first wherever the order is yours.
/// </para>
/// </remarks>
public static class RenderResources
{
    /// <summary>
    /// The prefix <see cref="NameFor"/> puts on a name. Not a path anybody's assets are likely to
    /// sit under, and the renderer's own spelling beside the <c>orblit:target/</c> it already
    /// reserves for render targets.
    /// </summary>
    public const string Scheme = "orblit:resource/";

    /// <summary><paramref name="path"/> as a resource name: <c>robot.glb</c> is <c>orblit:resource/robot.glb</c>.</summary>
    public static string NameFor(string path) => Scheme + path;

    /// <summary>
    /// Keeps <paramref name="bytes"/> under <paramref name="name"/> for every renderer in the
    /// process. Copied on the way in, so the bytes may be reused or dropped as soon as this returns.
    /// </summary>
    public static void Provide(string name, ReadOnlySpan<byte> bytes)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        Renderer.Check(NativeMethods.ProvideResource(name, bytes, (nuint)bytes.Length), "provide_resource");
    }

    /// <summary>
    /// Lets go of the bytes under <paramref name="name"/>, and says whether there were any.
    /// Whatever a renderer already loaded from them it keeps.
    /// </summary>
    public static bool Release(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        var result = NativeMethods.ReleaseResource(name);
        if (result == (int)RendererResult.Range)
        {
            return false;
        }

        Renderer.Check(result, "release_resource");
        return true;
    }
}

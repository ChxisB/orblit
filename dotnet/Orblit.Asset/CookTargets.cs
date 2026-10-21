namespace Orblit.Asset;

/// <summary>
/// A <see cref="CookTarget"/> property every texture importer reads: which compressed formats the
/// device can sample. These live here rather than in the renderer so that a cook running on a build
/// machine, with no renderer in the process, can still write the names the renderer will look for.
/// </summary>
public static class TextureFamily
{
    public const string Astc = "astc";
    public const string Bc = "bc";
    public const string Etc2 = "etc2";

    /// <summary>
    /// Universal Basic, transcoded on load to whatever the device has. Bigger to decode but one file
    /// for every device, which is what a web build wants when it cannot know what it is running on.
    /// </summary>
    public const string Basis = "basis";

    /// <summary>Uncompressed RGBA. Always readable, never small.</summary>
    public const string Raw = "raw";

    public static IReadOnlyList<string> All { get; } = [Astc, Bc, Etc2, Basis, Raw];
}

/// <summary>
/// The platforms a project can be cooked for, and what each of them samples. A cook has to know a
/// device's limits without the device being there, so these are the conservative floor for each
/// platform rather than what the best machine running it could do. Cooking for the floor costs a
/// little size on a good device. Cooking for the ceiling means an app that does not run on half the
/// phones it was sold to.
/// </summary>
/// <remarks>
/// This is the table that makes "each app build contains only its own target's formats" true. An iOS
/// bundle carries ASTC and no BC, because no iOS device reads BC and shipping it would be dead
/// weight in an app-store download.
/// </remarks>
public static class CookTargets
{
    /// <summary>macOS, Windows and Linux: desktop GPUs read BC, and always have.</summary>
    public static CookTarget Macos { get; } = Desktop("macos");

    public static CookTarget Windows { get; } = Desktop("windows");

    public static CookTarget Linux { get; } = Desktop("linux");

    /// <summary>iOS: every Metal device reads ASTC, so there is nothing else to carry. The size limit is the A7's, because the floor is the point.</summary>
    public static CookTarget Ios { get; } = Make("ios", [TextureFamily.Astc], 8192, halfFloat: true);

    /// <summary>
    /// Android: ASTC where there is ASTC, ETC2 where there is not. ETC2 is guaranteed by OpenGL ES 3.0,
    /// which is the floor the engine targets, and carrying both is the price of one binary running on both.
    /// </summary>
    public static CookTarget Android { get; } = Make("android", [TextureFamily.Astc, TextureFamily.Etc2], 4096, halfFloat: true);

    /// <summary>
    /// The web, where a cook cannot know what it is running on. Basis Universal rather than a
    /// compressed family, because the page is served to whatever opens it and transcoding on load is
    /// the only way one file works everywhere. It costs decode time at startup and saves a download
    /// that would otherwise carry three formats to use one.
    /// </summary>
    public static CookTarget Web { get; } = Make("web", [TextureFamily.Basis], 4096, halfFloat: false);

    public static IReadOnlyList<string> Names { get; } = ["macos", "windows", "linux", "ios", "android", "web"];

    private static IReadOnlyDictionary<string, CookTarget> ByName { get; } = new[] { Macos, Windows, Linux, Ios, Android, Web }
        .ToDictionary(target => target.Name);

    /// <summary>The target called <paramref name="name"/>, or null.</summary>
    public static CookTarget? Find(string name) => ByName.GetValueOrDefault(DartText.ToLower(name));

    private static CookTarget Desktop(string name) => Make(name, [TextureFamily.Bc], 16384, halfFloat: true);

    private static CookTarget Make(string name, string[] families, long maxTextureSize, bool halfFloat) =>
        new(name, new Dictionary<string, object?>
        {
            ["textureFamilies"] = families,
            ["maxTextureSize"] = maxTextureSize,
            ["halfFloatTextures"] = halfFloat,
        });
}

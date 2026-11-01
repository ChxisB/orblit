using Orblit.Filament;
using Orblit.Light;
using Orblit.VectorMath;

namespace Orblit.Examples;

/// <summary>
/// Textures named by path, and an environment filtered from a picture at run
/// time. The files come from the environment, so the example shows whatever
/// is on this machine and names nothing it cannot find.
/// </summary>
/// <remarks>
/// <c>ORBLIT_TEXTURES</c> is the folder and <c>ORBLIT_TEXTURE_FILES</c> the
/// files in it, split on commas. <c>ORBLIT_PICTURE</c> is an .hdr or .exr to
/// light the wall with, and <c>ORBLIT_TEXTURE_SIZE</c> caps the texture size.
/// </remarks>
public sealed class TexturesExample : Example
{
    private const int Across = 4;

    private readonly string _folder;
    private readonly IReadOnlyList<string> _files;
    private readonly string? _picture;
    private readonly int? _largestTexture;

    private bool _lit = true;

    /// <summary>
    /// Reads its settings from <paramref name="environment"/>, which the
    /// gallery passes as the process environment and a test passes as a table.
    /// </summary>
    public TexturesExample(Func<string, string?> environment)
    {
        _folder = environment("ORBLIT_TEXTURES") ?? "../orblit/assets/bistro/Textures";
        _files =
        [
            .. (environment("ORBLIT_TEXTURE_FILES") ??
                "Concrete_BaseColor.ktx2,Plaster_BaseColor.ktx2,Pavement_Cobblestone_02_BaseColor.ktx2," +
                "MASTER_Roofing_Shingle_Grey_BaseColor.ktx2")
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
        ];
        _picture = environment("ORBLIT_PICTURE");
        _largestTexture = int.TryParse(environment("ORBLIT_TEXTURE_SIZE"), out var size) ? size : null;
    }

    public override string Name => "Textures";

    public override string Blurb =>
        "A wall of textures named at once: pictures, Basis, and cooked sets chosen by the device. Lit by a " +
        "picture filtered at run time.";

    public override ExampleSection Section => ExampleSection.Materials;

    public override ViewPoint ViewPoint => new() { Distance = 11, Pitch = 0.05, Yaw = 0 };

    public override RenderScene Scene(RenderCamera camera, double seconds)
    {
        var named = Named();
        var environment = _lit ? FoundPicture() : null;
        return new RenderScene
        {
            Objects = [.. _files.Select((_, i) => Panel(i))],
            Materials = [.. _files.Select((file, i) => PanelMaterial(10 + i, file, named[i]))],
            Camera = camera,
            Pipeline = _largestTexture is { } largest
                ? new Pipeline { Textures = new TextureLimits { MaxSize = largest } }
                : new Pipeline(),
            Lights =
            [
                new RenderLight
                {
                    Key = 900,
                    Kind = RenderLightKind.Directional,
                    Direction = new Vector3(-0.3, -0.6, -1),
                    Intensity = environment is null ? 60000 : 20000,
                },
            ],
            Environment = environment is null ? RenderEnvironment.None : RenderEnvironment.FromImage(environment),
            Sky = new Sky
            {
                Colour = Tint.Hex(0x3A4452).Linear,
                Ambient = environment is null ? 24000 : 0,
                Drawn = environment is null,
            },
        };
    }

    public override IReadOnlyList<ExampleSetting> Settings() =>
    [
        new Toggle("Picture lights it", _lit, value => _lit = value)
        {
            Enabled = _picture is not null,
            Note = _picture ?? "Set ORBLIT_PICTURE to an .hdr or .exr.",
        },
        new Caption($"{Named().Count(path => path is not null)} of {_files.Count} named, from {_folder}"),
    ];

    public override string Code => """
        // A device names the cooked forms of a texture it can read, best first.
        // Name the first one that is there.
        var wall = Device.TextureCandidates("wall.ktx2")
            .Select(candidate => $"textures/{candidate}")
            .FirstOrDefault(File.Exists);

        new RenderMaterial { Key = 1, BaseColourMap = wall is null ? null : new Texture(wall) };

        new RenderScene
        {
            Objects = [...],
            Environment = RenderEnvironment.FromImage("pictures/place.hdr"),
            Camera = camera,
        }
        """;

    /// <summary>
    /// The path each file is named by, or null when it is not in the folder in
    /// any form the device reads. Nothing is named until the host says what the
    /// device is.
    /// </summary>
    /// <remarks>shortcut: looks on disk every frame, so keep the result per device if a profile shows the cost.</remarks>
    private IReadOnlyList<string?> Named() =>
    [
        .. _files.Select(file =>
        {
            var path = $"{_folder}/{file}";
            var found = Device is { } device &&
                (File.Exists(path) || device.TextureCandidates(file).Any(candidate => File.Exists($"{_folder}/{candidate}")));
            return found ? path : null;
        }),
    ];

    private string? FoundPicture() => Device is not null && _picture is not null && File.Exists(_picture) ? _picture : null;

    private RenderObject Panel(int i)
    {
        var rows = (_files.Count + Across - 1) / Across;
        var (column, row) = (i % Across, i / Across);
        var transform = Matrix4.Identity();
        transform.SetTranslation(new Vector3((column - (Across - 1) / 2.0) * 2.3, ((rows - 1) / 2.0 - row) * 2.3, 0));
        transform.Multiply(Matrix4.Diagonal3Values(1, 1, 0.05));
        return new RenderObject { Key = 10 + i, Material = 10 + i, Transform = transform, Colour = new Vector3(1, 1, 1) };
    }

    private static RenderMaterial PanelMaterial(long key, string file, string? named) => new()
    {
        Key = key,
        BaseColour = new Vector4(1, 1, 1, 1),
        Roughness = 0.7,
        BaseColourMap = named is null ? null : new Texture(named, IsColour(file)),
    };

    /// <summary>Data maps are read as they are stored. Only a colour is decoded from sRGB.</summary>
    private static bool IsColour(string file)
    {
        var lower = file.ToLowerInvariant();
        return !(lower.Contains("normal") || lower.Contains("specular") || lower.Contains("roughness"));
    }
}

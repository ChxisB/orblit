using Orblit.Filament;
using Orblit.Light;
using Orblit.VectorMath;

namespace Orblit.Examples;

/// <summary>
/// Somebody else's geometry.
/// </summary>
/// <remarks>
/// A glTF file arrives with its own materials rather than being tinted by
/// whatever colour the object happened to carry, and it is parsed once however
/// many objects name it. A file that cannot be read is drawn as the placeholder
/// cube and says so. That is the only useful thing to do with that failure.
/// </remarks>
public sealed class MeshesExample : Example
{
    /// <summary>
    /// Empty unless the environment names one. There is no model in this
    /// repository to ship, and a path that only works on one machine is worse
    /// than a field that says what it wants.
    /// </summary>
    private string _path;

    private double _copies = 3;
    private bool _spinning = true;

    /// <summary>
    /// Reads <c>ORBLIT_MESH</c> from <paramref name="environment"/>, which the
    /// gallery passes as the process environment and a test passes as a table.
    /// </summary>
    public MeshesExample(Func<string, string?> environment) => _path = environment("ORBLIT_MESH") ?? "";

    public override string Name => "Meshes";

    public override string Blurb => "A glTF file, loaded once and instanced, with failures reported back.";

    public override ExampleSection Section => ExampleSection.Content;

    public override ViewPoint ViewPoint => new() { Distance = 9, Pitch = 0.22 };

    public override RenderScene Scene(RenderCamera camera, double seconds)
    {
        var total = (int)Math.Round(_copies, MidpointRounding.AwayFromZero);
        var turn = _spinning ? seconds * 0.4 : 0.0;
        return new RenderScene
        {
            Objects = [.. Enumerable.Range(0, total).Select(i => Copy(i, total, turn)), Floor()],
            Lights =
            [
                new RenderLight
                {
                    Key = 610,
                    Kind = RenderLightKind.Directional,
                    Intensity = 76000,
                    Direction = new Vector3(-0.5, -1, -0.4).Normalized(),
                    Colour = Tint.Hex(0xFFF3E0).Linear,
                },
            ],
            Sky = new Sky { Colour = Tint.Hex(0x1B222C).Linear, Ambient = 14000 },
            Camera = camera,
        };
    }

    public override IReadOnlyList<ExampleSetting> Settings() =>
    [
        new TextEntry("Path to a .glb or .gltf", _path, value =>
        {
            _path = value.Trim();
            Note = null;
        })
        {
            Hint = "/Users/you/models/crate.glb",
        },
        new Setting("Copies", _copies, 1, 8, value => _copies = value) { Decimals = 0 },
        new Toggle("Spinning", _spinning, value => _spinning = value),
    ];

    public override string Code => """
        // A path, on as many objects as want it.
        new RenderObject
        {
            Key = 600,
            Transform = placement,
            Colour = new Vector3(0.72, 0.13, 0.08),   // used only if the file will not load
            Mesh = "/Users/you/models/crate.glb",
        }

        // Parsed once and kept, however many objects name it. A scene arrives on
        // every frame of a drag, and re-reading a glTF at that rate is unusable.
        // The second object using a file gets another instance of it rather than
        // another copy.

        // What could not be loaded comes back from the renderer rather than going
        // to a log, so a host can name the asset it is missing.
        foreach (var note in renderer.GetNotes())
        {
            Show(note.About, note.Saying);
        }
        """;

    private static RenderObject Floor()
    {
        var transform = Matrix4.Identity();
        transform.SetTranslation(new Vector3(0, -1.8, 0));
        transform.ScaleByDouble(9.0, 0.06, 9.0, 1);
        return new RenderObject
        {
            Key = 590,
            Transform = transform,
            Colour = Tint.Hex(0x3B424C).Linear,
            CastShadows = false,
        };
    }

    private RenderObject Copy(int index, int total, double turn)
    {
        var transform = Matrix4.Identity();
        transform.SetTranslation(new Vector3((index - ((total - 1) / 2.0)) * 2.4, -0.6, 0));
        transform.RotateY(turn + (index * 0.4));
        return new RenderObject
        {
            Key = 600 + index,
            Transform = transform,
            Colour = Tint.Hex(0xD9634F).Linear,
            // The same path on every one of them: parsed once, and every object
            // after the first gets an instance of it.
            Mesh = _path.Length == 0 ? null : _path,
        };
    }
}

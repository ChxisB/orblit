using Orblit.Filament;
using Orblit.Light;
using Orblit.Motion;
using Orblit.Sequence;
using Orblit.Stage;
using Orblit.VectorMath;

namespace Orblit.Examples;

/// <summary>
/// Somebody else's models, and what their files hold: glTF, FBX and OBJ.
/// </summary>
/// <remarks>
/// Meshes shows that a file loads. This shows what is in one beyond the
/// geometry: the clips a character can play, the looks a product comes in and
/// the lights a lamp carries. They are read back from the renderer as it loaded
/// them, so the controls are the file's own names rather than numbers typed
/// into a box.
///
/// The files are Khronos's samples, fetched rather than committed by
/// <c>tool/fetch_import_samples.sh</c>. They are read here as bytes and handed
/// to the renderer, the way a browser, a phone's package or a download would
/// be, so the same example works in all of them.
/// </remarks>
public sealed class ImportedExample : Example
{
    private const string ByRenderer = "The renderer";
    private const string ByOrblit = "Orblit";
    private const string AsMade = "As made";

    /// <summary>How long a change of clip takes to blend, in seconds.</summary>
    private const double FadeSeconds = 0.4;

    private static readonly IReadOnlyList<Sample> Samples =
    [
        new("Fox", "Fox.glb", "Three clips on one skin"),
        new("Cesium Man", "CesiumMan.glb", "A walk cycle"),
        new("Shoe", "MaterialsVariantsShoe.glb", "One model, three looks"),
        new("Lamp", "LightsPunctualLamp.glb", "Lights inside the file"),
        new("Clear coat", "ClearCoatTest.glb", "A lacquer over paint"),
        new("Sheen chair", "SheenChair.glb", "Velvet"),
        new("Barn lamp", "AnisotropyBarnLamp.glb", "Something the renderer cannot draw, said out loud"),
        new("Dancer", "Samba Dancing.fbx", "An FBX from Mixamo, converted on the way in"),
        new("Man", "male02/male02.obj", "An OBJ with its material library and pictures")
        {
            Companions =
            [
                "male02/male02.mtl",
                "male02/01_-_Default1noCulling.JPG",
                "male02/male-02-1noCulling.JPG",
                "male02/orig_02_-_Defaul1noCulling.JPG",
            ],
        },
    ];

    private readonly ISampleFiles _files;

    /// <summary>Where the fetch script puts the files, or wherever the environment says they are instead.</summary>
    private readonly string _directory;

    private Sample _sample;

    /// <summary>Which of the file's clips plays, or null for the first it has.</summary>
    private int? _clip;

    /// <summary>Which of the file's material variants it wears, or null for its own.</summary>
    private int? _variant;

    private bool _playing = true;
    private double _speed = 1;

    /// <summary>Whether the lights the file carries are stated as scene lights.</summary>
    private bool _fileLights = true;

    /// <summary>Whether the sun is up. The lamp's own lights are hard to see at noon.</summary>
    private bool _daylight = true;

    /// <summary>The clip being faded out of, and when the fade began.</summary>
    private int? _fadingFrom;

    private double _fadeStarted;

    /// <summary>
    /// Whether the file's clips are played by Orblit rather than by the
    /// renderer: imported from the file, sampled here and handed over as
    /// joints. The two are alternatives, never layers, because both would
    /// write the same joints.
    /// </summary>
    private bool _ourClips;

    /// <summary>Each glTF file's bytes, kept for importing its clips from.</summary>
    private readonly Dictionary<string, byte[]> _bytes = [];

    /// <summary>Each file's clips as Orblit reads them, or null for a file it could not.</summary>
    private readonly Dictionary<string, ClipsImported?> _imported = [];

    /// <summary>What plays Orblit's copy of the chosen clip, and when it last moved on.</summary>
    private ClipPlayer? _player;

    private double _advanced;

    /// <summary>What carries a frame to the file's skins, and which file they are for.</summary>
    private List<SkinBinding> _bindings = [];

    private string? _bound;

    /// <summary>Which files have been handed to the renderer, and which are on their way.</summary>
    private readonly HashSet<string> _provided = [];

    private readonly HashSet<string> _reading = [];

    /// <summary>When the scene last ran, so a change of clip starts its fade at the right time.</summary>
    private double _now;

    /// <summary>
    /// Reads <c>ORBLIT_SAMPLES</c> and <c>ORBLIT_MODEL</c> from
    /// <paramref name="environment"/>. The second picks the first model, for
    /// whatever drives the gallery from outside and has no panel to click. The
    /// files are read through <paramref name="files"/>.
    /// </summary>
    public ImportedExample(Func<string, string?> environment, ISampleFiles files)
    {
        _files = files;
        _directory = environment("ORBLIT_SAMPLES") ?? "../orblit/assets/samples";
        _sample = Samples.FirstOrDefault(sample => sample.Name == environment("ORBLIT_MODEL")) ?? Samples[0];
    }

    public override string Name => "Imported models";

    public override ExampleSection Section => ExampleSection.Content;

    public override string Blurb =>
        "Clips, material variants and lights read from glTF files, under the names the files give them.";

    public override ViewPoint ViewPoint => new() { Distance = 4.2, Pitch = 0.18, Height = 0.8, Yaw = 0.6 };

    public override Downloadable? Needs => new()
    {
        What = "Khronos's glTF samples",
        Size = "about 25 MB",
        From = "KhronosGroup/glTF-Sample-Assets",
        Licence = "CC BY 4.0 and others, per model",
        Command = ["tool/fetch_import_samples.sh"],
    };

    private string Resource => NameOf(_sample.File);

    public override RenderScene Scene(RenderCamera camera, double seconds)
    {
        _now = seconds;
        Provide(_sample);
        var ready = _provided.Contains(Resource);
        var info = Models.GetValueOrDefault(Resource);
        var placement = Standing(info);
        var (animation, joints) = ready && info is { Clips.Count: > 0 } ? Motion(info, seconds) : (null, null);
        return new RenderScene
        {
            Objects =
            [
                .. ready
                    ? [
                        new RenderObject
                        {
                            Key = 1,
                            Transform = placement,
                            Colour = Tint.Hex(0xD9634F).Linear,
                            Mesh = Resource,
                            Animation = animation,
                            Joints = joints,
                            Variant = _variant,
                        },
                    ]
                    : Array.Empty<RenderObject>(),
                Floor(),
            ],
            Lights = Lights(ready ? info : null, placement),
            Sky = new Sky { Colour = Tint.Hex(0x1B222C).Linear, Ambient = _daylight ? 14000 : 2 },
            Camera = _daylight ? camera : Night(camera),
        };
    }

    public override IReadOnlyList<ExampleSetting> Settings()
    {
        var info = Models.GetValueOrDefault(Resource);
        List<string> clips = [.. (info?.Clips ?? []).Select((clip, i) => clip.Name.Length == 0 ? $"Clip {i + 1}" : clip.Name)];
        return
        [
            new Choice("Model", [.. Samples.Select(sample => sample.Name)], _sample.Name, Choose),
            new Caption(_sample.Shows),
            ClipChoice(clips),
            .. PlayedBy(clips.Count > 0),
            LookChoice([.. info?.Variants ?? []]),
            new Toggle("Playing", _playing, value => _playing = value),
            new Setting("Speed", _speed, 0.1, 2, value => _speed = value) { Decimals = 1 },
            new Toggle("The file's lights", _fileLights, value => _fileLights = value),
            new Toggle("Daylight", _daylight, value => _daylight = value),
            .. info is { Unsupported.Count: > 0 }
                ? [new Caption($"Not drawn: {string.Join(", ", info.Unsupported)}")]
                : Array.Empty<ExampleSetting>(),
        ];
    }

    public override string Code => """
        // What a file holds comes back from the renderer as it is loaded.
        var (notes, models) = AssetInfo.Split(reported);

        // A clip by the file's own name, fading in from the one before.
        var info = models.Single(model => model.Path == "orblit:resource/fox.glb");
        new RenderObject
        {
            Key = 1,
            Transform = placement,
            Colour = grey,
            Mesh = "orblit:resource/fox.glb",
            Animation = new ModelAnimation
            {
                Clip = info.ClipNamed("Run")!.Value,
                Seconds = seconds,
                From = new ModelAnimation { Clip = info.ClipNamed("Walk")!.Value, Seconds = seconds },
                Fade = 0.5,
            },
        };

        // Or the same clips played by Orblit: imported from the file, sampled
        // here and set on the skin joint by joint. Never both on one model.
        var walk = GltfImport.Clips(bytes).Clips[1];
        var player = new ClipPlayer(walk, WhenDone.Loop);
        player.Play();
        var skin = info.Skins[0];
        var binding = new SkinBinding(SkinRig.ArmatureOf(skin), skin, index: 0);
        new RenderObject
        {
            // ...
            Joints = binding.JointsFrom(player.Advance(elapsed).Frame.Bones[""]),
        };

        // A look the file comes in, by its name.
        new RenderObject { /* ... */ Variant = info.Variants.ToList().IndexOf("beach") };

        // The file's lights, as ordinary lights that shadow like any other.
        Lights = [.. info.LightsFor(placement, keyOf: i => 100 + i)];
        """;

    private static string NameOf(string file) => RenderResources.NameFor($"samples/{file}");

    private void Choose(string name)
    {
        Note = null;
        if (name == _sample.Name)
        {
            return;
        }

        _sample = Samples.First(sample => sample.Name == name);
        _clip = null;
        _variant = null;
        _fadingFrom = null;
        _player = null;
    }

    private Choice ClipChoice(List<string> clips) => clips.Count == 0
        ? new Choice("Clip", ["None"], "None", null)
        : new Choice("Clip", clips, clips[Math.Clamp(_clip ?? 0, 0, clips.Count - 1)], value => FadeTo(clips.IndexOf(value)));

    private void FadeTo(int chosen)
    {
        if (chosen < 0 || chosen == (_clip ?? 0))
        {
            return;
        }

        _fadingFrom = _clip ?? 0;
        _fadeStarted = _now;
        _clip = chosen;
    }

    /// <summary>Who plays the file's clips, and what Orblit's way leaves out. Only glTF clips are imported.</summary>
    private List<ExampleSetting> PlayedBy(bool hasClips) =>
    [
        new Choice("Played by", [ByRenderer, ByOrblit], _ourClips ? ByOrblit : ByRenderer, hasClips && _sample.IsGltf ? PlayBy : null),
        .. _ourClips && _sample.IsGltf
            ? [
                new Caption(
                    "Imported as clips, sampled by Orblit and set on the skin joint by joint. "
                    + $"A change of clip cuts rather than fades.{ProblemsOf(Resource)}"),
            ]
            : Array.Empty<ExampleSetting>(),
    ];

    private Choice LookChoice(List<string> variants) => new(
        "Look",
        [AsMade, .. variants],
        _variant is { } wearing && wearing < variants.Count ? variants[wearing] : AsMade,
        variants.Count == 0 ? null : value => _variant = variants.IndexOf(value) is var chosen and >= 0 ? chosen : null);

    private void PlayBy(string value)
    {
        _ourClips = value == ByOrblit;
        _player = null;
    }

    /// <summary>
    /// Asks for <paramref name="sample"/>'s files once, and again after a
    /// failed try, so a fetch made while the gallery is open is picked up.
    /// </summary>
    private void Provide(Sample sample)
    {
        var resource = NameOf(sample.File);
        if (_provided.Contains(resource) || !_reading.Add(resource))
        {
            return;
        }

        _ = HandOver(sample, resource);
    }

    /// <summary>The companions first, so the file is never named before what it names.</summary>
    private async Task HandOver(Sample sample, string resource)
    {
        try
        {
            foreach (var file in sample.Companions.Append(sample.File))
            {
                if (await _files.ReadAsync(Path.Combine(_directory, file)) is not { } bytes)
                {
                    Note = $"No {file} in {_directory}. Fetch it with tool/fetch_import_samples.sh.";
                    return;
                }

                _files.Provide(NameOf(file), bytes);
                if (file == sample.File && sample.IsGltf)
                {
                    _bytes[resource] = bytes;
                }
            }

            _provided.Add(resource);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Note = $"{sample.File} could not be read: {error.Message}";
        }
        finally
        {
            _reading.Remove(resource);
        }
    }

    /// <summary>
    /// A placement that stands the model on the ground, about as tall as a
    /// person, whatever units its file was made in. Worked out from the bounds
    /// the renderer reported, so it is the file's own until those arrive.
    /// </summary>
    private static Matrix4 Standing(AssetInfo? info)
    {
        var placement = Matrix4.Identity();
        if (info is null)
        {
            return placement;
        }

        var low = info.BoundsMin;
        var high = info.BoundsMax;
        var size = high - low;
        var largest = Math.Max(size.X, Math.Max(size.Y, size.Z));
        if (largest <= 0)
        {
            return placement;
        }

        var scale = 1.6 / largest;
        placement.TranslateByDouble(-(low.X + high.X) / 2 * scale, -low.Y * scale, -(low.Z + high.Z) / 2 * scale, 1);
        placement.ScaleByDouble(scale, scale, scale, 1);
        return placement;
    }

    /// <summary>
    /// Orblit's joints when it is playing and has the clip, or else the
    /// renderer's animation.
    /// </summary>
    private (ModelAnimation? Animation, IReadOnlyList<JointPose>? Joints) Motion(AssetInfo info, double seconds)
    {
        var playing = Math.Clamp(_clip ?? 0, 0, info.Clips.Count - 1);
        return _ourClips && OurJoints(info, playing, seconds) is { } joints
            ? (null, joints)
            : (Animation(playing, seconds), null);
    }

    private ModelAnimation Animation(int playing, double seconds)
    {
        var time = _playing ? seconds * _speed : 0.0;
        var rate = _playing ? _speed : 0.0;
        var from = _fadingFrom;
        var fade = Math.Clamp((seconds - _fadeStarted) / FadeSeconds, 0.0, 1.0);
        if (fade >= 1)
        {
            _fadingFrom = null;
        }

        return new ModelAnimation
        {
            Clip = playing,
            Seconds = time,
            Speed = rate,
            From = from is { } leaving && fade < 1 ? new ModelAnimation { Clip = leaving, Seconds = time, Speed = rate } : null,
            Fade = fade,
        };
    }

    /// <summary>
    /// Every joint of the file's skins where Orblit's copy of clip
    /// <paramref name="chosen"/> has them, or null when Orblit has nothing to
    /// play and the renderer should.
    /// </summary>
    private List<JointPose>? OurJoints(AssetInfo info, int chosen, double seconds)
    {
        var clips = ImportedFrom(Resource)?.Clips ?? [];
        if (chosen >= clips.Count || info.Skins.Count == 0)
        {
            return null;
        }

        if (_bound != Resource)
        {
            _bindings = [.. info.Skins.Select((skin, i) => new SkinBinding(SkinRig.ArmatureOf(skin), skin, i))];
            _bound = Resource;
        }

        // A new clip starts from its beginning, cut rather than faded, and
        // loops as the renderer loops a file's clip.
        if (_player is not { } player || !ReferenceEquals(player.Clip, clips[chosen]))
        {
            player = _player = new ClipPlayer(clips[chosen], WhenDone.Loop);
            _advanced = seconds;
        }

        player.Speed = _speed;
        if (_playing)
        {
            player.Play();
        }
        else
        {
            player.Pause();
        }

        var frame = player.Advance(seconds - _advanced).Frame;
        _advanced = seconds;
        var bones = frame.Bones.GetValueOrDefault("") ?? [];
        return [.. _bindings.SelectMany(binding => binding.JointsFrom(bones))];
    }

    /// <summary>
    /// The file's clips as Orblit reads them, imported the first time they are
    /// asked for. Null until the file has been read, and for a file that is
    /// not glTF or could not be read as it.
    /// </summary>
    private ClipsImported? ImportedFrom(string resource)
    {
        if (_imported.TryGetValue(resource, out var known))
        {
            return known;
        }

        if (!_bytes.TryGetValue(resource, out var bytes))
        {
            return null;
        }

        try
        {
            return _imported[resource] = GltfImport.Clips(bytes);
        }
        catch (ClipFormatException error)
        {
            Note = $"Orblit could not read the clips: {error.Message}";
            return _imported[resource] = null;
        }
    }

    /// <summary>What the import left behind, said after the rest, or nothing.</summary>
    private string ProblemsOf(string resource) =>
        _imported.GetValueOrDefault(resource)?.Problems is { Count: > 0 } problems
            ? $" Left out: {string.Join(' ', problems)}"
            : "";

    /// <summary>
    /// The sun, and the file's own lights as ordinary lights. The renderer
    /// takes them out of the model, so they shadow and count like any other.
    /// </summary>
    private List<RenderLight> Lights(AssetInfo? info, Matrix4 placement)
    {
        List<RenderLight> lights = [];
        if (_daylight)
        {
            lights.Add(new RenderLight
            {
                Key = 3,
                Kind = RenderLightKind.Directional,
                Intensity = 76000,
                Direction = new Vector3(-0.5, -1, -0.4).Normalized(),
                Colour = Tint.Hex(0xFFF3E0).Linear,
            });
        }

        if (_fileLights && info is not null)
        {
            lights.AddRange(info.LightsFor(placement, index => 100 + index));
        }

        return lights;
    }

    /// <summary>
    /// At night, a camera for night. A file's lights are stated as the file
    /// made them, and the lamp's bulb is about twenty lumens. That is a real
    /// bulb's worth, which at a daylight exposure is black.
    /// </summary>
    private static RenderCamera Night(RenderCamera camera) => new()
    {
        Position = camera.Position,
        Target = camera.Target,
        FieldOfView = camera.FieldOfView,
        Aperture = 1.4,
        ShutterSpeed = 1.0 / 30,
        Sensitivity = 3200,
    };

    private static RenderObject Floor()
    {
        var transform = Matrix4.Identity();
        transform.SetTranslation(new Vector3(0, -0.05, 0));
        transform.ScaleByDouble(6, 0.05, 6, 1);
        return new RenderObject { Key = 2, Transform = transform, Colour = Tint.Hex(0x3B424C).Linear, CastShadows = false };
    }

    /// <summary>One of the files this example can show, and what it is there to show.</summary>
    /// <param name="Name">What the list calls it.</param>
    /// <param name="File">Its file, as the sample collection names it.</param>
    /// <param name="Shows">Why it is in the list.</param>
    private sealed record Sample(string Name, string File, string Shows)
    {
        /// <summary>
        /// The files it names beside itself, such as an OBJ's material library
        /// and its pictures. Handed over under names beside its own, as a host
        /// serving them from one folder would.
        /// </summary>
        public IReadOnlyList<string> Companions { get; init; } = [];

        public bool IsGltf => File.EndsWith(".glb", StringComparison.Ordinal) || File.EndsWith(".gltf", StringComparison.Ordinal);
    }
}

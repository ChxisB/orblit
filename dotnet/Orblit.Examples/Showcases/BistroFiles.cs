using System.Text.Json;
using Orblit.Filament;
using Orblit.Light;
using Orblit.VectorMath;

namespace Orblit.Examples;

/// <summary>
/// The Amazon Lumberyard Bistro's files, wherever the fetch script put them.
/// The street and the room are two models in one folder, and both are lit
/// from fixtures read out of their own emissive geometry.
/// </summary>
/// <remarks>
/// Every other example builds its scene out of cubes, which is honest about
/// what it shows and useless for judging whether the lighting looks right. A
/// cube lit badly still looks like a cube. This is somebody else's art, built
/// for a different renderer, with reference images of how it is supposed to
/// look. That is the only way to find out whether a lighting model is
/// convincing rather than merely arithmetic.
///
/// It is also the heaviest thing here by a wide margin: 1,296 meshes, 132
/// materials and 2.8 million triangles in the exterior, against a hundred
/// point lights. What a frame costs on this is worth more than what it costs
/// on a hundred thousand cubes, because it is shaped like a real scene.
///
/// Amazon Lumberyard Bistro, Open Research Content Archive (ORCA),
/// https://developer.nvidia.com/orca/amazon-lumberyard-bistro. Amazon
/// Lumberyard, CC BY 4.0. Fetched rather than shipped, because it is half a
/// gigabyte and not ours: <c>tool/fetch_bistro.sh exterior</c>.
/// </remarks>
internal sealed class BistroFiles
{
    /// <summary>What each kind of fixture is, in units a fitting is sold in.</summary>
    /// <remarks>
    /// Lumens rather than a brightness between nought and one, because these
    /// are real fittings in a real street. A sodium street lamp is a couple of
    /// thousand lumens and a festoon bulb is under a hundred. That ratio of
    /// twenty-five to one is most of why the reference images read as evening
    /// rather than as a stage set.
    ///
    /// Reach is a cull distance as much as a physical one. Beyond it the light
    /// adds nothing and the renderer can skip it. Set too short, it shows as a
    /// hard edge where a pool of light stops. Set to what a lamp really
    /// lights, the pools overlap the way they do in a street.
    /// </remarks>
    private static readonly Dictionary<string, Fitting> Fittings = new()
    {
        ["street"] = new(0xFFB870, 5200, 28),
        ["spot"] = new(0xFFD5A8, 1400, 12),
        ["sign"] = new(0xFFE0B0, 900, 9),
        ["orange"] = new(0xFF8A3D, 70, 4),
        ["red"] = new(0xFF4A4A, 70, 4),
        ["white"] = new(0xFFF2DC, 90, 4),
        ["pink"] = new(0xFF7ACB, 70, 4),
        ["blue"] = new(0x5AA6FF, 70, 4),
        ["green"] = new(0x6BE86B, 70, 4),
    };

    private readonly Func<string, bool> _exists;
    private readonly Func<string, string> _read;

    /// <summary>
    /// The engine repository's own folder by default, because that is where
    /// the fetch script puts them, and <c>ORBLIT_BISTRO</c> for a checkout
    /// that keeps them somewhere else.
    /// </summary>
    private readonly string _directory;

    /// <summary>
    /// Looks in <paramref name="environment"/> for the folder, and through
    /// <paramref name="exists"/> and <paramref name="read"/> at what is in it.
    /// The gallery passes the process environment and the file system. A test
    /// passes tables.
    /// </summary>
    public BistroFiles(Func<string, string?> environment, Func<string, bool> exists, Func<string, string> read)
    {
        _exists = exists;
        _read = read;
        _directory = environment("ORBLIT_BISTRO") ?? "../orblit/assets/bistro";
    }

    /// <summary>
    /// The prefiltered environment cmgen made when the scene was fetched, or
    /// empty when it has not been built. The scene then falls back to the flat
    /// ambient. That is worth saying out loud, because the difference between
    /// the two is most of the difference between a render and a photograph.
    /// </summary>
    public string Radiance => Present("bistro_ibl.ktx");

    public string Skybox => Present("bistro_skybox.ktx");

    /// <summary>What to say while <paramref name="asset"/> has not been fetched, or null once it has.</summary>
    public string? Missing(string asset) => _exists(ModelOf(asset))
        ? null
        : "The Bistro is not here yet. Set ORBLIT_BISTRO if your checkout keeps it somewhere else.";

    /// <summary>
    /// The model, as one object. Its own root node carries the turn from Z up
    /// and the scale into metres, so nothing is done to it here.
    /// </summary>
    public RenderObject Model(string asset) => new()
    {
        Key = 1,
        Transform = Matrix4.Identity(),
        Colour = new Vector3(0.8, 0.8, 0.8),
        Mesh = Path.GetFullPath(ModelOf(asset)),
    };

    /// <summary>The fixtures the fetch script took out of <paramref name="asset"/>, or none when it has not run.</summary>
    public List<BistroFixture> Fixtures(string asset)
    {
        var path = Path.Combine(_directory, $"{asset}.lights.json");
        if (!_exists(path))
        {
            return [];
        }

        using var rows = JsonDocument.Parse(_read(path));
        return [.. rows.RootElement.EnumerateArray().Select(Fixture)];
    }

    /// <summary>
    /// Each fixture of a kind this knows and of at least
    /// <paramref name="dimmest"/> lumens, as a point light keyed from 100.
    /// </summary>
    public static List<RenderLight> Lamps(IEnumerable<BistroFixture> fixtures, double dimmest)
    {
        var lamps = new List<RenderLight>();
        foreach (var fixture in fixtures)
        {
            if (Fittings.GetValueOrDefault(fixture.Kind) is { } fitting && fitting.Lumens >= dimmest)
            {
                lamps.Add(new RenderLight
                {
                    Key = 100 + lamps.Count,
                    Kind = RenderLightKind.Point,
                    Position = fixture.At,
                    Colour = Tint.Hex(fitting.Colour).Linear,
                    Intensity = fitting.Lumens,
                    FalloffRadius = fitting.Reach,
                    // A hundred shadow-casting points is not a thing any
                    // real-time renderer does, and it is not what makes this
                    // read. The shadows that matter come from the sun or moon.
                    CastShadows = false,
                });
            }
        }

        return lamps;
    }

    private string ModelOf(string asset) => Path.Combine(_directory, $"{asset}.gltf");

    private string Present(string name)
    {
        var path = Path.Combine(_directory, name);
        return _exists(path) ? Path.GetFullPath(path) : "";
    }

    private static BistroFixture Fixture(JsonElement row)
    {
        var at = row.GetProperty("at");
        return new BistroFixture(
            row.GetProperty("kind").GetString() ?? "",
            new Vector3(at[0].GetDouble(), at[1].GetDouble(), at[2].GetDouble()));
    }

    /// <summary>A fitting's colour as 0xRRGGBB, its lumens and how far it reaches in metres.</summary>
    private sealed record Fitting(int Colour, double Lumens, double Reach);
}

/// <summary>One emissive fixture in the scene, and where it is.</summary>
internal sealed record BistroFixture(string Kind, Vector3 At);

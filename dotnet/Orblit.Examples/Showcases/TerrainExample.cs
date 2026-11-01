using System.Globalization;
using Orblit.Filament;
using Orblit.Light;
using Orblit.Noise;
using Orblit.Stage;
using Orblit.Terrain;
using Orblit.VectorMath;
using Ground = Orblit.Terrain.Terrain;
using GroundSet = Orblit.Terrain.TerrainSet;

namespace Orblit.Examples;

/// <summary>
/// Ground as data, drawn as one grid.
/// </summary>
/// <remarks>
/// Half a kilometre of hills is sixteen regions of heights, a texel every two
/// metres, sent to the renderer once. What draws them is the same small grid a
/// few times over, each copy twice the size of the one inside it and all of
/// them centred on the camera, raised by the heights on the GPU. So the ground
/// is a handful of draws however far it reaches, and moving the camera sends
/// nothing.
///
/// Nothing here says where the rock goes. Every texel is left automatic, and
/// the renderer chooses between the two sets by how steep and how high the
/// ground is. So the sliders that change that choice change only settings.
///
/// Nothing says where the grass, the stones and the trees go either. They are
/// rules kept with the terrain: how thick, on which set, on what slope. A
/// placer works out where each one stands, a region at a time, again only
/// where the ground changes. Each is a block, so a layer in a region is one
/// population, and tens of thousands of tufts are a draw or two.
///
/// The box that drives round it stands on the ground by asking the terrain
/// where the ground is. Those are the same heights, read the way the renderer
/// reads them, with no physics involved.
/// </remarks>
public sealed class TerrainExample : Example
{
    /// <summary>How far the ground reaches each way from the middle, in metres.</summary>
    public const double Reach = RegionSize * RegionsOut * Spacing;

    /// <summary>How far out the rover drives, in metres.</summary>
    public const double RoverRadius = 60;

    // Texels a region is across, the regions each way from the middle, and
    // metres between texels.
    private const int RegionSize = 64;
    private const int RegionsOut = 2;
    private const double Spacing = 2;

    // Texels a set's pictures are across.
    private const int PictureSize = 128;

    // The cube runs from minus one to one, so these are half of 2.2 by 1.6 by
    // 3.6 metres.
    private static readonly Vector3 RoverHalf = new(1.1, 0.8, 1.8);

    // Grass on the grass set, stones on the rock, and trees on the gentler,
    // lower grass. A tree is a trunk and a crown, two layers on one seed so
    // that they stand together.
    private static readonly ScatterLayer[] Rules =
    [
        new("grass", seed: 1, density: 0.5, sets: [1], maxSlope: 35, size: (0.4, 0.3, 0.4),
            minScale: 0.5, maxScale: 1.4, lean: 0.7, lift: -0.08, colour: 0x5E8C3A, colourVariation: 0.25, range: 70),
        new("stones", seed: 2, density: 0.03, sets: [0], size: (1.2, 0.7, 0.9),
            minScale: 0.4, maxScale: 1.6, lean: 1, lift: -0.25, colour: 0x8A8580, colourVariation: 0.2, range: 160,
            castShadows: true),
        new("trunks", seed: 3, density: 0.004, sets: [1], maxSlope: 22, maxHeight: 30, size: (0.45, 4, 0.45),
            minScale: 0.7, maxScale: 1.3, lift: -0.3, colour: 0x5A3E28, castShadows: true),
        // The crown's lift is the trunk's top, less its sinking and a little
        // more, so the crown sits over the trunk rather than on it.
        new("crowns", seed: 3, density: 0.004, sets: [1], maxSlope: 22, maxHeight: 30, size: (2.6, 3.4, 2.6),
            minScale: 0.7, maxScale: 1.3, lift: 3.2, colour: 0x2F5A2A, colourVariation: 0.15, castShadows: true),
    ];

    private readonly Dictionary<string, byte[]> _pictures = [];

    // How high the tallest ridge can reach, in metres.
    private double _relief = 60;

    // How quickly slope hands the ground over to rock, and how quickly height
    // does, per hundred metres.
    private double _steepness = 1.2;
    private double _altitude = 0.2;

    // How hard the edge between grass and rock is.
    private double _sharpness = 0.87;

    // Whether rock is laid from three sides, so cliffs are not smeared.
    private bool _triplanar = true;

    // How many times the grid is drawn, each twice the size of the last.
    private double _rings = 6;

    private bool _rover = true;
    private bool _scatter = true;
    private int _seed = 1;

    // What the placer last drew, kept so that only a region whose ground
    // changed is placed and sent again.
    private IReadOnlyList<Population> _scattered = [];
    private long _scatteredRevision = -1;

    public TerrainExample()
    {
        PaintSets();
        Ground = Build();
    }

    /// <summary>The ground, as data: what is drawn, and what the rover stands on.</summary>
    public Ground Ground { get; private set; }

    /// <summary>Where the scatter stands, kept between frames.</summary>
    public ScatterPlacer Placer { get; } = new();

    public override string Name => "Terrain";

    public override string Blurb =>
        "Hills from sixteen regions of heights, textured by slope and height, with grass, stones and trees scattered by rule.";

    public override ExampleSection Section => ExampleSection.Showcases;

    public override ViewPoint ViewPoint => new() { Distance = 120, Pitch = 0.36, Height = 14, Yaw = 0.9 };

    public override RenderScene Scene(RenderCamera camera, double seconds)
    {
        Ground.AutoCover = new AutoCover(steep: 0, flat: 1, slope: _steepness, heightFalloff: _altitude);
        Ground.BlendSharpness = _sharpness;
        var rock = Ground.Sets[0];
        if (rock.Triplanar != _triplanar)
        {
            Ground.Sets[0] = new GroundSet(rock.Name, rock.Albedo, rock.Normal, rock.TileSize, _triplanar);
        }

        // Only where something changed: a region's ground, or the cover rule
        // the sliders above move, which takes the grass up or down the slopes.
        Placer.Update(Ground);
        if (Placer.Revision != _scatteredRevision)
        {
            _scattered = ScatterView.From(Placer, 100).Populations;
            _scatteredRevision = Placer.Revision;
        }

        return new RenderScene
        {
            Camera = camera,
            // Built every frame, and cheap to: the regions' maps are handed
            // over as they are, and only one whose revision moved is sent.
            Terrain =
            [
                TerrainView.From(Ground, 1, new TerrainViewOptions
                {
                    Pixels = path => _pictures.GetValueOrDefault(path),
                    Levels = (int)Math.Round(_rings, MidpointRounding.AwayFromZero),
                }),
            ],
            Objects = _rover ? [Rover(seconds)] : [],
            Populations = _scatter ? _scattered : [],
            Lights =
            [
                new RenderLight
                {
                    Key = 1,
                    Kind = RenderLightKind.Directional,
                    // Low, so the ridges throw shadows down their far sides.
                    Direction = new Vector3(-0.6, -0.45, -0.4).Normalized(),
                    Colour = Tint.Hex(0xFFEBD2).Linear,
                    Intensity = 96000,
                    CastShadows = true,
                },
            ],
            Fog = new Fog
            {
                Colour = Tint.Hex(0xB9CCDD).Linear,
                // Thick enough by the far edge that the ground ends in haze.
                Density = 0.012,
                Distance = 140,
                MaximumOpacity = 1,
            },
            Sky = new Sky
            {
                Zenith = Tint.Hex(0x4A7FB8).Linear,
                Horizon = Tint.Hex(0xB9CCDD).Linear,
                Ambient = 24000,
            },
        };
    }

    public override IReadOnlyList<ExampleSetting> Settings() =>
    [
        new Caption(
            $"{Ground.Regions.Count()} regions of {RegionSize} × {RegionSize} heights, sent once. "
            + "Everything below changes settings only, except Relief and World."),
        // Once the handle is let go, because this makes every region again.
        new Setting("Relief", _relief, 4, 80, value => _relief = value)
        {
            Settle = _ => Ground = Build(),
            Unit = " m",
            Decimals = 0,
        },
        new Setting("Steepness", _steepness, 0.5, 4, value => _steepness = value),
        new Setting("Altitude", _altitude, 0, 2, value => _altitude = value),
        new Setting("Sharpness", _sharpness, 0, 1, value => _sharpness = value),
        new Setting("Rings", _rings, 1, 8, value => _rings = value) { Decimals = 0 },
        new Toggle("Triplanar", _triplanar, value => _triplanar = value)
        {
            Note = _triplanar ? "Rock laid from three sides" : "Rock dropped from above, and stretched down cliffs",
        },
        new Toggle("Scatter", _scatter, value => _scatter = value)
        {
            Note = $"{Placer.Count} grass, stones and trees, placed by rule",
        },
        new Toggle("Rover", _rover, value => _rover = value)
        {
            Note = "Placed by HeightAt and NormalAt, with no physics",
        },
        new Choice("World", ["1", "2", "3", "4"], _seed.ToString(CultureInfo.InvariantCulture), option =>
        {
            _seed = int.Parse(option, CultureInfo.InvariantCulture);
            Ground = Build();
        }),
    ];

    public override string Code => """
        // Ground as data: regions of heights, made only where there is ground.
        var terrain = new Terrain(regionSize: 64, spacing: 2, sets:
        [
            new TerrainSet("rock", "rock.png", "rock_normal.png", tileSize: 12, triplanar: true),
            new TerrainSet("grass", "grass.png", "grass_normal.png", tileSize: 4),
        ]);
        foreach (var key in keys)
        {
            terrain.FillHeights(key, (x, z) => relief * hills.Unit(x / 120, z / 120));
        }

        // New ground is automatic: rock where it is steep or high, grass elsewhere.
        terrain.AutoCover = new AutoCover(steep: 0, flat: 1, slope: 1.2);

        // What grows on it, by rule. A trunk and a crown share a seed, so they
        // stand together.
        terrain.Scatter.AddRange(
        [
            new ScatterLayer("grass", seed: 1, density: 0.5, sets: [1],
                size: (0.4, 0.3, 0.4), lean: 0.7, colour: 0x5E8C3A, range: 70),
            new ScatterLayer("stones", seed: 2, density: 0.03, sets: [0],
                size: (1.2, 0.7, 0.9), lean: 1, lift: -0.25, colour: 0x8A8580),
            new ScatterLayer("trunks", seed: 3, density: 0.004, sets: [1],
                maxSlope: 22, size: (0.45, 4, 0.45), colour: 0x5A3E28),
            new ScatterLayer("crowns", seed: 3, density: 0.004, sets: [1],
                maxSlope: 22, size: (2.6, 3.4, 2.6), lift: 3.2, colour: 0x2F5A2A),
        ]);

        // Every frame. The maps are shared, and only a region whose revision
        // moved crosses to the renderer. The placer places again only where it
        // moved.
        placer.Update(terrain);
        new RenderScene
        {
            Terrain = [TerrainView.From(terrain, 1, new TerrainViewOptions { Pixels = path => decoded[path] })],
            Populations = ScatterView.From(placer, 100).Populations,
            Camera = camera,
        }

        // Standing on it needs no physics: the heights the renderer draws.
        var y = terrain.HeightAt(x, z);
        var up = terrain.NormalAt(x, z);
        """;

    // Makes the ground again from the seed and the relief. Every region is
    // new, so the next frame sends them all.
    private Ground Build()
    {
        var ground = new Ground(
            regionSize: RegionSize,
            spacing: Spacing,
            sets:
            [
                new GroundSet("rock", "rock.png", "rock_normal.png", tileSize: 12, triplanar: true),
                new GroundSet("grass", "grass.png", "grass_normal.png", tileSize: 4),
            ],
            scatter: Rules);

        var swell = new FractalNoise(new GradientNoise(_seed), Octaves: 5);
        var ridges = new FractalNoise(new RidgedNoise(new GradientNoise(_seed + 17)), Octaves: 5);
        double Height(double x, double z)
        {
            // Down to a plain towards the edges, so the ground ends in fog
            // rather than in a wall.
            var keep = 1 - Smooth(0.5, 0.95, Math.Max(Math.Abs(x), Math.Abs(z)) / Reach);
            var ridge = Math.Pow(ridges.Unit(x / 85, z / 85), 2.2);
            var mountains = swell.Unit((x / 260) + 4, (z / 260) + 4);
            return _relief * keep * ((0.28 * swell.Unit(x / 120, z / 120)) + (0.72 * ridge * mountains));
        }

        for (var z = -RegionsOut; z < RegionsOut; z++)
        {
            for (var x = -RegionsOut; x < RegionsOut; x++)
            {
                ground.FillHeights(new RegionKey(x, z), Height);
            }
        }

        return ground;
    }

    // Paints both sets' pictures in code: an albedo with a height in its
    // alpha, and a normal map worked out from that same height.
    private void PaintSets()
    {
        // Rock: slabs split by cracks, and tall, so it shows through the grass
        // where the two meet rather than fading into it. Ridged noise peaks in
        // thin lines, so turned over, the lines are the cracks.
        var slabs = new FractalNoise(new RidgedNoise(new TilingNoise(new GradientNoise(3), Period: 3)), Octaves: 2);
        var grain = new FractalNoise(new TilingNoise(new GradientNoise(5), Period: 12), Octaves: 2);
        Paint(
            "rock",
            (u, v) => 0.2 + (0.7 * (1 - Math.Pow(slabs.Unit(u * 3, v * 3), 4))) + (0.1 * grain.Unit(u * 12, v * 12)),
            (_, _, h) =>
            {
                var shade = 0.45 + (0.55 * h);
                return (132 * shade, 128 * shade, 124 * shade);
            },
            bumps: 3);

        // Grass: low, so rock wins wherever there is any, and blotched, so a
        // hillside of it is not one flat green.
        var tufts = new FractalNoise(new TilingNoise(new GradientNoise(9), Period: 32), Octaves: 3);
        var patches = new TilingNoise(new GradientNoise(11), Period: 4);
        Paint(
            "grass",
            (u, v) => 0.1 + (0.35 * tufts.Unit(u * 32, v * 32)),
            (u, v, h) =>
            {
                var dry = patches.Unit(u * 4, v * 4);
                var shade = 0.7 + (0.5 * h);
                return ((70 + (60 * dry)) * shade, (104 + (30 * dry)) * shade, (42 + (10 * dry)) * shade);
            },
            bumps: 2);
    }

    private void Paint(
        string name,
        Func<double, double, double> height,
        Func<double, double, double, (double R, double G, double B)> colour,
        double bumps)
    {
        const int size = PictureSize;
        var heights = new double[size * size];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                heights[(y * size) + x] = height((double)x / size, (double)y / size);
            }
        }

        double At(int x, int y) => heights[((y % size) * size) + (x % size)];

        var albedo = new byte[size * size * 4];
        var normal = new byte[size * size * 4];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var h = At(x, y);
                var (r, g, b) = colour((double)x / size, (double)y / size, h);
                var i = ((y * size) + x) * 4;
                albedo[i] = Byte(r);
                albedo[i + 1] = Byte(g);
                albedo[i + 2] = Byte(b);
                albedo[i + 3] = Byte(h * 255);

                // The slope of the same heights, wrapped at the edges so the
                // picture tiles.
                var n = new Vector3(
                    (At(x - 1 + size, y) - At(x + 1, y)) * bumps,
                    (At(x, y - 1 + size) - At(x, y + 1)) * bumps,
                    1).Normalized();
                normal[i] = Byte(((n.X * 0.5) + 0.5) * 255);
                normal[i + 1] = Byte(((n.Y * 0.5) + 0.5) * 255);
                normal[i + 2] = Byte(((n.Z * 0.5) + 0.5) * 255);
                // Rougher in the cracks than on the faces.
                normal[i + 3] = Byte(255 * (0.95 - (0.25 * h)));
            }
        }

        _pictures[$"{name}.png"] = albedo;
        _pictures[$"{name}_normal.png"] = normal;
    }

    private static byte Byte(double value) => (byte)Math.Clamp(Math.Round(value, MidpointRounding.AwayFromZero), 0, 255);

    private static double Smooth(double from, double to, double value)
    {
        var t = Math.Clamp((value - from) / (to - from), 0, 1);
        return t * t * (3 - (2 * t));
    }

    // A box driving a circle, standing on the ground and tilted with it.
    private RenderObject Rover(double seconds)
    {
        var turn = seconds * 0.12;
        var x = Math.Cos(turn) * RoverRadius;
        var z = Math.Sin(turn) * RoverRadius;
        var ground = Ground.HeightAt(x, z) ?? 0;
        var up = Ground.NormalAt(x, z) ?? new Vector3(0, 1, 0);

        // Along the circle, then laid onto the slope.
        var heading = new Vector3(-Math.Sin(turn), 0, Math.Cos(turn));
        var ahead = (heading - (up * heading.Dot(up))).Normalized();
        var side = up.Cross(ahead);
        // It stands its half-height and a hand clear of the ground, along the
        // ground's own up.
        var place = new Vector3(x, ground, z) + (up * (RoverHalf.Y + 0.15));

        return new RenderObject
        {
            Key = 2,
            Transform = Matrix4.FromList(
            [
                side.X * RoverHalf.X, side.Y * RoverHalf.X, side.Z * RoverHalf.X, 0,
                up.X * RoverHalf.Y, up.Y * RoverHalf.Y, up.Z * RoverHalf.Y, 0,
                ahead.X * RoverHalf.Z, ahead.Y * RoverHalf.Z, ahead.Z * RoverHalf.Z, 0,
                place.X, place.Y, place.Z, 1,
            ]),
            Colour = Tint.Hex(0xE0A030).Linear,
        };
    }
}

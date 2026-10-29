using System.Globalization;
using Orblit.Filament;
using Orblit.Light;
using Orblit.Noise;
using Orblit.VectorMath;

namespace Orblit.Examples;

/// <summary>
/// A world made of blocks, generated and walked over.
/// </summary>
/// <remarks>
/// The genre is well known, so the interesting part is what it costs rather
/// than how it looks. A landscape of cubes is the worst case for a renderer
/// that draws things one at a time, and there are sixty thousand here. They
/// are not drawn one at a time. They are one buffer of transforms, sent when
/// the world changes and not again, and the renderer decides what is near
/// enough to be worth drawing.
///
/// What makes it a world rather than a heap is that everything about a point
/// is a function of that point. So it is continuous, and it is the same every
/// time from the same seed.
/// </remarks>
public sealed class VoxelExample : Example
{
    private const byte Air = 0;
    private const byte Grass = 1;
    private const byte Dirt = 2;
    private const byte Stone = 3;
    private const byte Deepslate = 4;
    private const byte Sand = 5;
    private const byte Sandstone = 6;
    private const byte Snow = 7;
    private const byte Water = 8;
    private const byte Log = 9;
    private const byte Leaves = 10;

    // Each kind's colour, from air to leaves. Air is never drawn.
    private static readonly (double R, double G, double B)[] Colours =
    [
        (0, 0, 0),
        (0.26, 0.52, 0.20),
        (0.36, 0.26, 0.17),
        (0.44, 0.44, 0.45),
        (0.20, 0.20, 0.22),
        (0.80, 0.73, 0.50),
        (0.70, 0.62, 0.42),
        (0.93, 0.95, 0.98),
        (0.10, 0.30, 0.46),
        (0.30, 0.21, 0.12),
        (0.14, 0.38, 0.13),
    ];

    // How far the world reaches, in blocks from the middle, and how high the
    // hills go.
    private double _reach = 56;
    private double _relief = 14;

    // How far a block is still drawn from. Everything is in the buffer either
    // way. This decides how much of it is looked at.
    private double _range = 140;

    private bool _sea = true;
    private bool _trees = true;

    // Whether you are in the world or looking at it.
    private bool _walking = true;

    private int _seed = 7;
    private int _revision;
    private float[] _transforms = [];
    private float[] _colours = [];
    private int _blockCount;

    // Every block, one byte each. A grid rather than a list of what to draw,
    // because once somebody can dig, "what is at this point" is asked by the
    // body falling, by every step and by every ray under the crosshair. A grid
    // answers it in one lookup. Nought is air, and anything else is the kind.
    private byte[] _blocks = [];
    private int _side;
    private int _tall;

    // Where the eyes are, which way they face, and how fast the body falls.
    // Head height is a metre and sixty-two, which is a person.
    private Vector3 _eye = new(0.5, 20, 0.5);
    private double _yaw = 0.7;
    private double _pitch = -0.15;
    private double _fall;
    private double _wasAt;

    public VoxelExample()
    {
        Build();
    }

    /// <summary>
    /// The physical keys held down, by their W3C names such as <c>KeyW</c> and
    /// <c>Space</c>. A set rather than an event per key, because walking
    /// diagonally is two keys at once. The world reads it every frame.
    /// </summary>
    public HashSet<string> Held { get; } = [];

    /// <summary>How wide and how tall the world came out.</summary>
    internal (int Side, int Tall) Extent => (_side, _tall);

    /// <summary>How many cubes the last build decided were worth drawing.</summary>
    internal int DrawnCount => _blockCount;

    public override string Name => "Blocks";

    public override string Blurb => "A landscape of sixty thousand cubes, generated and sent once.";

    public override ExampleSection Section => ExampleSection.Showcases;

    public override ViewPoint ViewPoint => new() { Distance = 96, Pitch = 0.6, Height = 8, Yaw = 0.7 };

    // A share of the height rather than a number of blocks. A fixed level in a
    // world whose relief can be doubled turns the world dry the moment
    // somebody drags the slider.
    private int SeaLevel => Round(_relief * 0.28);

    /// <summary>What is at a point. Out of bounds is air, which is what the generator works to.</summary>
    internal int BlockAt(int x, int y, int z) => At(x, y, z);

    /// <summary>Turns the view by <paramref name="yaw"/> and <paramref name="pitch"/>, in radians.</summary>
    public void Look(double yaw, double pitch)
    {
        _yaw += yaw;
        // Short of straight up and straight down, where the view matrix
        // collapses and the picture turns over.
        _pitch = Math.Clamp(_pitch + pitch, -1.5, 1.5);
    }

    /// <summary>Takes out the block under the crosshair.</summary>
    public void Dig()
    {
        if (Looking() is not { } hit)
        {
            return;
        }

        Put(hit.X, hit.Y, hit.Z, Air);
        Pack();
    }

    /// <summary>Puts one back, against the face being looked at.</summary>
    public void Place()
    {
        if (Looking() is not { } hit || At(hit.FaceX, hit.FaceY, hit.FaceZ) != Air)
        {
            return;
        }

        Put(hit.FaceX, hit.FaceY, hit.FaceZ, Stone);
        Pack();
    }

    public override RenderScene Scene(RenderCamera camera, double seconds)
    {
        var far = _reach + 8;

        // The gallery's camera orbits a point, which is right for looking at a
        // scene and wrong for being inside one, so walking takes it over.
        if (_walking)
        {
            // Capped, because a tab left in the background comes back with a
            // gap of seconds, and a player who falls through the world for all
            // of it was not there to see it.
            var dt = Math.Clamp(seconds - _wasAt, 0, 0.05);
            _wasAt = seconds;
            // A query that steps, because the gallery's one call a frame is this.
            Step(dt);
            // A copy, because the next frame moves the eye and this scene may
            // still be read.
            camera = new RenderCamera { Position = _eye.Clone(), Target = _eye + Facing(), FieldOfView = 68 };
        }
        else
        {
            _wasAt = seconds;
        }

        return new RenderScene
        {
            Camera = camera,
            Objects = [],
            Populations =
            [
                // One box around the lot, because every member is culled by it.
                new Population(1, _transforms, _colours, new Bounds(new Vector3(-far, -2, -far), new Vector3(far, _relief + 4, far)))
                {
                    // Moved when the world is built, and only then. The renderer
                    // sends sixty thousand transforms again when it moves, so a
                    // camera move must not move it.
                    Revision = _revision,
                    Range = _range,
                    // A continuous surface cannot lose one cube at a time and
                    // stay whole. Sinking a cube leaves a plate where it was, and
                    // shrinking opens gaps. So the draws are cut at the range and
                    // the fog hides the edge.
                    Fade = Fade.None,
                    CastShadows = true,
                },
            ],
            Lights =
            [
                new RenderLight
                {
                    Key = 1,
                    Kind = RenderLightKind.Directional,
                    // Across rather than down. A sun overhead lights every top
                    // and nothing else, and the shadows are what turn a height
                    // field into hills.
                    Direction = new Vector3(-0.55, -0.62, -0.35).Normalized(),
                    Colour = Tint.Hex(0xFFF0D8).Linear,
                    Intensity = 92000,
                    CastShadows = true,
                },
            ],
            // The fog hides the hard edge at the range. It reaches the sky's
            // colour before the range does, so a clump of blocks leaving is one
            // nobody could see.
            Fog = new Fog
            {
                Colour = Tint.Hex(0xC5DAEC).Linear,
                // Worked out from the range, because the fog must be finished
                // before the culling starts. Clear at 0.55 of the range to two
                // per cent left at 0.95 of it needs a density times span of 3.9.
                Density = 3.9 / (_range * 0.4),
                Distance = _range * 0.55,
                MaximumOpacity = 1,
            },
            Sky = new Sky
            {
                Zenith = Tint.Hex(0x4E86C4).Linear,
                Horizon = Tint.Hex(0xC5DAEC).Linear,
                Ambient = 22000,
            },
            Pipeline = new Pipeline
            {
                // The blocks are big and their edges line up with the axes, so a
                // small map is plenty.
                Shadows = new ShadowSettings { Kind = ShadowKind.Soft, Cascades = 3, MapSize = 1024, Distance = 160 },
                Resolution = new Resolution { Adaptive = true, MinScale = 0.6 },
            },
            Post = new PostProcess
            {
                AntiAliasing = AntiAliasing.Temporal,
                Occlusion = new Occlusion { Enabled = true, Quality = 1, Radius = 1.2 },
            },
        };
    }

    public override IReadOnlyList<ExampleSetting> Settings() =>
    [
        new Caption($"{_blockCount} blocks drawn, one buffer."),
        // Built once the handle is let go, because sixty thousand blocks a
        // pixel is a slider nobody can drag.
        new Setting("Reach", _reach, 16, 80, value => _reach = value)
        {
            Settle = _ => Build(),
            Decimals = 0,
        },
        new Setting("Relief", _relief, 2, 26, value => _relief = value)
        {
            Settle = _ => Build(),
            Decimals = 0,
        },
        new Setting("Drawn to", _range, 20, 240, value => _range = value) { Decimals = 0 },
        new Toggle("Walk", _walking, value =>
        {
            _walking = value;
            if (value)
            {
                StandOnGround();
            }
        })
        {
            Note = _walking ? "WASD, space, drag to look, click to dig" : "Drag to orbit the world instead",
        },
        new Toggle("Trees", _trees, value =>
        {
            _trees = value;
            Build();
        })
        {
            Note = "Where it is warm enough and damp enough for them",
        },
        // The water comes and goes with the next build.
        new Toggle("Sea", _sea, value => _sea = value) { Note = $"Everything below {SeaLevel} is under it" },
        new Choice("World", ["1", "2", "3", "4"], _seed.ToString(CultureInfo.InvariantCulture), option =>
        {
            _seed = int.Parse(option, CultureInfo.InvariantCulture);
            Build();
        }),
    ];

    public override string Code => """
        // The world is a grid of bytes, not a list of what to draw. Once
        // somebody can dig, "what is at this point" is asked by the body
        // falling, by every step and by every ray under the crosshair. A grid
        // answers it in one lookup.
        byte[] blocks; // side * side * tall, nought is air

        // Only what can be seen is drawn. A block with six solid neighbours is
        // hidden from everywhere, and in a world of hills that is most of them.
        if (!buriedOnAllSides)
        {
            transforms.AddRange(Placed(x, y, z));
            colours.AddRange(ColourOf(kind));
        }

        // Moved one axis at a time. Move in one step and test afterwards, and
        // you are inside a wall with no way to know which way is out. One at a
        // time, a corner stops you sideways and lets you keep walking forwards,
        // which is sliding along a wall.
        foreach (var (dx, dz) in [(wish.X * pace * dt, 0.0), (0.0, wish.Z * pace * dt)]) { ... }

        // Digging walks a ray a fraction of a block at a time. A grid traversal
        // is faster, but this runs once a click on a ray six blocks long, and
        // being plainly correct is worth more here.
        """;

    private static int Round(double value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);

    // Every field runs to about half either side of zero rather than to one,
    // as measured, so each is opened out before it is used.
    private static double Field(NoiseField noise, int x, int z, double scale) =>
        Math.Clamp(noise.At(x * scale, z * scale) * 1.9, -1, 1);

    private Vector3 Facing() =>
        new(Math.Sin(_yaw) * Math.Cos(_pitch), Math.Sin(_pitch), Math.Cos(_yaw) * Math.Cos(_pitch));

    private bool Inside(int x, int y, int z) => x >= 0 && y >= 0 && z >= 0 && x < _side && z < _side && y < _tall;

    private byte At(int x, int y, int z) => Inside(x, y, z) ? _blocks[(((y * _side) + z) * _side) + x] : Air;

    private void Put(int x, int y, int z, byte kind)
    {
        if (Inside(x, y, z))
        {
            _blocks[(((y * _side) + z) * _side) + x] = kind;
        }
    }

    // Water is not solid, which is the whole of this example's physics. You
    // can walk into a lake and you cannot walk into a hill.
    private bool SolidAt(double x, double y, double z)
    {
        var kind = At((int)Math.Floor(x + (_side / 2.0)), (int)Math.Floor(y), (int)Math.Floor(z + (_side / 2.0)));
        return kind is not Air and not Water;
    }

    // Generates the world. One noise field gives rolling dunes and nothing
    // else. A block world reads as a place when its parts differ: a beach, a
    // desert, a snow line, trees where trees grow. So there are five fields
    // and none of them is height. How far inland decides how far above the
    // sea a region sits, and makes coasts. Erosion decides how much the land
    // may vary there, which separates a plain from a range of hills. Warmth
    // and damp decide what grows. Roughness keeps a hillside from being a
    // ramp.
    private void Build()
    {
        // Offsets rather than other seeds, so one number decides the world and
        // the fields stay independent.
        var land = new FractalNoise(new GradientNoise(_seed), Octaves: 3);
        var erosion = new FractalNoise(new GradientNoise(_seed + 101), Octaves: 2);
        var warmth = new FractalNoise(new GradientNoise(_seed + 202), Octaves: 2);
        var damp = new FractalNoise(new GradientNoise(_seed + 303), Octaves: 2);
        var rough = new FractalNoise(new GradientNoise(_seed + 404), Octaves: 3);

        _side = (Round(_reach) * 2) + 1;
        _tall = Round(_relief) + 8;
        _blocks = new byte[_side * _side * _tall];

        var half = _side / 2;
        var sea = SeaLevel;

        int HeightAt(int x, int z)
        {
            var c = Field(land, x, z, 0.021);
            var e = Field(erosion, x, z, 0.045);

            // Flat round the coast so beaches are wide, steeper through the
            // middle so there is somewhere to climb, and easing at the top so
            // peaks are rare rather than a plateau.
            var shaped = c < -0.3 ? (c + 1) / 0.7 * 0.28
                : c < 0.35 ? 0.28 + ((c + 0.3) / 0.65 * 0.42)
                : 0.70 + ((c - 0.35) / 0.65 * 0.30);

            // Erosion scales how far from the sea the land may get, which makes
            // a range of hills rather than a taller everything.
            var vary = 0.35 + ((1 - ((e + 1) / 2)) * 0.65);
            var above = (shaped - 0.30) * _relief * 1.5 * vary;
            var bumps = Field(rough, x, z, 0.14) * 1.6;
            return Math.Clamp(Round(sea + above + bumps), 1, _tall - 3);
        }

        for (var x = 0; x < _side; x++)
        {
            for (var z = 0; z < _side; z++)
            {
                var (wx, wz) = (x - half, z - half);
                var top = HeightAt(wx, wz);

                // Down to the lowest neighbour, so a cliff is a wall rather than
                // a row of floating tops, and digging into one finds rock.
                var lowest = Math.Min(
                    Math.Min(top, Math.Min(HeightAt(wx + 1, wz), HeightAt(wx - 1, wz))),
                    Math.Min(HeightAt(wx, wz + 1), HeightAt(wx, wz - 1)));
                Fill((x, z), Math.Max(Math.Min(lowest, sea - 1), 0), top, (Field(warmth, wx, wz, 0.017), Field(damp, wx, wz, 0.019)));
            }
        }

        if (_trees)
        {
            Plant(warmth, damp);
        }

        StandOnGround();
        Pack();
    }

    // One column, from its lowest block to its top, and the water over it.
    private void Fill((int X, int Z) at, int from, int top, (double T, double H) climate)
    {
        for (var y = from; y <= top; y++)
        {
            Put(at.X, y, at.Z, SurfaceAt(y, top, climate.T, climate.H));
        }

        if (_sea)
        {
            for (var y = top + 1; y <= SeaLevel; y++)
            {
                Put(at.X, y, at.Z, Water);
            }
        }
    }

    // The top of a column and the few under it are decided by climate, and
    // everything below is stone. A small rule that does most of the work of
    // making a world look like somewhere.
    private byte SurfaceAt(int y, int top, double t, double h)
    {
        var depth = top - y;

        // Deep down, a darker stone, so the bottom of a hole looks like one.
        if (depth > 4)
        {
            return y < _tall * 0.28 ? Deepslate : Stone;
        }

        // Anything at the water's edge is sand, whatever the climate.
        if (top <= SeaLevel + 1)
        {
            return depth <= 1 ? Sand : Sandstone;
        }

        // Desert: hot and dry, and sand down to sandstone.
        if (t > 0.35 && h < -0.1)
        {
            return depth <= 2 ? Sand : Sandstone;
        }

        return Upland(depth, top, t);
    }

    // Above the shore and out of the desert.
    private byte Upland(int depth, int top, double t)
    {
        // Snow by height first and cold second. A peak is white because it is
        // high, and a cold region is white lower down than a warm one.
        var snowLine = _tall * (0.62 - (t * 0.18));
        if (top > snowLine)
        {
            return depth == 0 ? Snow : Stone;
        }

        // Bare rock under the snow, so a mountain has a stony shoulder.
        if (top > snowLine - 3)
        {
            return Stone;
        }

        return depth == 0 ? Grass : Dirt;
    }

    // Trees go in after the ground, because they need to know where the
    // surface ended up. They sit on a grid with an offset rather than at
    // random, so a forest spreads out rather than clumping.
    private void Plant(NoiseField warmth, NoiseField damp)
    {
        // Seeded by the world, so the same world grows the same trees.
        var chance = new Random(_seed * 7717);
        var half = _side / 2;

        for (var gx = 2; gx < _side - 2; gx += 4)
        {
            for (var gz = 2; gz < _side - 2; gz += 4)
            {
                var x = gx + chance.Next(3) - 1;
                var z = gz + chance.Next(3) - 1;

                // Where it is warm enough and damp enough. Deserts and peaks get
                // none, which is most of what makes them deserts and peaks.
                var t = Field(warmth, x - half, z - half, 0.017);
                var h = Field(damp, x - half, z - half, 0.019);
                if (h < 0.05 || t < -0.35 || chance.NextDouble() > 0.55 + (h * 0.35))
                {
                    continue;
                }

                // On grass only. Not on sand, stone or in a lake.
                var ground = Ground(x, z);
                if (ground < 0 || At(x, ground, z) != Grass)
                {
                    continue;
                }

                var tall = 4 + chance.Next(3);
                if (ground + tall + 2 < _tall)
                {
                    Grow(x, ground, z, tall);
                }
            }
        }
    }

    // The highest block in a column, or minus one when it is all air.
    private int Ground(int x, int z)
    {
        for (var y = _tall - 1; y > 0; y--)
        {
            if (At(x, y, z) != Air)
            {
                return y;
            }
        }

        return -1;
    }

    // A trunk and a blob of leaves, two wide round the top of the trunk and
    // narrowing to a cap.
    private void Grow(int x, int ground, int z, int tall)
    {
        for (var i = 1; i <= tall; i++)
        {
            Put(x, ground + i, z, Log);
        }

        for (var dy = -1; dy <= 1; dy++)
        {
            Crown(x, ground + tall + dy, z, dy == 1 ? 1 : 2);
        }

        Put(x, ground + tall + 2, z, Leaves);
    }

    // One layer of leaves, spread out from the trunk. The
    // corners go, so it is round. The two wide layers are round the trunk's
    // top, so they leave its cell.
    private void Crown(int x, int y, int z, int spread)
    {
        for (var dx = -spread; dx <= spread; dx++)
        {
            for (var dz = -spread; dz <= spread; dz++)
            {
                var corner = Math.Abs(dx) == spread && Math.Abs(dz) == spread;
                var trunk = spread == 2 && dx == 0 && dz == 0;
                if (!corner && !trunk)
                {
                    Put(x + dx, y, z + dz, Leaves);
                }
            }
        }
    }

    // Only blocks with air beside them are drawn. A block shut in on all six
    // sides cannot be seen from anywhere, so this draws the surface rather
    // than the volume, about eight times fewer.
    private void Pack()
    {
        var transforms = new List<float>();
        var colours = new List<float>();
        var half = _side / 2.0;

        for (var y = 0; y < _tall; y++)
        {
            for (var z = 0; z < _side; z++)
            {
                for (var x = 0; x < _side; x++)
                {
                    var kind = At(x, y, z);
                    if (kind == Air || Buried(x, y, z))
                    {
                        continue;
                    }

                    // A translation and a half scale, so the cube is a unit
                    // across. Written out, because this runs sixty thousand times.
                    transforms.AddRange([0.5f, 0, 0, 0, 0, 0.5f, 0, 0, 0, 0, 0.5f, 0, (float)(x - half + 0.5), y, (float)(z - half + 0.5), 1]);
                    var (r, g, b) = ColourOf(kind, x, z);
                    colours.AddRange([(float)r, (float)g, (float)b]);
                }
            }
        }

        _transforms = [.. transforms];
        _colours = [.. colours];
        _blockCount = colours.Count / 3;
        _revision++;
    }

    private bool Buried(int x, int y, int z) =>
        At(x + 1, y, z) != Air && At(x - 1, y, z) != Air
        && At(x, y + 1, z) != Air && At(x, y - 1, z) != Air
        && At(x, y, z + 1) != Air && At(x, y, z - 1) != Air;

    // A little variation by place matters more than the colours do. Without
    // it a hillside is one flat green. Hashed from the position, so digging
    // one block out does not shuffle its neighbours.
    private static (double R, double G, double B) ColourOf(byte kind, int x, int z)
    {
        var shade = 0.88 + ((((x * 73856093L) ^ (z * 19349663L)) & 0xFF) / 255.0 * 0.2);
        var (r, g, b) = Colours[kind];

        // Water is the one kind left unshaded.
        return kind == Water ? (r, g, b) : (r * shade, g * shade, b * shade);
    }

    // Puts the player on whatever is in the middle of the world.
    private void StandOnGround()
    {
        for (var y = _tall - 1; y >= 0; y--)
        {
            if (At(_side / 2, y, _side / 2) != Air)
            {
                _eye = new Vector3(0.5, y + 2.7, 0.5);
                _fall = 0;
                return;
            }
        }

        _eye = new Vector3(0.5, _tall, 0.5);
    }

    // One frame of moving the player.
    private void Step(double dt)
    {
        const double pace = 7;
        const double gravity = 26;
        const double jump = 8.4;

        // Feet, not eyes. The camera is at head height and the body is what
        // the world is tested against.
        var feet = _eye.Y - 1.62;
        if (_fall <= 0 && SolidAt(_eye.X, feet - 0.08, _eye.Z))
        {
            _fall = Held.Contains("Space") ? jump : 0;
        }
        else
        {
            _fall -= gravity * dt;
        }

        var wish = Wish();
        foreach (var (dx, dz) in new[] { (wish.X * pace * dt, 0.0), (0.0, wish.Z * pace * dt) })
        {
            var (x, z) = (_eye.X + dx, _eye.Z + dz);
            // Two heights, because a step is blocked by anything at the knee or
            // the chest, and a floor is neither.
            if (!SolidAt(x, feet + 0.2, z) && !SolidAt(x, feet + 1.4, z))
            {
                _eye.X = x;
                _eye.Z = z;
            }
        }

        Rise(_fall * dt);
    }

    // Where the held keys ask to go, a unit long or nothing.
    private Vector3 Wish()
    {
        var ahead = new Vector3(Math.Sin(_yaw), 0, Math.Cos(_yaw));
        var side = new Vector3(Math.Cos(_yaw), 0, -Math.Sin(_yaw));
        var wish = Vector3.Zero();
        wish += Held.Contains("KeyW") ? ahead : Vector3.Zero();
        wish -= Held.Contains("KeyS") ? ahead : Vector3.Zero();
        wish -= Held.Contains("KeyA") ? side : Vector3.Zero();
        wish += Held.Contains("KeyD") ? side : Vector3.Zero();
        return wish.Length > 0 ? wish.Normalized() : wish;
    }

    private void Rise(double rise)
    {
        var wanted = _eye.Y + rise;
        if (rise < 0 && SolidAt(_eye.X, wanted - 1.62, _eye.Z))
        {
            // Landed: the feet go on top of the block rather than inside it.
            _eye.Y = Math.Floor(wanted - 1.62) + 1 + 1.62;
            _fall = 0;
        }
        else if (rise > 0 && SolidAt(_eye.X, wanted - 1.62 + 1.8, _eye.Z))
        {
            _fall = 0;
        }
        else
        {
            _eye.Y = wanted;
        }
    }

    // What is under the crosshair, and the empty cell in front of it. Stepped
    // a fraction of a block at a time rather than solved, because this runs
    // once a click on a ray six blocks long.
    private Hit? Looking()
    {
        var facing = Facing();
        var half = _side / 2.0;
        (int X, int Y, int Z)? before = null;

        for (var t = 0.0; t < 6.0; t += 0.03)
        {
            var p = _eye + (facing * t);
            var cell = (X: (int)Math.Floor(p.X + half), Y: (int)Math.Floor(p.Y), Z: (int)Math.Floor(p.Z + half));
            if (At(cell.X, cell.Y, cell.Z) is not Air and not Water)
            {
                var face = before ?? cell;
                return new Hit(cell.X, cell.Y, cell.Z, face.X, face.Y, face.Z);
            }

            before = cell;
        }

        return null;
    }

    private readonly record struct Hit(int X, int Y, int Z, int FaceX, int FaceY, int FaceZ);
}

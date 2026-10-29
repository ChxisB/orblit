using Orblit.Filament;
using Orblit.Sprite;
using Orblit.VectorMath;

namespace Orblit.Examples;

/// <summary>
/// The runner's world, made here rather than shipped.
/// </summary>
/// <remarks>
/// Every model in the game is a handful of boxes, balls and cones, and every
/// texture is a few lines of noise and paint. They are written out as ordinary
/// glTF and PNG files and handed to the renderer by name. So they go down the
/// same path a downloaded model does, and the game still has nothing to
/// download.
///
/// The whole set is a few hundred kilobytes and takes a blink to build.
/// </remarks>
internal static class RunnerArt
{
    /// <summary>Bumped whenever what any file holds changes.</summary>
    /// <remarks>
    /// A renderer that has loaded a name does not load it again. So art that
    /// changed under the same name would not be seen until the application
    /// restarted.
    /// </remarks>
    public const int Version = 2;

    /// <summary>How much road there is behind wherever the road is put.</summary>
    public const double RoadBehind = 120.0;

    /// <summary>
    /// How much road there is ahead. The road is moved along in whole repeats of
    /// its paint so the move cannot be seen, and ahead has to reach far enough
    /// that its end is lost in the haze.
    /// </summary>
    public const double RoadAhead = 480.0;

    /// <summary>Half the width of the road, kerb to kerb.</summary>
    public const double RoadHalfWidth = 4.4;

    /// <summary>How far along the road one repeat of its paint covers: a dash and a gap.</summary>
    public const double RoadRepeat = 8.0;

    /// <summary>How far one repeat of the grass texture covers.</summary>
    public const double GrassRepeat = 4.0;

    /// <summary>The length of one container, and so of one car of a train.</summary>
    public const double ContainerLength = 10.0;

    public static readonly string Road = Name("road.glb");
    public static readonly string Ground = Name("ground.glb");
    public static readonly string Asphalt = Name("asphalt.png");
    public static readonly string Grass = Name("grass.png");
    public static readonly string Pine = Name("pine.glb");
    public static readonly string Oak = Name("oak.glb");
    public static readonly string House = Name("house.glb");
    public static readonly string DarkHouse = Name("dark_house.glb");
    public static readonly string Bush = Name("bush.glb");
    public static readonly string RedBush = Name("red_bush.glb");
    public static readonly string Rock = Name("rock.glb");
    public static readonly string Billboard = Name("billboard.glb");
    public static readonly string Hill = Name("hill.glb");
    public static readonly string Coin = Name("coin.glb");
    public static readonly string Hurdle = Name("hurdle.glb");
    public static readonly string Bar = Name("bar.glb");
    public static readonly string Container = Name("container.glb");
    public static readonly string Body = Name("body.glb");
    public static readonly string Hand = Name("hand.glb");
    public static readonly string Foot = Name("foot.glb");
    public static readonly string Cloud = Name("cloud.glb");

    /// <summary>
    /// Slate, for every panel of the machine. Light enough to be a colour rather
    /// than a hole, because a darker machine read as a silhouette on the black
    /// asphalt.
    /// </summary>
    private const uint Shell = 0xFF5C6A82;

    /// <summary>The lighter steel of the joints and the plating over them.</summary>
    private const uint Steel = 0xFFB4BECE;

    /// <summary>The darker trim, which keeps the shapes apart where two panels meet.</summary>
    private const uint Trim = 0xFF2E3546;

    /// <summary>The hot orange of the chest, the visor's surround and the soles.</summary>
    private const uint Ember = 0xFFE5893F;

    /// <summary>What is behind the visor, which the glass only half hides.</summary>
    private const uint Glow = 0xFF7FE4FF;

    private const int PictureSize = 256;

    private static readonly double[] Sides = [-1.0, 1.0];

    /// <summary>The name the scene uses for <paramref name="file"/>.</summary>
    public static string Name(string file) => RenderResources.NameFor($"runner/v{Version}/{file}");

    /// <summary>
    /// A transform that puts a unit shape at <paramref name="at"/>, turned by
    /// <paramref name="yaw"/> about the vertical and <paramref name="roll"/>
    /// about the forward axis, and stretched by <paramref name="size"/>.
    /// </summary>
    public static Matrix4 Place((double X, double Y, double Z) at, (double X, double Y, double Z) size, double yaw = 0, double roll = 0)
    {
        var placed = Matrix4.Identity();
        placed.TranslateByDouble(at.X, at.Y, at.Z, 1);
        placed.RotateY(yaw);
        placed.RotateZ(roll);
        placed.ScaleByDouble(size.X, size.Y, size.Z, 1);
        return placed;
    }

    /// <summary>Every file, by the resource name the scene uses for it.</summary>
    public static IReadOnlyDictionary<string, byte[]> Build() => new Dictionary<string, byte[]>
    {
        [Road] = RoadModel(),
        [Ground] = GroundModel(),
        [Asphalt] = AsphaltPicture(),
        [Grass] = GrassPicture(),
        [Pine] = PineModel(),
        [Oak] = OakModel(),
        [House] = HouseModel(0xFFF3F0E8, 0xFF7ED9B7, 0xFF3D4B5E, 0xFF78A8CF),
        [DarkHouse] = HouseModel(0xFF474B5C, 0xFFA83A33, 0xFF26262C, 0xFFF2CB6A),
        [Bush] = BushModel(0xFF3E9C39, 0xFFE0413F),
        [RedBush] = BushModel(0xFFC7302C, 0xFFF08A3A),
        [Rock] = RockModel(),
        [Billboard] = BillboardModel(),
        [Hill] = HillModel(),
        [Coin] = CoinModel(),
        [Hurdle] = HurdleModel(),
        [Bar] = BarModel(),
        [Container] = ContainerModel(),
        [Body] = BodyModel(),
        [Hand] = HandModel(),
        [Foot] = FootModel(),
        [Cloud] = CloudModel(),
    };

    private static byte[] RoadModel()
    {
        var model = new RunnerModel();
        var up = new Vector3(0, 1, 0);

        // In twenty metre pieces rather than one. A triangle six hundred metres
        // long gives every per-vertex quantity nothing to vary along.
        for (var z = RoadBehind; z > -RoadAhead; z -= 20)
        {
            var far = Math.Max(z - 20, -RoadAhead);

            // Across the road is across the texture, and along it is one repeat of
            // the paint every eight metres.
            model.Textured(0.9, Piece.Polygon(
                [new(-RoadHalfWidth, 0, z), new(RoadHalfWidth, 0, z), new(RoadHalfWidth, 0, far), new(-RoadHalfWidth, 0, far)],
                up,
                [new(0, -z / RoadRepeat), new(1, -z / RoadRepeat), new(1, -far / RoadRepeat), new(0, -far / RoadRepeat)]));

            // The edges, so the road stands a hand's width proud of the grass
            // rather than being painted on it.
            foreach (var side in Sides)
            {
                var x = side * RoadHalfWidth;
                model.Textured(0.9, Piece.Polygon(
                    [new(x, 0, z), new(x, 0, far), new(x, -0.12, far), new(x, -0.12, z)],
                    new Vector3(side, 0, 0),
                    [new(0.99, 0), new(0.99, 1), new(0.99, 1), new(0.99, 0)]));
            }
        }

        return model.ToGlb();
    }

    private static byte[] GroundModel()
    {
        const double wide = 400.0;
        var model = new RunnerModel();
        var up = new Vector3(0, 1, 0);
        for (var z = RoadBehind; z > -RoadAhead; z -= 40)
        {
            var far = Math.Max(z - 40, -RoadAhead);
            for (var x = -wide; x < wide; x += 80)
            {
                model.Textured(0.95, Piece.Polygon(
                    [new(x, 0, z), new(x + 80, 0, z), new(x + 80, 0, far), new(x, 0, far)],
                    up,
                    [
                        new(x / GrassRepeat, -z / GrassRepeat),
                        new((x + 80) / GrassRepeat, -z / GrassRepeat),
                        new((x + 80) / GrassRepeat, -far / GrassRepeat),
                        new(x / GrassRepeat, -far / GrassRepeat),
                    ]));
            }
        }

        return model.ToGlb();
    }

    private static byte[] PineModel()
    {
        var model = new RunnerModel();
        model.Tube(Place((0, 0, 0), (0.17, 0.9, 0.17)), 0xFF6B4A2F);
        (double Y, double Radius, double Height, uint Colour)[] tiers =
        [
            (0.7, 1.25, 1.5, 0xFF2E7A38),
            (1.45, 1.0, 1.4, 0xFF368A3E),
            (2.2, 0.72, 1.6, 0xFF41994A),
        ];
        foreach (var tier in tiers)
        {
            var at = Place((0, tier.Y, 0), (tier.Radius, tier.Height, tier.Radius));
            model.Tube(at, tier.Colour, new TubeStyle(Top: 0, Segments: 9, Flat: true));
        }

        return model.ToGlb();
    }

    private static byte[] OakModel()
    {
        var model = new RunnerModel();
        model.Tube(Place((0, 0, 0), (0.2, 1.5, 0.2)), 0xFF7A5234);
        model.Tube(Place((0.1, 1.1, 0), (0.09, 0.7, 0.09), roll: -0.7), 0xFF7A5234);
        (double X, double Y, double Z, double R, uint Colour)[] crowns =
        [
            (0.0, 2.3, 0.0, 1.25, 0xFF5CB23A),
            (0.7, 2.75, 0.25, 0.85, 0xFF6BC244),
            (-0.6, 2.7, -0.3, 0.9, 0xFF55A836),
            (0.1, 3.15, -0.1, 0.7, 0xFF74CA4C),
        ];
        foreach (var crown in crowns)
        {
            var at = Place((crown.X, crown.Y, crown.Z), (crown.R, crown.R * 0.9, crown.R));
            model.Ball(at, crown.Colour, new BallStyle(12, 8, Flat: true));
        }

        return model.ToGlb();
    }

    /// <summary>A house whose front, with the door in it, faces -z.</summary>
    private static byte[] HouseModel(uint walls, uint roof, uint door, uint windows)
    {
        var model = new RunnerModel();
        model.Box(Place((0, 0.12, 0), (4.5, 0.24, 4.5)), 0xFF9C9C98);
        model.Box(Place((0, 1.64, 0), (4, 2.8, 4)), walls);

        // The eaves, a little wider and deeper than the walls.
        model.Roof(Place((0, 3.04, 0), (4.7, 1.7, 4.6)), roof);
        model.Box(Place((1.15, 3.9, 0.9), (0.5, 1.3, 0.5)), 0xFFB0523D);
        model.Box(Place((0, 1.1, -2.02), (0.95, 1.75, 0.1)), door);
        model.Box(Place((0, 2.02, -2.03), (1.15, 0.12, 0.12)), 0xFFFFFFFF);
        foreach (var x in (ReadOnlySpan<double>)[-1.3, 1.3])
        {
            model.Box(Place((x, 1.9, -2.02), (0.85, 0.85, 0.08)), windows);
            model.Box(Place((x, 1.42, -2.05), (1.0, 0.1, 0.14)), 0xFFFFFFFF);
        }

        foreach (var side in Sides)
        {
            foreach (var z in (ReadOnlySpan<double>)[-0.9, 0.9])
            {
                model.Box(Place((side * 2.02, 1.9, z), (0.08, 0.85, 0.85)), windows);
            }
        }

        return model.ToGlb();
    }

    private static byte[] BushModel(uint leaves, uint flowers)
    {
        var model = new RunnerModel();
        (double X, double Y, double Z, double R)[] lumps =
        [
            (0.0, 0.42, 0.0, 0.62),
            (0.55, 0.34, 0.15, 0.46),
            (-0.5, 0.32, -0.1, 0.44),
            (0.1, 0.3, 0.5, 0.4),
        ];
        foreach (var lump in lumps)
        {
            model.Ball(Place((lump.X, lump.Y, lump.Z), (lump.R, lump.R * 0.85, lump.R)), leaves, new BallStyle(10, 7, Flat: true));
        }

        var chance = new Random(3);
        for (var i = 0; i < 9; i++)
        {
            var lump = lumps[i % lumps.Length];
            var yaw = chance.NextDouble() * Math.PI * 2;
            var up = 0.2 + (chance.NextDouble() * 0.9);
            var at = (
                lump.X + (Math.Cos(yaw) * Math.Cos(up) * lump.R),
                lump.Y + (Math.Sin(up) * 0.85 * lump.R),
                lump.Z + (Math.Sin(yaw) * Math.Cos(up) * lump.R));
            model.Ball(Place(at, (0.08, 0.08, 0.08)), flowers, new BallStyle(6, 4));
        }

        return model.ToGlb();
    }

    private static byte[] RockModel()
    {
        var model = new RunnerModel();
        model.Ball(Place((0, 0.25, 0), (1, 0.7, 0.85)), 0xFF8E9196, new BallStyle(7, 5, Flat: true, Lumpy: 0.28, Roughness: 0.95));
        return model.ToGlb();
    }

    /// <summary>A billboard whose face is towards +z, to be read by somebody coming along the road.</summary>
    private static byte[] BillboardModel()
    {
        var model = new RunnerModel();
        foreach (var x in (ReadOnlySpan<double>)[-1.6, 1.6])
        {
            model.Box(Place((x, 1.6, 0.1), (0.2, 3.2, 0.2)), 0xFF5A5F68);
        }

        model.Box(Place((0, 3.3, 0), (4.4, 2.4, 0.2)), 0xFFEFEFEA);
        model.Box(Place((0, 3.3, 0.11), (4.05, 2.05, 0.04)), 0xFF2F9BDB);
        model.Box(Place((-0.35, 3.85, 0.14), (2.9, 0.32, 0.03)), 0xFFFFFFFF);
        model.Box(Place((-0.75, 3.35, 0.14), (2.1, 0.2, 0.03)), 0xFFFFD21F);
        model.Box(Place((-0.95, 2.85, 0.14), (1.7, 0.2, 0.03)), 0xFFFFFFFF);
        model.Ball(Place((1.3, 2.95, 0.12), (0.55, 0.55, 0.06)), 0xFFFFD21F, new BallStyle(16, 8));
        return model.ToGlb();
    }

    private static byte[] HillModel()
    {
        var model = new RunnerModel();
        model.Ball(Place((0, -0.08, 0), (1, 0.42, 1)), 0xFF6AB944, new BallStyle(22, 12, Roughness: 0.95));
        return model.ToGlb();
    }

    /// <summary>
    /// A fair-weather cloud, in lumps like the trees' crowns, so that it looks
    /// as if it belongs to the same world. About two units across.
    /// </summary>
    private static byte[] CloudModel()
    {
        var model = new RunnerModel();
        (double X, double Y, double Z, double R)[] lumps =
        [
            (0.0, 0.25, 0.0, 0.55),
            (0.5, 0.15, 0.1, 0.42),
            (-0.52, 0.12, -0.05, 0.4),
            (0.2, 0.5, -0.1, 0.4),
            (-0.25, 0.42, 0.12, 0.36),
            (0.85, 0.05, -0.05, 0.28),
            (-0.88, 0.02, 0.05, 0.26),
        ];
        foreach (var lump in lumps)
        {
            var at = Place((lump.X, lump.Y, lump.Z), (lump.R, lump.R * 0.85, lump.R));
            model.Ball(at, 0xFFFFFFFF, new BallStyle(10, 7, Flat: true, Roughness: 1));
        }

        return model.ToGlb();
    }

    /// <summary>A coin standing on its edge, its faces towards ±z.</summary>
    private static byte[] CoinModel()
    {
        var model = new RunnerModel();
        var style = new TubeStyle(Segments: 22, Roughness: 0.3);
        model.Tube(Disc(0.42, 0.08), 0xFFFFC21A, style);
        model.Tube(Disc(0.3, 0.11), 0xFFFFC21A, style);
        return model.ToGlb();

        static Matrix4 Disc(double radius, double thickness)
        {
            var disc = Matrix4.Identity();
            disc.RotateX(Math.PI / 2);
            disc.ScaleByDouble(radius, thickness, radius, 1);
            disc.TranslateByDouble(0, -0.5, 0, 1);
            return disc;
        }
    }

    /// <summary>What is jumped: a striped board on two legs, a metre tall, one lane wide.</summary>
    private static byte[] HurdleModel()
    {
        const int stripes = 7;
        const double wide = 2.36;
        var model = new RunnerModel();
        foreach (var x in (ReadOnlySpan<double>)[-1.05, 1.05])
        {
            model.Box(Place((x, 0.5, 0), (0.12, 1.0, 0.12)), 0xFF34363C);
            model.Box(Place((x, 0.04, 0), (0.16, 0.08, 0.7)), 0xFF34363C);
        }

        for (var i = 0; i < stripes; i++)
        {
            var x = (-wide / 2) + ((i + 0.5) * wide / stripes);
            model.Box(Place((x, 0.74, 0), (wide / stripes, 0.5, 0.14)), i % 2 == 0 ? 0xFFFFD21F : 0xFF232323, roughness: 0.6);
        }

        return model.ToGlb();
    }

    /// <summary>
    /// What is slid under: a striped board on tall posts, with a gap beneath it
    /// that only something ducking fits through.
    /// </summary>
    private static byte[] BarModel()
    {
        const int stripes = 6;
        const double wide = 2.5;
        var model = new RunnerModel();
        foreach (var x in (ReadOnlySpan<double>)[-1.22, 1.22])
        {
            model.Box(Place((x, 0.95, 0), (0.14, 1.9, 0.14)), 0xFFE8E8E4);
            model.Box(Place((x, 0.04, 0), (0.2, 0.08, 0.6)), 0xFF5A5F68);
        }

        for (var i = 0; i < stripes; i++)
        {
            var x = (-wide / 2) + ((i + 0.5) * wide / stripes);
            model.Box(Place((x, 1.3, 0), (wide / stripes, 0.5, 0.12)), i % 2 == 0 ? 0xFFE8322C : 0xFFF6F6F2, roughness: 0.6);
        }

        return model.ToGlb();
    }

    /// <summary>
    /// What is gone round: a shipping container, its near end at z = 0 and the
    /// rest of it towards -z.
    /// </summary>
    private static byte[] ContainerModel()
    {
        const double length = ContainerLength;
        const uint green = 0xFF2E9E62;
        const uint dark = 0xFF217A4A;
        var model = new RunnerModel();
        model.Box(Place((0, 1.55, -length / 2), (2.4, 2.9, length)), green);

        // Ribs down both sides, which make a box read as a container.
        for (var i = 1; i < 14; i++)
        {
            var z = -i * length / 14;
            foreach (var side in Sides)
            {
                model.Box(Place((side * 1.22, 1.55, z), (0.07, 2.7, 0.16)), dark);
            }
        }

        foreach (var side in Sides)
        {
            model.Box(Place((side * 1.2, 3.0, -length / 2), (0.12, 0.12, length)), dark);
            model.Box(Place((side * 1.2, 0.12, -length / 2), (0.14, 0.24, length)), 0xFF2B2E33);
        }

        // The doors, on the end that faces whoever is running at it.
        model.Box(Place((0, 1.55, 0.02), (2.3, 2.8, 0.06)), dark);
        foreach (var x in (ReadOnlySpan<double>)[-0.55, -0.2, 0.2, 0.55])
        {
            model.Box(Place((x, 1.55, 0.07), (0.07, 2.6, 0.06)), 0xFFB7C0C4);
        }

        model.Box(Place((0, 1.55, 0.06), (0.04, 2.75, 0.06)), 0xFF1B5E39);
        return model.ToGlb();
    }

    /// <summary>
    /// The runner's body: a machine facing -z, its middle at the origin, about a
    /// metre and a quarter from its hips to the top of its head.
    /// </summary>
    /// <remarks>
    /// The runner is five floating pieces: a body, two hands and two boots, with
    /// nothing joining them. An arm that bent would need bones and a rig. A hand
    /// that travels where an arm would have carried it needs only a matrix, and
    /// reads as a run all the same.
    ///
    /// Slate and steel with a hot orange on it, which is the engine's own
    /// colouring, against a world of greens and blues.
    /// </remarks>
    private static byte[] BodyModel()
    {
        var model = new RunnerModel();
        Chest(model);
        Back(model);
        Head(model);
        return model.ToGlb();
    }

    private static void Chest(RunnerModel model)
    {
        // A box with its corners taken off by a slightly smaller box turned through
        // an eighth. It is cheaper than rounding, and at this size it reads as a bevel.
        model.Box(Place((0, 0.04, 0), (0.52, 0.62, 0.40)), Shell, roughness: 0.42);
        model.Box(Place((0, 0.04, 0), (0.50, 0.58, 0.46), yaw: Math.PI / 8), Shell, roughness: 0.42);

        // The plate across the front, and the lamp set into it. The lamp is the
        // brightest thing on the runner, at the height a camera behind holds in
        // the middle of the frame.
        model.Box(Place((0, 0.08, -0.19), (0.34, 0.40, 0.06)), Steel, roughness: 0.3);
        model.Ball(Place((0, 0.10, -0.23), (0.09, 0.09, 0.05)), Ember, new BallStyle(14, 10, Roughness: 0.18));

        // The shoulders, which no arm hangs from. The hands swing round them, and
        // the eye supplies the rest.
        foreach (var side in Sides)
        {
            model.Ball(Place((side * 0.29, 0.26, 0), (0.14, 0.13, 0.14)), Steel, new BallStyle(14, 10, Roughness: 0.35));
            model.Ball(Place((side * 0.34, 0.26, 0), (0.06, 0.07, 0.07)), Ember, new BallStyle(10, 8, Roughness: 0.25));
        }

        // The waist and the hips, narrower than the chest, so the shape tapers to
        // where the boots are rather than stopping flat.
        model.Tube(Place((0, -0.30, 0), (0.17, 0.12, 0.15)), Steel, new TubeStyle(Top: 1.35, Segments: 12, Roughness: 0.4));
        model.Box(Place((0, -0.44, 0), (0.38, 0.16, 0.32)), Shell, roughness: 0.45);
    }

    /// <summary>
    /// The back, which is the side anybody playing sees, because the camera is
    /// behind the runner for the whole game. A pack with a lit vent down each
    /// side, so what follows the runner is two orange lights.
    /// </summary>
    private static void Back(RunnerModel model)
    {
        model.Box(Place((0, 0.06, 0.20), (0.36, 0.46, 0.14)), Trim, roughness: 0.4);
        foreach (var side in Sides)
        {
            model.Box(Place((side * 0.11, 0.06, 0.27), (0.09, 0.34, 0.04)), Ember, roughness: 0.25);
        }

        model.Box(Place((0, 0.26, 0.27), (0.30, 0.05, 0.04)), Steel, roughness: 0.3);
    }

    private static void Head(RunnerModel model)
    {
        // The head is turned an eighth the other way from the chest, which stops
        // the two boxes reading as one.
        model.Tube(Place((0, 0.36, 0), (0.10, 0.06, 0.10)), Steel, new TubeStyle(Segments: 10, Roughness: 0.35));
        model.Box(Place((0, 0.58, 0), (0.40, 0.34, 0.38), yaw: -Math.PI / 16), Shell, roughness: 0.4);

        // The visor: a dark glass band with the light behind it showing at the
        // sides, and an orange brow over it.
        model.Box(Place((0, 0.58, -0.19), (0.30, 0.15, 0.06)), 0xFF13161C, roughness: 0.12);
        model.Box(Place((0, 0.58, -0.205), (0.22, 0.07, 0.04)), Glow, roughness: 0.1);
        model.Box(Place((0, 0.70, -0.17), (0.34, 0.05, 0.08)), Ember, roughness: 0.3);

        // The plates over the ears.
        foreach (var side in Sides)
        {
            var ear = Matrix4.Identity();
            ear.TranslateByDouble(side * 0.21, 0.57, 0.01, 1);
            ear.RotateZ(side * Math.PI / 2);
            ear.ScaleByDouble(0.09, 0.04, 0.09, 1);
            model.Tube(ear, Steel, new TubeStyle(Segments: 10, Roughness: 0.3));
        }

        // A light on the back of the head, at the height the eye goes to.
        model.Box(Place((0, 0.60, 0.19), (0.16, 0.07, 0.04)), Glow, roughness: 0.15);

        // The one aerial.
        model.Tube(Place((0.10, 0.82, 0.06), (0.022, 0.16, 0.022), roll: -0.22), Steel, new TubeStyle(Top: 0.5, Segments: 6));
        model.Ball(Place((0.06, 0.96, 0.09), (0.045, 0.045, 0.045)), Ember, new BallStyle(10, 8, Roughness: 0.2));
    }

    /// <summary>A hand, centred on its own origin, so the game can put it wherever the swing of an arm would.</summary>
    private static byte[] HandModel()
    {
        var model = new RunnerModel();
        model.Ball(Place((0, 0, -0.01), (0.115, 0.115, 0.13)), Steel, new BallStyle(14, 10, Roughness: 0.45));

        // A cuff at the back of it, towards the shoulder, which gives the hand a
        // front and a back and so a direction of travel.
        var cuff = Matrix4.Identity();
        cuff.TranslateByDouble(0, 0, 0.09, 1);
        cuff.RotateX(Math.PI / 2);
        cuff.ScaleByDouble(0.085, 0.04, 0.085, 1);
        model.Tube(cuff, Ember, new TubeStyle(Segments: 10, Roughness: 0.3));
        return model.ToGlb();
    }

    /// <summary>A boot, pointing -z, centred on its own origin for the same reason.</summary>
    private static byte[] FootModel()
    {
        var model = new RunnerModel();
        model.Box(Place((0, 0.02, -0.03), (0.17, 0.13, 0.28)), Shell, roughness: 0.5);
        model.Box(Place((0, -0.06, -0.04), (0.19, 0.05, 0.30)), Ember, roughness: 0.35);

        // The ankle, which shows from behind when the boot is thrown forward on a slide.
        model.Ball(Place((0, 0.09, 0.06), (0.09, 0.08, 0.09)), Steel, new BallStyle(12, 8, Roughness: 0.35));
        return model.ToGlb();
    }

    /// <summary>
    /// Asphalt, one road wide and one repeat of the paint long: the lane dashes,
    /// the edge lines and the kerb.
    /// </summary>
    private static byte[] AsphaltPicture()
    {
        const int size = PictureSize;
        var pixels = new byte[size * size * 4];
        var chance = new Random(11);
        var lumps = new PeriodicNoise(8, new Random(5));
        for (var row = 0; row < size; row++)
        {
            var dashed = (row + 0.5) / size * RoadRepeat < RoadRepeat / 2;
            for (var column = 0; column < size; column++)
            {
                var x = -RoadHalfWidth + ((column + 0.5) / size * RoadHalfWidth * 2);
                var shade = 88.0 + ((lumps.At((double)column / size, (double)row / size) - 0.5) * 12) + ((chance.NextDouble() - 0.5) * 18);
                Put(pixels, (row * size) + column, Painted(Math.Abs(x), dashed, Worn(shade, x), chance));
            }
        }

        return PngCodec.Encode(size, size, pixels);
    }

    /// <summary>Darker where the wheels go, which is what makes a road look used.</summary>
    private static double Worn(double shade, double x)
    {
        foreach (var lane in (ReadOnlySpan<double>)[-2.6, 0.0, 2.6])
        {
            foreach (var wheel in (ReadOnlySpan<double>)[-0.75, 0.75])
            {
                var off = Math.Abs(x - lane - wheel);
                if (off < 0.3)
                {
                    shade -= (1 - (off / 0.3)) * 7;
                }
            }
        }

        return shade;
    }

    /// <summary>The paint, the kerb or the bare asphalt, <paramref name="ax"/> metres from the middle of the road.</summary>
    private static (double R, double G, double B) Painted(double ax, bool dashed, double shade, Random chance)
    {
        var lane = Math.Abs(ax - 1.3) < 0.07 && dashed;
        var edge = Math.Abs(ax - 4.02) < 0.08;
        if (lane || edge)
        {
            var paint = 228 + ((chance.NextDouble() - 0.5) * 20);
            return (paint, paint, paint - 6);
        }

        if (ax > 4.22)
        {
            var kerb = 176 + ((chance.NextDouble() - 0.5) * 16);
            return (kerb, kerb - 2, kerb - 8);
        }

        return (shade - 3, shade + 1, shade + 9);
    }

    /// <summary>
    /// Grass, one repeat square, bright and mottled, and seamless so the repeat
    /// is not a grid across the fields.
    /// </summary>
    private static byte[] GrassPicture()
    {
        const int size = PictureSize;
        var pixels = new byte[size * size * 4];
        var chance = new Random(17);
        var broad = new PeriodicNoise(4, new Random(1));
        var middle = new PeriodicNoise(12, new Random(2));
        var fine = new PeriodicNoise(40, new Random(3));
        for (var row = 0; row < size; row++)
        {
            for (var column = 0; column < size; column++)
            {
                var (u, v) = ((double)column / size, (double)row / size);
                var mottle = (broad.At(u, v) * 0.5) + (middle.At(u, v) * 0.32) + (fine.At(u, v) * 0.18);
                var speck = (chance.NextDouble() - 0.5) * 22;

                // From a deep green in the hollows to a sunlit yellow green on top.
                Put(pixels, (row * size) + column, (92 + (mottle * 92) + speck, 150 + (mottle * 62) + speck, 26 + (mottle * 22) + (speck * 0.4)));
            }
        }

        return PngCodec.Encode(size, size, pixels);
    }

    private static void Put(byte[] pixels, int index, (double R, double G, double B) colour)
    {
        var at = index * 4;
        pixels[at] = Channel(colour.R);
        pixels[at + 1] = Channel(colour.G);
        pixels[at + 2] = Channel(colour.B);
        pixels[at + 3] = 255;
    }

    private static byte Channel(double value) => (byte)Math.Clamp(Math.Round(value, MidpointRounding.AwayFromZero), 0, 255);

    /// <summary>Smooth noise that repeats every so many cells, so a texture made of it tiles without a seam.</summary>
    private sealed class PeriodicNoise(int cells, Random chance)
    {
        private readonly double[] _values = Enumerable.Range(0, cells * cells).Select(_ => chance.NextDouble()).ToArray();

        /// <summary>The noise at (<paramref name="u"/>, <paramref name="v"/>), each from zero to one across the texture.</summary>
        public double At(double u, double v)
        {
            var (x, y) = (u * cells, v * cells);
            var (x0, y0) = ((int)Math.Floor(x), (int)Math.Floor(y));
            var (fx, fy) = (Ease(x - x0), Ease(y - y0));
            var top = Corner(x0, y0) + ((Corner(x0 + 1, y0) - Corner(x0, y0)) * fx);
            var bottom = Corner(x0, y0 + 1) + ((Corner(x0 + 1, y0 + 1) - Corner(x0, y0 + 1)) * fx);
            return top + ((bottom - top) * fy);
        }

        private static double Ease(double t) => t * t * (3 - (2 * t));

        private double Corner(int i, int j) => _values[((j % cells) * cells) + (i % cells)];
    }
}

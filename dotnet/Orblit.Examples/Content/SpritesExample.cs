using Orblit.Filament;
using Orblit.Sprite;
using Orblit.VectorMath;

namespace Orblit.Examples;

/// <summary>
/// A 2D scene, drawn by the same renderer as every 3D one.
/// </summary>
/// <remarks>
/// Three layers, each one draw however many sprites it holds: a brick
/// backdrop, a crowd of coins turning over, and sparks added on top. The
/// sprite sheet is painted here, in code, and kept as a picture.
///
/// The backdrop is the part worth watching. Its tiles are sent once, and what
/// scrolls it is the layer's transform, twenty floats a frame. The coins are
/// the other extreme. Every one of them moves, so all of them are sent every
/// frame, and even at twenty thousand that is one buffer and one draw.
/// </remarks>
public sealed class SpritesExample : Example
{
    private static readonly OrderedDictionary<string, int> Counts = new()
    {
        ["200"] = 200,
        ["2k"] = 2000,
        ["20k"] = 20000,
    };

    /// <summary>The sheet: four columns and four rows of sixteen-pixel cells.</summary>
    private const int Side = 64;

    private const int Cell = 16;

    /// <summary>
    /// How tall the view is, in world units. A cell is shown at one unit, so a
    /// sixteen-pixel sprite is an exact multiple of a pixel at most window
    /// sizes.
    /// </summary>
    private const double ViewHeight = 18.0;

    private const int Sparks = 80;

    /// <summary>Null when the sheet could not be kept, and then there is nothing to draw.</summary>
    private readonly Texture? _sheet;

    private readonly float[] _tiles = [];
    private readonly IReadOnlyList<(double U0, double V0, double U1, double V1)> _turns = [];
    private readonly (double U0, double V0, double U1, double V1) _glow;

    private string _amount = "2k";

    /// <summary>
    /// Nearest sampling, for pixel art. Off smooths the pixels into mush, which
    /// is the point of being able to see it.
    /// </summary>
    private bool _sharp = true;

    /// <summary>Corners on whole pixels, so a sprite moving by a fraction of one does not shimmer.</summary>
    private bool _snap = true;

    private bool _scrolling = true;
    private bool _sparks = true;

    private Coin[] _crowd = [];
    private float[] _coins = [];
    private int _coinRevision;
    private readonly float[] _sparkSprites = new float[Sparks * LayerSprite.Stride];
    private int _sparkRevision;

    /// <summary>Paints the sheet and keeps it in <paramref name="pictures"/>.</summary>
    public SpritesExample(IPictureFolder pictures)
    {
        if (pictures.Keep("sprites", "sheet.png", Side, Sheet()) is not { } path)
        {
            Note = "The sprite sheet could not be kept, so there is nothing to draw.";
            return;
        }

        _sheet = new Texture(path);
        var atlas = Atlas.Grid(path, Side, Side, Cell, Cell);
        _tiles = Backdrop(Uv(atlas, "frame_4"));
        _turns = [.. atlas.Sequence("frame_").Take(4).Select(frame => frame.Uv(Side, Side))];
        _glow = Uv(atlas, "frame_8");
    }

    public override string Name => "Sprites";

    public override ExampleSection Section => ExampleSection.Content;

    public override string Blurb =>
        "Pixel art in layers: one draw per layer, drawn in order, and a backdrop that scrolls without resending a tile.";

    public override RenderScene Scene(RenderCamera camera, double seconds) => new()
    {
        Objects = [],
        Sprites = _sheet is { } sheet ? Layers(sheet, seconds) : [],
        Camera = new RenderCamera
        {
            Position = new Vector3(0, 0, 20),
            Target = Vector3.Zero(),
            Orthographic = true,
            ViewHeight = ViewHeight,
        },
        Sky = new Sky { Colour = new Vector3(0.01, 0.01, 0.02), Ambient = 0 },
        // Straight through, with nothing smoothing edges. A sprite's colours are
        // the artist's, and its edges are meant to be exactly as sharp as drawn.
        Post = new PostProcess
        {
            AntiAliasing = AntiAliasing.Off,
            Dithering = false,
            Grading = new Grading { ToneMapping = ToneMapping.Linear },
        },
    };

    public override IReadOnlyList<ExampleSetting> Settings() =>
    [
        new Choice("Coins", [.. Counts.Keys], _amount, option => _amount = option),
        new Toggle("Sharp pixels", _sharp, value => _sharp = value),
        new Toggle("Snap to pixels", _snap, value => _snap = value)
        {
            Note = "Corners land on whole pixels, so nothing shimmers as it moves.",
        },
        new Toggle("Scroll the backdrop", _scrolling, value => _scrolling = value)
        {
            Note = "By moving its layer: not one tile is sent again.",
        },
        new Toggle("Sparks", _sparks, value => _sparks = value)
        {
            Note = "An additive layer, drawn on top by its order.",
        },
    ];

    public override string Code => """
        // The sheet, as a picture: from an asset, a download or, here, painted in code.
        var atlas = Atlas.Grid("sheet.png", imageWidth: 64, imageHeight: 64, cellWidth: 16, cellHeight: 16);

        // A layer is one image and any number of rectangles of it: one draw.
        var (u0, v0, u1, v1) = atlas["frame_0"]!.Uv(64, 64);
        new SpriteLayer(2, SpriteLayer.Pack([new LayerSprite { X = 0, Y = 0, U0 = u0, V0 = v0, U1 = u1, V1 = v1 }]))
        {
            Image = new Texture("sheet.png"),
            Revision = revision, // bump when the sprites change
        };

        // A backdrop scrolls by its layer's transform, and sends no tiles to do it.
        new SpriteLayer(1, tiles) { Order = -10, Transform = Matrix4.TranslationValues(scroll, 0, 0) };

        // Seen through an orthographic camera, graded straight through.
        new RenderCamera { Position = new Vector3(0, 0, 20), Target = Vector3.Zero(), Orthographic = true, ViewHeight = 18 };
        """;

    private List<SpriteLayer> Layers(Texture sheet, double seconds)
    {
        var filter = _sharp ? MaterialFilter.Sharp : MaterialFilter.Smooth;
        // Scrolled by exactly one tile's width and back, so the wrap is seamless.
        // Wrapped twice so it is never negative, not even minus nought.
        var scroll = _scrolling ? ((-(seconds * 1.5) % 2.0) + 2.0) % 2.0 : 0.0;
        PlaceCoins(seconds);
        List<SpriteLayer> layers =
        [
            new SpriteLayer(1, _tiles)
            {
                Image = sheet,
                Order = -10,
                Filter = filter,
                Snap = _snap,
                Tint = new Vector4(0.35, 0.35, 0.42, 1),
                Transform = Matrix4.TranslationValues(scroll, 0, 0),
            },
            new SpriteLayer(2, _coins) { Image = sheet, Filter = filter, Snap = _snap, Revision = _coinRevision },
        ];
        if (_sparks)
        {
            PlaceSparks(seconds);
            layers.Add(new SpriteLayer(3, _sparkSprites)
            {
                Image = sheet,
                Order = 5,
                Filter = MaterialFilter.Smooth,
                Blend = SpriteBlend.Add,
                Revision = _sparkRevision,
            });
        }

        return layers;
    }

    /// <summary>A wall of bricks two tiles wider than the view, sent once.</summary>
    private static float[] Backdrop((double U0, double V0, double U1, double V1) tile)
    {
        const int across = 44;
        const int down = 20;
        return SpriteLayer.Pack(
        [
            .. Enumerable.Range(0, down).SelectMany(row => Enumerable.Range(0, across).Select(column => new LayerSprite
            {
                X = (column * 2.0) - across + 1,
                Y = (row * 2.0) - down + 1,
                Width = 2,
                Height = 2,
                U0 = tile.U0,
                V0 = tile.V0,
                U1 = tile.U1,
                V1 = tile.V1,
            })),
        ]);
    }

    /// <summary>Every coin, written into the one buffer the layer keeps.</summary>
    private void PlaceCoins(double seconds)
    {
        var count = Counts[_amount];
        if (_crowd.Length != count)
        {
            _crowd = Coin.Crowd(count);
            _coins = new float[count * LayerSprite.Stride];
        }

        for (var i = 0; i < _crowd.Length; i++)
        {
            var coin = _crowd[i];
            var hop = Math.Abs(Math.Sin((seconds * coin.Speed) + coin.Phase)) * 1.2;
            var (u0, v0, u1, v1) = _turns[(int)Math.Floor((seconds * 8) + (coin.Phase * 4)) % 4];
            new LayerSprite { X = coin.X, Y = coin.Y + hop, U0 = u0, V0 = v0, U1 = u1, V1 = v1 }
                .WriteInto(_coins, i * LayerSprite.Stride);
        }

        _coinRevision++;
    }

    private void PlaceSparks(double seconds)
    {
        for (var i = 0; i < Sparks; i++)
        {
            var turn = (seconds * 0.6) + (i * 2 * Math.PI / Sparks);
            var reach = 5 + (2 * Math.Sin((seconds * 1.3) + i));
            var size = 1.5 + Math.Sin((seconds * 5) + (i * 1.7));
            new LayerSprite
            {
                X = Math.Cos(turn) * reach,
                Y = Math.Sin(turn) * reach * 0.6,
                Width = size,
                Height = size,
                U0 = _glow.U0,
                V0 = _glow.V0,
                U1 = _glow.U1,
                V1 = _glow.V1,
                Red = 1,
                Green = 0.55,
                Blue = 0.2,
                Alpha = 0.9,
            }.WriteInto(_sparkSprites, i * LayerSprite.Stride);
        }

        _sparkRevision++;
    }

    private static (double U0, double V0, double U1, double V1) Uv(Atlas atlas, string cell) =>
        // A grid names every cell it cuts, and a 64-pixel sheet of 16-pixel cells has sixteen.
        atlas[cell]!.Uv(Side, Side);

    private static byte[] Sheet()
    {
        var pixels = new byte[Side * Side * 4];
        // Four frames of a coin turning over, along the top row. The same disc,
        // narrower each frame until it is edge on, then wider again.
        double[] halfWidths = [7.0, 5.0, 1.5, 5.0];
        for (var frame = 0; frame < 4; frame++)
        {
            PaintCoin(pixels, frame, halfWidths[frame]);
        }

        PaintBrick(pixels);
        PaintSpark(pixels);
        return pixels;
    }

    private static void PaintCoin(byte[] pixels, int frame, double halfWidth)
    {
        for (var y = 0; y < Cell; y++)
        {
            for (var x = 0; x < Cell; x++)
            {
                var dx = (x + 0.5 - 8) / halfWidth;
                var dy = (y + 0.5 - 8) / 7;
                var d = (dx * dx) + (dy * dy);
                if (d <= 1)
                {
                    Put(pixels, (frame * Cell) + x, y, CoinColour(x, y, rim: d > 0.55));
                }
            }
        }
    }

    private static (int R, int G, int B, int A) CoinColour(int x, int y, bool rim) =>
        x < 7 && y < 6 && !rim ? (255, 244, 170, 255) : rim ? (196, 132, 28, 255) : (244, 190, 52, 255);

    /// <summary>
    /// A brick, first cell of the second row. Mortar every eight pixels down,
    /// and every sixteen across, offset by half on alternate courses.
    /// </summary>
    private static void PaintBrick(byte[] pixels)
    {
        for (var y = 0; y < Cell; y++)
        {
            for (var x = 0; x < Cell; x++)
            {
                var course = y / 8;
                var across = (x + (course % 2 == 1 ? 8 : 0)) % Cell;
                var mortar = y % 8 == 0 || across == 0;
                var grain = ((x * 7) + (y * 13)) % 5 * 6;
                Put(pixels, x, Cell + y, mortar ? (120, 116, 110, 255) : (150 + grain, 62 + (grain / 2), 48, 255));
            }
        }
    }

    /// <summary>
    /// A spark, first cell of the third row. A soft round glow whose alpha
    /// falls off with distance, which is what an additive layer wants.
    /// </summary>
    private static void PaintSpark(byte[] pixels)
    {
        for (var y = 0; y < Cell; y++)
        {
            for (var x = 0; x < Cell; x++)
            {
                var dx = (x + 0.5 - 8) / 8;
                var dy = (y + 0.5 - 8) / 8;
                var fall = Math.Clamp(1 - Math.Sqrt((dx * dx) + (dy * dy)), 0.0, 1.0);
                var level = (int)Math.Round(fall * fall * 255, MidpointRounding.AwayFromZero);
                Put(pixels, x, (2 * Cell) + y, (255, 255, 255, level));
            }
        }
    }

    private static void Put(byte[] pixels, int x, int y, (int R, int G, int B, int A) colour)
    {
        var at = ((y * Side) + x) * 4;
        pixels[at] = (byte)colour.R;
        pixels[at + 1] = (byte)colour.G;
        pixels[at + 2] = (byte)colour.B;
        pixels[at + 3] = (byte)colour.A;
    }

    /// <summary>Where a coin sits and how it hops, the same every run.</summary>
    private sealed record Coin(double X, double Y, double Speed, double Phase)
    {
        public static Coin[] Crowd(int count)
        {
            var random = new Random(11);
            return
            [
                .. Enumerable.Range(0, count).Select(_ => new Coin(
                    (random.NextDouble() * 30) - 15,
                    (random.NextDouble() * 14) - 8,
                    2 + (random.NextDouble() * 3),
                    random.NextDouble() * Math.PI * 2)),
            ];
        }
    }
}

using Orblit.VectorMath;

namespace Orblit.Examples;

// The numbers the game is made of. They are here, together and named, rather
// than spelled out where they are used, because tuning the game means changing
// these and nothing else.
public sealed partial class RunnerExample
{
    /// <summary>
    /// Three lanes, which is the number this kind of game has. Two gives no
    /// middle to go back to, and four gives no obvious one.
    /// </summary>
    private static readonly double[] Lanes = [-2.6, 0.0, 2.6];

    private const double StartSpeed = 14.0;
    private const double Gravity = 30.0;

    /// <summary>
    /// Up at nine and a half metres a second. Under this gravity that is a hop
    /// a metre and a half high and six tenths of a second long. It is well over
    /// a hurdle, and short enough that a jump is a decision rather than a way
    /// of life.
    /// </summary>
    private const double JumpSpeed = 9.4;

    private const double SlideTime = 0.7;

    /// <summary>
    /// How far the runner reaches along the road either side of its middle,
    /// and across it from its middle to an obstacle's.
    /// </summary>
    private const double HalfDepth = 0.35;

    private const double Reach = 1.55;

    /// <summary>
    /// What clears what. The hurdle's top is at a metre. The bar's underside is
    /// at 1.05, which a runner standing at 1.3 does not fit under and one
    /// sliding at 0.8 does.
    /// </summary>
    private const double HurdleClear = 0.92;

    private const double BarBottom = 1.05;
    private const double Standing = 1.3;
    private const double Sliding = 0.8;

    private const double Chunk = 40.0;
    private const int Chunks = 11;
    private const int Behind = 3;

    /// <summary>
    /// How far is laid ahead: to the end of the chunks, where the haze has all
    /// but finished.
    /// </summary>
    private const double Ahead = (Chunks - Behind) * Chunk;

    private const double Rebase = 1024.0;
    private const int BladesPerChunk = 1100;
    private const double GrassRange = 60.0;
    private const int Unlaid = -1 << 30;

    /// <summary>
    /// How many of each kind of scenery one chunk may hold, which is what
    /// keeps a chunk's keys apart from the next one's.
    /// </summary>
    private static readonly int[] Caps = [6, 5, 1, 1, 3, 1, 3, 1, 1];

    private static readonly string[] SceneryMeshes =
    [
        RunnerArt.Pine,
        RunnerArt.Oak,
        RunnerArt.House,
        RunnerArt.DarkHouse,
        RunnerArt.Bush,
        RunnerArt.RedBush,
        RunnerArt.Rock,
        RunnerArt.Billboard,
        RunnerArt.Hill,
    ];

    /// <summary>
    /// The clouds, where they are relative to the runner: across, up, how far
    /// ahead, and how big.
    /// </summary>
    private static readonly (double X, double Y, double Ahead, double Size)[] CloudPlaces =
    [
        (-170.0, 58.0, 280.0, 17.0),
        (-60.0, 74.0, 320.0, 13.0),
        (30.0, 46.0, 250.0, 11.0),
        (120.0, 66.0, 300.0, 16.0),
        (230.0, 50.0, 270.0, 14.0),
        (-280.0, 40.0, 260.0, 12.0),
        (300.0, 80.0, 330.0, 15.0),
    ];

    private static readonly Vector3 White = Vector3.All(1);
}

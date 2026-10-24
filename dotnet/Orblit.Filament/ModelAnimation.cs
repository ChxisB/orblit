using Orblit.VectorMath;

namespace Orblit.Filament;

/// <summary>Which of a model file's clips plays on an object, and where it is.</summary>
public sealed record ModelAnimation
{
    /// <summary>
    /// Whole numbers a pose packs: the clip, the clip being left, the flags below and the object's
    /// material variant. The renderer reads the same count.
    /// </summary>
    public const int IntStride = 4;

    /// <summary>Floats a pose packs: this clip's seconds and speed, the left clip's, and the fade.</summary>
    public const int Stride = 5;

    internal const int LoopsFlag = 1 << 0;
    internal const int FromLoopsFlag = 1 << 1;

    /// <summary>What an object that names no clip packs: no clip, held still.</summary>
    internal static readonly ModelAnimation Rest = new() { Clip = -1, Speed = 0, Loop = false };

    /// <summary>The clip, by its position in <see cref="AssetInfo.Clips"/>. One the file lacks is a note and nothing plays.</summary>
    public required int Clip { get; init; }

    /// <summary>Where the clip is when the scene is sent, in its own seconds.</summary>
    public double Seconds { get; init; }

    /// <summary>Clip seconds that pass per second of the host's clock. Nought holds the clip, which is how one is scrubbed.</summary>
    public double Speed { get; init; } = 1;

    /// <summary>Whether it wraps past its end, or stops on its last frame.</summary>
    public bool Loop { get; init; } = true;

    /// <summary>The clip being left while a change of clip fades in. Its own <see cref="From"/> is not read.</summary>
    public ModelAnimation? From { get; init; }

    /// <summary>How far the fade has gone: nought is all <see cref="From"/>, one is all this clip. Ignored without a <see cref="From"/>.</summary>
    public double Fade { get; init; } = 1;
}

/// <summary>
/// A joint of a model's skin, set by hand after any clip, so a hand-set joint always wins. It
/// replaces the joint's local transform.
/// </summary>
public sealed record JointPose(int Skin, int Joint, Matrix4 Transform);

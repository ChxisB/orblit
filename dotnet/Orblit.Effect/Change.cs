using Orblit.VectorMath;

namespace Orblit.Effect;

/// <summary>
/// What an effect has done, at a moment.
/// A difference rather than a state: an offset from where something was, a
/// turn from how it was facing, a multiplier on how big it was. That is what
/// lets two effects run at once, moving and spinning or fading and growing,
/// and be added together without either needing to know about the other.
/// </summary>
public sealed class Change
{
    public Change(
        Vector3? move = null,
        Quaternion? turn = null,
        Vector3? grow = null,
        Vector4? tint = null,
        double fade = 1)
    {
        Move = move ?? Vector3.Zero();
        Turn = turn ?? Quaternion.Identity();
        Grow = grow ?? Vector3.All(1);
        Tint = tint ?? new Vector4(1, 1, 1, 1);
        Fade = fade;
    }

    /// <summary>Nothing has happened.</summary>
    public static Change None => new();

    /// <summary>How far it has moved from where it was.</summary>
    public Vector3 Move { get; }

    /// <summary>
    /// How far it has turned from how it was facing. Apply it with
    /// <see cref="Effects.Applied"/>: the matrix is what a renderer uses.
    /// </summary>
    public Quaternion Turn { get; }

    /// <summary>How much bigger it is than it was. One is unchanged.</summary>
    public Vector3 Grow { get; }

    /// <summary>What its colour is multiplied by.</summary>
    public Vector4 Tint { get; }

    /// <summary>What its opacity is multiplied by.</summary>
    public double Fade { get; }

    /// <summary>
    /// Whether this changes anything at all.
    /// </summary>
    public bool IsNothing =>
        Move.Length2 < 1e-18
        && (Grow - Vector3.All(1)).Length2 < 1e-18
        && Math.Abs(Fade - 1) < 1e-9
        && Math.Abs(Turn.W - 1) < 1e-9;

    /// <summary>
    /// Two changes, both applied. Offsets add, turns compose, scales and tints
    /// multiply: each combined the way that quantity actually combines, which
    /// is why this cannot be one loop over a list of doubles.
    /// </summary>
    public Change And(Change other) => new(
        move: Move + other.Move,
        turn: Turn * other.Turn,
        grow: new Vector3(Grow.X * other.Grow.X, Grow.Y * other.Grow.Y, Grow.Z * other.Grow.Z),
        tint: new Vector4(Tint.X * other.Tint.X, Tint.Y * other.Tint.Y, Tint.Z * other.Tint.Z, Tint.W * other.Tint.W),
        fade: Fade * other.Fade);

    public override string ToString() => FormattableString.Invariant($"Change(move: {Move}, grow: {Grow}, fade: {Fade})");
}

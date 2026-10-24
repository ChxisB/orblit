using System.Collections.ObjectModel;
using Orblit.VectorMath;

namespace Orblit.Input;

/// <summary>
/// What an on-screen interface says its player is doing. A game fills one from the events of the
/// <c>stick</c>, <c>touch-button</c> and <c>swipe-pad</c> elements of <c>Orblit.Ui</c>, and gives
/// what it holds to <see cref="FrameControls"/>. The two packages never reference each other. They
/// agree on the shape of a payload, and a payload of any other shape is ignored, because it came
/// from a script that was being edited.
/// </summary>
/// <remarks>
/// Name each element once. Two buttons called <c>jump</c> share one slot, so lifting one finger
/// releases the other.
/// </remarks>
public sealed class TouchControls
{
    private readonly OrderedSet<string> _held = new();
    private readonly Dictionary<PadStick, Vector2> _sticks = new();
    private readonly Vector2 _swiped = Vector2.Zero();

    public TouchControls()
    {
        Sticks = new ReadOnlyDictionary<PadStick, Vector2>(_sticks);
    }

    /// <summary>The regions being touched now. A live view, so one object does for every frame.</summary>
    public IReadOnlyCollection<string> Held => _held;

    /// <summary>
    /// The sticks being pushed now, by the pad stick each stands in for. A stick at rest has no
    /// entry. Positive is up and to the right. A live view.
    /// </summary>
    public IReadOnlyDictionary<PadStick, Vector2> Sticks { get; }

    /// <summary>How far fingers have moved across a swipe pad since <see cref="EndFrame"/>. Positive is up and to the right, in logical pixels.</summary>
    public Vector2 Swiped => _swiped.Clone();

    /// <summary>Presses or releases <paramref name="region"/> for a payload of true or false.</summary>
    public void Hold(string region, object? payload)
    {
        switch (payload)
        {
            case true:
                _held.Add(region);
                break;
            case false:
                _held.Remove(region);
                break;
        }
    }

    /// <summary>Puts <paramref name="which"/> where a payload of <c>[x, y]</c> says, each from -1 to 1. A push past the edge of the circle is brought back to it.</summary>
    public void MoveStick(PadStick which, object? payload)
    {
        if (Pair(payload) is not { } point)
        {
            return;
        }

        if (point.Length2 > 1)
        {
            point.Normalize();
        }

        if (point.Length2 == 0)
        {
            _sticks.Remove(which);
        }
        else
        {
            _sticks[which] = point;
        }
    }

    /// <summary>Adds a payload of <c>[dx, dy]</c> to <see cref="Swiped"/>.</summary>
    public void Swipe(object? payload)
    {
        if (Pair(payload) is { } point)
        {
            _swiped.X += point.X;
            _swiped.Y += point.Y;
        }
    }

    /// <summary>Forgets the swipes, once the frame has used them.</summary>
    public void EndFrame()
    {
        _swiped.X = 0;
        _swiped.Y = 0;
    }

    /// <summary>The two numbers of a payload, or null for anything else.</summary>
    private static Vector2? Pair(object? payload) =>
        payload is IReadOnlyList<object?> { Count: 2 } list
        && JsonNumber.Of(list[0]) is { } x
        && JsonNumber.Of(list[1]) is { } y
        && double.IsFinite(x)
        && double.IsFinite(y)
            ? new Vector2(x, y)
            : null;
}

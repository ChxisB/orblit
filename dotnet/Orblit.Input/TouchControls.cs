using System.Collections.ObjectModel;
using Orblit.VectorMath;

namespace Orblit.Input;

/// <summary>
/// What an on-screen interface says its player is doing. A game fills one from the events of the
/// touch controls in <c>Orblit.Ui.Avalonia</c>, and gives what it holds to
/// <see cref="FrameControls"/>. The two packages never reference each other. A stick or a swipe
/// arrives as an <see cref="Vector2"/>, and one with a value that is not finite is ignored.
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

    /// <summary>Presses <paramref name="region"/> when <paramref name="held"/> is true and releases it when it is false.</summary>
    public void Hold(string region, bool held)
    {
        if (held)
        {
            _held.Add(region);
        }
        else
        {
            _held.Remove(region);
        }
    }

    /// <summary>Puts <paramref name="which"/> at <paramref name="push"/>, each from -1 to 1. A push past the edge of the circle is brought back to it.</summary>
    public void MoveStick(PadStick which, Vector2 push)
    {
        if (!IsFinite(push))
        {
            return;
        }

        var point = push.Clone();
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

    /// <summary>Adds <paramref name="by"/> to <see cref="Swiped"/>.</summary>
    public void Swipe(Vector2 by)
    {
        if (IsFinite(by))
        {
            _swiped.X += by.X;
            _swiped.Y += by.Y;
        }
    }

    /// <summary>Forgets the swipes, once the frame has used them.</summary>
    public void EndFrame()
    {
        _swiped.X = 0;
        _swiped.Y = 0;
    }

    private static bool IsFinite(Vector2 point) => double.IsFinite(point.X) && double.IsFinite(point.Y);
}

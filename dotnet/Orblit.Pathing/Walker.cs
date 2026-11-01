using Orblit.Agent;
using Orblit.VectorMath;

namespace Orblit.Pathing;

/// <summary>
/// Finds a route between two places, or null when there is none.
/// A <see cref="NavGrid.FindPath"/> or a <see cref="NavMesh.FindPath"/> is one. So is anything else
/// that answers the same question, such as a route a game looked up.
/// </summary>
public delegate IReadOnlyList<Vector3>? Planner(Vector3 from, Vector3 to);

/// <summary>
/// Whether the straight line between two places is still free to walk.
/// A <see cref="NavGrid.IsClear"/> is one.
/// </summary>
public delegate bool Clear(Vector3 from, Vector3 to);

/// <summary>
/// A body that walks to a place by the route a <see cref="Planner"/> gives it.
/// The route is followed with <see cref="FollowPath"/> and the body is moved by
/// <see cref="Steerable.Integrate"/>, so its speed and how hard it can turn are the
/// body's own. The walker knows nothing of where walls are; that is the
/// planner's job, and a body that cannot turn sharply needs a planner that
/// keeps wider of corners.
/// With <see cref="IsClear"/> it also notices when the way ahead is shut, such as a door
/// that closed after the route was planned. It looks along the next
/// <see cref="Lookahead"/> legs, and a shut one makes it plan again from where it stands,
/// with no call to <see cref="GoTo"/>. The planner and <see cref="IsClear"/> must see the same
/// obstacles, so give both the same grid. If the new route is shut as well,
/// or there is none, it stands, keeping its goal.
/// </summary>
public sealed class NavWalker
{
    private IReadOnlyList<Vector3> _route = [];
    private long _next;
    private Vector3? _goal;

    // shortcut: a walker with no route stays put until GoTo is called again, such as from a
    // behaviour tree on a cooldown. Upgrade to a retry on a timer when a game wants agents to
    // wait at a door.
    // F2: five parameters, two required, all called by name.
    public NavWalker(Steerable body, Planner planner, double reached = 0.5, Clear? isClear = null, long lookahead = 3)
    {
        if (lookahead < 1)
        {
            throw new ArgumentException("Must be at least 1.", nameof(lookahead));
        }

        Body = body;
        Planner = planner;
        Reached = reached;
        IsClear = isClear;
        Lookahead = lookahead;
    }

    public Steerable Body { get; }

    public Planner Planner { get; }

    /// <summary>Checks a leg of the route as the body walks, or null to trust the route.</summary>
    public Clear? IsClear { get; }

    /// <summary>How many legs of the route ahead of the body <see cref="IsClear"/> looks along.</summary>
    public long Lookahead { get; }

    /// <summary>How close to a point of the route counts as having got there.</summary>
    public double Reached { get; }

    /// <summary>Where it is going, or null when it is not going anywhere.</summary>
    public Vector3? Goal => _goal?.Clone();

    /// <summary>The points it is walking through, from where it was told to go.</summary>
    public IReadOnlyList<Vector3> Route => _route.ToList().AsReadOnly();

    /// <summary>Whether there is a route to walk. False after <see cref="GoTo"/> means the planner found none.</summary>
    public bool HasRoute => _route.Count != 0;

    /// <summary>Whether it has walked the whole route.</summary>
    public bool IsArrived => HasRoute && new FollowPath(_route, _next, Reached).IsDone(Body);

    /// <summary>
    /// Plans a route from where the body is to <paramref name="goal"/> and starts along it.
    /// Whether one was found is <see cref="HasRoute"/>. A goal with no route leaves the
    /// body standing.
    /// </summary>
    public void GoTo(Vector3 goal)
    {
        _goal = goal.Clone();
        _route = Planner(Body.Position.Clone(), goal) ?? [];
        _next = 0;
    }

    /// <summary>Forgets the route. The body keeps the speed it has.</summary>
    public void Stop()
    {
        _goal = null;
        _route = [];
        _next = 0;
    }

    /// <summary>Moves the body along the route for <paramref name="seconds"/>.</summary>
    public void Update(double seconds)
    {
        if (!HasRoute)
        {
            return;
        }

        if (IsShut())
        {
            Replan();
        }

        if (!HasRoute)
        {
            return;
        }

        var follow = new FollowPath(_route, _next, Reached);
        Body.Integrate(follow.Force(Body), seconds);
        _next = follow.NextIndex(Body);
    }

    /// <summary>
    /// Whether a leg within <see cref="Lookahead"/> of the body is no longer clear, from
    /// the body to the next point and on along the route.
    /// </summary>
    private bool IsShut()
    {
        if (IsClear is not { } clear)
        {
            return false;
        }

        var from = Body.Position;
        var last = Math.Min(_route.Count, _next + Lookahead);
        for (var at = _next; at < last; at++)
        {
            if (!clear(from, _route[(int)at]))
            {
                return true;
            }

            from = _route[(int)at];
        }

        return false;
    }

    private void Replan()
    {
        if (_goal is not { } goal)
        {
            return;
        }

        GoTo(goal);
        if (IsShut())
        {
            _route = [];
        }
    }
}

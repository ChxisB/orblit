using Orblit.VectorMath;

namespace Orblit.Pathing;

/// <summary>
/// A way between two points that is not over the ground: a jump across a gap,
/// a ladder, a door, a teleporter.
/// <see cref="Kind"/> names it for whatever plays the crossing, such as an animation. The
/// planner only carries it. <see cref="Cost"/> is added to the length walked, so a ladder
/// that takes a while to climb costs more than a door. A link is one way from
/// <see cref="From"/> to <see cref="To"/> unless <see cref="TwoWay"/>, because a drop is not a climb.
/// With <see cref="Agents"/> empty, anything may use it. Otherwise only an agent of one of
/// those kinds may. A link with a <see cref="Gate"/> is shut while the planner has that
/// gate locked.
/// </summary>
public sealed class NavLink
{
    // F2: seven parameters, three required, all called by name.
    public NavLink(
        Vector3 from,
        Vector3 to,
        string kind,
        double cost = 0,
        IEnumerable<string>? agents = null,
        string? gate = null,
        bool twoWay = false)
    {
        if (kind.Length == 0)
        {
            throw new ArgumentException("Must name the kind of link.", nameof(kind));
        }

        if (!(cost >= 0 && double.IsFinite(cost)))
        {
            throw new ArgumentException("Must be zero or more.", nameof(cost));
        }

        From = from.Clone();
        To = to.Clone();
        Kind = kind;
        Cost = cost;
        Agents = new HashSet<string>(agents ?? []);
        Gate = gate;
        TwoWay = twoWay;
    }

    public Vector3 From { get; }

    public Vector3 To { get; }

    public string Kind { get; }

    public double Cost { get; }

    public IReadOnlySet<string> Agents { get; }

    public string? Gate { get; }

    public bool TwoWay { get; }
}

/// <summary>One step of a route.</summary>
public abstract class RouteStep
{
    private protected RouteStep()
    {
    }
}

/// <summary>Walk these points over the ground, in order.</summary>
public sealed class WalkStep(IReadOnlyList<Vector3> points) : RouteStep
{
    public IReadOnlyList<Vector3> Points { get; } = points;
}

/// <summary>
/// Cross <see cref="Link"/>. When <see cref="Reversed"/>, the way is from the link's <see cref="NavLink.To"/> to
/// its <see cref="NavLink.From"/>, which only a two way link allows.
/// </summary>
public sealed class LinkStep(NavLink link, bool reversed) : RouteStep
{
    public NavLink Link { get; } = link;

    public bool Reversed { get; } = reversed;

    public Vector3 Start => Reversed ? Link.To : Link.From;

    public Vector3 End => Reversed ? Link.From : Link.To;
}

/// <summary>
/// Routes over the ground and across links.
/// The ground is any <see cref="Planner"/>, such as a <see cref="NavGrid.FindPath"/> or a
/// <see cref="NavMesh.FindPath"/>. A route is the cheapest sum of ground walked and link
/// costs, written as the steps to take in order. Moving along them, and
/// playing the crossing of a link, is for whatever follows the route. The
/// planner moves nothing.
/// A route is found fresh each time it is asked for, so a gate locked or
/// unlocked between two asks changes the second.
/// </summary>
public sealed class LinkPlanner
{
    private readonly HashSet<string> _locked = [];

    public LinkPlanner(Planner ground, IReadOnlyList<NavLink> links)
    {
        Ground = ground;
        Links = links.ToList().AsReadOnly();
    }

    public Planner Ground { get; }

    public IReadOnlyList<NavLink> Links { get; }

    /// <summary>Shuts every link with this gate.</summary>
    public void Lock(string gate) => _locked.Add(gate);

    /// <summary>Opens every link with this gate.</summary>
    public void Unlock(string gate) => _locked.Remove(gate);

    public bool IsLocked(string gate) => _locked.Contains(gate);

    /// <summary>
    /// The steps from <paramref name="from"/> to <paramref name="to"/> for an agent of kind <paramref name="agent"/>, or null when
    /// there is no way.
    /// A walk of no length next to a link is left out, so standing at the foot
    /// of a ladder starts with the climb. With no link in the route it is one
    /// <see cref="WalkStep"/>, as the ground planner gave it.
    /// </summary>
    public IReadOnlyList<RouteStep>? FindRoute(Vector3 from, Vector3 to, string agent = "") =>
        new LinkSearch(Ground, from, to, HopsFor(agent)).Run();

    private List<LinkStep> HopsFor(string agent)
    {
        var hops = new List<LinkStep>();
        foreach (var link in Links)
        {
            if ((link.Gate is { } gate && IsLocked(gate)) || (link.Agents.Count != 0 && !link.Agents.Contains(agent)))
            {
                continue;
            }

            hops.Add(new LinkStep(link, reversed: false));
            if (link.TwoWay)
            {
                hops.Add(new LinkStep(link, reversed: true));
            }
        }

        return hops;
    }
}

/// <summary>
/// How a place was reached: walking from <c>From</c>, then crossing <c>Hop</c> if it
/// is not null.
/// </summary>
internal sealed record Arrival(int From, IReadOnlyList<Vector3> Walk, double Length, LinkStep? Hop);

/// <summary>
/// Dijkstra over the places a route can stand: the start, the goal, and where
/// each link comes out. From a place the ground leads to the goal or to the
/// foot of any link, and a link leads to where it comes out.
/// </summary>
internal sealed class LinkSearch
{
    private const int Start = 0;
    private const int Goal = 1;

    private readonly Planner _ground;
    private readonly List<LinkStep> _hops;
    private readonly List<Vector3> _places;
    private readonly double[] _spent;
    private readonly bool[] _done;
    private readonly Arrival?[] _came;

    internal LinkSearch(Planner ground, Vector3 from, Vector3 to, List<LinkStep> hops)
    {
        _ground = ground;
        _hops = hops;
        _places = [from, to, .. hops.Select(hop => hop.End)];
        _spent = new double[hops.Count + 2];
        Array.Fill(_spent, double.PositiveInfinity);
        _done = new bool[hops.Count + 2];
        _came = new Arrival?[hops.Count + 2];
        _spent[Start] = 0;
    }

    // shortcut: a scan for the cheapest place and a ground search from each place to every link,
    // so the ground is searched about the square of the number of links. Fine for tens of links.
    // Upgrade to a heap, and a search from the ground's own graph, before a level has hundreds.
    internal List<RouteStep>? Run()
    {
        while (true)
        {
            var node = Cheapest();
            if (node < 0)
            {
                return null;
            }

            if (node == Goal)
            {
                return Steps();
            }

            _done[node] = true;
            Relax(node, Goal, null);
            for (var k = 0; k < _hops.Count; k++)
            {
                Relax(node, k + 2, _hops[k]);
            }
        }
    }

    private static double LengthOf(IReadOnlyList<Vector3> points)
    {
        var total = 0.0;
        for (var i = 1; i < points.Count; i++)
        {
            total += points[i].DistanceTo(points[i - 1]);
        }

        return total;
    }

    private int Cheapest()
    {
        var best = -1;
        for (var i = 0; i < _spent.Length; i++)
        {
            if (_done[i] || _spent[i] == double.PositiveInfinity)
            {
                continue;
            }

            if (best < 0 || _spent[i] < _spent[best])
            {
                best = i;
            }
        }

        return best;
    }

    /// <summary>
    /// Offers <paramref name="target"/> as reached from <paramref name="node"/>: by walking to it, or by walking
    /// to the foot of <paramref name="hop"/> and crossing it.
    /// </summary>
    private void Relax(int node, int target, LinkStep? hop)
    {
        var walk = _ground(_places[node], hop?.Start ?? _places[target]);
        if (walk is null)
        {
            return;
        }

        var length = LengthOf(walk);
        var total = _spent[node] + length + (hop?.Link.Cost ?? 0);
        if (total >= _spent[target])
        {
            return;
        }

        _spent[target] = total;
        _came[target] = new Arrival(node, walk, length, hop);
    }

    private List<RouteStep> Steps()
    {
        var arrivals = new List<Arrival>();
        for (var at = Goal; at != Start; at = _came[at]!.From)
        {
            arrivals.Add(_came[at]!);
        }

        arrivals.Reverse();
        var crosses = arrivals.Any(arrival => arrival.Hop is not null);
        var steps = new List<RouteStep>();
        foreach (var arrival in arrivals)
        {
            if (!crosses || arrival.Length > 0)
            {
                steps.Add(new WalkStep(arrival.Walk));
            }

            if (arrival.Hop is { } hop)
            {
                steps.Add(hop);
            }
        }

        return steps;
    }
}

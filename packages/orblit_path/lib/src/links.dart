import 'package:vector_math/vector_math_64.dart';

import 'walker.dart' show Planner;

/// A way between two points that is not over the ground: a jump across a gap,
/// a ladder, a door, a teleporter.
///
/// [kind] names it for whatever plays the crossing, such as an animation. The
/// planner only carries it. [cost] is added to the length walked, so a ladder
/// that takes a while to climb costs more than a door. A link is one way from
/// [from] to [to] unless [twoWay], because a drop is not a climb.
///
/// With [agents] empty, anything may use it. Otherwise only an agent of one of
/// those kinds may. A link with a [gate] is shut while the planner has that
/// gate locked.
final class NavLink {
  NavLink({
    required Vector3 from,
    required Vector3 to,
    required this.kind,
    this.cost = 0,
    this.agents = const {},
    this.gate,
    this.twoWay = false,
  }) : from = from.clone(),
       to = to.clone() {
    if (kind.isEmpty) {
      throw ArgumentError.value(kind, 'kind', 'Must name the kind of link.');
    }
    if (!(cost >= 0 && cost.isFinite)) {
      throw ArgumentError.value(cost, 'cost', 'Must be zero or more.');
    }
  }

  final Vector3 from;
  final Vector3 to;
  final String kind;
  final double cost;
  final Set<String> agents;
  final String? gate;
  final bool twoWay;
}

/// One step of a route.
sealed class RouteStep {
  const RouteStep();
}

/// Walk these points over the ground, in order.
final class WalkStep extends RouteStep {
  const WalkStep(this.points);

  final List<Vector3> points;
}

/// Cross [link]. When [reversed], the way is from the link's [NavLink.to] to
/// its [NavLink.from], which only a two way link allows.
final class LinkStep extends RouteStep {
  const LinkStep(this.link, {required this.reversed});

  final NavLink link;
  final bool reversed;

  Vector3 get start => reversed ? link.to : link.from;
  Vector3 get end => reversed ? link.from : link.to;
}

/// Routes over the ground and across links.
///
/// The ground is any [Planner], such as a [NavGrid.findPath] or a
/// [NavMesh.findPath]. A route is the cheapest sum of ground walked and link
/// costs, written as the steps to take in order. Moving along them, and
/// playing the crossing of a link, is for whatever follows the route. The
/// planner moves nothing.
///
/// A route is found fresh each time it is asked for, so a gate locked or
/// unlocked between two asks changes the second.
final class LinkPlanner {
  LinkPlanner({required this.ground, required List<NavLink> links})
    : links = List.unmodifiable(links);

  final Planner ground;
  final List<NavLink> links;
  final Set<String> _locked = {};

  /// Shuts every link with this gate.
  void lock(String gate) => _locked.add(gate);

  /// Opens every link with this gate.
  void unlock(String gate) => _locked.remove(gate);

  bool isLocked(String gate) => _locked.contains(gate);

  /// The steps from [from] to [to] for an agent of kind [agent], or null when
  /// there is no way.
  ///
  /// A walk of no length next to a link is left out, so standing at the foot
  /// of a ladder starts with the climb. With no link in the route it is one
  /// [WalkStep], as the ground planner gave it.
  List<RouteStep>? findRoute(Vector3 from, Vector3 to, {String agent = ''}) {
    final hops = _hopsFor(agent);
    return _Search(ground, from, to, hops).run();
  }

  List<_Hop> _hopsFor(String agent) {
    final hops = <_Hop>[];
    for (final link in links) {
      final gate = link.gate;
      if (gate != null && isLocked(gate)) continue;
      if (link.agents.isNotEmpty && !link.agents.contains(agent)) continue;
      hops.add(_Hop(link, reversed: false));
      if (link.twoWay) hops.add(_Hop(link, reversed: true));
    }
    return hops;
  }
}

/// A link taken in one direction.
final class _Hop {
  const _Hop(this.link, {required this.reversed});

  final NavLink link;
  final bool reversed;

  Vector3 get start => reversed ? link.to : link.from;
  Vector3 get end => reversed ? link.from : link.to;
}

/// How a place was reached: walking from [from], then crossing [hop] if it
/// is not null.
final class _Arrival {
  const _Arrival(this.from, this.walk, this.length, this.hop);

  final int from;
  final List<Vector3> walk;
  final double length;
  final _Hop? hop;
}

const int _start = 0;
const int _goal = 1;

/// Dijkstra over the places a route can stand: the start, the goal, and where
/// each link comes out. From a place the ground leads to the goal or to the
/// foot of any link, and a link leads to where it comes out.
///
/// shortcut: a scan for the cheapest place and a ground search from each place
/// to every link, so the ground is searched about the square of the number of
/// links. Fine for tens of links. Upgrade to a heap, and a search from the
/// ground's own graph, before a level has hundreds.
final class _Search {
  _Search(this.ground, Vector3 from, Vector3 to, this.hops)
    : places = [from, to, for (final hop in hops) hop.end],
      spent = List.filled(hops.length + 2, double.infinity),
      done = List.filled(hops.length + 2, false),
      came = List.filled(hops.length + 2, null) {
    spent[_start] = 0;
  }

  final Planner ground;
  final List<_Hop> hops;
  final List<Vector3> places;
  final List<double> spent;
  final List<bool> done;
  final List<_Arrival?> came;

  List<RouteStep>? run() {
    while (true) {
      final node = _cheapest();
      if (node < 0) return null;
      if (node == _goal) return _steps();
      done[node] = true;
      _relax(node, _goal, null);
      for (var k = 0; k < hops.length; k++) {
        _relax(node, k + 2, hops[k]);
      }
    }
  }

  int _cheapest() {
    var best = -1;
    for (var i = 0; i < spent.length; i++) {
      if (done[i] || spent[i] == double.infinity) continue;
      if (best < 0 || spent[i] < spent[best]) best = i;
    }
    return best;
  }

  /// Offers [target] as reached from [node]: by walking to it, or by walking
  /// to the foot of [hop] and crossing it.
  void _relax(int node, int target, _Hop? hop) {
    final walk = ground(places[node], hop?.start ?? places[target]);
    if (walk == null) return;
    final length = _lengthOf(walk);
    final total = spent[node] + length + (hop?.link.cost ?? 0);
    if (total >= spent[target]) return;
    spent[target] = total;
    came[target] = _Arrival(node, walk, length, hop);
  }

  List<RouteStep> _steps() {
    final arrivals = <_Arrival>[];
    for (var at = _goal; at != _start; at = came[at]!.from) {
      arrivals.add(came[at]!);
    }
    final crosses = arrivals.any((arrival) => arrival.hop != null);
    return [
      for (final arrival in arrivals.reversed) ...[
        if (!crosses || arrival.length > 0) WalkStep(arrival.walk),
        if (arrival.hop case final hop?)
          LinkStep(hop.link, reversed: hop.reversed),
      ],
    ];
  }

  static double _lengthOf(List<Vector3> points) {
    var total = 0.0;
    for (var i = 1; i < points.length; i++) {
      total += points[i].distanceTo(points[i - 1]);
    }
    return total;
  }
}

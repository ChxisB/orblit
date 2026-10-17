import 'dart:math' as math;

import 'package:orblit_agent/orblit_agent.dart';
import 'package:vector_math/vector_math_64.dart';

/// Finds a route between two places, or null when there is none.
///
/// A [NavGrid.findPath] or a [NavMesh.findPath] is one. So is anything else
/// that answers the same question, such as a route a game looked up.
typedef Planner = List<Vector3>? Function(Vector3 from, Vector3 to);

/// Whether the straight line between two places is still free to walk.
///
/// A [NavGrid.isClear] is one.
typedef Clear = bool Function(Vector3 from, Vector3 to);

/// A body that walks to a place by the route a [Planner] gives it.
///
/// The route is followed with [FollowPath] and the body is moved by
/// [Steerable.integrate], so its speed and how hard it can turn are the
/// body's own. The walker knows nothing of where walls are; that is the
/// planner's job, and a body that cannot turn sharply needs a planner that
/// keeps wider of corners.
///
/// With [isClear] it also notices when the way ahead is shut, such as a door
/// that closed after the route was planned. It looks along the next
/// [lookahead] legs, and a shut one makes it plan again from where it stands,
/// with no call to [goTo]. The planner and [isClear] must see the same
/// obstacles, so give both the same grid. If the new route is shut as well,
/// or there is none, it stands, keeping its [goal].
///
/// shortcut: a walker with no route stays put until [goTo] is called again,
/// such as from a behaviour tree on a cooldown. Upgrade to a retry on a
/// timer when a game wants agents to wait at a door.
final class NavWalker {
  NavWalker({
    required this.body,
    required this.planner,
    this.reached = 0.5,
    this.isClear,
    this.lookahead = 3,
  }) {
    if (lookahead < 1) {
      throw ArgumentError.value(lookahead, 'lookahead', 'Must be at least 1.');
    }
  }

  final Steerable body;
  final Planner planner;

  /// Checks a leg of the route as the body walks, or null to trust the route.
  final Clear? isClear;

  /// How many legs of the route ahead of the body [isClear] looks along.
  final int lookahead;

  /// How close to a point of the route counts as having got there.
  final double reached;

  List<Vector3> _route = const [];
  int _next = 0;
  Vector3? _goal;

  /// Where it is going, or null when it is not going anywhere.
  Vector3? get goal => _goal?.clone();

  /// The points it is walking through, from where it was told to go.
  List<Vector3> get route => List.unmodifiable(_route);

  /// Whether there is a route to walk. False after [goTo] means the planner
  /// found none.
  bool get hasRoute => _route.isNotEmpty;

  /// Whether it has walked the whole route.
  bool get isArrived =>
      hasRoute &&
      FollowPath(_route, index: _next, reached: reached).isDone(body);

  /// Plans a route from where the body is to [goal] and starts along it.
  ///
  /// Whether one was found is [hasRoute]. A goal with no route leaves the
  /// body standing.
  void goTo(Vector3 goal) {
    _goal = goal.clone();
    _route = planner(body.position.clone(), goal) ?? const [];
    _next = 0;
  }

  /// Forgets the route. The body keeps the speed it has.
  void stop() {
    _goal = null;
    _route = const [];
    _next = 0;
  }

  /// Moves the body along the route for [seconds].
  void update(double seconds) {
    if (!hasRoute) return;
    if (_isShut()) _replan();
    if (!hasRoute) return;
    final follow = FollowPath(_route, index: _next, reached: reached);
    body.integrate(follow.force(body), seconds);
    _next = follow.nextIndex(body);
  }

  /// Whether a leg within [lookahead] of the body is no longer clear, from
  /// the body to the next point and on along the route.
  bool _isShut() {
    final clear = isClear;
    if (clear == null) return false;
    var from = body.position;
    final last = math.min(_route.length, _next + lookahead);
    for (var at = _next; at < last; at++) {
      if (!clear(from, _route[at])) return true;
      from = _route[at];
    }
    return false;
  }

  void _replan() {
    final goal = _goal;
    if (goal == null) return;
    goTo(goal);
    if (_isShut()) _route = const [];
  }
}

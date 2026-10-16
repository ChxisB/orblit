import 'package:orblit_agent/orblit_agent.dart';
import 'package:vector_math/vector_math_64.dart';

/// Finds a route between two places, or null when there is none.
///
/// A [NavGrid.findPath] or a [NavMesh.findPath] is one. So is anything else
/// that answers the same question, such as a route a game looked up.
typedef Planner = List<Vector3>? Function(Vector3 from, Vector3 to);

/// A body that walks to a place by the route a [Planner] gives it.
///
/// The route is followed with [FollowPath] and the body is moved by
/// [Steerable.integrate], so its speed and how hard it can turn are the
/// body's own. The walker knows nothing of where walls are; that is the
/// planner's job, and a body that cannot turn sharply needs a planner that
/// keeps wider of corners.
final class NavWalker {
  NavWalker({required this.body, required this.planner, this.reached = 0.5});

  final Steerable body;
  final Planner planner;

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
    final follow = FollowPath(_route, index: _next, reached: reached);
    body.integrate(follow.force(body), seconds);
    _next = follow.nextIndex(body);
  }
}

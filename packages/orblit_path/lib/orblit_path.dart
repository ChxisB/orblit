/// Curves to follow and ways across ground.
///
/// A [Spline] is a curve measured in metres, with a tilt at each knot,
/// that a [PathFollower] rides, a [Rail] slides along and a [Patrol] gives
/// an agent places to go. A [NavGrid] is ground cut into cells and a
/// [NavMesh] the same ground as a few large rectangles; either finds a way
/// across it, round walls, and a [NavWalker] walks an agent along that way
/// with the steering of `orblit_agent`.
///
/// Nothing here draws or moves an entity. It answers where, and the game
/// puts something there.
library;

export 'src/bake.dart' show MeshWalk, bakeMesh, bakeTerrain, carve, marked;
export 'src/follower.dart' show PathEnd, PathFollower, Rail;
export 'src/grid.dart' show NavCell, NavGrid;
export 'src/navmesh.dart' show NavMesh;
export 'src/patrol.dart' show Patrol, PatrolStop;
export 'src/spline.dart'
    show
        PathFormatException,
        PathFrame,
        PathKnot,
        PathNearest,
        Spline,
        pathExtension;
export 'src/walker.dart' show NavWalker, Planner;

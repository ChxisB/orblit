import '../component.dart';
import '../values.dart';

/// How a joint lets its body move.
enum JointKind {
  /// Not at all: welded where it stands.
  fixed,

  /// Any way it likes about the joint's point, and never away from it. A
  /// pendulum, a ball and socket.
  point,

  /// About the joint's x axis only, like a door. Limited by
  /// [JointAxis.aboutX], and it takes a motor.
  hinge,

  /// Along the joint's x axis only, never turning, like a drawer. Limited by
  /// [JointAxis.alongX], and it takes a motor.
  slider,

  /// Keeps the joint's point and the middle of its body a distance apart, and
  /// lets both turn. Limited by [JointAxis.alongX], which is then a range of
  /// lengths: a rope is nought to its length. Unlimited, it is a rod.
  distance,

  /// About its point, with its body's x axis kept within [JointComponent.swing]
  /// of the joint's. A shoulder or a hip. [JointAxis.aboutX] limits the twist.
  cone,

  /// Each of the six ways set on its own in [JointComponent.limits]: free
  /// where there is no limit, and locked where a limit's low is its high.
  sixAxis,
}

/// One of the six ways a joint's body can move: along the joint's three axes
/// and about them.
enum JointAxis {
  alongX,
  alongY,
  alongZ,
  aboutX,
  aboutY,
  aboutZ;

  /// Whether this is a turn, measured in degrees, rather than a distance in
  /// metres.
  bool get turns => index >= 3;
}

/// The least and the most of one [JointAxis]: metres along an axis, degrees
/// about one.
class JointRange {
  const JointRange(this.low, this.high);

  /// A range that is one value: locked there.
  const JointRange.at(double value) : low = value, high = value;

  final double low;
  final double high;

  bool get locked => low == high;
}

/// A joint: holds the body this entity belongs to to the body it hangs from.
///
/// Which two bodies is where the entity sits, not ids it names. The joint's
/// body is the nearest body at or above this entity — this entity's own, or
/// its parent's, and so on up — and what it hangs from is the nearest body
/// above that, or the world when there is none. A forearm hangs from an upper
/// arm because it is under it in the tree; a door on the world has nothing
/// above it with a body. Named that way because an id named in a component is
/// an id every copy, prefab and paste has to find and change, and a parent
/// link is one they already do. It cannot close a loop — a chain tied at both
/// ends — for the same reason.
///
/// The joint's point and axes are this entity's own, where it stands when the
/// scene begins: a hinge turns about this entity's x axis through its origin.
/// So a joint that is not at its body's middle — a door's hinge, an elbow — is
/// an entity of its own under the body, placed there. A distance joint keeps
/// this point a distance from the middle of its body.
///
/// What it holds is how the bodies stood when the scene began, so every
/// limit is measured from there, and each measure is the joint's body as the
/// body it hangs from sees it: a hinge made with its door shut reads nought
/// when the door is shut, and an elbow's angle turns with the upper arm.
///
/// Every field is written every time, including the ones this kind does not
/// use, for the reason [BodyComponent] gives: a hinge switched to a slider and
/// back should come back with the range it had.
class JointComponent extends SceneComponent {
  JointComponent({
    this.kind = JointKind.hinge,
    Map<JointAxis, JointRange> limits = const {},
    this.swing = 45,
    this.speed = 0,
    this.strength = 0,
    this.breakingForce = 0,
    this.breakingTorque = 0,
    this.collide = false,
  }) : limits = Map.unmodifiable(limits);

  static JointComponent fromJson(Map<String, Object?> json) => JointComponent(
    kind: Values.named(JointKind.values, json['kind']) ?? JointKind.hinge,
    limits: _limitsOf(json['limits']),
    swing: Values.number(json, 'swing', 45),
    speed: Values.number(json, 'speed', 0),
    strength: Values.number(json, 'strength', 0),
    breakingForce: Values.number(json, 'breakingForce', 0),
    breakingTorque: Values.number(json, 'breakingTorque', 0),
    collide: Values.flag(json, 'collide', fallback: false),
  );

  /// The same joint with some of it changed. [limits], when given, replaces
  /// every limit; [limit] and [free] change one.
  JointComponent copyWith({
    JointKind? kind,
    Map<JointAxis, JointRange>? limits,
    double? swing,
    double? speed,
    double? strength,
    double? breakingForce,
    double? breakingTorque,
    bool? collide,
  }) => JointComponent(
    kind: kind ?? this.kind,
    limits: limits ?? this.limits,
    swing: swing ?? this.swing,
    speed: speed ?? this.speed,
    strength: strength ?? this.strength,
    breakingForce: breakingForce ?? this.breakingForce,
    breakingTorque: breakingTorque ?? this.breakingTorque,
    collide: collide ?? this.collide,
  );

  /// The same joint with [axis] limited to [range].
  JointComponent limit(JointAxis axis, JointRange range) =>
      copyWith(limits: {...limits, axis: range});

  /// The same joint with [axis] free.
  JointComponent free(JointAxis axis) =>
      copyWith(limits: {...limits}..remove(axis));

  final JointKind kind;

  /// The range each axis is kept within, in metres along and degrees about.
  /// An axis not here is free, or whatever the kind makes it; a kind reads
  /// only the axes it says it does.
  final Map<JointAxis, JointRange> limits;

  /// How far a cone lets its body's x axis swing from the joint's, in
  /// degrees.
  final double swing;

  /// A hinge's or a slider's motor: the speed it drives at, in degrees or
  /// metres a second, and the most it pushes with, in newton-metres or
  /// newtons. No strength is no motor; no speed with a little strength is
  /// friction in the joint.
  final double speed;
  final double strength;

  /// Past this force in newtons, or this torque in newton-metres, the joint
  /// breaks. Nought never breaks.
  final double breakingForce;
  final double breakingTorque;

  /// Whether the two bodies still collide with each other. Off by default,
  /// because two bodies joined nearly always overlap where they are joined.
  final bool collide;

  /// The limits this kind reads, which are all the solver will hear of.
  Iterable<JointAxis> get axes => switch (kind) {
    JointKind.fixed || JointKind.point => const [],
    JointKind.hinge || JointKind.cone => const [JointAxis.aboutX],
    JointKind.slider || JointKind.distance => const [JointAxis.alongX],
    JointKind.sixAxis => JointAxis.values,
  };

  /// The two bodies the joint on entity [id] holds together: `body`, the
  /// nearest entity at or above [id] with a body, and `holder`, the nearest
  /// one above that. `holder` is null for the world. `body` is null when
  /// there is nothing to hold, and the joint does nothing.
  ///
  /// Written against two lookups rather than a document, so everything that
  /// keeps a tree — a document, an editor's own scene — answers the same.
  static ({String? body, String? holder}) endsOf(
    String id, {
    required String? Function(String id) parentOf,
    required bool Function(String id) hasBody,
  }) {
    // A tree that loops back on itself ends the climb where it comes round,
    // and never at the body it started from.
    final seen = <String>{};
    String? nearest(String? from) {
      for (var at = from; at != null && seen.add(at); at = parentOf(at)) {
        if (hasBody(at)) return at;
      }
      return null;
    }

    final body = nearest(id);
    if (body == null) return (body: null, holder: null);
    return (body: body, holder: nearest(parentOf(body)));
  }

  @override
  String get type => SceneComponents.joint;

  @override
  Map<String, Object?> toJson() => {
    'kind': kind.name,
    'limits': {
      for (final axis in JointAxis.values)
        if (limits[axis] case final range?) axis.name: [range.low, range.high],
    },
    'swing': swing,
    'speed': speed,
    'strength': strength,
    'breakingForce': breakingForce,
    'breakingTorque': breakingTorque,
    'collide': collide,
  };

  /// Each limit a file gives as two numbers under an axis's name. Anything
  /// else there is skipped, so a hand-edit that gets one wrong loses that one
  /// and keeps the rest.
  static Map<JointAxis, JointRange> _limitsOf(Object? raw) {
    final json = Values.object(raw);
    return {
      for (final axis in JointAxis.values)
        if (json[axis.name] case [final num low, final num high])
          axis: JointRange(low.toDouble(), high.toDouble()),
    };
  }
}

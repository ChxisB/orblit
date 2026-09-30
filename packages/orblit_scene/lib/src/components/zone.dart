import 'package:vector_math/vector_math_64.dart';

import '../component.dart';
import '../values.dart';

/// A region that changes how the bodies inside it move.
///
/// It goes on an entity that has a [BodyComponent], and that body is the
/// region: its shape, placed where the entity is. Having a zone makes the body
/// a trigger, so nothing collides with it and it pushes nothing, whether or not
/// [BodyComponent.trigger] is set. An entity with a zone and no body does
/// nothing, as a joint with nothing to hold does.
///
/// Each field that is set replaces what the body inside has, and one left out
/// leaves the body's own. Water is a weak gravity and a lot of damping. A lift
/// shaft is a gravity that points up.
///
/// Where two zones overlap, each field is decided on its own by the higher
/// [priority], and by the entity that was made first at a tie. A zone acts on
/// free bodies that are awake. A character asks for its own gravity, and a
/// fixed or driven body is not moved by any.
///
/// A field that is left out is left out of the file too. Writing a value for
/// it would say the zone changes something it does not.
class ZoneComponent extends SceneComponent {
  ZoneComponent({
    Vector3? gravity,
    this.linearDamping,
    this.angularDamping,
    this.priority = 0,
  }) : gravity = gravity?.clone();

  static ZoneComponent fromJson(Map<String, Object?> json) {
    final gravity = json['gravity'];
    return ZoneComponent(
      gravity: gravity is List ? Values.vector(gravity) : null,
      linearDamping: Values.maybeNumber(json, 'linearDamping'),
      angularDamping: Values.maybeNumber(json, 'angularDamping'),
      priority: Values.number(json, 'priority', 0).round(),
    );
  }

  /// The same zone with some of it changed. A field cannot be cleared here,
  /// only set; build a new zone to leave one out.
  ZoneComponent copyWith({
    Vector3? gravity,
    double? linearDamping,
    double? angularDamping,
    int? priority,
  }) => ZoneComponent(
    gravity: gravity ?? this.gravity,
    linearDamping: linearDamping ?? this.linearDamping,
    angularDamping: angularDamping ?? this.angularDamping,
    priority: priority ?? this.priority,
  );

  /// Metres per second squared, as a vector: nought holds a body where it is,
  /// and up lifts it. Null leaves the world's gravity.
  final Vector3? gravity;

  /// How quickly a body slows down and stops spinning inside, as a fraction a
  /// second, in place of its own. Null leaves the body's.
  final double? linearDamping;
  final double? angularDamping;

  /// Which zone wins where two overlap. Higher wins, field by field.
  final int priority;

  @override
  String get type => SceneComponents.zone;

  @override
  Map<String, Object?> toJson() {
    final gravity = this.gravity;
    return Values.pruned({
      'gravity': gravity == null ? null : Values.vectorToJson(gravity),
      'linearDamping': linearDamping,
      'angularDamping': angularDamping,
      'priority': priority,
    });
  }
}

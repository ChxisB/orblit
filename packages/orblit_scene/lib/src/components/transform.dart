import 'dart:math' as math;

import 'package:vector_math/vector_math_64.dart';

import '../component.dart';
import '../values.dart';

/// Where an entity sits, relative to its parent.
///
/// Rotation in degrees around X, Y and Z, because that is what the file is
/// read by people and the number somebody typed into the inspector should be
/// the number they find in the file. The conversion to a matrix belongs to
/// whoever is drawing.
///
/// Its own component rather than three fields on the entity: a thing that is
/// only a name and some children — a folder in the outliner — has no position,
/// and giving it one at the origin means every reparent silently moves its
/// contents.
class TransformComponent extends SceneComponent {
  TransformComponent({Vector3? position, Vector3? rotation, Vector3? scale})
    : position = position ?? Vector3.zero(),
      rotation = rotation ?? Vector3.zero(),
      scale = scale ?? Vector3.all(1);

  static TransformComponent fromJson(Map<String, Object?> json) =>
      TransformComponent(
        position: Values.vector(json['position']),
        rotation: Values.vector(json['rotation']),
        scale: Values.vector(json['scale'], fallback: 1),
      );

  final Vector3 position;
  final Vector3 rotation;
  final Vector3 scale;

  /// The rotation [degrees] stands for, composed Z, then Y, then X.
  ///
  /// That order is the scene's, and every reader of a transform has to use
  /// it: Euler angles do not commute, so another order turns anything rotated
  /// about more than one axis by an amount that looks plausible and is wrong.
  static Matrix3 rotationOf(Vector3 degrees) =>
      Matrix3.rotationZ(radians(degrees.z))
        ..multiply(Matrix3.rotationY(radians(degrees.y)))
        ..multiply(Matrix3.rotationX(radians(degrees.x)));

  /// The three angles that [rotationOf] turns back into [rotation].
  ///
  /// At the pole, where a quarter turn about Y leaves the other two
  /// indistinguishable, the roll is folded into the yaw, because at that
  /// point the rotation itself no longer says which of the two it was.
  static Vector3 anglesOf(Matrix3 rotation) {
    final sinY = -rotation.entry(2, 0);
    if (sinY.abs() >= 0.9999999) {
      return Vector3(
        0,
        sinY.isNegative ? -90 : 90,
        degrees(math.atan2(-rotation.entry(0, 1), rotation.entry(1, 1))),
      );
    }
    return Vector3(
      degrees(math.atan2(rotation.entry(2, 1), rotation.entry(2, 2))),
      degrees(math.asin(sinY.clamp(-1.0, 1.0))),
      degrees(math.atan2(rotation.entry(1, 0), rotation.entry(0, 0))),
    );
  }

  @override
  String get type => SceneComponents.transform;

  @override
  Map<String, Object?> toJson() => {
    'position': Values.vectorToJson(position),
    'rotation': Values.vectorToJson(rotation),
    'scale': Values.vectorToJson(scale),
  };
}

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

  @override
  String get type => SceneComponents.transform;

  @override
  Map<String, Object?> toJson() => {
    'position': Values.vectorToJson(position),
    'rotation': Values.vectorToJson(rotation),
    'scale': Values.vectorToJson(scale),
  };
}

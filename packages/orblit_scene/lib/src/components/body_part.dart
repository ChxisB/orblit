import 'package:vector_math/vector_math_64.dart';

import '../values.dart';
import 'body.dart';

/// A convex part of a body. Scale, turn and centre are local to the compound.
/// Dynamics and material belong to the body that holds the part.
final class BodyPart {
  BodyPart({
    this.shape = BodyShape.box,
    Vector3? size,
    this.radius = 0.5,
    this.height = 2,
    List<double> hull = const [],
    Vector3? centre,
    Quaternion? rotation,
    Vector3? scale,
  }) : size = (size ?? Vector3.all(1)).clone(),
       hull = List.unmodifiable(hull),
       centre = (centre ?? Vector3.zero()).clone(),
       rotation = (rotation ?? Quaternion.identity()).clone(),
       scale = (scale ?? Vector3.all(1)).clone();

  final BodyShape shape;
  final Vector3 size;
  final double radius;
  final double height;
  final List<double> hull;
  final Vector3 centre;
  final Quaternion rotation;
  final Vector3 scale;

  BodyPart copyWith({
    BodyShape? shape,
    Vector3? size,
    double? radius,
    double? height,
    List<double>? hull,
    Vector3? centre,
    Quaternion? rotation,
    Vector3? scale,
  }) => BodyPart(
    shape: shape ?? this.shape,
    size: size ?? this.size,
    radius: radius ?? this.radius,
    height: height ?? this.height,
    hull: hull ?? this.hull,
    centre: centre ?? this.centre,
    rotation: rotation ?? this.rotation,
    scale: scale ?? this.scale,
  );

  static BodyPart fromJson(Map<String, Object?> json) {
    final turn = Values.numbers(json['rotation']);
    return BodyPart(
      shape: Values.named(BodyShape.values, json['shape']) ?? BodyShape.box,
      size: Values.vector(json['size'], fallback: 1),
      radius: Values.number(json, 'radius', 0.5),
      height: Values.number(json, 'height', 2),
      hull: Values.numbers(json['hull']),
      centre: Values.vector(json['centre']),
      rotation: turn.length == 4
          ? Quaternion(turn[0], turn[1], turn[2], turn[3])
          : Quaternion.identity(),
      scale: Values.vector(json['scale'], fallback: 1),
    );
  }

  static List<BodyPart> listFromJson(Object? raw) => raw is List
      ? [
          for (final value in raw)
            if (value is Map<String, Object?>) fromJson(value),
        ]
      : const [];

  Map<String, Object?> toJson() => {
    'shape': shape.name,
    'size': Values.vectorToJson(size),
    'radius': radius,
    'height': height,
    'hull': hull,
    'centre': Values.vectorToJson(centre),
    'rotation': [rotation.x, rotation.y, rotation.z, rotation.w],
    'scale': Values.vectorToJson(scale),
  };
}

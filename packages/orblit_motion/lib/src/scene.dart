import 'package:orblit_scene/orblit_scene.dart';
import 'package:vector_math/vector_math_64.dart';

import 'frame.dart';

/// Where a clip's targets are, for one entity playing it.
///
/// A clip names what it moves in the ids of the document it was made in,
/// with the entity playing it as the empty target. Played on an instance of
/// a prefab, that document is the prefab, and its ids are found in the
/// scene under the instance's path: `bulb` in a lamp's clip is
/// `street1/lamp3/bulb` for the lamp placed as `street1/lamp3`. Played on
/// something that is not in any instance, the ids are the scene's own.
class ClipScope {
  const ClipScope(this.owner, {this.instance, this.root});

  /// The scope of [owner] as [document] holds it.
  ///
  /// An instance's root plays in its prefab's ids, a part of an instance in
  /// the ids of the prefab it is a part of, and anything else in the
  /// document's.
  factory ClipScope.inScene(SceneDocument document, String owner) {
    if (document[owner]?[SceneComponents.prefab] != null) {
      return ClipScope(owner, instance: owner);
    }
    final enclosing = EntityPath.enclosing(owner);
    return ClipScope(
      owner,
      instance: enclosing.isEmpty ? null : enclosing.first,
    );
  }

  /// The entity playing the clip.
  final String owner;

  /// The instance whose prefab the clip's ids are in, or null when they are
  /// the scene's own.
  final String? instance;

  /// The id the prefab gives its own root, when it is known. Only needed for
  /// a clip that names the prefab's root by id rather than as the owner,
  /// which one made by a part of the prefab can.
  final String? root;

  /// The id in the scene of [target].
  String resolve(String target) {
    if (target.isEmpty) return owner;
    final instance = this.instance;
    if (instance == null) return target;
    if (target == root) return instance;
    return EntityPath.join(instance, target);
  }

  /// [id] as a target of this scope, or null when a clip played here has no
  /// way to name it: something outside the instance it plays in.
  String? targetOf(String id) {
    if (id == owner) return '';
    final instance = this.instance;
    if (instance == null) return id;
    if (id == instance) return root;
    return EntityPath.localTo(instance, id, root: '');
  }
}

/// The changes that make [document] look the way [frame] says.
///
/// One [SetField] a property, from what the scene has to what the frame
/// has, so the list is its own undo and a preview can be put back exactly.
/// Nothing for a property already at its value, nor for an entity or a
/// component the scene does not have: a clip made for a lamp with a flame,
/// played on one without, moves what is there.
///
/// Bones are left out. A skeleton is the model's, and is posed by whoever
/// draws it, not written into the scene.
List<SetField> sceneOpsFor(
  ClipFrame frame,
  SceneDocument document,
  ClipScope scope,
) {
  final ops = <SetField>[];
  for (final MapEntry(key: target, value: properties) in frame.values.entries) {
    final id = scope.resolve(target);
    final entity = document[id];
    if (entity == null) continue;
    for (final MapEntry(key: property, value: value) in properties.entries) {
      final dot = property.indexOf('.');
      if (dot <= 0) continue;
      final type = property.substring(0, dot);
      final field = property.substring(dot + 1);
      final component = entity[type];
      if (component == null) continue;
      final from = component.toJson()[field];
      final to = _json(property, value);
      if (Values.same(from, to)) continue;
      ops.add(SetField(id, type, field, from: from, to: to));
    }
  }
  return ops;
}

/// [value] as the scene writes it.
///
/// A rotation onto a transform becomes the transform's degrees, because a
/// transform is written in degrees; anywhere else it is four numbers, the
/// way a clip writes one.
Object _json(String property, Object value) => switch (value) {
  final Quaternion rotation when property == 'transform.rotation' =>
    Values.vectorToJson(
      TransformComponent.anglesOf(rotation.asRotationMatrix()),
    ),
  final Quaternion rotation => [rotation.x, rotation.y, rotation.z, rotation.w],
  final Vector3 vector => Values.vectorToJson(vector),
  _ => value,
};

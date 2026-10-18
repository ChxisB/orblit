import 'package:orblit_scene/orblit_scene.dart';

/// Ways to find one entity in a scene, failing with what is there when it is
/// not.
extension SceneFinders on SceneDocument {
  /// The entity called [name]. Names need not be unique, so this fails when
  /// two entities share it. Use [entityAt] then.
  SceneEntity entityNamed(String name) {
    final found = entities.where((entity) => entity.name == name).toList();
    if (found.length == 1) return found.single;
    final names = entities.map((entity) => '"${entity.name}"').join(', ');
    throw StateError(
      found.isEmpty
          ? 'No entity is called "$name". The names are $names.'
          : '${found.length} entities are called "$name": '
                '${found.map((entity) => entity.id).join(', ')}.',
    );
  }

  /// The entity whose id is [path]. An entity inside a prefab instance has
  /// its path as its id, `street1/lamp3/bulb`.
  SceneEntity entityAt(String path) =>
      this[path] ?? (throw StateError('No entity has the id "$path".'));
}

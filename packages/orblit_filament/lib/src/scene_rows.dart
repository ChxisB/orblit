import 'population.dart';
import 'scene.dart';

/// Rows that say what a scene submitted, for the render verbs of
/// `orblit_remote`. A host passes `drawRows` as `RemoteHost.draws` and
/// `materialRow` as `RemoteHost.material`.
///
/// A row is the scene's statement, not the renderer's work. Culling, range
/// fade and batching come after it. The renderer's own count is in
/// `OrblitFrameCapture`.
extension OrblitSceneRows on OrblitScene {
  /// One row for each object, then each population. The `id` is `object:` or
  /// `population:` and the key, because the two key spaces are separate.
  ///
  /// A null `mesh` is the built-in cube. `materialListed` is false when an
  /// object names a material the scene does not list, which draws it in its
  /// own colour on the default surface.
  List<Map<String, Object?>> drawRows() {
    final listed = {for (final material in materials) material.key};
    return [
      for (final object in objects) _objectRow(object, listed),
      for (final population in populations) _populationRow(population),
    ];
  }

  /// The material with [key], with the ids of the objects made of it, or null
  /// when the scene lists none.
  Map<String, Object?>? materialRow(int key) {
    final material = materials.where((m) => m.key == key).firstOrNull;
    if (material == null) return null;
    return {
      ...material.toJson(),
      'usedBy': [
        for (final object in objects)
          if (object.material == key) 'object:${object.key}',
      ],
    };
  }
}

Map<String, Object?> _objectRow(OrblitObject object, Set<int> listed) => {
  'id': 'object:${object.key}',
  'kind': 'object',
  'key': object.key,
  'mesh': object.mesh,
  'material': object.material,
  'materialListed': listed.contains(object.material),
  'instances': 1,
  'visible': object.visible,
  'layer': object.layer,
  'castShadows': object.castShadows,
  'receiveShadows': object.receiveShadows,
  'colour': object.colour.storage.toList(),
  'transform': object.transform.storage.toList(),
};

Map<String, Object?> _populationRow(OrblitPopulation population) => {
  'id': 'population:${population.key}',
  'kind': 'population',
  'key': population.key,
  'mesh': population.mesh,
  'material': null,
  'materialListed': false,
  'instances': population.count,
  'visible': population.isVisible,
  'layer': population.layer,
  'castShadows': population.castShadows,
  'receiveShadows': population.receiveShadows,
  'range': population.range,
  'fade': population.fade.name,
  'revision': population.revision,
  'minimum': population.minimum.storage.toList(),
  'maximum': population.maximum.storage.toList(),
};

import 'package:orblit_filament/orblit_filament.dart';
import 'package:orblit_terrain/orblit_terrain.dart';
import 'package:vector_math/vector_math_64.dart';

/// Turns what a [ScatterPlacer] put down into what the renderer draws.
///
/// A block — a layer with no mesh — is drawn as a population, one to a region
/// and layer, keyed [key] plus the group's id. Its buffers are handed over as
/// they are, not copied, and it carries the group's revision, so a region the
/// renderer already holds costs nothing to pass again and one placed again is
/// sent because its revision moved. That is what lets a meadow be a hundred
/// thousand blades.
///
/// A model is drawn as one object each, because a population is drawn from
/// the built-in cube alone. Objects made of one mesh and one material are
/// drawn together, so a model layer should name a material, resolved to the
/// key the scene lists it under by [material]; without one each copy is drawn
/// on its own in the model's own materials. [mesh] turns the layer's project
/// path into the absolute one the renderer loads, and is the identity if
/// null. An object's key is [objectKey], plus a million or so for each group
/// id, plus its place in the group, so keep [objectKey] clear of the scene's
/// other objects. Every object is compared with the last frame's, so this is
/// for trees and boulders by the thousand, not grass by the million: a region
/// with more than a million of one model throws [ArgumentError]. An object
/// has no range, so a model layer's is not kept: it is drawn however far off.
///
/// Groups with nothing in them are left out, which is how the renderer learns
/// that a layer has gone from a region. So are model layers when [models] is
/// false, for a view that has no way to resolve their files.
///
/// Rebuilding the objects costs a matrix each, so ask again only when
/// [ScatterPlacer.revision] has moved.
({List<OrblitPopulation> populations, List<OrblitObject> objects}) scatterFrom(
  ScatterPlacer placer, {
  required int key,
  int objectKey = 0,
  String Function(String path)? mesh,
  int? Function(String path)? material,
  bool models = true,
}) {
  final layers = placer.layers;
  final populations = <OrblitPopulation>[];
  final objects = <OrblitObject>[];

  for (final group in placer.groups) {
    if (group.count == 0) continue;
    final layer = layers[group.layer];
    final model = layer.mesh;
    if (model == null) {
      populations.add(
        OrblitPopulation(
          key: key + group.id,
          transforms: group.transforms,
          colours: group.colours,
          minimum: group.minimum,
          maximum: group.maximum,
          range: layer.range,
          revision: group.revision,
          castShadows: layer.castShadows,
        ),
      );
      continue;
    }
    if (!models) continue;

    if (group.count >= _perGroup) {
      throw ArgumentError(
        'The "${layer.name}" layer put ${group.count} models in one region; '
        'past $_perGroup, draw it as blocks.',
      );
    }
    final path = mesh == null ? model : mesh(model);
    final surface = layer.material == null || material == null
        ? null
        : material(layer.material!);
    final base = objectKey + group.id * _perGroup;
    for (var i = 0; i < group.count; i++) {
      objects.add(
        OrblitObject(
          key: base + i,
          transform: Matrix4.zero()..copyFromArray(group.transforms, i * 16),
          colour: Vector3(
            group.colours[i * 3],
            group.colours[i * 3 + 1],
            group.colours[i * 3 + 2],
          ),
          mesh: path,
          material: surface,
          castShadows: layer.castShadows,
        ),
      );
    }
  }
  return (populations: populations, objects: objects);
}

/// How many keys each group's objects have to themselves. A multiple rather
/// than a shift, so a key past 32 bits comes out the same on the web.
const int _perGroup = 1048576;

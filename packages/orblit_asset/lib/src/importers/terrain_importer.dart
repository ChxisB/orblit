import 'dart:convert';
import 'dart:typed_data';

import 'package:orblit_terrain/orblit_terrain.dart';

import '../asset_id.dart';
import '../import_settings.dart';
import '../importer.dart';

/// Checks a terrain at cook time and carries it into the bundle.
///
/// A terrain is one `.oterrain` file and a `.oregion` file for each region it
/// lists. Neither is cooked into anything smaller. A region is already
/// written without the maps that sit at their defaults, and the files are
/// what the runtime reads. So both are carried as written, and what the cook
/// adds is the check a build otherwise makes at run time, as a hole in the
/// ground on somebody's phone.
///
/// A terrain depends on every region it lists and on every image, model and
/// material its sets and scatter layers name, so one that is missing fails
/// the terrain's cook the way a missing model fails a scene's. A region file
/// is checked against its own name. The terrain then checks each of its
/// regions against its own size, which no one region can know.
final class TerrainImporter extends Importer {
  const TerrainImporter();

  /// The output of a cooked terrain. The bundle files it as
  /// `<asset id>.oterrain`, so `world.oterrain` becomes
  /// `world.oterrain.oterrain`.
  static const String terrainOutput = 'oterrain';

  /// The output of a cooked region, filed as `x0_z0.oregion.oregion`.
  static const String regionOutput = 'oregion';

  @override
  String get name => 'terrain';

  @override
  int get version => 1;

  @override
  Set<String> get extensions => const {'oterrain', 'oregion'};

  @override
  Map<String, Object?> resolveSettings(ImportSettings settings) => const {};

  @override
  Future<Set<AssetId>> dependenciesOf(
    AssetId id,
    Uint8List bytes,
    Map<String, Object?> settings,
  ) async {
    if (id.extension != 'oterrain') return const {};
    final load = _terrainIn(id, bytes);
    return {
      for (final key in load.regions) regionFile(id, key),
      for (final path in _pathsIn(load.terrain)) _assetAt(id, path),
    };
  }

  @override
  Future<ImportResult> import(ImportRequest request) =>
      request.id.extension == 'oterrain'
      ? _importTerrain(request)
      : _importRegion(request);

  /// The source id of region [key], beside the terrain at [terrain].
  static AssetId regionFile(AssetId terrain, RegionKey key) =>
      AssetId.parse(_beside(terrain, key.fileName));

  Future<ImportResult> _importRegion(ImportRequest request) async {
    final id = request.id;
    final load = _regionIn(id, request.bytes);
    final holds = load.region.key;
    final named = RegionKey.fromFileName(id.name);
    if (named != null && named != holds) {
      throw ImportFailure(
        id,
        'this file is named for region ${named.x}, ${named.z} and holds '
        'region ${holds.x}, ${holds.z}',
      );
    }
    return ImportResult(
      outputs: {regionOutput: request.bytes},
      notes: [
        if (named == null)
          '${id.name} is not named for its place, so no terrain will find '
              'it. This one is ${holds.fileName}.',
        ...load.problems,
      ],
    );
  }

  Future<ImportResult> _importTerrain(ImportRequest request) async {
    final id = request.id;
    final load = _terrainIn(id, request.bytes);
    final size = load.terrain.regionSize;
    for (final key in load.regions) {
      final file = regionFile(id, key);
      final region = _regionIn(file, await request.source.read(file)).region;
      if (region.key != key) {
        throw ImportFailure(
          id,
          'lists region ${key.x}, ${key.z}, and ${file.name} holds region '
          '${region.key.x}, ${region.key.z}',
        );
      }
      if (region.size != size) {
        throw ImportFailure(
          id,
          '${file.name} is ${region.size} texels across and this '
          'terrain\'s regions are $size, so it cannot be put in',
        );
      }
    }
    return ImportResult(
      outputs: {terrainOutput: request.bytes},
      notes: [
        '${load.regions.length} '
            'region${load.regions.length == 1 ? '' : 's'} of $size texels, '
            '${load.terrain.spacing} m apart',
        ...load.problems,
      ],
    );
  }

  static TerrainLoad _terrainIn(AssetId id, Uint8List bytes) {
    try {
      return Terrain.decode(utf8.decode(bytes));
    } on TerrainFormatException catch (error) {
      throw ImportFailure(id, error.message, cause: error);
    } on FormatException catch (error) {
      throw ImportFailure(
        id,
        'this is not text: ${error.message}',
        cause: error,
      );
    }
  }

  static RegionLoad _regionIn(AssetId id, Uint8List bytes) {
    try {
      return TerrainRegion.decode(bytes);
    } on RegionFormatException catch (error) {
      throw ImportFailure(id, error.message, cause: error);
    }
  }

  /// Everything the sets and the scatter layers name, as the paths the file
  /// wrote. Each is "a path within the project".
  static Iterable<String> _pathsIn(Terrain terrain) sync* {
    for (final set in terrain.sets) {
      if (set.albedo != null) yield set.albedo!;
      if (set.normal != null) yield set.normal!;
    }
    for (final layer in terrain.scatter) {
      if (layer.mesh != null) yield layer.mesh!;
      if (layer.material != null) yield layer.material!;
    }
  }

  static AssetId _assetAt(AssetId terrain, String path) {
    final id = AssetId.tryParse(path);
    if (id != null) return id;
    throw ImportFailure(
      terrain,
      'names "$path", which is not a path within the project',
    );
  }

  static String _beside(AssetId id, String name) =>
      id.directory.isEmpty ? name : '${id.directory}/$name';
}

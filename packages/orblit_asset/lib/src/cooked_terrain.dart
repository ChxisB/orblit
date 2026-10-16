import 'dart:convert';
import 'dart:typed_data';

import 'package:orblit_terrain/orblit_terrain.dart';

import 'asset_id.dart';
import 'asset_source.dart';
import 'importers/terrain_importer.dart';

/// A terrain read back out of a cooked bundle or a pack.
///
/// [terrain] holds the settings and no regions. Give it and [regions] to a
/// [TerrainStreamer], which reads the ones near the focus as it moves.
final class CookedTerrain {
  CookedTerrain._(this.terrain, this.regions, this.problems);

  /// Opens the terrain cooked from the source file [file], such as
  /// `terrain/world.oterrain`, reading its settings from [source] and
  /// finding its regions there.
  ///
  /// Throws [AssetNotFound] when the terrain itself is not there and a
  /// [TerrainFormatException] when it cannot be read. A region it lists that
  /// the source does not hold is left out and named in [problems], once, and
  /// is never asked for.
  static Future<CookedTerrain> open(AssetSource source, AssetId file) async {
    final bytes = await source.read(
      _cooked(file, TerrainImporter.terrainOutput),
    );
    final load = Terrain.decode(utf8.decode(bytes));
    final present = <RegionKey>[];
    final problems = [...load.problems];
    for (final key in load.regions) {
      final region = _cooked(
        TerrainImporter.regionFile(file, key),
        TerrainImporter.regionOutput,
      );
      if (await source.exists(region)) {
        present.add(key);
      } else {
        problems.add('${key.fileName} is listed and is not in the bundle.');
      }
    }
    return CookedTerrain._(
      load.terrain,
      _CookedRegions(source, file, present),
      List.unmodifiable(problems),
    );
  }

  final Terrain terrain;

  /// The regions the bundle holds, as the terrain's file lists them.
  final RegionSource regions;

  /// What the terrain's file left out and which listed regions are missing.
  final List<String> problems;

  /// Where the cook filed [output] of [source]: `<asset id>.<output>`.
  static AssetId _cooked(AssetId source, String output) =>
      AssetId.parse('$source.$output');
}

final class _CookedRegions implements RegionSource {
  _CookedRegions(this._source, this._terrain, List<RegionKey> keys)
    : keys = List.unmodifiable(keys);

  final AssetSource _source;
  final AssetId _terrain;

  @override
  final List<RegionKey> keys;

  @override
  Future<Uint8List> read(RegionKey key) => _source.read(
    CookedTerrain._cooked(
      TerrainImporter.regionFile(_terrain, key),
      TerrainImporter.regionOutput,
    ),
  );
}

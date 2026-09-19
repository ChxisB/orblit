import 'dart:convert';
import 'dart:io';

import 'asset_id.dart';
import 'asset_manifest.dart';
import 'content_hash.dart';
import 'cook.dart';
import 'cook_cache.dart';
import 'cook_cache_io.dart';
import 'directory_io.dart';
import 'import_settings.dart';
import 'importer.dart';
import 'importers/gltf_importer.dart';
import 'importers/native_importers_io.dart';

/// Cooking a whole project for one target, and writing the result somewhere a
/// build can pick it up.
///
/// This is the part a build hook calls. `bin/cook.dart` is argument parsing
/// over it and nothing else, so that a project driving the cook from its own
/// script gets exactly what the command gets — including the exit code's
/// meaning, which is the thing a build actually reads.
class CookProject {
  CookProject({
    required this.assetsPath,
    required this.outPath,
    required this.target,
    String? cachePath,
    ImporterRegistry? importers,
    this.limitBytes = 2 * 1024 * 1024 * 1024,
    this.concurrency = 4,
  }) : cachePath = cachePath ?? _defaultCachePath(),
       importers = importers ?? defaultImporters();

  /// The folder assets are read from. Ids are relative to it, so an asset's
  /// name does not change when the project moves.
  final String assetsPath;

  /// Where the bundle is written. Each target gets its own folder under it,
  /// because a build copies one folder and must not have to filter it.
  final String outPath;

  final CookTarget target;

  /// Where cooked bytes are kept between runs. Outside the project by
  /// default: a cache in a repository is a cache somebody commits.
  final String cachePath;

  final ImporterRegistry importers;

  /// How large the cache may grow before it starts evicting.
  final int? limitBytes;

  final int concurrency;

  /// Every importer that works on this machine.
  ///
  /// The pure-Dart ones first, because glTF and scenes are claimed by
  /// extension and nothing else wants those extensions, and because a project
  /// adding its own importer usually wants it ahead of the native ones.
  static ImporterRegistry defaultImporters() => ImporterRegistry([
    const GltfImporter(),
    const SceneImporter(),
    ...nativeImporters,
  ]);

  /// Where the bundle for this target goes.
  String get bundlePath => beside(outPath, target.name);

  /// Cooks everything under [assetsPath] and writes the bundle.
  ///
  /// Returns the report, having already written it: a caller that only wants
  /// the exit code reads [CookReport.ok], and a caller that wants to print
  /// what happened has it all.
  Future<CookReport> run({void Function(CookResult result)? onResult}) async {
    final source = DirectoryAssetSource(assetsPath);
    final cache = DirectoryCookCache(cachePath, limitBytes: limitBytes);
    final cook = Cook(
      source: source,
      cache: cache,
      importers: importers,
      target: target,
      concurrency: concurrency,
    );

    final report = await cook.cookAll(await assets(), onResult: onResult);
    await write(report, cache);
    return report;
  }

  /// Every asset under [assetsPath], as ids, in a fixed order.
  ///
  /// Sorted, because a cook whose order depends on how the file system chose
  /// to list a directory is a cook whose *log* differs between machines even
  /// when its output does not — and the first thing anyone does with two CI
  /// runs is diff them.
  ///
  /// Settings files and hidden files are left out. A settings file is an input
  /// to a cook, never an asset, and `.DS_Store` has never been anybody's
  /// texture.
  Future<List<AssetId>> assets() async {
    final root = Directory(assetsPath);
    if (!root.existsSync()) {
      throw ArgumentError.value(assetsPath, 'assetsPath', 'is not a directory');
    }
    final prefix = root.absolute.path;
    final found = <AssetId>[];
    await for (final entry in root.list(recursive: true, followLinks: false)) {
      if (entry is! File) continue;
      final relative = entry.absolute.path
          .substring(prefix.length)
          .split(Platform.pathSeparator)
          .where((part) => part.isNotEmpty)
          .join('/');
      if (relative.split('/').any((part) => part.startsWith('.'))) continue;
      final id = AssetId.tryParse(relative);
      if (id == null || ImportSettings.isSettingsFile(id)) continue;
      found.add(id);
    }
    found.sort((a, b) => a.toString().compareTo(b.toString()));
    return found;
  }

  /// Writes the bundle: every output beside the asset that produced it, and a
  /// manifest naming what each one now is.
  ///
  /// An output is written as `<asset id>.<output name>` — `wall.png.bc.ktx2`,
  /// `chair.fbx.glb`. Keeping the source's own name and extension in the
  /// middle looks redundant and is not: it is what lets somebody looking at a
  /// bundle answer "where did this come from", and what stops two assets whose
  /// names differ only by extension from writing over each other.
  ///
  /// The bundle is emptied first. A cook that left yesterday's files behind
  /// would ship an asset that no longer exists, and the second time anyone
  /// noticed would be in a crash report.
  Future<AssetManifest> write(CookReport report, CookCache cache) async {
    final bundle = Directory(bundlePath);
    if (bundle.existsSync()) await bundle.delete(recursive: true);
    await bundle.create(recursive: true);

    final entries = <AssetId, AssetEntry>{};
    for (final result in report.results) {
      final asset = result.asset;
      if (asset == null) continue;
      for (final output in asset.outputs) {
        final bytes = await cache.read(output.hash);
        if (bytes == null) {
          throw StateError(
            'The cache reported ${result.id} as cooked but no longer has the '
            'bytes for ${output.name}. Something removed them underneath this '
            'cook; running it again will cook them afresh.',
          );
        }
        final name = '${result.id}.${output.name}';
        final file = File(
          beside(bundlePath, name.replaceAll('/', Platform.pathSeparator)),
        );
        await file.parent.create(recursive: true);
        await file.writeAsBytes(bytes, flush: true);
        entries[AssetId.parse(name)] = AssetEntry(output.hash, bytes.length);
      }
    }

    final manifest = AssetManifest(entries: entries);
    await File(
      beside(bundlePath, 'manifest.json'),
    ).writeAsString(manifest.encode(), flush: true);
    return manifest;
  }

  /// A cache outside the project, shared by every project on the machine.
  ///
  /// Shared because the point of content addressing is that two projects using
  /// the same texture cook it once, and because a developer switching branches
  /// should not pay for it twice.
  static String _defaultCachePath() {
    final environment = Platform.environment;
    final explicit = environment['ORBLIT_CACHE'];
    if (explicit != null && explicit.isNotEmpty) return explicit;

    final home = environment['HOME'] ?? environment['USERPROFILE'];
    if (home == null || home.isEmpty) {
      return beside(Directory.systemTemp.path, 'orblit-cook-cache');
    }
    return beside(beside(home, '.cache'), 'orblit-cook');
  }
}

/// A hash of a whole bundle, for asking "did this build change anything".
///
/// A manifest is already the answer to "what is in here", and hashing its
/// bytes gives one number a build can compare between runs without reading
/// any assets. Two clean cooks of an unchanged project produce the same one,
/// which is the claim CI checks.
ContentHash bundleHash(AssetManifest manifest) =>
    ContentHash.of(utf8.encode(manifest.encode()));

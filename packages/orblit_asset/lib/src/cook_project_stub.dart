// The web half of the conditional export in cook_project.dart.
//
// Cooking a project means walking a directory and starting programs, and a
// browser does neither. The class is here so that code naming it compiles
// everywhere, and says what to do instead the moment anyone builds one.
import 'dart:convert';

import 'asset_manifest.dart';
import 'content_hash.dart';
import 'cook.dart';
import 'cook_cache.dart';
import 'importer.dart';

/// Cooking a whole project, which needs a file system and is not available on
/// this platform.
class CookProject {
  CookProject({
    required this.assetsPath,
    required this.outPath,
    required this.target,
    String? cachePath,
    ImporterRegistry? importers,
    this.limitBytes,
    this.concurrency = 4,
  }) : cachePath = cachePath ?? '',
       importers = importers ?? ImporterRegistry() {
    throw UnsupportedError(
      'Cooking a project needs a file system and the ability to start a '
      'program, and a browser has neither. Cook on a build machine — '
      '`dart run orblit_asset:cook --target web` — and load the result.',
    );
  }

  final String assetsPath;
  final String outPath;
  final CookTarget target;
  final String cachePath;
  final ImporterRegistry importers;
  final int? limitBytes;
  final int concurrency;

  String get bundlePath =>
      throw UnsupportedError('Cooking needs a file system.');

  Future<CookReport> run({void Function(CookResult result)? onResult}) =>
      throw UnsupportedError('Cooking needs a file system.');

  Future<List<Object>> assets() =>
      throw UnsupportedError('Cooking needs a file system.');

  Future<AssetManifest> write(CookReport report, CookCache cache) =>
      throw UnsupportedError('Cooking needs a file system.');
}

/// A hash of a whole bundle, for asking "did this build change anything".
ContentHash bundleHash(AssetManifest manifest) =>
    ContentHash.of(utf8.encode(manifest.encode()));

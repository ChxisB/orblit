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
import 'cook_targets.dart';
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

  /// Every importer that works on this machine, which in a browser is the
  /// ones that need nothing but Dart.
  static ImporterRegistry defaultImporters() => ImporterRegistry();

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

/// Thrown when a cook run as part of a build could not cook everything.
class CookFailed implements Exception {
  CookFailed(this.report);

  final CookReport report;

  Iterable<CookResult> get failures => report.withStatus(CookStatus.failed);
}

/// Cooking during a build, which happens on a build machine and not in a
/// browser.
Future<CookReport> cookDuringBuild({
  required String packageRoot,
  required String? targetOs,
  String assets = 'assets',
  String out = 'assets/cooked',
  String? cachePath,
  ImporterRegistry? importers,
  int concurrency = 4,
  void Function(Iterable<Uri> files)? dependencies,
  void Function(String line)? log,
}) => throw UnsupportedError(
  'Assets are cooked by the build that produces a web app, not by the web '
  'app. Run `dart run orblit_asset:cook --target web` and serve the bundle.',
);

/// What the machine this is running on wants assets cooked as, which in a
/// browser is always the web.
CookTarget get currentCookTarget => CookTargets.web;

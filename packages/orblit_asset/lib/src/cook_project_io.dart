import 'dart:convert';
import 'dart:io';

import 'asset_id.dart';
import 'asset_manifest.dart';
import 'content_hash.dart';
import 'cook.dart';
import 'cook_cache.dart';
import 'cook_cache_io.dart';
import 'cook_targets.dart';
import 'directory_io.dart';
import 'import_settings.dart';
import 'importer.dart';
import 'importers/atlas_importer.dart';
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
    const AtlasImporter(),
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

/// Thrown when a cook run as part of a build could not cook everything.
///
/// A build hook that returned quietly after failing to cook a texture would
/// produce an app that builds, installs, launches and then cannot draw one of
/// its own assets. Failing the build is the cheaper of the two.
class CookFailed implements Exception {
  CookFailed(this.report);

  final CookReport report;

  /// Only the results that failed, which is what somebody reading a build log
  /// needs; the rest of the report is there for anyone who wants it.
  Iterable<CookResult> get failures => report.withStatus(CookStatus.failed);

  @override
  String toString() {
    final lines = [
      for (final failure in failures) '  ${failure.id}: ${failure.error}',
    ];
    return 'Cooking assets failed for ${lines.length} of '
        '${report.results.length}:\n${lines.join('\n')}';
  }
}

/// Cooks a project's assets as part of a build, for the platform being built.
///
/// This is the build-hook half of the cook. A game's `hook/build.dart` calls
/// it with what the hook already knows, and gets a bundle cooked for the
/// platform the build is for:
///
/// ```dart
/// import 'package:code_assets/code_assets.dart';
/// import 'package:hooks/hooks.dart';
/// import 'package:orblit_asset/orblit_asset.dart';
///
/// void main(List<String> args) async {
///   await build(args, (input, output) async {
///     await cookDuringBuild(
///       packageRoot: input.packageRoot.toFilePath(),
///       targetOs: input.config.code.targetOS.name,
///       dependencies: output.addDependencies,
///     );
///   });
/// }
/// ```
///
/// The hook types are not named here on purpose, so that depending on
/// `orblit_asset` does not drag `hooks` and `code_assets` into a project that
/// only wants to read assets. Everything crossing the boundary is a string, a
/// `Uri` or a function — which also means the same call works from a plain
/// script, which is how most projects will run it.
///
/// **What this does not do.** It does not hand the build a data asset. Data
/// assets only work on Flutter's master channel, so a bundle cooked here is
/// written into the package and shipped as an ordinary Flutter asset, which
/// means the project's `pubspec.yaml` has to list [out] under `flutter:
/// assets:`. When data assets reach stable this is where they go in, and the
/// `pubspec.yaml` entry is what goes away.
///
/// [targetOs] is the name of the platform being built for, as the hook spells
/// it — `ios`, `macos`, `android`, `windows`, `linux`. A null one means the
/// web, because a web build's hook is not told a target at all; that is the
/// one platform where the absence is the answer.
///
/// [dependencies] is given every file this cook read: the assets themselves,
/// their settings files, and whatever those assets pointed at. A hook that
/// reports them is re-run when one changes and skipped when none did, which is
/// the difference between a cook that costs nothing on an untouched build and
/// one that costs a texture encode every time.
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
}) async {
  final target = CookTargets.find(targetOs ?? 'web');
  if (target == null) {
    throw ArgumentError.value(
      targetOs,
      'targetOs',
      'is not a platform Orblit cooks for. Orblit cooks for '
          '${CookTargets.names.join(', ')}',
    );
  }

  final assetsPath = _under(packageRoot, assets);
  final project = CookProject(
    assetsPath: assetsPath,
    outPath: _under(packageRoot, out),
    target: target,
    cachePath: cachePath,
    importers: importers,
    concurrency: concurrency,
  );

  final report = await project.run();
  log?.call('orblit: cooked ${target.name}: ${report.summary}');

  if (dependencies != null) {
    // Both the assets and their settings files, because changing how a texture
    // is cooked has to rebuild it just as surely as changing the texture. The
    // settings file is reported whether or not it exists: a hook watching a
    // path that does not exist yet is how it notices one being added.
    final watched = <Uri>{};
    for (final result in report.results) {
      watched.add(_fileUnder(assetsPath, '${result.id}'));
      watched.add(_fileUnder(assetsPath, '${result.id}.import.json'));
    }
    for (final id in report.dependencies) {
      watched.add(_fileUnder(assetsPath, '$id'));
    }
    dependencies(watched);
  }

  if (!report.ok) throw CookFailed(report);
  return report;
}

/// A path inside a package, where the inner part is written with `/` whatever
/// the platform is — as a pubspec writes it.
String _under(String root, String relative) => relative
    .split('/')
    .where((part) => part.isNotEmpty)
    .fold(root, (path, part) => beside(path, part));

Uri _fileUnder(String root, String relative) =>
    Uri.file(_under(root, relative));

/// What the machine this is running on wants assets cooked as.
///
/// A runtime import is cooking for the device doing the importing, so unlike
/// an offline cook there is no choice to make — asking the caller to pick
/// would only give them a way to get it wrong.
///
/// A platform Orblit has no target for throws rather than falling back to
/// something plausible. A wrong guess here does not fail; it produces textures
/// the device cannot decode, and that turns up as a blank model rather than as
/// this line.
CookTarget get currentCookTarget {
  final os = Platform.operatingSystem;
  final target = CookTargets.find(os);
  if (target == null) {
    throw UnsupportedError(
      'Orblit does not cook assets for $os. It cooks for '
      '${CookTargets.names.join(', ')}.',
    );
  }
  return target;
}

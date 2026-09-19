import 'dart:async';

import 'asset_id.dart';
import 'asset_source.dart';
import 'content_hash.dart';
import 'cook_cache.dart';
import 'cook_key.dart';
import 'import_settings.dart';
import 'importer.dart';

/// How one asset came out of a cook.
enum CookStatus {
  /// An importer ran and the cache now holds what it wrote.
  cooked,

  /// The cache already held it: nothing ran.
  cached,

  /// No importer claimed the asset, so it was left alone. A README and an
  /// `.import.json` both land here, and neither is a problem.
  skipped,

  /// Something threw. [CookResult.error] says what.
  failed,
}

/// What happened to one asset.
class CookResult {
  const CookResult({
    required this.id,
    required this.status,
    this.importer,
    this.key,
    this.asset,
    this.dependencies = const {},
    this.notes = const [],
    this.error,
    this.trace,
  });

  final AssetId id;
  final CookStatus status;

  /// The importer that claimed it, absent only when nothing did.
  final Importer? importer;

  /// The key its outputs are filed under, absent when it was skipped or when
  /// it failed before the key could be built.
  final CookKey? key;

  /// The outputs, present whenever the status is [CookStatus.cooked] or
  /// [CookStatus.cached].
  final CookedAsset? asset;

  /// The other assets that went into it, which a build system needs in order
  /// to know what to watch.
  final Set<AssetId> dependencies;

  final List<String> notes;
  final Object? error;
  final StackTrace? trace;

  bool get ok => status != CookStatus.failed;

  @override
  String toString() => switch (status) {
    CookStatus.cooked => 'cooked $id with $importer',
    CookStatus.cached => 'cached $id',
    CookStatus.skipped => 'skipped $id',
    CookStatus.failed => 'failed $id: $error',
  };
}

/// What happened to all of them.
class CookReport {
  CookReport(List<CookResult> results) : results = List.unmodifiable(results);

  final List<CookResult> results;

  Iterable<CookResult> withStatus(CookStatus status) =>
      results.where((result) => result.status == status);

  int count(CookStatus status) => withStatus(status).length;

  /// Nothing failed. Note that a report with nothing in it is fine by this
  /// measure, so a caller that expected assets should check for them itself.
  bool get ok => results.every((result) => result.ok);

  /// Every asset that went into every asset cooked, which is what a build
  /// hook watches so that editing a texture rebuilds the model using it.
  Set<AssetId> get dependencies => {
    for (final result in results) ...result.dependencies,
  };

  /// One line, for the end of a cook.
  String get summary {
    final parts = [
      for (final status in CookStatus.values)
        if (count(status) > 0) '${count(status)} ${status.name}',
    ];
    return parts.isEmpty ? 'nothing to cook' : parts.join(', ');
  }

  @override
  String toString() => summary;
}

/// Runs importers, and does not run them when the cache already has the
/// answer.
///
/// The whole point of this class is the order of the steps. Settings are
/// resolved before the key is built, dependencies are found before the key is
/// built, and the key is built before anything expensive happens — because a
/// cache that is consulted after the work is done saves nothing.
class Cook {
  Cook({
    required this.source,
    required this.cache,
    required this.importers,
    required this.target,
    ImportSettingsReader? settings,
    this.concurrency = 4,
  }) : settings = settings ?? ImportSettingsReader(source) {
    if (concurrency < 1) {
      throw ArgumentError.value(
        concurrency,
        'concurrency',
        'must be at least 1',
      );
    }
  }

  /// Where source bytes come from.
  final AssetSource source;

  /// Where cooked bytes go, and where they are looked for first.
  final CookCache cache;

  final ImporterRegistry importers;

  /// The machine being cooked for. One [Cook] cooks for one target; cooking
  /// for several means several of these, which is what the cook command does.
  final CookTarget target;

  /// Reads `.import.json`, with its own per-run cache so a folder of a hundred
  /// sprites reads the folder's settings once.
  final ImportSettingsReader settings;

  /// How many importers may run at once. Importers are mostly native tools
  /// that use every core they are given, so the useful number here is small —
  /// it is about keeping several *processes* fed, not about parallelism
  /// inside one.
  final int concurrency;

  /// Hashes of source files, kept for the length of one [Cook] so that a
  /// texture depended on by twenty models is read and hashed once.
  ///
  /// This is why a [Cook] is a short-lived object: it assumes nothing on disk
  /// changes underneath it. A long-running editor makes a new one per cook
  /// rather than holding one open.
  final Map<AssetId, Future<ContentHash>> _hashes = {};

  /// Cooks one asset, and answers with what happened rather than throwing —
  /// one unreadable texture should not end a build of a thousand assets.
  Future<CookResult> cookOne(AssetId id) async {
    Importer? importer;
    try {
      final settings = await this.settings.forAsset(id);
      importer = importers.forAsset(id, settings);
      if (importer == null) {
        return CookResult(id: id, status: CookStatus.skipped);
      }

      final resolved = importer.resolveSettings(settings);
      final bytes = await source.read(id);
      final dependencies = await importer.dependenciesOf(id, bytes, resolved);

      final key = CookKey(
        importer: importer.name,
        importerVersion: importer.version,
        source: ContentHash.of(bytes),
        dependencies: {
          for (final dependency in dependencies)
            dependency: await _hashOf(dependency),
        },
        settings: resolved,
        target: target.recipe,
      );

      final hit = await cache.lookUp(key);
      if (hit != null) {
        return CookResult(
          id: id,
          status: CookStatus.cached,
          importer: importer,
          key: key,
          asset: hit,
          dependencies: dependencies,
        );
      }

      final result = await importer.import(
        ImportRequest(
          id: id,
          bytes: bytes,
          settings: resolved,
          target: target,
          source: source,
        ),
      );
      final stored = await cache.store(key, result.outputs);
      return CookResult(
        id: id,
        status: CookStatus.cooked,
        importer: importer,
        key: key,
        asset: stored,
        dependencies: dependencies,
        notes: result.notes,
      );
    } catch (error, trace) {
      return CookResult(
        id: id,
        status: CookStatus.failed,
        importer: importer,
        error: error,
        trace: trace,
      );
    }
  }

  /// Cooks all of them, [concurrency] at a time, reporting each as it lands.
  ///
  /// [onResult] is called in completion order, not in [ids] order, so a
  /// caller printing progress gets it as it happens. The returned report is
  /// in [ids] order, because that is the order a person reads a summary in.
  Future<CookReport> cookAll(
    Iterable<AssetId> ids, {
    void Function(CookResult result)? onResult,
  }) async {
    final pending = ids.toList();
    final results = List<CookResult?>.filled(pending.length, null);
    var next = 0;

    Future<void> worker() async {
      while (true) {
        final index = next++;
        if (index >= pending.length) return;
        final result = await cookOne(pending[index]);
        results[index] = result;
        onResult?.call(result);
      }
    }

    await Future.wait([
      for (var i = 0; i < concurrency && i < pending.length; i++) worker(),
    ]);
    return CookReport(results.cast<CookResult>());
  }

  Future<ContentHash> _hashOf(AssetId id) =>
      _hashes[id] ??= source.read(id).then(ContentHash.of);
}

/// A [CookTarget] property every texture importer reads: which compressed
/// formats the device can sample.
///
/// These live here rather than in the renderer so that a cook running on a
/// build machine, with no renderer in the process, can still write the names
/// the renderer will look for.
abstract final class TextureFamily {
  static const String astc = 'astc';
  static const String bc = 'bc';
  static const String etc2 = 'etc2';

  /// Universal Basic, transcoded on load to whatever the device has. Bigger
  /// to decode but one file for every device, which is what a web build wants
  /// when it cannot know what it is running on.
  static const String basis = 'basis';

  /// Uncompressed RGBA. Always readable, never small.
  static const String raw = 'raw';

  static const List<String> all = [astc, bc, etc2, basis, raw];
}

/// Reads [CookTarget.properties] with the names the built-in importers agree
/// on, so that a missing or mistyped property is caught once, here, rather
/// than differently in each importer.
extension CookTargetProperties on CookTarget {
  /// Which compressed texture families to write. Empty means the caller did
  /// not say, and an importer should write all of them.
  List<String> get textureFamilies {
    final value = properties['textureFamilies'];
    if (value == null) return const [];
    if (value is! List) {
      throw ArgumentError.value(
        value,
        'textureFamilies',
        'must be a list of family names, one of ${TextureFamily.all}',
      );
    }
    return [for (final family in value) family as String];
  }

  /// The largest texture the device will accept, or null for no limit.
  int? get maxTextureSize {
    final value = properties['maxTextureSize'];
    if (value == null) return null;
    if (value is! int || value <= 0) {
      throw ArgumentError.value(
        value,
        'maxTextureSize',
        'must be a positive whole number of pixels',
      );
    }
    return value;
  }

  /// Whether the device samples half-float textures, which decides whether an
  /// environment map can stay in floating point or has to be encoded.
  bool get halfFloatTextures => properties['halfFloatTextures'] == true;
}

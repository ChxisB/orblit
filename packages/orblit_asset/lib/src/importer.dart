import 'dart:typed_data';

import 'asset_id.dart';
import 'asset_source.dart';
import 'import_settings.dart';

/// What a cook is being cooked *for*.
///
/// The bytes an importer writes depend on the machine that will read them: a
/// phone wants ASTC, a desktop wants BC, and a browser at WebGL 2 wants
/// neither above a certain size. That is a fact about the target, not about
/// the asset, so it travels beside the asset rather than inside it.
///
/// This is deliberately plain data. The renderer knows all of it — it is most
/// of `OrblitDeviceProfile` — but `orblit_asset` must not depend on the
/// renderer, or the cook could never run anywhere the renderer does not build.
/// A caller that has a profile turns it into one of these; a caller that is
/// cooking for a platform it is not running on writes one out by hand.
class CookTarget {
  const CookTarget({required this.name, this.properties = const {}});

  /// A target with no properties at all, which every importer must still
  /// produce *something* for. Useful in tests and for assets whose output
  /// cannot vary by device, like a scene's dependency list.
  static const CookTarget any = CookTarget(name: 'any');

  /// What to call this target in logs and in a cooked bundle's folder name:
  /// `macos`, `android`, `web`. Part of the key, so two targets that differ
  /// only in name still get separate entries — which is what you want, since
  /// the name is how a bundle is found again.
  final String name;

  /// Everything else the importers read, by their own agreed names:
  /// `textureFamilies`, `maxTextureSize`, `halfFloatTextures`, `tier`. An
  /// importer takes what it understands and ignores the rest.
  ///
  /// Only put things here that *change the bytes*. The number of worker
  /// threads a device has changes how fast the cook runs, never what it
  /// writes, so putting it here would split the cache in half for nothing.
  final Map<String, Object?> properties;

  /// What goes into a [CookKey]'s `target`.
  Map<String, Object?> get recipe => {'name': name, ...properties};

  @override
  String toString() => 'CookTarget($name)';
}

/// The one asset an importer has been asked to turn into bytes, and everything
/// it is allowed to look at while doing so.
class ImportRequest {
  const ImportRequest({
    required this.id,
    required this.bytes,
    required this.settings,
    required this.target,
    required this.source,
  });

  /// The asset being cooked.
  final AssetId id;

  /// Its bytes, already read. The driver has read them anyway to hash them,
  /// so reading them twice would be waste.
  final Uint8List bytes;

  /// The settings for this asset, already resolved: every default filled in
  /// and every value read as the type the importer declared. An importer can
  /// index this without checking, because [Importer.resolveSettings] put the
  /// keys there.
  final Map<String, Object?> settings;

  /// What machine the bytes are for.
  final CookTarget target;

  /// Where to read anything else from — a glTF's buffers and images, a
  /// scene's models. Only the ids [Importer.dependenciesOf] returned may be
  /// read: anything else is a file the key does not cover, so a change to it
  /// would not recook, and the cache would serve stale bytes forever.
  final AssetSource source;
}

/// What came out.
class ImportResult {
  ImportResult({required this.outputs, this.notes = const []});

  /// The files produced, by the name each is filed under inside the cooked
  /// entry. An importer that produces one file conventionally calls it after
  /// the format — `ktx2`, `glb`, `osplat` — rather than repeating the asset's
  /// own name, which the entry already knows.
  final Map<String, List<int>> outputs;

  /// Anything a person should see: a texture that was not a power of two, a
  /// material the atlas could not merge. Not errors — an importer that cannot
  /// proceed throws [ImportFailure] — but the things that explain a surprising
  /// result later.
  final List<String> notes;
}

/// An importer could not do its job.
///
/// Thrown rather than returned, because there is no half-cooked asset: either
/// the outputs are there or the cook failed and the caller has to decide
/// whether one broken asset stops the build.
class ImportFailure implements Exception {
  const ImportFailure(this.id, this.reason, {this.cause});

  final AssetId id;
  final String reason;

  /// Whatever the underlying tool said, when there was one.
  final Object? cause;

  @override
  String toString() =>
      'Could not import $id: $reason${cause == null ? '' : '\n$cause'}';
}

/// Turns one kind of source file into the bytes a device loads.
///
/// An importer is a pure function of its inputs as far as the cache is
/// concerned: the same source bytes, dependencies, settings and target must
/// give the same outputs, or a cache hit is a lie. That is why [version]
/// exists — when the function changes, the version changes with it and every
/// old entry stops matching, without anyone having to find and delete them.
abstract class Importer {
  const Importer();

  /// A stable name, used in the key and in `.import.json`'s `importer` field.
  /// Renaming one invalidates its entries, so do it on purpose.
  String get name;

  /// Bumped whenever the output bytes could change for an unchanged input:
  /// a fixed bug, a new encoder, a different default. Forgetting this is the
  /// one mistake the cache cannot protect you from, since a stale hit looks
  /// exactly like a correct one.
  int get version;

  /// The lower-case extensions, without dots, this importer claims.
  Set<String> get extensions;

  /// Whether this importer handles [id] when nothing named an importer
  /// explicitly. The default answers from [extensions], which is right for
  /// almost everything; an importer that has to look at the bytes — a `.bin`
  /// that might be several things — overrides it.
  bool handles(AssetId id) => extensions.contains(id.extension);

  /// Fills in defaults and reads each value as the type this importer wants,
  /// so that `1` and `1.0` and `"1"` become one settings map and therefore one
  /// key rather than three.
  ///
  /// This is also where a bad setting is caught, while the file that set it
  /// can still be named: throw a [FormatException] and the driver reports it
  /// against the asset. Returning a map with a key this importer does not
  /// understand is allowed but pointless — it splits the cache without
  /// changing any bytes.
  Map<String, Object?> resolveSettings(ImportSettings settings);

  /// Every other asset whose contents change these outputs.
  ///
  /// This runs *before* the key is built, so it must be cheap: parse the glTF's
  /// JSON for its URIs, read the scene's model list. Do not cook anything here.
  ///
  /// It must also be complete. A dependency left out is not in the key, so
  /// editing it will not recook, and the stale result will survive every
  /// rebuild until someone clears the cache by hand. When in doubt, include
  /// it: a dependency too many costs one recook, a dependency too few costs
  /// an afternoon.
  Future<Set<AssetId>> dependenciesOf(
    AssetId id,
    Uint8List bytes,
    Map<String, Object?> settings,
  ) async => const {};

  /// Does the work.
  Future<ImportResult> import(ImportRequest request);

  @override
  String toString() => '$name v$version';
}

/// The importers a cook may choose from.
///
/// Registration order decides ties: the first importer that [Importer.handles]
/// an asset wins, so a project that adds its own importer for `.png` ahead of
/// the built-in one gets its own.
class ImporterRegistry {
  ImporterRegistry([Iterable<Importer> importers = const []]) {
    importers.forEach(add);
  }

  final List<Importer> _importers = [];
  final Map<String, Importer> _byName = {};

  /// The importers, in the order they will be tried.
  Iterable<Importer> get importers => List.unmodifiable(_importers);

  void add(Importer importer) {
    final existing = _byName[importer.name];
    if (existing != null) {
      throw ArgumentError.value(
        importer.name,
        'importer',
        'is already registered, as $existing. Two importers with one name '
            'would share cache entries while writing different bytes.',
      );
    }
    _byName[importer.name] = importer;
    _importers.add(importer);
  }

  /// The importer named by [name], or null.
  Importer? byName(String name) => _byName[name];

  /// Which importer cooks [id], honouring an `importer` named in [settings]
  /// over what the extension says.
  ///
  /// Returns null when nothing claims the asset, which is not an error: a
  /// project holds README files and `.import.json`s too, and a cook that
  /// refused to run because of them would be useless.
  Importer? forAsset(
    AssetId id, [
    ImportSettings settings = const ImportSettings(),
  ]) {
    final named = settings.importer;
    if (named != null) {
      final importer = _byName[named];
      if (importer == null) {
        throw ArgumentError.value(
          named,
          'importer',
          'is named by the settings for $id, but no such importer is '
              'registered. Known importers: '
              '${(_byName.keys.toList()..sort()).join(', ')}.',
        );
      }
      return importer;
    }
    for (final importer in _importers) {
      if (importer.handles(id)) return importer;
    }
    return null;
  }
}

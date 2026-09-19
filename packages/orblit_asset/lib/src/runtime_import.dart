import 'dart:typed_data';

import 'asset_id.dart';
import 'asset_source.dart';
import 'cook.dart';
import 'cook_cache.dart';
import 'import_settings.dart';
import 'importer.dart';
import 'importers/gltf_importer.dart';
import 'importers/native_importers.dart';

/// Importing a file somebody hands the app while it is running.
///
/// A user drops a model onto a level, opens one from their photo library, or
/// downloads a skin somebody else made. The engine has to end up with the same
/// thing it would have had if that file had been in the project all along, and
/// the way to get that is to run the same importers rather than a second,
/// looser path that drifts from the first.
///
/// So this is [Cook] again, with the parts a project supplies replaced by the
/// parts a running app has: the device's cache instead of a build machine's,
/// this device's target instead of a chosen one, and one file somebody handed
/// over instead of a folder full of them.
///
/// **The cache is the point.** Importing a model costs real time, and a user
/// who adds one to their world expects it to be there next launch without
/// paying again. Because the key covers the file's own bytes, re-importing the
/// same file is free, and importing a file the project already shipped is free
/// too — it is the same key the build machine used.
///
/// **Not every importer runs here.** The ones that drive a command-line tool
/// need that tool, which a build machine has and a phone does not. They are
/// still registered, because a desktop app is a running app too and has them;
/// what a phone gets is an [ImportFailure] naming the tool it could not find,
/// rather than a file that silently imported to nothing.
class RuntimeImport {
  RuntimeImport({
    required this.cache,
    required this.target,
    ImporterRegistry? importers,
  }) : importers = importers ?? defaultImporters();

  /// Where results are kept. On a device this is the app's cache directory —
  /// which is to say, somewhere the system may empty when it needs the space,
  /// which is correct: everything in it can be made again from the file.
  final CookCache cache;

  /// What this device wants. A runtime import is cooking for the machine it is
  /// running on, so there is exactly one right answer and the caller should
  /// take it from `currentCookTarget` rather than picking.
  final CookTarget target;

  final ImporterRegistry importers;

  /// Every importer that could run in an app.
  ///
  /// The same list the cook command uses. A runtime import that quietly had
  /// fewer importers than the build machine would mean a file that works when
  /// shipped and fails when dropped in, which is the confusing way round.
  static ImporterRegistry defaultImporters() => ImporterRegistry([
    const GltfImporter(),
    const SceneImporter(),
    ...nativeImporters,
  ]);

  /// Whether anything here claims a file by this name.
  ///
  /// Worth asking before offering a user a file picker full of things that
  /// will not open. [ImportSettings] can name an importer explicitly, so the
  /// settings are part of the question.
  bool handles(
    AssetId id, [
    ImportSettings settings = const ImportSettings(),
  ]) => importers.forAsset(id, settings) != null;

  /// What to call a file a user chose, given wherever it came from.
  ///
  /// A file picker hands back a path — `/var/mobile/.../My Model.glb`,
  /// `C:\Users\chris\Downloads\robot.glb`, or a URL. None of those is an asset
  /// id: an id is a name inside a project, and the whole point of it is that
  /// it does not carry a machine's directory layout around. The last segment
  /// is what survives, because it is the part the user would recognise and the
  /// only part that says what kind of file this is.
  ///
  /// Null when nothing usable is left — a path ending in a slash, a name that
  /// is all separators. The caller then has a file it cannot name, and asking
  /// the user is better than inventing something.
  static AssetId? nameFor(String path) {
    final cleaned = path.split('?').first.split('#').first;
    final segments = cleaned.split(RegExp(r'[/\\]'));
    for (final segment in segments.reversed) {
      final name = segment.trim();
      if (name.isEmpty || name == '.' || name == '..') continue;
      return AssetId.tryParse(name.replaceAll(':', '_'));
    }
    return null;
  }

  /// Imports one file, or gives back what a previous import of it produced.
  ///
  /// [alongside] is for a file that is not self-contained. A `.gltf` names its
  /// buffers and images in separate files, so a user who picked only the
  /// `.gltf` has handed over a fragment; pass the folder they picked, or the
  /// contents of the archive they gave, and those references resolve. A `.glb`
  /// needs none of this, which is why it is the format to ask users for.
  ///
  /// Never throws for a file that cannot be imported. The result says what
  /// happened, because "the user picked something we cannot read" is an
  /// ordinary thing for a user to do and not an error in the program.
  Future<CookResult> import(
    AssetId id,
    List<int> bytes, {
    ImportSettings settings = const ImportSettings(),
    AssetSource? alongside,
  }) {
    final supplied = MemoryAssetSource({id: bytes});
    final source = alongside == null
        ? supplied
        : LayeredAssetSource([supplied, alongside]);
    final cook = Cook(
      source: source,
      cache: cache,
      importers: importers,
      target: target,
      settings: settings == const ImportSettings()
          ? ImportSettingsReader(source)
          : _Fixed(source, settings),
      // One at a time: a runtime import is one file, and a phone that spent
      // four cores on it would drop the frames the user is looking at.
      concurrency: 1,
    );
    return cook.cookOne(id);
  }

  /// The bytes of one of an import's outputs, by the name the importer gave
  /// it — `glb`, `bc.ktx2`, `osplat`.
  ///
  /// Null when the import produced no such output, or when the cache has since
  /// dropped it. A caller that gets null from a result it just imported has
  /// been evicted mid-use and should import again.
  Future<Uint8List?> read(CookedAsset asset, String output) async {
    final entry = asset[output];
    return entry == null ? null : cache.read(entry.hash);
  }

  /// The one output of an import that produced exactly one, which is the usual
  /// case for a model or a splat cloud.
  ///
  /// Null when there were none or several: a caller that does not know how
  /// many outputs to expect should not be guessing which of them it wanted.
  Future<Uint8List?> readOnly(CookedAsset asset) async {
    if (asset.outputs.length != 1) return null;
    return cache.read(asset.outputs.single.hash);
  }
}

/// Settings given at the call, falling back to whatever the source has.
///
/// A user importing a file usually says what they want right there — "this is
/// a normal map" — rather than by writing a settings file next to it. This
/// lets them, without losing the settings that a file shipped with.
class _Fixed extends ImportSettingsReader {
  _Fixed(super.source, this._fixed);

  final ImportSettings _fixed;

  @override
  Future<ImportSettings> forAsset(AssetId id) async =>
      (await super.forAsset(id)).mergedWith(_fixed);
}

import 'dart:convert';

import 'asset_id.dart';
import 'asset_source.dart';

/// The suffix that makes a file a settings file rather than an asset.
const String _suffix = '.import.json';

/// How one asset should be cooked, as read from the project.
///
/// A file does not say what it is. A PNG can be a colour map, a normal map or
/// a sprite sheet, and the three are cooked into three different formats;
/// Phase 4's shell script had to guess from the file's name or be told by a
/// glTF. Settings are where the project says outright, once, in a file that
/// sits next to the asset and goes into the repository with it.
class ImportSettings {
  const ImportSettings({this.importer, this.values = const {}});

  /// The importer the project asked for by name, or null to let the file's
  /// kind decide.
  ///
  /// Worth having even though the extension usually answers it: a `.png` that
  /// is really a lookup table wants the raw importer, not the texture one, and
  /// renaming the file to say so is not something an artist should have to do.
  final String? importer;

  /// What that importer was asked for, by its own names.
  ///
  /// Deliberately untyped here. Each importer knows its own settings and
  /// reads them itself, so adding one does not mean editing this class, and
  /// this class cannot reject a setting a newer importer understands.
  final Map<String, Object?> values;

  /// These settings with [other] laid over them, key by key.
  ///
  /// Shallow on purpose: a key in [other] replaces the same key here outright
  /// rather than being merged into it. Deep merging reads well in a
  /// description and badly in practice — there is no way to *remove* a nested
  /// value once a folder has set it, and working out where a value came from
  /// stops being possible by reading two files.
  ImportSettings mergedWith(ImportSettings other) => ImportSettings(
    importer: other.importer ?? importer,
    values: {...values, ...other.values},
  );

  /// The id of the settings file for [id]: `textures/wall.png.import.json`.
  ///
  /// The suffix is added to the whole name rather than replacing the
  /// extension, so `wall.png` and `wall.jpg` in one folder keep settings of
  /// their own instead of fighting over `wall.import.json`.
  static AssetId fileFor(AssetId id) => AssetId.parse('$id$_suffix');

  /// The id of the settings file that applies to everything in [directory],
  /// which is `.import.json` at the root of the project.
  static AssetId folderFileFor(String directory) =>
      AssetId.parse(directory.isEmpty ? _suffix : '$directory/$_suffix');

  /// Whether [id] is a settings file rather than something to cook.
  ///
  /// A cook that walks a directory would otherwise try to import its own
  /// settings files, and — since they are JSON — succeed at something useless.
  static bool isSettingsFile(AssetId id) => id.name.endsWith(_suffix);

  /// The asset a per-asset settings file belongs to, or null when [id] is not
  /// one.
  static AssetId? assetFor(AssetId id) {
    final text = id.toString();
    if (!text.endsWith(_suffix) || text.length == _suffix.length) return null;
    final without = text.substring(0, text.length - _suffix.length);
    return without.endsWith('/') ? null : AssetId.tryParse(without);
  }

  /// Reads what a settings file says, throwing a [FormatException] naming the
  /// file when it does not say anything readable.
  ///
  /// A settings file is written by hand, so the errors are written for
  /// whoever is holding the keyboard: which file, and what about it is wrong.
  static ImportSettings parse(String text, {required AssetId from}) {
    final Object? parsed;
    try {
      parsed = jsonDecode(text);
    } on FormatException catch (error) {
      throw FormatException(
        '"$from" is not readable as import settings, because it is not '
        'JSON: ${error.message}',
      );
    }
    if (parsed is! Map<String, Object?>) {
      throw FormatException(
        '"$from" has to be a JSON object — {"importer": …, "settings": …} — '
        'and it is not one.',
      );
    }

    final version = parsed['formatVersion'];
    if (version is! int?) {
      throw FormatException(
        '"$from" says its formatVersion is $version, and a format version is '
        'a whole number.',
      );
    }
    if ((version ?? formatVersion) > formatVersion) {
      throw FormatException(
        '"$from" is format version $version, and this build reads up to '
        '$formatVersion. Cooking it with an older reader would silently drop '
        'whatever the newer version added.',
      );
    }

    final importer = parsed['importer'];
    if (importer is! String?) {
      throw FormatException(
        '"$from" says its importer is $importer, and an importer is named '
        'with a string.',
      );
    }
    if (importer != null && importer.isEmpty) {
      throw FormatException(
        '"$from" names its importer as an empty string. Leave the field out '
        'to let the file kind decide.',
      );
    }

    final settings = parsed['settings'];
    if (settings is! Map<String, Object?>?) {
      throw FormatException(
        '"$from" has a "settings" that is not a JSON object, so there is no '
        'way to tell which setting is which.',
      );
    }

    return ImportSettings(
      importer: importer,
      values: settings == null ? const {} : Map.unmodifiable(settings),
    );
  }

  /// Bumped when the shape changes in a way an older reader could misread.
  static const int formatVersion = 1;

  /// The settings as the JSON a project keeps, with a trailing newline.
  String encode() {
    const encoder = JsonEncoder.withIndent('  ');
    final keys = values.keys.toList()..sort();
    return '${encoder.convert({
      'formatVersion': formatVersion,
      if (importer != null) 'importer': importer,
      'settings': {for (final key in keys) key: values[key]},
    })}\n';
  }

  @override
  String toString() => 'ImportSettings(importer: $importer, values: $values)';
}

/// Works out which settings apply to an asset, from the files around it.
///
/// Four files can have a say about `textures/wood/wall.png`, and they are read
/// in this order, each laid over the one before:
///
///   `.import.json`                       the project
///   `textures/.import.json`              everything in textures
///   `textures/wood/.import.json`         everything in that folder
///   `textures/wood/wall.png.import.json` that file
///
/// Folders carry settings because that is how the work actually arrives: a
/// folder of sprites is all pixel art, a folder of normal maps is all normal
/// maps. Without it every one of two hundred files needs a settings file of
/// its own, which nobody keeps up to date, and the Phase 4 script's guess from
/// the file name stays the real answer.
class ImportSettingsReader {
  ImportSettingsReader(this.source);

  final AssetSource source;

  /// Settings files already read, so cooking a folder reads each one once
  /// rather than once per asset in it.
  ///
  /// A cook is a single pass over a project that does not change while it
  /// runs, so nothing here expires. A tool that watches for edits makes a new
  /// reader instead — which is cheap, and much easier to be sure of than
  /// working out which of these to drop when a file changes.
  final Map<AssetId, ImportSettings?> _read = {};

  /// What applies to [id], with each file above it laid over the last.
  Future<ImportSettings> forAsset(AssetId id) async {
    var settled = const ImportSettings();
    for (final file in filesFor(id)) {
      final found = await _fileAt(file);
      if (found != null) settled = settled.mergedWith(found);
    }
    return settled;
  }

  /// Every settings file that can have a say about [id], outermost first.
  ///
  /// Exposed because a cook needs it for a second reason: these are the files
  /// that, when edited, mean [id] has to be looked at again.
  static List<AssetId> filesFor(AssetId id) {
    final files = <AssetId>[AssetId.parse(_suffix)];
    final directory = id.directory;
    if (directory.isNotEmpty) {
      final walked = <String>[];
      for (final segment in directory.split('/')) {
        walked.add(segment);
        files.add(AssetId.parse('${walked.join('/')}/$_suffix'));
      }
    }
    return files..add(ImportSettings.fileFor(id));
  }

  Future<ImportSettings?> _fileAt(AssetId file) async {
    if (_read.containsKey(file)) return _read[file];
    ImportSettings? settings;
    try {
      settings = ImportSettings.parse(
        utf8.decode(await source.read(file)),
        from: file,
      );
    } on AssetNotFound {
      settings = null;
    }
    return _read[file] = settings;
  }
}

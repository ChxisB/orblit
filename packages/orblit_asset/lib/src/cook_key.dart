import 'dart:convert';

import 'asset_id.dart';
import 'content_hash.dart';

/// Everything that decided what a cooked asset is, written down.
///
/// A cache that skips work has to answer one question and get it right every
/// time: would running this again produce the same bytes? A key that leaves
/// something out answers yes when the truth is no, and the project then builds
/// against a stale texture no clean build will reproduce — the worst kind of
/// bug, because the machine that finds it is never the machine that has it.
/// So the key is the whole recipe: the file, the files it reads, the importer
/// and its version, the settings, and the device the result is for.
///
/// The recipe is kept as readable JSON rather than only hashed, because a
/// cache miss nobody expected is otherwise unexplainable. Two recipes can be
/// printed side by side, and the line that differs is the answer.
///
/// [importerVersion] is the part that gets forgotten. Bytes, settings and
/// target can all be unchanged while the code that reads them is fixed, and
/// every entry that importer wrote is then wrong. Raising the version is how a
/// fix reaches caches that already exist — there is no other signal, because
/// nothing on disk changed.
class CookKey {
  CookKey({
    required this.importer,
    required this.importerVersion,
    required this.source,
    Map<AssetId, ContentHash> dependencies = const {},
    Map<String, Object?> settings = const {},
    Map<String, Object?> target = const {},
  }) : dependencies = Map.unmodifiable(dependencies),
       settings = Map.unmodifiable(settings),
       target = Map.unmodifiable(target) {
    if (importer.isEmpty) {
      throw ArgumentError.value(
        importer,
        'importer',
        'A cook key has to say which importer made it.',
      );
    }
    if (importerVersion < 1) {
      throw ArgumentError.value(
        importerVersion,
        'importerVersion',
        'An importer version starts at 1 and only goes up.',
      );
    }
  }

  /// Bumped when the shape of a recipe changes.
  ///
  /// Every key changes with it, so every entry an older build wrote is missed
  /// rather than matched. That is the point: a recipe that has grown a field
  /// the old one did not have cannot be compared with one that lacks it, and
  /// treating the two as equal is exactly the stale hit this class exists to
  /// prevent.
  static const int keyFormatVersion = 1;

  /// Which importer ran: `gltf`, `texture`, `splat`.
  final String importer;

  /// Which version of that importer ran.
  final int importerVersion;

  /// What the source file is, byte for byte.
  final ContentHash source;

  /// The other files the importer read, by the id each was read as.
  ///
  /// A glTF's buffers and images, a scene's models. Named as well as hashed,
  /// because moving a texture from one id to another changes what the result
  /// refers to even when every byte in the project is the same.
  final Map<AssetId, ContentHash> dependencies;

  /// The settings the importer ran with, already resolved.
  ///
  /// Resolved, not the raw contents of an `.import.json`: what belongs in the
  /// key is what the importer actually used, with its defaults filled in and
  /// its values read as the types it reads them as. Hashing the file instead
  /// would make `1` and `1.0` two keys for one cook and — worse — would miss a
  /// default that changed underneath a settings file that did not.
  final Map<String, Object?> settings;

  /// What the result is for: the target platform, and whatever about the
  /// device changes the output — texture families, a size limit, a tier.
  ///
  /// Plain data rather than a device profile, because this package
  /// deliberately knows nothing about a renderer. Whoever holds a profile
  /// turns it into the few fields that actually change the bytes; putting the
  /// whole profile in would make a cook depend on a worker-thread count.
  final Map<String, Object?> target;

  /// The recipe as canonical JSON: the same text for the same inputs, on
  /// every machine and in every run.
  ///
  /// Object keys are sorted, inside settings and target too, so a map built in
  /// a different order is not a different key. Dart's map order is insertion
  /// order, which is a perfectly good way to recook a whole project after
  /// reordering two lines of code.
  late final String recipe = _encode({
    'keyFormatVersion': keyFormatVersion,
    'importer': importer,
    'importerVersion': importerVersion,
    'source': source.hex,
    'dependencies': {
      for (final id
          in dependencies.keys.toList()
            ..sort((a, b) => a.toString().compareTo(b.toString())))
        id.toString(): dependencies[id]!.hex,
    },
    'settings': settings,
    'target': target,
  });

  /// The key itself: the SHA-256 of [recipe].
  late final ContentHash hash = ContentHash.of(utf8.encode(recipe));

  @override
  bool operator ==(Object other) => other is CookKey && other.hash == hash;

  @override
  int get hashCode => hash.hashCode;

  /// The recipe, which is what anybody holding a key wants to see.
  @override
  String toString() => recipe;

  /// [value] as JSON with every object's keys in order.
  ///
  /// Anything that is not JSON is refused here, where the field holding it can
  /// still be named, rather than by [jsonEncode] several layers down with
  /// nothing but the value to go on. A setting holding a `Duration` or a
  /// `File` is a mistake worth a sentence.
  static String _encode(Object? value) {
    final buffer = StringBuffer();
    _write(value, buffer, 'the recipe');
    return buffer.toString();
  }

  static void _write(Object? value, StringBuffer out, String where) {
    switch (value) {
      case null:
      case bool _:
      case int _:
      case String _:
        out.write(jsonEncode(value));
      case double _:
        if (value.isNaN || value.isInfinite) {
          throw ArgumentError.value(
            value,
            where,
            'A cook key cannot hold $value, because JSON has no way to write '
            'it and a key that cannot be written cannot be compared.',
          );
        }
        out.write(jsonEncode(value));
      case List<Object?> items:
        out.write('[');
        for (var i = 0; i < items.length; i++) {
          if (i > 0) out.write(',');
          _write(items[i], out, '$where[$i]');
        }
        out.write(']');
      case Map<Object?, Object?> map:
        final keys = <String>[];
        for (final key in map.keys) {
          if (key is! String) {
            throw ArgumentError.value(
              key,
              where,
              "A cook key holds JSON, and a JSON object's keys are strings. "
              'This one is a ${key.runtimeType}.',
            );
          }
          keys.add(key);
        }
        keys.sort();
        out.write('{');
        for (var i = 0; i < keys.length; i++) {
          if (i > 0) out.write(',');
          out
            ..write(jsonEncode(keys[i]))
            ..write(':');
          _write(map[keys[i]], out, '$where.${keys[i]}');
        }
        out.write('}');
      default:
        throw ArgumentError.value(
          value,
          where,
          'A cook key holds only JSON — null, a number, a string, a boolean, '
          'a list or a map. This is a ${value.runtimeType}.',
        );
    }
  }
}

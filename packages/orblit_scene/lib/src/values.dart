import 'package:orblit_light/orblit_light.dart' show Tint;
import 'package:vector_math/vector_math_64.dart';

/// Reading values out of decoded JSON, and writing them back the same way.
///
/// Every field in a scene file passes through here. A file is a thing people
/// hand-edit, merge and generate, so any value in it may be missing, may be
/// the wrong type, or may be a number where a string was meant. None of that
/// should lose the other nine hundred fields that were fine — so these read
/// what they can and fall back rather than throwing, and the places where a
/// bad value genuinely costs something say so by keeping a note instead.
///
/// The writing half exists so that the two stay together: a reader and a
/// writer that live in different files drift, and the day they do is the day
/// a scene stops surviving its own round trip.
abstract final class Values {
  /// A number, or [fallback] when the file has something else there.
  static double number(Map<String, Object?> json, String key, double fallback) {
    final value = json[key];
    return value is num ? value.toDouble() : fallback;
  }

  /// A number that may legitimately be absent, which is not the same as one
  /// that defaults: a spot angle on a thing that is not a spot has no value,
  /// and writing one would invent a fact.
  static double? maybeNumber(Map<String, Object?> json, String key) {
    final value = json[key];
    return value is num ? value.toDouble() : null;
  }

  static bool flag(
    Map<String, Object?> json,
    String key, {
    required bool fallback,
  }) {
    final value = json[key];
    return value is bool ? value : fallback;
  }

  static String? text(Map<String, Object?> json, String key) {
    final value = json[key];
    return value is String ? value : null;
  }

  /// One of an enum's values by its name, or null when the name is not one.
  ///
  /// Null rather than a default, so a caller can tell "this file named
  /// something I do not know" apart from "this file said nothing" — the first
  /// is worth a note, the second is not.
  static T? named<T extends Enum>(List<T> values, Object? name) {
    for (final value in values) {
      if (value.name == name) return value;
    }
    return null;
  }

  /// Three numbers, or [fallback] in all three places.
  ///
  /// A short or ragged list falls back per-component rather than wholesale: a
  /// scale written `[2, 2]` by hand means two of the three, and reading it as
  /// `[1, 1, 1]` would move the object further from what was meant than
  /// reading it as `[2, 2, 1]` does.
  static Vector3 vector(Object? raw, {double fallback = 0}) {
    if (raw is! List) return Vector3.all(fallback);
    double at(int index) {
      if (index >= raw.length) return fallback;
      final value = raw[index];
      return value is num ? value.toDouble() : fallback;
    }

    return Vector3(at(0), at(1), at(2));
  }

  static List<double> vectorToJson(Vector3 value) => [
    value.x,
    value.y,
    value.z,
  ];

  /// A colour written `#RRGGBB`, as everywhere else in Orblit writes one.
  static Tint tint(Object? raw, {Tint fallback = const Tint.hex(0xD9634F)}) {
    if (raw is! String) return fallback;
    final digits = raw.startsWith('#') ? raw.substring(1) : raw;
    if (digits.length != 6) return fallback;
    final value = int.tryParse(digits, radix: 16);
    return value == null ? fallback : Tint.hex(value);
  }

  static String tintToJson(Tint value) {
    String pair(double channel) =>
        (channel * 255).round().clamp(0, 255).toRadixString(16).padLeft(2, '0');
    return '#${pair(value.red)}${pair(value.green)}${pair(value.blue)}'
        .toUpperCase();
  }

  /// A nested object, or an empty one when there is nothing there.
  ///
  /// Empty rather than null so the caller reads its fields the same way
  /// whether the block was written or not, and every one of them falls back.
  static Map<String, Object?> object(Object? raw) =>
      raw is Map<String, Object?> ? raw : const {};

  /// The strings out of a list, skipping anything that is not one.
  static List<String> texts(Object? raw) {
    if (raw is! List) return const [];
    return [
      for (final one in raw)
        if (one is String) one,
    ];
  }

  /// Whether two decoded-JSON values are the same value.
  ///
  /// What a diff is built on. Comparing the JSON rather than the components
  /// means one comparison covers every component there is, including the ones
  /// this version has never heard of — and a component that grows a field
  /// cannot forget to grow its equality with it, which is the bug that makes
  /// an edit quietly un-undoable.
  ///
  /// Numbers compare across `int` and `double` on purpose: a file may say `1`
  /// where we write `1.0`, and treating a scene as changed because of that
  /// would mark every old file dirty the moment it was opened.
  static bool same(Object? a, Object? b) {
    if (a is num && b is num) return a == b;
    if (a is Map<String, Object?> && b is Map<String, Object?>) {
      if (a.length != b.length) return false;
      for (final entry in a.entries) {
        if (!b.containsKey(entry.key)) return false;
        if (!same(entry.value, b[entry.key])) return false;
      }
      return true;
    }
    if (a is List && b is List) {
      if (a.length != b.length) return false;
      for (var i = 0; i < a.length; i++) {
        if (!same(a[i], b[i])) return false;
      }
      return true;
    }
    return a == b;
  }

  /// Drops the keys whose value is null, so an optional field that is not set
  /// leaves nothing behind in the file.
  ///
  /// Written as a pass over the finished map rather than as a `?` on each
  /// line, because the components have a lot of optional fields and the
  /// conditional-entry form makes the shape of a component hard to read.
  static Map<String, Object?> pruned(Map<String, Object?> json) => {
    for (final entry in json.entries)
      if (entry.value != null) entry.key: entry.value,
  };
}

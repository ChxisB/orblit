import 'dart:convert';

import 'kit_manifest.dart' show kitShow;

/// A JSON object being read, whose every complaint says where it came from.
///
/// The entries, the index and the release lists are files somebody else
/// publishes, so each word a reader echoes goes through [kitShow].
final class JsonObject {
  const JsonObject(this.map, this.where);

  /// Throws a [FormatException] unless [bytes] are UTF-8 JSON, no longer than
  /// [limit], holding one object.
  factory JsonObject.parse(
    List<int> bytes,
    String where, {
    required int limit,
  }) {
    if (bytes.length > limit) {
      throw FormatException('$where is over $limit bytes.');
    }
    final Object? parsed;
    try {
      parsed = jsonDecode(utf8.decode(bytes));
    } on FormatException catch (error) {
      throw FormatException('$where is not JSON: ${kitShow(error.message)}');
    }
    if (parsed is Map<String, Object?>) return JsonObject(parsed, where);
    throw FormatException('$where is not a JSON object.');
  }

  final Map<String, Object?> map;

  /// What the file or part is called, such as `entry "brass-lamp/crates"`.
  final String where;

  /// A complaint about this object. [what] may already end in a full stop.
  FormatException problem(String what) =>
      FormatException(what.endsWith('.') ? '$where $what' : '$where $what.');

  String text(String key) {
    final value = map[key];
    if (value is String) return value;
    throw problem('has no "$key" text');
  }

  int integer(String key) {
    final value = map[key];
    if (value is int) return value;
    throw problem('has no "$key" whole number');
  }

  bool flag(String key) {
    final value = map[key];
    if (value is bool) return value;
    throw problem('has no "$key" true or false');
  }

  /// The text at [key], or null when it is not there.
  String? optionalText(String key) => map.containsKey(key) ? text(key) : null;

  JsonObject section(String key) {
    final value = map[key];
    if (value is Map<String, Object?>) return JsonObject(value, '$where, $key');
    throw problem('has no "$key" object');
  }

  List<JsonObject> objects(String key) {
    final values = _list(key);
    return [
      for (var i = 0; i < values.length; i++)
        if (values[i] case final Map<String, Object?> value)
          JsonObject(value, '$where, $key ${i + 1}')
        else
          throw problem('holds something other than an object in "$key"'),
    ];
  }

  List<String> strings(String key) {
    final values = _list(key);
    return [
      for (final value in values)
        if (value is String)
          value
        else
          throw problem('holds ${kitShow('$value')}, not text, in "$key"'),
    ];
  }

  /// Throws unless every key is one of [keys], so a typo is caught.
  void only(Set<String> keys) {
    for (final key in map.keys) {
      if (!keys.contains(key)) {
        throw problem(
          'has "${kitShow(key)}", which is not a key of this file. Check the '
          'spelling',
        );
      }
    }
  }

  List<Object?> _list(String key) {
    final value = map[key];
    if (value is List<Object?>) return value;
    throw problem('has no "$key" list');
  }
}

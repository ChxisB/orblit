import 'surface_error.dart';

/// The types a surface uniform can have.
enum SurfaceType {
  boolean('bool', 'bool'),
  integer('int', 'int'),
  scalar('float', 'float'),
  vector2('vec2', 'float2'),
  vector3('vec3', 'float3'),
  vector4('vec4', 'float4'),
  texture('sampler2D', 'sampler2d');

  const SurfaceType(this.word, this.matType);

  /// What the author writes.
  final String word;

  /// What the material file calls it.
  final String matType;

  /// How many numbers a value takes. A texture takes none.
  int get width => switch (this) {
    boolean || integer || scalar => 1,
    vector2 => 2,
    vector3 => 3,
    vector4 => 4,
    texture => 0,
  };

  static SurfaceType? ofWord(String word) {
    for (final type in values) {
      if (type.word == word) return type;
    }
    return null;
  }
}

/// One uniform an author declared.
///
/// The hints are kept for the editor: the engine itself needs only the name,
/// the type and the default.
final class SurfaceParam {
  const SurfaceParam({
    required this.name,
    required this.type,
    required this.line,
    this.instance = false,
    this.group,
    this.hints = const {},
    this.defaultValue,
  });

  final String name;
  final SurfaceType type;
  final int line;

  /// Whether each object may carry its own value.
  final bool instance;
  final String? group;

  /// Hint name to its arguments, as written: `source_color` is `[]` and
  /// `hint_range(0, 1)` is `['0', '1']`.
  final Map<String, List<String>> hints;

  /// Numbers for a number type, `true` or `false` for a bool, or null for a
  /// texture and for a uniform with no default.
  final Object? defaultValue;

  bool get isColor => hints.containsKey('source_color');

  Map<String, Object?> toJson() => {
    'name': name,
    'type': type.word,
    'instance': instance,
    if (group != null) 'group': group,
    if (hints.isNotEmpty) 'hints': hints,
    if (defaultValue != null) 'default': defaultValue,
  };
}

const _knownHints = {
  'source_color',
  'hint_range',
  'hint_enum',
  'hint_normal',
  'hint_default_white',
  'hint_default_black',
  'hint_default_transparent',
  'filter_nearest',
  'filter_linear',
  'filter_nearest_mipmap',
  'filter_linear_mipmap',
  'repeat_enable',
  'repeat_disable',
};

final _hintShape = RegExp(r'^(\w+)\s*(?:\((.*)\))?$');

/// `hint_range(0, 1), source_color` to its hints, splitting only on commas
/// outside brackets.
Map<String, List<String>> parseHints(String text, SurfaceOrigin at) {
  final hints = <String, List<String>>{};
  for (final piece in splitTopLevel(text)) {
    final match = _hintShape.firstMatch(piece.trim());
    if (match == null) throw SurfaceError(at, 'cannot read hint `$piece`');
    final name = match.group(1)!;
    if (!_knownHints.contains(name)) {
      throw SurfaceError(at, 'unknown hint `$name`');
    }
    final args = match.group(2);
    hints[name] = args == null ? const [] : splitTopLevel(args);
  }
  return hints;
}

/// Splits on commas that are not inside brackets, trimmed.
List<String> splitTopLevel(String text) {
  final parts = <String>[];
  var depth = 0;
  var start = 0;
  for (var i = 0; i < text.length; i++) {
    final c = text[i];
    if (c == '(') depth++;
    if (c == ')') depth--;
    if (c == ',' && depth == 0) {
      parts.add(text.substring(start, i).trim());
      start = i + 1;
    }
  }
  parts.add(text.substring(start).trim());
  return parts;
}

/// A default written as a literal: `1.5`, `true`, `vec3(0.5)` or
/// `vec4(1.0, 0.5, 0.2, 1.0)`. A single argument fills every component.
Object parseDefault(SurfaceType type, String text, SurfaceOrigin at) {
  final value = text.trim();
  if (type == SurfaceType.boolean) return _boolean(value, at);
  final inner = type.width == 1 ? value : _constructorArgs(type, value, at);
  final numbers = [for (final part in splitTopLevel(inner)) _number(part, at)];
  if (type.width == 1 && numbers.length == 1) return numbers.single;
  if (numbers.length == 1) return List.filled(type.width, numbers.single);
  if (numbers.length != type.width) {
    throw SurfaceError(at, '${type.word} needs ${type.width} numbers');
  }
  return numbers;
}

bool _boolean(String value, SurfaceOrigin at) {
  if (value == 'true') return true;
  if (value == 'false') return false;
  throw SurfaceError(at, 'a bool default is `true` or `false`');
}

String _constructorArgs(SurfaceType type, String value, SurfaceOrigin at) {
  final prefix = '${type.word}(';
  if (!value.startsWith(prefix) || !value.endsWith(')')) {
    throw SurfaceError(at, 'a ${type.word} default is `$prefix...)`');
  }
  return value.substring(prefix.length, value.length - 1);
}

num _number(String text, SurfaceOrigin at) {
  final parsed = num.tryParse(text.replaceFirst(RegExp(r'[fF]$'), ''));
  if (parsed == null) throw SurfaceError(at, 'cannot read `$text` as a number');
  return parsed;
}

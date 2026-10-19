import 'dart:convert';

import 'package:orblit_progress/orblit_progress.dart';

import 'node.dart';

/// The extension a dialogue file carries.
const String dialogueExtension = '.odialogue';

/// A file that is not a dialogue, or is one from a newer Orblit.
final class DialogueFormatException implements Exception {
  const DialogueFormatException(this.message);

  final String message;

  @override
  String toString() => 'DialogueFormatException: $message';
}

/// Where a conversation starts when its conditions hold.
final class Variant {
  Variant({required this.start, List<Condition> when = const []})
    : when = List.unmodifiable(when);

  final String start;
  final List<Condition> when;
}

/// A conversation: variants, and the nodes they lead into.
///
/// The first variant whose conditions hold is the one that starts, so the
/// same character says something new as the table changes. A variant with no
/// conditions always holds, and goes last as the default.
final class Dialogue {
  Dialogue({
    required this.key,
    required List<Variant> variants,
    required List<Node> nodes,
  }) : variants = List.unmodifiable(variants),
       nodes = Map.unmodifiable({for (final node in nodes) node.id: node}) {
    if (key.isEmpty) throw ArgumentError.value(key, 'key', 'is empty');
    if (variants.isEmpty) {
      throw ArgumentError.value(variants, 'variants', 'is empty');
    }
    final ids = <String>{};
    for (final node in nodes) {
      if (!ids.add(node.id)) {
        throw ArgumentError.value(node.id, 'nodes', 'repeats an id');
      }
    }
    _checkVariants();
    _checkTargets();
    _checkNoSilentLoop();
  }

  /// Reads the text of a `.odialogue` file. Throws [DialogueFormatException]
  /// for text that is not one, for one from a newer Orblit and for one that
  /// breaks a rule of [Dialogue], [Node] or [Condition].
  factory Dialogue.parse(String text) {
    final Object? json;
    try {
      json = jsonDecode(text);
    } on FormatException catch (error) {
      throw DialogueFormatException(
        'This is not a dialogue file: ${error.message}',
      );
    }
    if (json is! Map<String, Object?> || json['kind'] != marker) {
      throw const DialogueFormatException('This is not a dialogue file.');
    }
    final written = json['formatVersion'];
    if (written is! int || written < 1) {
      throw const DialogueFormatException('This file has no format version.');
    }
    if (written > formatVersion) {
      throw const DialogueFormatException('This is from a newer Orblit.');
    }
    return _build(json);
  }

  static const String marker = 'orblit.dialogue';
  static const int formatVersion = 1;

  final String key;
  final List<Variant> variants;
  final Map<String, Node> nodes;

  /// The node a conversation starts at for a player whose table is [table],
  /// or null when no variant holds. It changes nothing and runs no hook, so a
  /// game can ask it to see whether a character has something to say.
  String? startFor(ProgressTable table) {
    for (final variant in variants) {
      if (Condition.allHold(variant.when, table)) return variant.start;
    }
    return null;
  }

  /// The names of the functions nodes ask for, in the order they first appear.
  Set<String> get beforeNames => {
    for (final node in nodes.values) ?node.before,
  };

  String encode() => const JsonEncoder.withIndent('  ').convert({
    'kind': marker,
    'formatVersion': formatVersion,
    'key': key,
    'variants': [
      for (final variant in variants)
        {
          'start': variant.start,
          if (variant.when.isNotEmpty)
            'when': [for (final c in variant.when) c.toJson()],
        },
    ],
    'nodes': {for (final node in nodes.values) node.id: node.toJson()},
  });

  /// A variant with no conditions always holds, so any after it is dead.
  void _checkVariants() {
    final open = variants.indexWhere((v) => v.when.isEmpty);
    if (open >= 0 && open < variants.length - 1) {
      throw ArgumentError.value(
        variants[open].start,
        'variants',
        'has no conditions, so the variants after it never start',
      );
    }
  }

  void _checkTargets() {
    for (final variant in variants) {
      if (!nodes.containsKey(variant.start)) {
        throw ArgumentError.value(variant.start, 'variants', 'is not a node');
      }
    }
    for (final node in nodes.values) {
      for (final target in node.leadsTo) {
        if (!nodes.containsKey(target)) {
          throw ArgumentError.value(target, node.id, 'leads to no node');
        }
      }
    }
  }

  /// A set or a jump shows nothing, so a ring of them would never stop.
  void _checkNoSilentLoop() {
    for (final node in nodes.values) {
      final seen = <String>{};
      Node? at = node;
      while (at != null && (at is SetNode || at is JumpNode)) {
        if (!seen.add(at.id)) {
          throw ArgumentError.value(at.id, 'nodes', 'loop without a line');
        }
        final next = at.leadsTo.firstOrNull;
        at = next == null ? null : nodes[next];
      }
    }
  }
}

Dialogue _build(Map<String, Object?> json) {
  final key = json['key'];
  final variants = json['variants'];
  final nodes = json['nodes'];
  if (key is! String ||
      variants is! List<Object?> ||
      nodes is! Map<String, Object?>) {
    throw const DialogueFormatException(
      'A dialogue has a key, a list of variants and a map of nodes.',
    );
  }
  try {
    return Dialogue(
      key: key,
      variants: [for (final v in variants) _variantFromJson(v)],
      nodes: [
        for (final MapEntry(:key, :value) in nodes.entries)
          _nodeFromJson(key, value),
      ],
    );
  } on FormatException catch (error) {
    throw DialogueFormatException(error.message);
  } on ArgumentError catch (error) {
    throw DialogueFormatException(
      '${error.name ?? 'dialogue'} ${error.message} (${error.invalidValue}).',
    );
  }
}

Map<String, Object?> _object(Object? json, String what) {
  if (json is Map<String, Object?>) return json;
  throw FormatException('$what is an object, not $json.');
}

String _text(Map<String, Object?> json, String field) {
  final value = json[field];
  if (value is String) return value;
  throw FormatException('"$field" is text, not $value.');
}

String? _maybeText(Map<String, Object?> json, String field) {
  final value = json[field];
  if (value == null || value is String) return value as String?;
  throw FormatException('"$field" is text, not $value.');
}

Variant _variantFromJson(Object? json) {
  final map = _object(json, 'A variant');
  return Variant(
    start: _text(map, 'start'),
    when: Condition.listFromJson(map['when']),
  );
}

Node _nodeFromJson(String id, Object? json) {
  final map = _object(json, 'Node "$id"');
  final before = _maybeText(map, 'before');
  return switch (map['type']) {
    'line' => LineNode(
      id: id,
      speaker: _text(map, 'speaker'),
      text: _text(map, 'text'),
      next: _maybeText(map, 'next'),
      before: before,
    ),
    'choice' => ChoiceNode(
      id: id,
      options: [for (final o in _list(map, 'options')) _optionFromJson(o)],
      before: before,
    ),
    'set' => SetNode(
      id: id,
      writes: [for (final w in _list(map, 'writes')) _writeFromJson(w)],
      next: _maybeText(map, 'next'),
      before: before,
    ),
    'jump' => JumpNode(id: id, next: _text(map, 'next'), before: before),
    final other => throw FormatException(
      'Node "$id" has type $other, not line, choice, set or jump.',
    ),
  };
}

List<Object?> _list(Map<String, Object?> json, String field) {
  final value = json[field];
  if (value is List<Object?>) return value;
  throw FormatException('"$field" is a list, not $value.');
}

ChoiceOption _optionFromJson(Object? json) {
  final map = _object(json, 'An option');
  return ChoiceOption(
    text: _text(map, 'text'),
    next: _maybeText(map, 'next'),
    when: Condition.listFromJson(map['when']),
  );
}

Write _writeFromJson(Object? json) {
  final map = _object(json, 'A write');
  final value = map['value'];
  if (value is! int) {
    throw const FormatException('A write needs a whole number.');
  }
  return Write(_text(map, 'name'), value);
}

import 'package:orblit_progress/orblit_progress.dart';

/// One step of a conversation. [before] names a function the game registered,
/// which the runner calls before it shows or runs the node.
sealed class Node {
  Node({required this.id, this.before}) {
    if (id.isEmpty) throw ArgumentError.value(id, 'id', 'is empty');
    if (before != null && before!.isEmpty) {
      throw ArgumentError.value(before, 'before', 'is empty');
    }
  }

  final String id;
  final String? before;

  /// The ids of the nodes this one can lead to.
  Iterable<String> get leadsTo;

  Map<String, Object?> toJson();
}

/// Someone says [text]. The conversation moves on when the player does.
/// [next] is null when the conversation ends here.
final class LineNode extends Node {
  LineNode({
    required super.id,
    required this.speaker,
    required this.text,
    this.next,
    super.before,
  });

  final String speaker;
  final String text;
  final String? next;

  @override
  Iterable<String> get leadsTo => [?next];

  @override
  Map<String, Object?> toJson() => {
    'type': 'line',
    'speaker': speaker,
    'text': text,
    if (next != null) 'next': next,
    if (before != null) 'before': before,
  };
}

/// What a player can pick. It is offered only while every condition in [when]
/// holds. [next] is null when picking it ends the conversation.
final class ChoiceOption {
  ChoiceOption({
    required this.text,
    required this.next,
    List<Condition> when = const [],
  }) : when = List.unmodifiable(when);

  final String text;
  final String? next;
  final List<Condition> when;

  Map<String, Object?> toJson() => {
    'text': text,
    if (next != null) 'next': next,
    if (when.isNotEmpty) 'when': [for (final c in when) c.toJson()],
  };
}

/// The player picks one of [options]. One with nothing to offer ends the
/// conversation.
final class ChoiceNode extends Node {
  ChoiceNode({
    required super.id,
    required List<ChoiceOption> options,
    super.before,
  }) : options = List.unmodifiable(options) {
    if (options.isEmpty) throw ArgumentError.value(id, 'id', 'has no options');
  }

  final List<ChoiceOption> options;

  @override
  Iterable<String> get leadsTo => [for (final option in options) ?option.next];

  @override
  Map<String, Object?> toJson() => {
    'type': 'choice',
    'options': [for (final option in options) option.toJson()],
    if (before != null) 'before': before,
  };
}

/// A value to write: [name] becomes [value].
final class Write {
  Write(this.name, this.value) {
    if (name.isEmpty) throw ArgumentError.value(name, 'name', 'is empty');
    if (value < 0 || value > 0xFFFFFFFF) {
      throw ArgumentError.value(value, 'value', 'cannot be stored');
    }
  }

  final String name;
  final int value;
}

/// Writes values in the player's table and moves on without showing anything.
final class SetNode extends Node {
  SetNode({
    required super.id,
    required List<Write> writes,
    this.next,
    super.before,
  }) : writes = List.unmodifiable(writes);

  final List<Write> writes;
  final String? next;

  @override
  Iterable<String> get leadsTo => [?next];

  @override
  Map<String, Object?> toJson() => {
    'type': 'set',
    'writes': [
      for (final write in writes) {'name': write.name, 'value': write.value},
    ],
    if (next != null) 'next': next,
    if (before != null) 'before': before,
  };
}

/// Moves on to [next] without showing anything.
final class JumpNode extends Node {
  JumpNode({required super.id, required this.next, super.before});

  final String next;

  @override
  Iterable<String> get leadsTo => [next];

  @override
  Map<String, Object?> toJson() => {
    'type': 'jump',
    'next': next,
    if (before != null) 'before': before,
  };
}

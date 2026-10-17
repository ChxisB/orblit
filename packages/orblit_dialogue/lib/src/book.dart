import 'package:orblit_progress/orblit_progress.dart';

import 'dialogue.dart';
import 'node.dart';
import 'runner.dart';

/// The conversations a game has, checked against its progress schema and the
/// functions it registered.
final class DialogueBook {
  /// Checks every dialogue when the game starts, so a typo fails here and not
  /// when a player talks: every name a condition or a write uses must be in
  /// [schema], a written value must fit its name's bits, and every `before`
  /// must be one of [hooks].
  DialogueBook({
    required ProgressSchema schema,
    required Map<String, Before> hooks,
    required List<Dialogue> dialogues,
  }) : _hooks = Map.unmodifiable(hooks),
       _dialogues = {for (final d in dialogues) d.key: d} {
    if (_dialogues.length != dialogues.length) {
      throw ArgumentError.value(dialogues, 'dialogues', 'repeat a key');
    }
    for (final dialogue in dialogues) {
      _check(schema, hooks, dialogue);
    }
  }

  final Map<String, Before> _hooks;
  final Map<String, Dialogue> _dialogues;

  Iterable<Dialogue> get dialogues => _dialogues.values;

  /// Throws [ArgumentError] for a key that is not a dialogue.
  Dialogue dialogue(String key) =>
      _dialogues[key] ?? (throw ArgumentError.value(key, 'key', 'is unknown'));

  /// Starts the conversation [key] for the player whose table is [table].
  DialogueRunner start(String key, ProgressTable table) =>
      DialogueRunner(dialogue: dialogue(key), table: table, hooks: _hooks);
}

void _check(
  ProgressSchema schema,
  Map<String, Before> hooks,
  Dialogue dialogue,
) {
  for (final name in dialogue.beforeNames) {
    if (!hooks.containsKey(name)) {
      throw ArgumentError.value(name, dialogue.key, 'is not a registered hook');
    }
  }
  for (final (where, condition) in _conditions(dialogue)) {
    _checkName(schema, where, condition.name, condition.value);
  }
  for (final node in dialogue.nodes.values.whereType<SetNode>()) {
    for (final write in node.writes) {
      _checkName(schema, '${dialogue.key}/${node.id}', write.name, write.value);
    }
  }
}

Iterable<(String, Condition)> _conditions(Dialogue dialogue) sync* {
  for (final variant in dialogue.variants) {
    for (final c in variant.when) {
      yield ('${dialogue.key} variant', c);
    }
  }
  for (final node in dialogue.nodes.values.whereType<ChoiceNode>()) {
    for (final option in node.options) {
      for (final c in option.when) {
        yield ('${dialogue.key}/${node.id}', c);
      }
    }
  }
}

void _checkName(ProgressSchema schema, String where, String name, int value) {
  if (!schema.has(name)) {
    throw ArgumentError.value(name, where, 'is not in the schema');
  }
  if (!schema.fits(name, value)) {
    throw ArgumentError.value(value, where, '$name has too few bits for it');
  }
}

import 'package:orblit_progress/orblit_progress.dart';

import 'dialogue.dart';
import 'node.dart';

/// A function the game registers for a node's `before`. It gets the table as
/// it stands and returns the table the conversation goes on with, so a hook
/// that moves a quest returns what the quest book gave back. A hook that only
/// gives an item or opens a door returns the table it was given.
typedef Before = ProgressTable Function(ProgressTable table);

/// Walks one conversation for one player. It holds no screen: the game reads
/// [current] and [options], draws them, and calls [next] or [choose].
///
/// The runner starts as soon as it is made. When no variant of the dialogue
/// holds, [isOver] is true at once and nothing was run.
final class DialogueRunner {
  DialogueRunner({
    required Dialogue dialogue,
    required ProgressTable table,
    required Map<String, Before> hooks,
  }) : _dialogue = dialogue,
       _hooks = hooks,
       _table = table {
    for (final variant in dialogue.variants) {
      if (Condition.allHold(variant.when, table)) {
        _enter(variant.start);
        return;
      }
    }
  }

  final Dialogue _dialogue;
  final Map<String, Before> _hooks;
  ProgressTable _table;
  Node? _current;
  List<ChoiceOption> _options = const [];

  /// The table with every write and hook so far. Send the player
  /// `table.changesSince(before)` and save the table.
  ProgressTable get table => _table;

  /// The line or choice being shown, or null when the conversation is over.
  Node? get current => _current;

  bool get isOver => _current == null;

  /// What the player may pick at a choice, in the order of the file. A choice
  /// with conditions that fail has fewer than it was written with. Empty when
  /// [current] is not a choice.
  List<ChoiceOption> get options => _options;

  /// Goes on from a line. Throws [StateError] anywhere else.
  void next() {
    final node = _current;
    if (node is! LineNode) throw StateError('Only a line goes on with next.');
    _enter(node.next);
  }

  /// Picks the option at [index] of [options]. Throws [StateError] when
  /// [current] is not a choice and [RangeError] for an index [options] does
  /// not have.
  void choose(int index) {
    if (_current is! ChoiceNode) {
      throw StateError('Only a choice goes on with choose.');
    }
    _enter(_options[index].next);
  }

  void _enter(String? first) {
    _current = null;
    _options = const [];
    var at = first;
    while (at != null) {
      final node = _dialogue.nodes[at]!;
      final before = node.before;
      if (before != null) _table = _hooks[before]!(_table);
      switch (node) {
        case LineNode():
          _current = node;
          return;
        case ChoiceNode(:final options):
          final offered = [
            for (final o in options)
              if (Condition.allHold(o.when, _table)) o,
          ];
          if (offered.isEmpty) return;
          _current = node;
          _options = offered;
          return;
        case SetNode(:final writes, next: final following):
          for (final write in writes) {
            _table = _table.set(write.name, write.value).table;
          }
          at = following;
        case JumpNode(next: final following):
          at = following;
      }
    }
  }
}

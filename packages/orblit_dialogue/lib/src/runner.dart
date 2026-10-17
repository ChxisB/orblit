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
/// holds, [isOver] is true at once and nothing was run. [DialogueBook.start]
/// makes it, having checked the hooks.
///
/// The runner works on the table it was given and does not see changes made
/// to the player's table meanwhile. A game that lets other things change the
/// table during a conversation ends the conversation when they do, or reads
/// the table again before it saves [table].
final class DialogueRunner {
  DialogueRunner({
    required Dialogue dialogue,
    required ProgressTable table,
    required Map<String, Before> hooks,
  }) : _dialogue = dialogue,
       _hooks = hooks,
       _table = table {
    final start = dialogue.startFor(table);
    if (start != null) _enter(start);
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

  /// Runs from [first] to the next line or choice, or to the end. The fields
  /// are written once at the end, so a hook that throws leaves the runner
  /// where it was.
  void _enter(String? first) {
    var table = _table;
    Node? shown;
    var offered = <ChoiceOption>[];
    var at = first;
    while (at != null) {
      final node = _dialogue.nodes[at]!;
      final before = node.before;
      if (before != null) table = _hooks[before]!(table);
      switch (node) {
        case LineNode():
          shown = node;
          at = null;
        case ChoiceNode(:final options):
          offered = [
            for (final o in options)
              if (Condition.allHold(o.when, table)) o,
          ];
          if (offered.isNotEmpty) shown = node;
          at = null;
        case SetNode(:final writes, next: final following):
          for (final write in writes) {
            table = table.set(write.name, write.value).table;
          }
          at = following;
        case JumpNode(next: final following):
          at = following;
      }
    }
    _table = table;
    _current = shown;
    _options = List.unmodifiable(offered);
  }
}

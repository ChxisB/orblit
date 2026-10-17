import 'package:orblit_progress/orblit_progress.dart';

import 'quest.dart';

/// Why a quest did not move.
enum QuestRefusal {
  /// The stage asked for is the one the player is in, or one behind it.
  notForward,

  /// A condition of the stage asked for does not hold.
  requirement,
}

/// What [QuestBook.advance] did.
sealed class QuestMove {
  const QuestMove();
}

/// The quest moved. Write [table] over the player's, send [changes] to the
/// player, and give the reward when [completed].
final class Moved extends QuestMove {
  const Moved({
    required this.table,
    required this.changes,
    required this.completed,
    required this.back,
  });

  final ProgressTable table;
  final Changes changes;

  /// The quest entered its last stage from an earlier one.
  final bool completed;

  /// The quest went back a stage, which only `allowBack` permits. Record why.
  final bool back;
}

/// The quest stayed where it was.
final class Refused extends QuestMove {
  const Refused(this.reason);

  final QuestRefusal reason;
}

/// The quests a game has, and the rules for moving a player through them.
///
/// The book holds no player. Each question and each move takes the player's
/// [ProgressTable] and, for a move, gives a new one back, so the game decides
/// what to save, to send and to reward.
final class QuestBook {
  /// Checks every quest against [schema] when the game starts, so a typo
  /// fails here and not when a player reaches it: the stage name must be in
  /// the schema with room for every stage, and every condition must name a
  /// value the schema has.
  QuestBook({required ProgressSchema schema, required List<Quest> quests})
    : _quests = {for (final quest in quests) quest.key: quest} {
    if (_quests.length != quests.length) {
      throw ArgumentError.value(quests, 'quests', 'repeat a key');
    }
    for (final quest in quests) {
      _check(schema, quest);
    }
  }

  final Map<String, Quest> _quests;

  Iterable<Quest> get quests => _quests.values;

  /// Throws [ArgumentError] for a key that is not a quest.
  Quest quest(String key) =>
      _quests[key] ?? (throw ArgumentError.value(key, 'key', 'is unknown'));

  /// The index of the stage [table] has the player in. Zero is not begun.
  int stageOf(ProgressTable table, String key) =>
      table.get(questStageName(key));

  bool isStarted(ProgressTable table, String key) => stageOf(table, key) > 0;

  bool isComplete(ProgressTable table, String key) =>
      stageOf(table, key) >= quest(key).last;

  /// The text for the stage the player is in.
  String journal(ProgressTable table, String key) =>
      quest(key).stages[stageOf(table, key)].journal;

  /// Moves the player to stage [to]. A quest goes forward: a stage that is
  /// not ahead is refused unless [allowBack] is set and [to] is behind. A
  /// stage ahead is refused when one of its conditions does not hold, and a
  /// stage may be skipped to. Throws [RangeError] for a stage the quest does
  /// not have.
  QuestMove advance(
    ProgressTable table,
    String key,
    int to, {
    bool allowBack = false,
  }) {
    final quest = this.quest(key);
    RangeError.checkValueInInterval(to, 0, quest.last, 'to');
    final from = stageOf(table, key);
    final back = to < from;
    if (to == from || (back && !allowBack)) {
      return const Refused(QuestRefusal.notForward);
    }
    if (!back && !Condition.allHold(quest.stages[to].requires, table)) {
      return const Refused(QuestRefusal.requirement);
    }
    final written = table.set(questStageName(key), to);
    return Moved(
      table: written.table,
      changes: written.changes,
      completed: to == quest.last,
      back: back,
    );
  }
}

void _check(ProgressSchema schema, Quest quest) {
  final name = questStageName(quest.key);
  if (!schema.has(name)) {
    throw ArgumentError.value(name, quest.key, 'is not in the schema');
  }
  final bits = schema.placeOf(name).bits;
  if (bits < 31 && quest.last >= 1 << bits) {
    throw ArgumentError.value(
      name,
      quest.key,
      'has $bits bits, too few for ${quest.stages.length} stages',
    );
  }
  for (final stage in quest.stages) {
    for (final condition in stage.requires) {
      if (!schema.has(condition.name)) {
        throw ArgumentError.value(
          condition.name,
          '${quest.key}/${stage.id}',
          'is not in the schema',
        );
      }
    }
  }
}

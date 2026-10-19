import 'dart:convert';

import 'package:orblit_progress/orblit_progress.dart';

/// The extension a quest file carries.
const String questExtension = '.oquest';

/// A file that is not a quest, or is one from a newer Orblit.
final class QuestFormatException implements Exception {
  const QuestFormatException(this.message);

  final String message;

  @override
  String toString() => 'QuestFormatException: $message';
}

/// The name a quest's stage has in the progress table.
String questStageName(String key) => 'quest.$key.stage';

final RegExp _keyPattern = RegExp(r'^[A-Za-z0-9_-]+$');

/// One step of a quest: the text the journal shows while the player is in it,
/// and what must hold before the player may enter it.
final class Stage {
  Stage({
    required this.id,
    required this.journal,
    List<Condition> requires = const [],
  }) : requires = List.unmodifiable(requires) {
    if (id.isEmpty) throw ArgumentError.value(id, 'id', 'is empty');
  }

  final String id;
  final String journal;
  final List<Condition> requires;

  Map<String, Object?> toJson() => {
    'id': id,
    'journal': journal,
    if (requires.isNotEmpty) 'requires': [for (final c in requires) c.toJson()],
  };
}

/// A quest: stages in order, from the one a player has not begun to the one
/// that is complete.
///
/// A player's place is one integer, the index of a stage. A quest goes
/// forward only. One that branches uses more than one name in the table.
final class Quest {
  Quest({required this.key, required this.title, required List<Stage> stages})
    : stages = List.unmodifiable(stages) {
    if (!_keyPattern.hasMatch(key)) {
      throw ArgumentError.value(key, 'key', 'is not letters, digits, _ or -');
    }
    if (title.isEmpty) throw ArgumentError.value(title, 'title', 'is empty');
    if (stages.length < 2) {
      throw ArgumentError.value(
        stages.length,
        'stages',
        'needs a start and an end',
      );
    }
    final ids = <String>{};
    for (final stage in stages) {
      if (!ids.add(stage.id)) {
        throw ArgumentError.value(stage.id, 'stages', 'repeats a stage id');
      }
    }
  }

  /// Reads the text of a `.oquest` file. Throws [QuestFormatException] for
  /// text that is not one, for one written by a newer Orblit, and for one
  /// that breaks a rule of [Quest] or [Condition].
  factory Quest.parse(String text) {
    final Object? json;
    try {
      json = jsonDecode(text);
    } on FormatException catch (error) {
      throw QuestFormatException('This is not a quest file: ${error.message}');
    }
    if (json is! Map<String, Object?> || json['kind'] != marker) {
      throw const QuestFormatException('This is not a quest file.');
    }
    final written = json['formatVersion'];
    if (written is! int || written < 1) {
      throw const QuestFormatException('This quest has no format version.');
    }
    if (written > formatVersion) {
      throw const QuestFormatException('This quest is from a newer Orblit.');
    }
    final key = json['key'];
    final title = json['title'];
    final stages = json['stages'];
    if (key is! String || title is! String || stages is! List<Object?>) {
      throw const QuestFormatException(
        'A quest has a key, a title and a list of stages.',
      );
    }
    try {
      return Quest(
        key: key,
        title: title,
        stages: [for (final stage in stages) _stageFromJson(stage)],
      );
    } on FormatException catch (error) {
      throw QuestFormatException(error.message);
    } on ArgumentError catch (error) {
      throw QuestFormatException(
        '${error.name ?? 'quest'} ${error.message} (${error.invalidValue}).',
      );
    }
  }

  static const String marker = 'orblit.quest';
  static const int formatVersion = 1;

  final String key;
  final String title;
  final List<Stage> stages;

  /// The index of the stage that is complete.
  int get last => stages.length - 1;

  String encode() => const JsonEncoder.withIndent('  ').convert({
    'kind': marker,
    'formatVersion': formatVersion,
    'key': key,
    'title': title,
    'stages': [for (final stage in stages) stage.toJson()],
  });
}

Stage _stageFromJson(Object? json) {
  if (json is! Map<String, Object?>) {
    throw const FormatException('A stage is an object.');
  }
  final id = json['id'];
  final journal = json['journal'];
  if (id is! String || journal is! String) {
    throw const FormatException('A stage has an id and journal text.');
  }
  return Stage(
    id: id,
    journal: journal,
    requires: Condition.listFromJson(json['requires']),
  );
}

import 'dart:convert';

/// One step between two formats of a file: decoded JSON at [from] in, at
/// [to] out, with a note in `notes` for anything somebody should hear about.
abstract interface class FormatStep {
  int get from;
  int get to;

  Map<String, Object?> apply(Map<String, Object?> json, List<String> notes);
}

/// How to open one kind of Orblit file.
///
/// Clips, blends and cutscenes are opened the same way: the text must be
/// JSON that says which kind of file it is, and a file from a newer Orblit
/// is refused rather than half read.
final class FileFormat {
  const FileFormat({
    required this.marker,
    required this.noun,
    required this.version,
    required this.fail,
    this.steps = const [],
  });

  /// What the file's `kind` says it is.
  final String marker;

  /// What the file is called in a sentence, such as `clip`.
  final String noun;

  /// The format this Orblit writes.
  final int version;

  /// What is thrown for a file that cannot be opened, made from what
  /// somebody is told.
  final Exception Function(String message) fail;

  /// Every step up from an older format, oldest first.
  final List<FormatStep> steps;

  /// The decoded JSON of [text], brought up to [version], with a note in
  /// [problems] for anything a step changed.
  Map<String, Object?> open(String text, List<String> problems) {
    final Object? parsed;
    try {
      parsed = jsonDecode(text);
    } on FormatException catch (error) {
      throw fail('This is not a $noun file: ${error.message}');
    }
    if (parsed is! Map<String, Object?> || parsed['kind'] != marker) {
      throw fail('This is not a $noun file.');
    }
    final written = parsed['formatVersion'];
    if (written is int && written > version) {
      throw fail('This $noun was written by a newer Orblit.');
    }
    var json = parsed;
    for (final step in steps) {
      if (written is int && step.from >= written) {
        json = step.apply(json, problems);
      }
    }
    return json;
  }
}

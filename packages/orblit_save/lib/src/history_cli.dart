import 'history_store.dart';
import 'tools.dart';

const String _usage = '''
dart run orblit_save:history <saves-directory> list [key]
dart run orblit_save:history <saves-directory> restore <key> <time> --server-stopped
dart run orblit_save:history <saves-directory> restore-all <time> [--apply] --server-stopped

A <time> is ISO 8601, as list prints it. restore-all lists what it would
change and writes only with --apply. The command cannot see who is playing,
so a write needs --server-stopped to say you stopped the server.''';

const String _apply = '--apply';
const String _stopped = '--server-stopped';
const int _failed = 1;
const int _badUsage = 64;

/// Runs `dart run orblit_save:history` and returns its exit code.
Future<int> runHistory(
  List<String> args, {
  required StringSink out,
  required StringSink err,
  required DateTime Function() clock,
}) async {
  final flags = {
    for (final arg in args)
      if (arg.startsWith('--')) arg,
  };
  final words = [
    for (final arg in args)
      if (!arg.startsWith('--')) arg,
  ];
  if (!_isUsable(flags, words)) return _badUsageWith(err);
  final store = HistoryStore.files(words[0]);
  // The command cannot see who is online. --server-stopped says it is safe.
  final tools = SaveTools(store: store, isLive: (_) => false, clock: clock);
  final rest = words.skip(2).toList();
  try {
    return switch (words[1]) {
      'list' when rest.length <= 1 => await _list(store, rest, out),
      'restore' when rest.length == 2 => await _restore(tools, rest, out),
      'restore-all' when rest.length == 1 => await _restoreAll(
        tools,
        rest.single,
        flags.contains(_apply),
        out,
      ),
      _ => _badUsageWith(err),
    };
  } on StateError catch (e) {
    err.writeln(e.message);
    return _failed;
  } on FormatException catch (e) {
    err.writeln(e.message);
    return _failed;
  }
}

bool _isUsable(Set<String> flags, List<String> words) {
  if (words.length < 2 || flags.difference({_apply, _stopped}).isNotEmpty) {
    return false;
  }
  final command = words[1];
  final writes =
      command == 'restore' ||
      (command == 'restore-all' && flags.contains(_apply));
  return !writes || flags.contains(_stopped);
}

int _badUsageWith(StringSink err) {
  err.writeln(_usage);
  return _badUsage;
}

Future<int> _list(HistoryStore store, List<String> rest, StringSink out) async {
  if (rest.isEmpty) {
    (await store.history.keys()).forEach(out.writeln);
    return 0;
  }
  for (final v in await store.history.list(rest.single)) {
    out.writeln(
      '${v.at.toIso8601String()}  ${v.reason.label}  ${v.hash.substring(0, 12)}',
    );
  }
  return 0;
}

Future<int> _restore(SaveTools tools, List<String> rest, StringSink out) async {
  await tools.restore(rest[0], DateTime.parse(rest[1]).toUtc());
  out.writeln('Restored ${rest[0]}.');
  return 0;
}

Future<int> _restoreAll(
  SaveTools tools,
  String time,
  bool apply,
  StringSink out,
) async {
  final plan = await tools.restorePlan(DateTime.parse(time).toUtc());
  for (final e in plan.changes) {
    out.writeln('${e.key}  ${e.at.toIso8601String()}  ${e.reason.label}');
  }
  if (!apply) {
    out.writeln('${plan.changes.length} to restore. Add --apply to write.');
    return 0;
  }
  await tools.applyRestore(plan);
  out.writeln('Restored ${plan.changes.length}.');
  return 0;
}

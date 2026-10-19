import 'dart:io';

import 'package:orblit_save/src/history_cli.dart';

Future<void> main(List<String> args) async {
  exitCode = await runHistory(
    args,
    out: stdout,
    err: stderr,
    clock: DateTime.now,
  );
}

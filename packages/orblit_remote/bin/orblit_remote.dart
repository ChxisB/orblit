/// Serves a running game's control surface to an assistant over stdin and
/// stdout, as tools.
///
///     dart run orblit_remote ws://127.0.0.1:PORT/?token=TOKEN [--allow-write]
///
/// The address is the line the game prints when it starts the server, with or
/// without its `orblit-remote` prefix. When no address is given on the command
/// line, `ORBLIT_REMOTE_ADDRESS` holds it, which keeps the token out of the
/// process list. Messages are one JSON object per line. Nothing but messages
/// goes to stdout. Notes go to stderr.
library;

import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:orblit_remote/src/remote_client.dart';
import 'package:orblit_remote/src/tools.dart';
import 'package:orblit_remote/src/verbs.dart';

const _usage =
    'usage: orblit_remote [--allow-write] ws://127.0.0.1:PORT/?token=TOKEN\n'
    'The host must be 127.0.0.1 or localhost. Quote the address in a shell.\n'
    'Without an address, ORBLIT_REMOTE_ADDRESS is read.';

Future<void> main(List<String> arguments) async {
  // The pipe's encoding is the system's by default, which is not UTF-8 on
  // every platform.
  stdout.encoding = utf8;
  final allowWrite = arguments.contains('--allow-write');
  final words = arguments.where((a) => a != '--allow-write').toList();
  final address = _address(words, Platform.environment);
  if (address == null) {
    stderr.writeln(_usage);
    exitCode = 64;
    return;
  }
  final RemoteClient client;
  try {
    client = await RemoteClient.connect(address);
  } on RemoteError catch (error) {
    // exit, not a return: a connect that timed out may still be pending, and
    // an open socket would keep the process alive.
    stderr.writeln(error.message);
    await stderr.flush();
    exit(69);
  }
  stderr.writeln(
    'orblit_remote: connected to ${address.host}:${address.port}, '
    '${allowWrite ? 'writes allowed' : 'read only'}',
  );
  var ending = false;
  unawaited(
    client.closed.then((_) async {
      if (ending) return;
      stderr.writeln('orblit_remote: the game closed the connection');
      await Future.wait([stdout.flush(), stderr.flush()]);
      exit(69);
    }),
  );
  await _serve(ToolServer(client.call, allowWrite: allowWrite));
  ending = true;
  await client.close();
}

/// The `ws://` address from [words], or from the environment when [words] is
/// empty. Null for none, a bad one, an unknown flag, or a host that is not
/// this machine. The token is sent to whatever host is named, so the host is
/// checked here.
Uri? _address(List<String> words, Map<String, String> environment) {
  if (words.any((word) => word.startsWith('--'))) return null;
  final given = words.join(' ').trim();
  final text = given.isEmpty ? environment['ORBLIT_REMOTE_ADDRESS'] : given;
  final uri = Uri.tryParse(text?.trim().split(RegExp(r'\s+')).last ?? '');
  if (uri == null || uri.scheme != 'ws' || !uri.hasPort) return null;
  return const {'127.0.0.1', 'localhost'}.contains(uri.host) ? uri : null;
}

// shortcut: one message at a time, so a long step holds up a ping behind it.
// Add concurrent handling when a client needs it.
Future<void> _serve(ToolServer server) async {
  // A stray byte must come out as a parse error, not end the session.
  final lines = stdin
      .transform(const Utf8Decoder(allowMalformed: true))
      .transform(const LineSplitter());
  await for (final line in lines) {
    if (line.trim().isEmpty) continue;
    final reply = await server.handle(line);
    if (reply != null) stdout.writeln(reply);
  }
  await stdout.flush();
}

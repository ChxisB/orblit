/// Serves a running game's control surface to an assistant over stdin and
/// stdout, as tools.
///
///     dart run orblit_remote ws://127.0.0.1:PORT/?token=TOKEN [--allow-write]
///
/// The address is the line the game prints when it starts the server, with or
/// without its `orblit-remote` prefix. Messages are one JSON object per line.
/// Nothing but messages goes to stdout. Notes go to stderr.
library;

import 'dart:convert';
import 'dart:io';

import 'package:orblit_remote/src/remote_client.dart';
import 'package:orblit_remote/src/tools.dart';
import 'package:orblit_remote/src/verbs.dart';

const _usage =
    'usage: orblit_remote [--allow-write] ws://127.0.0.1:PORT/?token=TOKEN\n'
    'The host must be 127.0.0.1 or localhost. Quote the address in a shell.';

Future<void> main(List<String> arguments) async {
  final allowWrite = arguments.contains('--allow-write');
  final address = _address(arguments.where((a) => a != '--allow-write'));
  if (address == null) {
    stderr.writeln(_usage);
    exitCode = 64;
    return;
  }
  final RemoteClient client;
  try {
    client = await RemoteClient.connect(address);
  } on RemoteError catch (error) {
    stderr.writeln(error.message);
    exitCode = 69;
    return;
  }
  stderr.writeln(
    'orblit_remote: connected to ${address.host}:${address.port}, '
    '${allowWrite ? 'writes allowed' : 'read only'}',
  );
  await _serve(ToolServer(client.call, allowWrite: allowWrite));
  await client.close();
}

/// The `ws://` address in [arguments], or null for none, a bad one, or a host
/// that is not this machine. The token is sent to whatever host is named, so
/// the host is checked here.
Uri? _address(Iterable<String> arguments) {
  final words = arguments.join(' ').trim().split(RegExp(r'\s+'));
  final uri = Uri.tryParse(words.last);
  if (uri == null || uri.scheme != 'ws' || !uri.hasPort) return null;
  return const {'127.0.0.1', 'localhost'}.contains(uri.host) ? uri : null;
}

// shortcut: one message at a time, so a long step holds up a ping behind it.
// Add concurrent handling when a client needs it.
Future<void> _serve(ToolServer server) async {
  final lines = stdin.transform(utf8.decoder).transform(const LineSplitter());
  await for (final line in lines) {
    if (line.trim().isEmpty) continue;
    final reply = await server.handle(line);
    if (reply != null) stdout.writeln(reply);
  }
  await stdout.flush();
}

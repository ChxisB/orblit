import 'dart:io';

import 'package:orblit_asset/orblit_asset.dart';

/// Packs a folder as a kit, checks one and installs one into a project.
///
/// Usage:
///   `dart run orblit_asset:kit pack <folder> -o <file>`
///   `dart run orblit_asset:kit check <file>`
///   `dart run orblit_asset:kit extract <file> <project> [--overwrite]`
///
/// Exit codes: 0 when it worked, 1 when a kit or a folder was bad or an
/// install would replace a file, 2 when the arguments did not make sense.
Future<void> main(List<String> arguments) async {
  if (arguments.isEmpty || arguments.first == '--help') {
    stdout.write(_usage);
    exit(arguments.isEmpty ? 2 : 0);
  }
  final rest = arguments.skip(1).toList();
  try {
    switch (arguments.first) {
      case 'pack':
        await _pack(rest);
      case 'check':
        await _check(_only(arguments.first, rest, 1).single);
      case 'extract':
        await _extract(rest);
      default:
        _usageError('unknown command "${arguments.first}"');
    }
  } on KitConflicts catch (error) {
    stderr.writeln('kit: $error');
    stderr.writeln('kit: run again with --overwrite to replace them.');
    exit(1);
  } on FormatException catch (error) {
    stderr.writeln('kit: ${error.message}');
    exit(1);
  } on ArgumentError catch (error) {
    stderr.writeln('kit: ${[?error.name, '${error.message}'].join(' ')}');
    exit(1);
  } on FileSystemException catch (error) {
    stderr.writeln('kit: ${error.message}: ${error.path}');
    exit(1);
  }
}

String get _usage => '''
Usage: dart run orblit_asset:kit <command>

  pack <folder> -o <file>
      Packs <folder> as a kit. The folder holds the files to give a project
      and a kit.json. A hidden file, such as .DS_Store, stops it.
  check <file>
      Reads the manifest, applies the kit rules and hashes every blob.
  extract <file> <project> [--overwrite]
      Writes the files of a kit into <project>. A file the project already
      has with other contents stops it, unless --overwrite is given.
''';

Never _usageError(String message) {
  stderr.writeln('kit: $message');
  stderr.write(_usage);
  exit(2);
}

List<String> _only(String command, List<String> rest, int count) {
  if (rest.length != count) _usageError('$command takes $count arguments');
  return rest;
}

Future<void> _pack(List<String> arguments) async {
  String? folder;
  String? out;
  for (var i = 0; i < arguments.length; i++) {
    if (arguments[i] == '-o' || arguments[i] == '--out') {
      if (++i >= arguments.length) _usageError('-o needs a value');
      out = arguments[i];
    } else {
      if (folder != null) _usageError('one folder at a time');
      folder = arguments[i];
    }
  }
  if (folder == null || out == null) _usageError('pack needs a folder and -o');
  final summary = await packKit(folder: folder, outPath: out);
  stdout.writeln(
    '${summary.assets - 1} files, ${summary.bytes} bytes\n'
    '${summary.hash}  $out',
  );
}

Future<Kit> _open(FilePackBytes source, String path) async {
  if (!File(path).existsSync()) {
    throw FileSystemException('There is no such file', path);
  }
  return Kit.open(await AssetPack.open(source));
}

Future<void> _check(String path) async {
  final source = FilePackBytes(path);
  try {
    final kit = await _open(source, path);
    final problems = await kit.check();
    if (problems.isNotEmpty) {
      for (final line in problems) {
        stderr.writeln('  $line');
      }
      stderr.writeln('kit: ${problems.length} problems.');
      exit(1);
    }
    final manifest = kit.manifest;
    stdout.writeln(
      '${manifest.name} ${manifest.version}, ${kit.files.length} files, '
      'all intact${manifest.containsScripts ? ', holds scripts' : ''}',
    );
  } finally {
    await source.close();
  }
}

Future<void> _extract(List<String> arguments) async {
  final overwrite = arguments.contains('--overwrite');
  final paths = _only('extract', [
    for (final argument in arguments)
      if (argument != '--overwrite') argument,
  ], 2);
  final source = FilePackBytes(paths[0]);
  try {
    final kit = await _open(source, paths[0]);
    final written = await extractKit(kit, paths[1], overwrite: overwrite);
    stdout.writeln('${written.length} files written to ${paths[1]}');
  } finally {
    await source.close();
  }
}

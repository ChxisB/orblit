import 'dart:convert';
import 'dart:io';

import 'package:orblit_asset/orblit_asset.dart';

/// Packs a folder as a kit, checks one, installs one into a project, lists a
/// kit for the store and builds the store index from a folder of entries.
///
/// Usage:
///   `dart run orblit_asset:kit pack <folder> -o <file>`
///   `dart run orblit_asset:kit check <file>`
///   `dart run orblit_asset:kit extract <file> <project> [--overwrite]`
///   `dart run orblit_asset:kit publish <folder> --url <url> [-o <file>]`
///   `        [--entries <folder>] [--notes <text>]`
///   `dart run orblit_asset:kit index <entries> -o <folder>`
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
      case 'publish':
        await _publish(rest);
      case 'index':
        await _index(rest);
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
  publish <folder> --url <url> [-o <file>] [--entries <folder>]
          [--notes <text>]
      Packs <folder>, checks the file as CI will, and prints the entry that
      lists it for the store. <url> is where you will host the file. The
      file is named after the last part of <url>, or by -o. With --entries,
      the entry is written to <entries>/<publisher>/<slug>.json. Nothing is
      uploaded and no git command runs.
  index <entries> -o <folder>
      Downloads every release the entries in <entries> list, checks each
      one, and writes index.json and the kit pages into <folder>, which must
      not exist yet. Any bad release stops it and writes nothing.
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

/// A folder and the `-o` that goes with it, for the commands that write one.
({String folder, String out}) _folderAndOut(
  String command,
  List<String> arguments,
) {
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
  if (folder == null || out == null) {
    _usageError('$command needs a folder and -o');
  }
  return (folder: folder, out: out);
}

Future<void> _pack(List<String> arguments) async {
  final (:folder, :out) = _folderAndOut('pack', arguments);
  final summary = await packKit(folder: folder, outPath: out);
  stdout.writeln(
    '${summary.assets - 1} files, ${summary.bytes} bytes\n'
    '${summary.hash}  $out',
  );
}

/// A file name a url can end in and a shell can take as it stands.
final RegExp _plainName = RegExp(r'^[A-Za-z0-9][A-Za-z0-9._-]*$');

/// The folder and the options of `publish`, with `--out` spelt `-o`.
({String folder, Map<String, String> options}) _publishArguments(
  List<String> arguments,
) {
  const named = {'--url', '-o', '--out', '--entries', '--notes'};
  String? folder;
  final options = <String, String>{};
  for (var i = 0; i < arguments.length; i++) {
    final argument = arguments[i];
    if (named.contains(argument)) {
      if (++i >= arguments.length) _usageError('$argument needs a value');
      options[argument == '--out' ? '-o' : argument] = arguments[i];
    } else if (argument.startsWith('-')) {
      _usageError('unknown option "$argument"');
    } else if (folder != null) {
      _usageError('one folder at a time');
    } else {
      folder = argument;
    }
  }
  if (folder == null) _usageError('publish needs a folder');
  return (folder: folder, options: options);
}

/// The file `publish` writes: `-o`, or the last part of [url] when it is a
/// plain name.
String _publishedFile(Map<String, String> options, String url) {
  final out = options['-o'];
  if (out != null) return out;
  final segments = Uri.tryParse(url)?.pathSegments ?? const <String>[];
  final name = segments.isEmpty ? '' : segments.last;
  if (!_plainName.hasMatch(name)) {
    _usageError('the url does not end in a file name, so give one with -o');
  }
  return name;
}

Future<void> _publish(List<String> arguments) async {
  final (:folder, :options) = _publishArguments(arguments);
  final url = options['--url'];
  if (url == null) _usageError('publish needs --url, where the file will be');
  final file = _publishedFile(options, url);
  final entries = options['--entries'];
  final published = await publishKit(
    folder: folder,
    url: url,
    outPath: file,
    // The clock is read here, where the command is wired.
    released: DateTime.now().toUtc(),
    notes: options['--notes'] ?? '',
    entriesPath: entries,
  );
  final entry = published.entry;
  stdout.writeln(
    'Packed $file: ${published.summary.assets - 1} files, '
    '${published.summary.bytes} bytes\n'
    '${published.summary.hash}\n\n'
    'The entry for ${entry.publisher}/${entry.slug}:\n'
    '${utf8.decode(entry.encode())}',
  );
  stdout.write(_nextSteps(file, url, entries, published.entryPath));
}

/// What the person does after `publish`, which runs none of it.
String _nextSteps(String file, String url, String? entries, String? written) {
  final entry = written == null
      ? 'Save the entry above as kits/<publisher>/<slug>.json in the '
            'orblit-kits repository. If that file lists releases already, '
            'add this release to them instead.'
      : 'Wrote the entry to $written.';
  return '''
$entry

Next, by hand:
  1. Upload $file so that it is served at $url
  2. Check the listing with kit index ${entries ?? 'kits'} -o <new folder>
  3. Commit the entry and open a pull request.
''';
}

Future<void> _index(List<String> arguments) async {
  final (:folder, :out) = _folderAndOut('index', arguments);
  final transport = HttpTransport();
  try {
    final built = await buildKitStore(
      entriesPath: folder,
      outPath: out,
      transport: transport,
    );
    stdout.writeln(
      '${built.kits} kits, ${built.releases} releases written to $out',
    );
  } finally {
    transport.close();
  }
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

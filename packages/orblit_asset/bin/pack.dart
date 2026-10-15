import 'dart:io';

import 'package:orblit_asset/orblit_asset.dart';

/// Packs a cooked bundle into one `.opack` file, and looks inside one.
///
/// Usage:
///   `dart run orblit_asset:pack build <bundle> --target <name> -o <file>`
///   `... --base <pack>` once for each pack shipped, to write a patch.
///   `dart run orblit_asset:pack list <file>`
///   `dart run orblit_asset:pack check <file>`
///
/// Exit codes: 0 when it worked, 1 when a pack or bundle was bad, 2 when the
/// arguments did not make sense. A build reads the code. The text is for a
/// person.
Future<void> main(List<String> arguments) async {
  if (arguments.isEmpty || arguments.first == '--help') {
    stdout.write(_usage);
    exit(arguments.isEmpty ? 2 : 0);
  }
  try {
    switch (arguments.first) {
      case 'build':
        await _build(arguments.skip(1).toList());
      case 'list':
        await _list(_onlyPath(arguments));
      case 'check':
        await _check(_onlyPath(arguments));
      default:
        _usageError('unknown command "${arguments.first}"');
    }
  } on FormatException catch (error) {
    stderr.writeln('pack: ${error.message}');
    exit(1);
  } on PackMountRefused catch (error) {
    stderr.writeln('pack: ${error.message}');
    exit(1);
  } on FileSystemException catch (error) {
    stderr.writeln('pack: ${error.message}: ${error.path}');
    exit(1);
  }
}

String get _usage =>
    '''
Usage: dart run orblit_asset:pack <command>

  build <bundle> --target <name> -o <file> [--base <pack>]...
      Packs the cooked bundle in <bundle> for one target.
      The target is one of: ${CookTargets.names.join(", ")}
      With --base, writes a patch against the packs already shipped. Name
      them in the order they are mounted, the whole pack first.
  list <file>
      Prints what a pack holds.
  check <file>
      Hashes every blob. Exits 1 when a byte has changed.
''';

Never _usageError(String message) {
  stderr.writeln('pack: $message');
  stderr.write(_usage);
  exit(2);
}

String _onlyPath(List<String> arguments) {
  if (arguments.length != 2) _usageError('${arguments.first} takes one file');
  return arguments[1];
}

Future<void> _build(List<String> arguments) async {
  String? bundle;
  String? out;
  CookTarget? target;
  final bases = <String>[];
  for (var i = 0; i < arguments.length; i++) {
    switch (arguments[i]) {
      case '--target':
        target = CookTargets.find(_value(arguments, i++));
        if (target == null) {
          _usageError(
            'that is not a target. The targets are '
            '${CookTargets.names.join(', ')}',
          );
        }
      case '-o' || '--out':
        out = _value(arguments, i++);
      case '--base':
        bases.add(_value(arguments, i++));
      default:
        if (bundle != null) _usageError('one bundle at a time');
        bundle = arguments[i];
    }
  }
  if (bundle == null || out == null || target == null) {
    _usageError('build needs a bundle, --target and -o');
  }
  _checkTargetMatchesFolder(bundle, target);

  final summary = await _packOver(bundle, out, target, bases);
  final changes = bases.isEmpty
      ? ''
      : ', ${summary.added} new, ${summary.replaced} replaced, '
            '${summary.removed} removed';
  stdout.writeln(
    '${summary.assets} assets in ${summary.blobs} blobs, '
    '${summary.bytes} bytes, ${summary.sharedBytes} saved by sharing'
    '$changes\n'
    '${summary.hash}  $out',
  );
}

/// Packs [bundle], opening each of [bases] and closing them when it is done.
Future<PackSummary> _packOver(
  String bundle,
  String out,
  CookTarget target,
  List<String> bases,
) async {
  final sources = <FilePackBytes>[];
  try {
    final packs = <AssetPack>[];
    for (final path in bases) {
      if (!File(path).existsSync()) {
        throw FileSystemException('There is no such file', path);
      }
      final source = FilePackBytes(path);
      sources.add(source);
      packs.add(await AssetPack.open(source));
    }
    return await packBundle(
      bundlePath: bundle,
      outPath: out,
      target: target,
      base: packs,
    );
  } finally {
    for (final source in sources) {
      await source.close();
    }
  }
}

String _value(List<String> arguments, int index) {
  if (index + 1 >= arguments.length) {
    _usageError('${arguments[index]} needs a value');
  }
  return arguments[index + 1];
}

/// A bundle folder is named for its target, so one that says otherwise is a
/// mistake that would pack ASTC textures as a macOS pack.
void _checkTargetMatchesFolder(String bundle, CookTarget target) {
  final folder = Directory(
    bundle,
  ).absolute.uri.pathSegments.where((part) => part.isNotEmpty).last;
  final named = CookTargets.find(folder);
  if (named != null && named.name != target.name) {
    _usageError(
      '"$bundle" holds the ${named.name} bundle, and --target says '
      '${target.name}',
    );
  }
}

Future<AssetPack> _open(String path) async {
  if (!File(path).existsSync()) {
    throw FileSystemException('There is no such file', path);
  }
  return AssetPack.open(FilePackBytes(path));
}

Future<void> _list(String path) async {
  final pack = await _open(path);
  stdout.writeln('target   ${pack.target}');
  stdout.writeln('hash     ${pack.hash}');
  stdout.writeln('base     ${pack.base ?? 'none'}');
  stdout.writeln('assets   ${pack.manifest.entries.length}');
  stdout.writeln('removed  ${pack.removed.length}');
  final ids = pack.manifest.entries.keys.toList()
    ..sort((a, b) => a.toString().compareTo(b.toString()));
  for (final id in ids) {
    final entry = pack.manifest.entries[id]!;
    stdout.writeln(
      '  ${entry.hash.hex.substring(0, 12)}  '
      '${entry.bytes.toString().padLeft(10)}  $id',
    );
  }
  for (final id in pack.removed) {
    stdout.writeln('  removed  $id');
  }
}

Future<void> _check(String path) async {
  final pack = await _open(path);
  final corrupt = await pack.findCorrupt();
  if (corrupt.isEmpty) {
    stdout.writeln('${pack.manifest.entries.length} assets, all intact');
    return;
  }
  for (final id in corrupt) {
    stderr.writeln('  changed  $id');
  }
  stderr.writeln('pack: ${corrupt.length} assets do not match their hash.');
  exit(1);
}

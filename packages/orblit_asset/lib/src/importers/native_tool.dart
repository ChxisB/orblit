import 'dart:io';

import '../asset_id.dart';
import '../importer.dart';

/// One of the native programs the cook drives.
///
/// The importers do not encode anything themselves. Texture compression,
/// mesh import and splat quantisation are all C++ that already exists, is
/// already tested, and is already what the by-hand scripts in `tool/` call.
/// An importer's job is to decide the flags, run the program and file what it
/// wrote — so that the thing a build does and the thing a person does by hand
/// stay the same thing.
class NativeTool {
  NativeTool(this.name, {String? path, this.environmentVariable})
    : _path = path;

  /// `orblit_texture_cook`, `cmgen`. Used in errors and to find the program.
  final String name;

  /// The environment variable that overrides where it is, following the
  /// convention `tool/cook_textures.sh` and `tool/bake_environment.sh`
  /// already set: `ORBLIT_TEXTURE_COOK`, `ORBLIT_CMGEN`.
  final String? environmentVariable;

  final String? _path;

  /// Where the built programs are looked for when nothing says otherwise.
  ///
  /// Settable because a build machine puts them somewhere else, and because a
  /// test needs to point at a program it wrote itself. A colon-separated
  /// `ORBLIT_TOOL_PATH` adds to it without any code change.
  static List<String> searchPath = [
    ...?Platform.environment['ORBLIT_TOOL_PATH']?.split(':'),
    'packages/orblit_filament/native/headless/build',
    'packages/orblit_filament/native/texture_cook/build',
    'packages/orblit_filament/darwin/third_party/filament-mac/filament/bin',
  ];

  /// The program, or null when it is not where it should be.
  ///
  /// Resolved every time rather than cached, because a cook run from an editor
  /// can outlive a rebuild of the tools it drives.
  String? locate() {
    if (_path != null) return _path;

    final override = environmentVariable == null
        ? null
        : Platform.environment[environmentVariable!];
    if (override != null && override.isNotEmpty) return override;

    for (final directory in searchPath) {
      final candidate = '$directory${Platform.pathSeparator}$name';
      final file = File(candidate);
      if (file.existsSync()) return file.absolute.path;
    }

    // Last: whatever is on PATH, which is how cmgen usually arrives.
    final which = Process.runSync(Platform.isWindows ? 'where' : 'which', [
      name,
    ], runInShell: true);
    if (which.exitCode == 0) {
      final found = (which.stdout as String).split('\n').first.trim();
      if (found.isNotEmpty) return found;
    }
    return null;
  }

  /// Runs it, and turns a bad exit into an [ImportFailure] that says what was
  /// run and what the program printed.
  ///
  /// A tool's own diagnostics are the useful part of an import failure —
  /// "unsupported PNG bit depth" tells someone what to do, and "exit code 1"
  /// does not — so they are carried through rather than summarised.
  Future<void> run(List<String> arguments, {required AssetId on}) async {
    final program = locate();
    if (program == null) {
      throw ImportFailure(
        on,
        'needs $name, which is not built. Look for it in '
        '${searchPath.join(', ')}'
        '${environmentVariable == null ? '' : ', or set $environmentVariable'}.',
      );
    }

    final result = await Process.run(program, arguments);
    if (result.exitCode != 0) {
      final said = [
        (result.stderr as String).trim(),
        (result.stdout as String).trim(),
      ].where((text) => text.isNotEmpty).join('\n');
      throw ImportFailure(
        on,
        '$name exited ${result.exitCode}.\n'
        '  $program ${arguments.join(' ')}',
        cause: said.isEmpty ? null : said,
      );
    }
  }
}

/// A directory that exists for one import and is gone afterwards.
///
/// The native tools read and write files, and an importer deals in bytes, so
/// every one of them needs this. It is here rather than repeated so that the
/// cleanup is written once — a cook that fails partway must not leave a
/// half-written file where the next run could pick it up.
Future<T> withTemporaryDirectory<T>(
  String prefix,
  Future<T> Function(Directory directory) body,
) async {
  final directory = await Directory.systemTemp.createTemp('orblit_$prefix');
  try {
    return await body(directory);
  } finally {
    try {
      await directory.delete(recursive: true);
    } on FileSystemException {
      // A temporary directory that outlives the process is untidy, not
      // broken, and the operating system clears it eventually. Failing the
      // import over it would turn a successful cook into a failure.
    }
  }
}

/// A path to [name] inside [directory].
///
/// Written out because the native tools take paths, every importer therefore
/// builds some, and a separator dropped into a string interpolation is a
/// mistake that only shows up on the platform nobody tested on.
String inside(Directory directory, String name) => beside(directory.path, name);

/// A path to [name] inside the directory at [path].
String beside(String path, String name) =>
    '$path${Platform.pathSeparator}$name';

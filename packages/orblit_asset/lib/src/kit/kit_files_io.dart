// The half of the conditional export in kit_files.dart for platforms with a
// file system.
import 'dart:io';
import 'dart:typed_data';

import '../asset_id.dart';
import '../asset_manifest.dart';
import '../content_hash.dart';
import '../directory.dart';
import '../pack/pack_bundle_io.dart';
import '../pack/pack_summary.dart';
import 'kit.dart';
import 'kit_manifest.dart';
import 'kit_rules.dart';

/// Packs [folder] as one kit file at [outPath].
///
/// The folder holds the files a person should get and a `kit.json` the author
/// wrote. The tool fills in `containsScripts` itself, from the files. A folder
/// that breaks a rule of [kitProblems], or holds a link, is refused before any
/// file is read for its bytes. Throws a [FormatException] that lists every
/// problem.
Future<PackSummary> packKit({
  required String folder,
  required String outPath,
}) async {
  final root = Directory(folder);
  if (!root.existsSync()) {
    throw ArgumentError.value(folder, 'folder', 'is not a directory');
  }
  final sizes = await _walk(root);
  final authored = File('${root.path}${Platform.pathSeparator}kit.json');
  if (!authored.existsSync()) {
    throw FormatException('"$folder" has no kit.json.');
  }
  final manifest = KitManifest.readAuthored(
    await authored.readAsBytes(),
    containsScripts: carriesScripts(sizes.keys),
  );
  final problems = kitProblems(manifest, sizes);
  if (problems.isNotEmpty) throw FormatException(problems.join('\n'));
  final packed = manifest.encode();
  Future<Uint8List> read(AssetId id) => id == kitManifestId
      ? Future.value(packed)
      : File(_pathOf(root.path, id)).readAsBytes();
  return writePack(
    manifest: AssetManifest(
      entries: {
        for (final id in sizes.keys)
          id: AssetEntry(ContentHash.of(await read(id)), sizes[id]!),
        kitManifestId: AssetEntry(ContentHash.of(packed), packed.length),
      },
    ),
    outPath: outPath,
    target: kitTarget,
    read: read,
  );
}

/// Every file under [root] with its size, except the manifest.
Future<Map<AssetId, int>> _walk(Directory root) async {
  final sizes = <AssetId, int>{};
  final prefix = '${root.path}${Platform.pathSeparator}';
  await for (final entity in root.list(recursive: true, followLinks: false)) {
    if (entity is Link) {
      throw FormatException('"${entity.path}" is a link. A kit holds files.');
    }
    if (entity is! File) continue;
    final relative = entity.path
        .substring(prefix.length)
        .replaceAll(Platform.pathSeparator, '/');
    if (relative == '$kitManifestId') continue;
    sizes[AssetId.parse(relative)] = await entity.length();
  }
  return sizes;
}

String _pathOf(String root, AssetId id) =>
    [root, ...id.toString().split('/')].join(Platform.pathSeparator);

/// The files an install would replace. Nothing was written when it is thrown.
final class KitConflicts implements Exception {
  const KitConflicts(this.ids);

  final List<AssetId> ids;

  @override
  String toString() =>
      'These files are already in the project with other contents: '
      '${ids.join(', ')}.';
}

/// Writes the files of [kit] into the folder [projectPath] and answers which
/// it wrote.
///
/// A file the project already has with other bytes stops the install with a
/// [KitConflicts] before anything is written, unless [overwrite] is set. A
/// file that is there and identical is left alone. The manifest is not
/// written. Each blob is hashed again on the way out, a folder is created one
/// step at a time, and a link anywhere on the way, in the project or at the
/// file, is refused, so a kit cannot write outside the project through one.
///
/// shortcut: files are written one at a time, so a failure half way leaves the
/// earlier ones. Running the install again is safe, because identical files
/// are skipped. Stage the whole kit and swap it in if that matters.
Future<List<AssetId>> extractKit(
  Kit kit,
  String projectPath, {
  bool overwrite = false,
}) async {
  if (!Directory(projectPath).existsSync()) {
    throw ArgumentError.value(projectPath, 'projectPath', 'is not a directory');
  }
  final root = await Directory(projectPath).resolveSymbolicLinks();
  final conflicts = await kit.conflictsIn(DirectoryAssetSource(root));
  if (conflicts.isNotEmpty && !overwrite) throw KitConflicts(conflicts);
  final written = <AssetId>[];
  for (final MapEntry(:key, :value) in kit.pack.manifest.entries.entries) {
    if (key == kitManifestId) continue;
    final bytes = await kit.pack.read(key);
    if (ContentHash.of(bytes) != value.hash) {
      throw FormatException('"$key" does not match its hash in the kit.');
    }
    final path = _pathOf(root, key);
    if (await _identical(path, value.hash)) continue;
    _makeFolders(root, key);
    if (FileSystemEntity.typeSync(path, followLinks: false) ==
        FileSystemEntityType.link) {
      throw FormatException('"$key" is a link in the project.');
    }
    final partial = File('$path.partial');
    await partial.writeAsBytes(bytes);
    await partial.rename(path);
    written.add(key);
  }
  return written;
}

Future<bool> _identical(String path, ContentHash hash) async {
  final file = File(path);
  return file.existsSync() && ContentHash.of(await file.readAsBytes()) == hash;
}

/// Creates the folders above [id] one at a time, never going through a link.
void _makeFolders(String root, AssetId id) {
  var path = root;
  final parts = id.toString().split('/')..removeLast();
  for (final part in parts) {
    path = '$path${Platform.pathSeparator}$part';
    switch (FileSystemEntity.typeSync(path, followLinks: false)) {
      case FileSystemEntityType.directory:
        break;
      case FileSystemEntityType.notFound:
        Directory(path).createSync();
      default:
        throw FormatException(
          '"$path" is in the way of "$id" and is not a plain folder.',
        );
    }
  }
}

// The half of the conditional export in kit_files.dart for platforms with a
// file system.
import 'dart:io';
import 'dart:math';
import 'dart:typed_data';

import '../asset_id.dart';
import '../asset_manifest.dart';
import '../content_hash.dart';
import '../directory.dart';
import '../pack/asset_pack.dart';
import '../pack/pack_bundle_io.dart';
import '../pack/pack_file_io.dart';
import '../pack/pack_summary.dart';
import 'kit.dart';
import 'kit_entry.dart';
import 'kit_manifest.dart';
import 'kit_published.dart';
import 'kit_rules.dart';

/// Packs [folder] as one kit file at [outPath].
///
/// The folder holds the files a person should get and a `kit.json` the author
/// wrote. The tool fills in `containsScripts` itself, from the files. A folder
/// that breaks a rule of [kitProblems], or holds a link, is refused before any
/// file is read for its bytes. A kit file left inside the folder by an earlier
/// run is not packed into the next. Throws a [FormatException] that lists
/// every problem.
Future<PackSummary> packKit({
  required String folder,
  required String outPath,
}) async {
  final root = Directory(folder);
  if (!root.existsSync()) {
    throw ArgumentError.value(folder, 'folder', 'is not a directory');
  }
  final sizes = await _walk(root, await _outputs(outPath));
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
  final entries = {
    for (final id in sizes.keys)
      id: AssetEntry(ContentHash.of(await read(id)), sizes[id]!),
    kitManifestId: AssetEntry(ContentHash.of(packed), packed.length),
  };
  try {
    return await writePack(
      manifest: AssetManifest(entries: entries),
      outPath: outPath,
      target: kitTarget,
      read: read,
    );
  } on FormatException catch (error) {
    throw FormatException(
      '${error.message} Something in "$folder" changed while the kit was '
      'written. Pack it again.',
    );
  }
}

/// Packs [folder] as the kit file [outPath] and answers the entry that lists
/// it, hosted at [url].
///
/// The file is opened again and checked, as CI will check it, so a file that
/// does not read back is refused here. The release takes its version from the
/// kit's own manifest and its hash and size from the file. [released] gives
/// the day, as its own clock spells it, and [notes] is the one line of change
/// notes.
///
/// With [entriesPath], the folder of entries `kit index` reads, the entry
/// file `<publisher>/<slug>.json` is written there, with the release added to
/// the ones it listed. A version already listed is refused, because a listed
/// release is not edited. The entry is written whole by rename. Without it,
/// only the kit file is written. Nothing is uploaded.
///
/// The kit file is packed beside [outPath] and moved into place only when
/// every check has passed, so a refusal leaves [outPath] as it was. Throws a
/// [FormatException] for a kit, a url, a note or an entry that is refused, and
/// for an [outPath] inside [folder], where the next pack would take it in.
Future<KitPublished> publishKit({
  required String folder,
  required String url,
  required String outPath,
  required DateTime released,
  String notes = '',
  String? entriesPath,
}) async {
  if (entriesPath != null && !Directory(entriesPath).existsSync()) {
    throw ArgumentError.value(entriesPath, 'entriesPath', 'is not a directory');
  }
  await _refuseInside(folder, outPath);
  final staged = '$outPath.staged';
  try {
    final summary = await packKit(folder: folder, outPath: staged);
    final manifest = await _readBack(staged, summary);
    final release = KitRelease(
      version: manifest.version,
      url: url,
      hash: summary.hash,
      bytes: summary.bytes,
      released: released.toIso8601String().substring(0, 10),
      notes: notes,
    );
    final path = entriesPath == null ? null : _entryFile(entriesPath, manifest);
    final entry = await _entryWith(path, manifest, release);
    await File(staged).rename(outPath);
    if (path != null) {
      File(path).parent.createSync(recursive: true);
      await _write(path, entry.encode());
    }
    return KitPublished(
      summary: summary,
      release: release,
      entry: entry,
      entryPath: path,
    );
  } finally {
    final left = File(staged);
    if (left.existsSync()) left.deleteSync();
  }
}

/// Where the entry of [manifest] lives in the entries folder [root].
String _entryFile(String root, KitManifest manifest) => [
  root,
  manifest.publisher,
  '${manifest.slug}.json',
].join(Platform.pathSeparator);

/// Throws when a kit file at [outPath] would sit inside [folder].
Future<void> _refuseInside(String folder, String outPath) async {
  if (!Directory(folder).existsSync()) return; // packKit names this problem.
  final root = await Directory(folder).resolveSymbolicLinks();
  final parent = await File(outPath).absolute.parent.resolveSymbolicLinks();
  final inside =
      parent == root || parent.startsWith('$root${Platform.pathSeparator}');
  if (inside) {
    throw FormatException(
      'The kit file "$outPath" would be inside "$folder", and the next pack '
      'would take it in. Put it in another folder.',
    );
  }
}

/// The manifest of the kit file at [path], after every blob in it has been
/// hashed again. Throws when the file is not the one [written] describes.
Future<KitManifest> _readBack(String path, PackSummary written) async {
  final source = FilePackBytes(path);
  try {
    final pack = await AssetPack.open(source);
    if (pack.hash != written.hash || File(path).lengthSync() != written.bytes) {
      throw FormatException('"$path" is not the file that was written.');
    }
    final kit = await Kit.open(pack);
    final problems = await kit.check();
    if (problems.isNotEmpty) throw FormatException(problems.join('\n'));
    return kit.manifest;
  } finally {
    await source.close();
  }
}

/// The entry at [path], or a new one when it is not there, with [release]
/// added. Throws when [release] is listed already or the entry would be over
/// the size `kit index` reads.
Future<KitEntry> _entryWith(
  String? path,
  KitManifest manifest,
  KitRelease release,
) async {
  final file = path == null ? null : File(path);
  final listed = file == null || !file.existsSync()
      ? const <KitRelease>[]
      : KitEntry.parse(
          await file.readAsBytes(),
          publisher: manifest.publisher,
          slug: manifest.slug,
        ).releases;
  if (listed.any((held) => held.version == release.version)) {
    throw FormatException(
      '${manifest.publisher}/${manifest.slug} lists ${release.version} '
      'already. A listed release is not edited. Raise "version" in kit.json.',
    );
  }
  final entry = KitEntry(
    publisher: manifest.publisher,
    slug: manifest.slug,
    releases: [...listed, release],
  );
  if (entry.encode().length > kitEntryLimit) {
    throw FormatException(
      '${manifest.publisher}/${manifest.slug} would be over $kitEntryLimit '
      'bytes, which is more than kit index reads.',
    );
  }
  return entry;
}

/// The paths of the kit file and its temporary file, so a run does not pack
/// its own output.
///
/// shortcut: compared as the file system spells them, so an output named with
/// other letter case than it has on a case insensitive disk is packed. Fold
/// the case when that bites.
Future<Set<String>> _outputs(String outPath) async {
  final file = File(outPath);
  final parent = await file.parent.resolveSymbolicLinks();
  final name = file.uri.pathSegments.last;
  final path = '$parent${Platform.pathSeparator}$name';
  return {path, '$path.partial'};
}

/// Every file under [root] with its size, except the manifest and [skip].
Future<Map<AssetId, int>> _walk(Directory root, Set<String> skip) async {
  final sizes = <AssetId, int>{};
  final real = Directory(await root.resolveSymbolicLinks());
  final prefix = real.path.endsWith(Platform.pathSeparator)
      ? real.path
      : '${real.path}${Platform.pathSeparator}';
  await for (final entity in real.list(recursive: true, followLinks: false)) {
    if (entity is Link) {
      throw FormatException('"${entity.path}" is a link. A kit holds files.');
    }
    if (entity is! File || skip.contains(entity.path)) continue;
    final relative = entity.path
        .substring(prefix.length)
        .replaceAll(Platform.pathSeparator, '/');
    if (relative.toLowerCase() == '$kitManifestId') continue;
    sizes[AssetId.parse(relative)] = await entity.length();
  }
  return sizes;
}

String _pathOf(String root, AssetId id) =>
    [root, ...id.toString().split('/')].join(Platform.pathSeparator);

/// The files an install would replace, as the kit names them. Nothing was
/// written when it is thrown.
final class KitConflicts implements Exception {
  const KitConflicts(this.ids);

  final List<AssetId> ids;

  @override
  String toString() =>
      'These files are already in the project with other contents: '
      '${ids.map((id) => kitShow('$id')).join(', ')}.';
}

/// Writes the files of [kit] into the folder [projectPath] and answers which
/// it wrote, as the kit names them.
///
/// With [into], the files go under that folder of the project, which has to
/// pass [kitPlaceProblem]. With [only], just those files are written, and a
/// file the person left out cannot conflict. Both are settled before the first
/// file is written.
///
/// The kit is checked first, by the rules of [kitProblems] and by the hash of
/// every blob, so a pack built by hand cannot write a name that packing would
/// have refused. A file the project already has with other bytes stops the
/// install with a [KitConflicts], unless [overwrite] is set. A file that is
/// there and identical is left alone. The manifest is not written. A link or a
/// plain file where a folder must go, or a folder or link where a file must
/// go, stops the install too. Each blob is hashed again on the way out and
/// written beside its place under a new name, then renamed, so a file is whole
/// or not there.
///
/// A file that [overwrite] replaces gets the default permissions, so an
/// executable flag on it is lost.
///
/// shortcut: files are written one at a time, so a disk that fills half way
/// leaves the earlier ones. Running the install again is safe, because
/// identical files are skipped. Stage the whole kit and swap it in if that
/// matters. A person who swaps a folder for a link while the install runs can
/// still redirect a later file, so run it in a folder only you write to.
Future<List<AssetId>> extractKit(
  Kit kit,
  String projectPath, {
  bool overwrite = false,
  String into = '',
  Set<AssetId>? only,
}) async {
  if (!Directory(projectPath).existsSync()) {
    throw ArgumentError.value(projectPath, 'projectPath', 'is not a directory');
  }
  final picked = kit.chosen(only);
  final misplaced = kitPlaceProblem(into, picked);
  if (misplaced != null) throw FormatException(misplaced);
  final problems = await kit.check();
  if (problems.isNotEmpty) throw FormatException(problems.join('\n'));
  final root = await Directory(projectPath).resolveSymbolicLinks();
  final conflicts = await kit.conflictsIn(
    DirectoryAssetSource(root),
    into: into,
    only: only,
  );
  if (conflicts.isNotEmpty && !overwrite) throw KitConflicts(conflicts);
  final places = {for (final id in picked) id: kitPlace(id, into)};
  for (final place in places.values) {
    _checkPlace(root, place);
  }
  final written = <AssetId>[];
  for (final MapEntry(key: id, value: place) in places.entries) {
    final hash = kit.pack.manifest.entries[id]!.hash;
    if (await _put(kit, id, _pathOf(root, place), hash)) written.add(id);
  }
  return written;
}

/// Writes [id] of [kit] to [path] unless it is there already. Answers whether
/// it wrote.
Future<bool> _put(Kit kit, AssetId id, String path, ContentHash hash) async {
  final bytes = await kit.pack.read(id);
  if (ContentHash.of(bytes) != hash) {
    throw FormatException('"$id" does not match its hash in the kit.');
  }
  if (await _identical(path, hash)) return false;
  File(path).parent.createSync(recursive: true);
  await _write(path, bytes);
  return true;
}

Future<bool> _identical(String path, ContentHash hash) async {
  final file = File(path);
  return file.existsSync() && ContentHash.of(await file.readAsBytes()) == hash;
}

/// Throws when [id] cannot be written under [root]: a folder it needs is a
/// link or a file, or its own place is a folder or a link. Writes nothing.
void _checkPlace(String root, AssetId id) {
  var path = root;
  final parts = id.toString().split('/');
  for (final part in parts.take(parts.length - 1)) {
    path = '$path${Platform.pathSeparator}$part';
    final type = FileSystemEntity.typeSync(path, followLinks: false);
    if (type == FileSystemEntityType.notFound) return;
    if (type != FileSystemEntityType.directory) {
      throw FormatException(
        '"$path" is in the way of "$id" and is not a plain folder.',
      );
    }
  }
  final type = FileSystemEntity.typeSync(_pathOf(root, id), followLinks: false);
  if (type != FileSystemEntityType.notFound &&
      type != FileSystemEntityType.file) {
    throw FormatException('"$id" is a folder or a link in the project.');
  }
}

/// Writes [bytes] to a new file beside [path] and renames it into place. The
/// new file is made exclusively, so nothing already there is written through
/// or destroyed, and a failure removes it.
Future<void> _write(String path, List<int> bytes) async {
  final suffix = Random().nextInt(1 << 32).toRadixString(16);
  final temporary = File('$path.$suffix.partial');
  var made = false;
  try {
    await temporary.create(exclusive: true);
    made = true;
    await temporary.writeAsBytes(bytes, flush: true);
    await temporary.rename(path);
  } on Object {
    if (made && temporary.existsSync()) temporary.deleteSync();
    rethrow;
  }
}

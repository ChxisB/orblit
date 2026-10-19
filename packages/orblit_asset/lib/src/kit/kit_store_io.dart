// The half of the conditional export in kit_store.dart for platforms with a
// file system.
import 'dart:async';
import 'dart:io';
import 'dart:math';
import 'dart:typed_data';

import '../net/policy.dart';
import '../net/transport.dart';
import '../pack/asset_pack.dart';
import '../pack/pack_file_io.dart';
import '../pack/pack_folder.dart';
import 'kit.dart';
import 'kit_entry.dart';
import 'kit_index.dart';
import 'kit_manifest.dart';

/// Reads the entries in [entriesPath], downloads and checks every release
/// they list, and writes the files a browser reads into the new folder
/// [outPath]: `index.json`, and `kits/<publisher>/<slug>/` with a
/// `releases.json` and the latest release's previews.
///
/// [entriesPath] holds `<publisher>/<slug>.json`. A file name that starts with
/// a dot at the top, such as `.gitkeep`, is left alone. Anything else that is
/// not an entry is a problem, and so is a link.
///
/// A release is fetched over https from its url, up to the number of bytes the
/// entry gives and for no longer than [timeout]. The pack has to hash to the
/// entry's hash and be the size it says, every blob has to match its hash, the
/// kit has to pass `kitProblems`, and its manifest has to name the same
/// publisher, slug and version as the entry. Nothing is written until every
/// release of every kit has passed, and [outPath] must not exist. The files
/// are written beside it and renamed into place, so what is published is only
/// what was checked this time. Throws a [FormatException] that lists every
/// problem. Answers how many kits and releases it wrote.
///
/// shortcut: every release is downloaded on every run, one after another, and
/// the previews of every kit are held in memory until the last release has
/// passed. Keep packs by hash between runs, fetch several at once and write
/// each kit as it passes when the store grows past a few hundred kits.
Future<({int kits, int releases})> buildKitStore({
  required String entriesPath,
  required String outPath,
  required AssetTransport transport,
  Duration timeout = const Duration(minutes: 10),
}) async {
  final root = Directory(entriesPath);
  if (!root.existsSync()) {
    throw ArgumentError.value(entriesPath, 'entries', 'is not a directory');
  }
  if (FileSystemEntity.typeSync(outPath, followLinks: false) !=
      FileSystemEntityType.notFound) {
    throw ArgumentError.value(
      outPath,
      'out',
      'is there already. Name a folder that does not exist yet.',
    );
  }
  final scratch = await Directory.systemTemp.createTemp('orblit_kit_store_');
  try {
    final build = _StoreBuild(scratch.path, transport, timeout);
    final kits = <_Built>[];
    for (final entry in await _readEntries(root, build.problems)) {
      final built = await build.kit(entry);
      if (built != null) kits.add(built);
    }
    if (build.problems.isNotEmpty) {
      throw FormatException(build.problems.join('\n'));
    }
    await _write(outPath, kits);
    return (
      kits: kits.length,
      releases: kits.fold(0, (sum, kit) => sum + kit.releases.releases.length),
    );
  } finally {
    await scratch.delete(recursive: true);
  }
}

typedef _Built = ({
  KitRow row,
  KitReleases releases,
  Map<String, Uint8List> previews,
});

typedef _Checked = ({
  PublishedRelease release,
  KitManifest manifest,
  Map<String, Uint8List> previews,
});

Future<List<FileSystemEntity>> _sorted(Directory folder) async =>
    await folder.list(followLinks: false).toList()
      ..sort((a, b) => a.path.compareTo(b.path));

String _nameOf(FileSystemEntity entity) => entity.uri.pathSegments.lastWhere(
  (segment) => segment.isNotEmpty,
  orElse: () => '',
);

Future<List<KitEntry>> _readEntries(
  Directory root,
  List<String> problems,
) async {
  final entries = <KitEntry>[];
  for (final publisher in await _sorted(root)) {
    final name = _nameOf(publisher);
    if (publisher is File && name.startsWith('.')) continue;
    if (publisher is Directory) {
      entries.addAll(await _readPublisher(name, publisher, problems));
    } else {
      problems.add('"${kitShow(name)}" is not a folder of entries.');
    }
  }
  return entries;
}

Future<List<KitEntry>> _readPublisher(
  String publisher,
  Directory folder,
  List<String> problems,
) async {
  final entries = <KitEntry>[];
  for (final file in await _sorted(folder)) {
    final name = _nameOf(file);
    final unreadable = await _unreadable(file, name, '$publisher/$name');
    if (unreadable != null) {
      problems.add(unreadable);
      continue;
    }
    try {
      entries.add(
        KitEntry.parse(
          await File(file.path).readAsBytes(),
          publisher: publisher,
          slug: name.substring(0, name.length - '.json'.length),
        ),
      );
    } on FormatException catch (error) {
      problems.add(error.message);
    }
  }
  return entries;
}

/// Why [file] is not read as an entry, or null when it is one. Looks before
/// any byte is read, so a pipe or a huge file cannot stall the build.
Future<String?> _unreadable(
  FileSystemEntity file,
  String name,
  String path,
) async {
  final shown = kitShow(path);
  final notEntry = '"$shown" is not an entry. An entry is a .json file.';
  if (file is! File || !name.endsWith('.json')) return notEntry;
  final stat = await file.stat();
  if (stat.type != FileSystemEntityType.file) return notEntry;
  return stat.size > kitEntryLimit
      ? '"$shown" is over $kitEntryLimit bytes.'
      : null;
}

/// Downloads and checks releases, one at a time, keeping every problem.
///
/// F5: each step answers what it found and also adds its reasons to
/// [problems], so one run reports every bad release and not the first.
final class _StoreBuild {
  _StoreBuild(String scratch, this._transport, this._timeout)
    : _folder = PackFolder(scratch);

  final PackFolder _folder;
  final AssetTransport _transport;
  final Duration _timeout;

  /// Every reason the store cannot be published, one line each.
  final List<String> problems = [];

  /// Checks every release of [entry]. Answers the files to publish for it, or
  /// null when a release failed, with the reasons in [problems].
  Future<_Built?> kit(KitEntry entry) async {
    final checked = <_Checked>[];
    for (final release in entry.releases) {
      final one = await _check(entry, release);
      if (one != null) checked.add(one);
    }
    if (checked.length != entry.releases.length) return null;
    // The releases are newest first in both lists.
    final latest = checked.first;
    final base = 'kits/${entry.publisher}/${entry.slug}';
    final names = latest.previews.keys.toList();
    return (
      row: KitRow(
        manifest: latest.manifest,
        updated: entry.latest.released,
        thumbnail: names.isEmpty ? null : '$base/${names.first}',
      ),
      releases: KitReleases(
        previews: names,
        releases: [for (final one in checked) one.release],
      ),
      previews: latest.previews,
    );
  }

  Future<_Checked?> _check(KitEntry entry, KitRelease release) async {
    final name = '${entry.publisher}/${entry.slug} ${release.version}';
    if (!await _fetch(name, release)) return null;
    final bytes = FilePackBytes(_packPath(release));
    try {
      return await _inspect(name, entry, release, bytes);
    } on FormatException catch (error) {
      problems.add('$name: ${kitShow(error.message)}');
      return null;
    } finally {
      await bytes.close();
      await _folder.remove(release.hash);
    }
  }

  /// Downloads the release into the scratch folder, which checks its hash.
  Future<bool> _fetch(String name, KitRelease release) async {
    try {
      await _folder
          .download(
            release.offer,
            transport: _transport,
            origin: _originOf(release),
          )
          .timeout(_timeout);
      return true;
    } on Exception catch (error) {
      problems.add('$name could not be fetched. ${kitShow('$error')}');
      return false;
    }
  }

  Future<_Checked?> _inspect(
    String name,
    KitEntry entry,
    KitRelease release,
    FilePackBytes bytes,
  ) async {
    final kit = await Kit.open(await AssetPack.open(bytes));
    final found = [
      ..._disagreements(entry, release, kit.manifest),
      ...await kit.check(),
      ?_sizeProblem(release, await bytes.length()),
    ];
    if (found.isNotEmpty) {
      problems.addAll([for (final line in found) '$name: $line']);
      return null;
    }
    return (
      release: PublishedRelease(
        release: release,
        engine: kit.manifest.engine,
        containsScripts: kit.manifest.containsScripts,
      ),
      manifest: kit.manifest,
      previews: release.version == entry.latest.version
          ? await _previews(kit)
          : const <String, Uint8List>{},
    );
  }

  String _packPath(KitRelease release) =>
      '${_folder.path}${Platform.pathSeparator}${release.hash.hex}.opack';
}

/// The origin of the url, which is the host the request goes to first, and a
/// limit of the size the entry gives.
///
/// [HttpTransport] follows a redirect to any host and the policy does not look
/// at where it leads, so a release on a host that redirects, such as a GitHub
/// release, works. The pack hash is what vouches for the bytes.
AssetOrigin _originOf(KitRelease release) => AssetOrigin(
  Uri(scheme: 'https', host: Uri.parse(release.url).host),
  policy: FetchPolicy(maxBytes: release.bytes),
);

List<String> _disagreements(
  KitEntry entry,
  KitRelease release,
  KitManifest manifest,
) => [
  for (final (field, wanted, found) in [
    ('publisher', entry.publisher, manifest.publisher),
    ('slug', entry.slug, manifest.slug),
    ('version', release.version, manifest.version),
  ])
    if (wanted != found)
      'its kit.json says $field "${kitShow(found)}" and the entry says '
          '"$wanted".',
];

String? _sizeProblem(KitRelease release, int found) => found == release.bytes
    ? null
    : 'the file is $found bytes and the entry says ${release.bytes}.';

/// The previews of the kit, named `preview-<n>.<extension>`.
Future<Map<String, Uint8List>> _previews(Kit kit) async {
  final previews = <String, Uint8List>{};
  for (final (index, id) in kit.manifest.previews.indexed) {
    final size = kit.files[id]!;
    if (size > kitPreviewBytes) {
      throw FormatException(
        'The preview "${kitShow('$id')}" is $size bytes. A preview is at most '
        '$kitPreviewBytes.',
      );
    }
    final bytes = await kit.pack.read(id);
    if (!_isImage(id.extension, bytes)) {
      throw FormatException(
        'The preview "${kitShow('$id')}" does not start like a '
        '${id.extension} image.',
      );
    }
    previews['preview-$index.${id.extension}'] = bytes;
  }
  return previews;
}

/// True when [bytes] begin as a file of [extension] does.
///
/// shortcut: only the first bytes are read, so a damaged picture passes. The
/// editor decodes under its own pixel limit.
bool _isImage(String extension, Uint8List bytes) => switch (extension) {
  'png' => _starts(bytes, const [
    0x89,
    0x50,
    0x4e,
    0x47,
    0x0d,
    0x0a,
    0x1a,
    0x0a,
  ]),
  'jpg' || 'jpeg' => _starts(bytes, const [0xff, 0xd8, 0xff]),
  _ =>
    _starts(bytes, const [0x52, 0x49, 0x46, 0x46]) &&
        bytes.length >= 12 &&
        String.fromCharCodes(bytes.sublist(8, 12)) == 'WEBP',
};

bool _starts(Uint8List bytes, List<int> prefix) =>
    bytes.length >= prefix.length &&
    [
      for (var i = 0; i < prefix.length; i++) bytes[i] == prefix[i],
    ].every((x) => x);

Future<void> _write(String outPath, List<_Built> kits) async {
  // Staged beside the final place and renamed, so a folder that appeared while
  // the build ran is never written into or deleted.
  final suffix = Random().nextInt(1 << 32).toRadixString(16);
  final staging = Directory(
    '${outPath.replaceFirst(RegExp(r'[/\\]+$'), '')}.$suffix.partial',
  );
  await staging.create(recursive: true);
  try {
    await _writeFiles(staging.path, kits);
    await staging.rename(outPath);
  } on Object {
    await staging.delete(recursive: true);
    rethrow;
  }
}

Future<void> _writeFiles(String root, List<_Built> kits) async {
  for (final kit in kits) {
    final manifest = kit.row.manifest;
    final folder = Directory(
      [
        root,
        'kits',
        manifest.publisher,
        manifest.slug,
      ].join(Platform.pathSeparator),
    );
    await folder.create(recursive: true);
    await File(
      '${folder.path}${Platform.pathSeparator}releases.json',
    ).writeAsBytes(kit.releases.encode());
    for (final MapEntry(:key, :value) in kit.previews.entries) {
      await File(
        '${folder.path}${Platform.pathSeparator}$key',
      ).writeAsBytes(value);
    }
  }
  await File(
    '$root${Platform.pathSeparator}index.json',
  ).writeAsBytes(KitIndex([for (final kit in kits) kit.row]).encode());
}

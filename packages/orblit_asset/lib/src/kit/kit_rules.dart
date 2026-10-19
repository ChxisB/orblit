import 'dart:math';

import '../asset_id.dart';
import 'kit_manifest.dart';

/// The largest file a kit may hold.
const int kitFileLimit = 64 * 1024 * 1024;

/// The most files a kit may hold.
const int kitCountLimit = 5000;

/// The most bytes a kit may hold in all, counting a file once for each name.
const int kitTotalLimit = 1024 * 1024 * 1024;

/// The largest kit file a release may be: the files of a kit, and room for
/// the header, the directory and the padding between blobs.
const int kitPackLimit = kitTotalLimit + 16 * 1024 * 1024;

/// The file at the root of a project that says which kits were installed.
const String kitLockName = 'kits.lock';

const int _segmentLimit = 100;
const int _pathLimit = 240;

/// Extensions of a file a person could run, compile or load as code. A kit
/// with one says so in `containsScripts`.
///
/// The flag is a hint. A file with no extension can still be a program, so a
/// person reads a kit before running anything in it.
const Set<String> scriptExtensions = {
  'js',
  'mjs',
  'cjs',
  'jsx',
  'ts',
  'mts',
  'cts',
  'tsx',
  'dart',
  'sh',
  'bash',
  'zsh',
  'fish',
  'command',
  'bat',
  'cmd',
  'ps1',
  'psm1',
  'vbs',
  'wsf',
  'py',
  'pyw',
  'rb',
  'pl',
  'php',
  'lua',
  'scpt',
  'applescript',
  'gradle',
  'kts',
  'jar',
  'exe',
  'dll',
  'so',
  'dylib',
  'msi',
  'lnk',
};

/// Names that run code whatever their extension, in lower case.
const Set<String> scriptNames = {
  'makefile',
  'gradlew',
  'mvnw',
  'cmakelists.txt',
  'package.json',
  'pubspec.yaml',
  'pubspec_overrides.yaml',
};

final RegExp _device = RegExp(
  r'^(con|prn|aux|nul|com[1-9]|lpt[1-9])(\..*)?$',
  caseSensitive: false,
);

// Letters, digits and a few marks. Anything else, such as a Windows reserved
// character, an accent that has two spellings or a bidirectional mark, would
// make a name read differently from the file it writes.
final RegExp _plain = RegExp(r'^[A-Za-z0-9_][A-Za-z0-9_.() +-]*$');

/// True when [id] is a file a person could run, compile or load as code.
bool isKitScript(AssetId id) =>
    scriptExtensions.contains(id.extension) ||
    scriptNames.contains(id.name.toLowerCase());

/// True when one of [ids] is a script.
bool carriesScripts(Iterable<AssetId> ids) => ids.any(isKitScript);

/// Where [id] lands when a kit is written under [folder]. An empty [folder]
/// is the project's own root.
AssetId kitPlace(AssetId id, String folder) =>
    folder.isEmpty ? id : AssetId.parse('$folder/$id');

/// Why the files [ids] cannot be written under [folder], or null when they
/// can.
///
/// The folder is held to the rules a file name is: no hidden folder, no name
/// a file system reads as something else. Otherwise a kit installed into
/// `.git/hooks` would write what a person never looked at.
///
/// At the root, a file that any case of `kits.lock` names would replace the
/// record of what kits installed.
String? kitPlaceProblem(String folder, Iterable<AssetId> ids) {
  if (folder.isEmpty) {
    return ids.any((id) => '$id'.toLowerCase() == kitLockName)
        ? 'A file named $kitLockName would replace the list of installed '
              'kits. Install the kit into a folder.'
        : null;
  }
  final shown = kitShow(folder);
  if (AssetId.tryParse(folder) == null) {
    return '"$shown" is not a folder name. Use names joined by single '
        'slashes, with none at either end.';
  }
  for (final part in folder.split('/')) {
    final why = _segmentWhy(part);
    if (why != null) {
      return '"$shown" has a part, "${kitShow(part)}", that $why.';
    }
  }
  final longest = ids.fold(0, (most, id) => max(most, '$id'.length));
  return folder.length + 1 + longest > _pathLimit
      ? '"$shown" is so long that a file in the kit would pass the limit of '
            '$_pathLimit characters.'
      : null;
}

/// What is wrong with a kit that holds [files], each an id and its size in
/// bytes, with the manifest left out. Empty when nothing is.
///
/// A kit is written into a person's project by a program that trusts it, so
/// the rules refuse every name that would land somewhere a person did not
/// look: a hidden folder, a name the file system reads as something else, two
/// names that are one file on a case insensitive disk, and a name that is both
/// a file and a folder. They also bound the count and the size, so a kit
/// cannot fill a disk.
List<String> kitProblems(
  KitManifest manifest,
  Map<AssetId, int> files, {
  int fileLimit = kitFileLimit,
}) {
  final sizes = _sizeProblems(files);
  if (files.length > kitCountLimit) return sizes;
  return [
    if (files.isEmpty) 'The kit holds no files.',
    ...sizes,
    for (final MapEntry(:key, :value) in files.entries)
      ..._fileProblems(key, value, fileLimit),
    ..._caseClashes(files.keys),
    ..._folderClashes(files.keys),
    for (final id in manifest.previews)
      if (!files.containsKey(id))
        'The preview "${kitShow('$id')}" is not in the kit.',
    if (manifest.containsScripts != carriesScripts(files.keys))
      manifest.containsScripts
          ? 'The manifest says the kit holds scripts and it holds none.'
          : 'The manifest says the kit holds no scripts and it holds some.',
  ];
}

List<String> _sizeProblems(Map<AssetId, int> files) {
  final total = files.values.fold(0, (sum, bytes) => sum + bytes);
  return [
    if (files.length > kitCountLimit)
      'The kit holds ${files.length} files, over the limit of '
          '$kitCountLimit.',
    if (total > kitTotalLimit)
      'The kit holds $total bytes in all, over the limit of $kitTotalLimit.',
  ];
}

List<String> _fileProblems(AssetId id, int bytes, int limit) => [
  if (bytes > limit)
    '"${kitShow('$id')}" is $bytes bytes, over the limit of $limit.',
  if ('$id'.length > _pathLimit)
    '"${kitShow('$id')}" is longer than $_pathLimit characters.',
  for (final segment in '$id'.split('/')) ...?_segmentProblem(id, segment),
];

List<String>? _segmentProblem(AssetId id, String segment) {
  final why = _segmentWhy(segment);
  return why == null
      ? null
      : ['"${kitShow('$id')}" has a part, "${kitShow(segment)}", that $why.'];
}

/// What is wrong with one part of a path, ending a sentence that starts with
/// the part, or null.
String? _segmentWhy(String segment) {
  if (segment.length > _segmentLimit) {
    return 'is longer than $_segmentLimit characters';
  }
  if (segment.startsWith('.')) {
    return 'starts with a dot, so it would write a hidden file';
  }
  if (segment.endsWith('.') || segment.endsWith(' ')) {
    return 'ends with a dot or a space, which Windows drops';
  }
  if (_device.hasMatch(segment)) return 'is a Windows device name';
  if (!_plain.hasMatch(segment)) {
    return 'uses a character other than letters, digits, spaces and _.()+-';
  }
  return null;
}

List<String> _caseClashes(Iterable<AssetId> ids) {
  final seen = <String, AssetId>{};
  final problems = <String>[];
  for (final id in ids) {
    final other = seen['$id'.toLowerCase()];
    if (other != null) {
      problems.add(
        '"${kitShow('$other')}" and "${kitShow('$id')}" differ only in case.',
      );
    }
    seen['$id'.toLowerCase()] = id;
  }
  return problems;
}

/// A name that is a file in one place and a folder in another cannot be
/// written, so the kit would half install.
List<String> _folderClashes(Iterable<AssetId> ids) {
  final files = {for (final id in ids) '$id'.toLowerCase(): id};
  final problems = <String>[];
  for (final id in ids) {
    final parts = '$id'.toLowerCase().split('/');
    for (var i = 1; i < parts.length; i++) {
      final file = files[parts.take(i).join('/')];
      if (file != null) {
        problems.add(
          '"${kitShow('$file')}" is a file and "${kitShow('$id')}" needs it '
          'to be a folder.',
        );
      }
    }
  }
  return problems;
}

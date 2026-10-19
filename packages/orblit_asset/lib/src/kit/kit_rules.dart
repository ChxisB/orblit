import '../asset_id.dart';
import 'kit_manifest.dart';

/// The largest file a kit may hold.
const int kitFileLimit = 64 * 1024 * 1024;

/// Extensions of a file a person could run or compile. A kit with one says so
/// in `containsScripts`.
const Set<String> scriptExtensions = {
  'js',
  'mjs',
  'cjs',
  'dart',
  'sh',
  'bash',
  'bat',
  'cmd',
  'ps1',
  'py',
};

final RegExp _control = RegExp(r'[\u0000-\u001f\u007f]');
final RegExp _device = RegExp(
  r'^(con|prn|aux|nul|com[1-9]|lpt[1-9])(\..*)?$',
  caseSensitive: false,
);

/// True when one of [ids] is a script.
bool carriesScripts(Iterable<AssetId> ids) =>
    ids.any((id) => scriptExtensions.contains(id.extension));

/// What is wrong with a kit that holds [files], each an id and its size in
/// bytes, with the manifest left out. Empty when nothing is.
///
/// A kit is written into a person's project by a program that trusts it, so
/// the rules refuse every name that would land somewhere a person did not
/// look: a hidden folder, a name the file system reads as something else, and
/// two names that are one file on a case insensitive disk.
List<String> kitProblems(
  KitManifest manifest,
  Map<AssetId, int> files, {
  int fileLimit = kitFileLimit,
}) => [
  if (files.isEmpty) 'The kit holds no files.',
  for (final MapEntry(:key, :value) in files.entries)
    ..._fileProblems(key, value, fileLimit),
  ..._clashes(files.keys),
  for (final id in manifest.previews)
    if (!files.containsKey(id)) 'The preview "$id" is not in the kit.',
  if (manifest.containsScripts != carriesScripts(files.keys))
    manifest.containsScripts
        ? 'The manifest says the kit holds scripts and it holds none.'
        : 'The manifest says the kit holds no scripts and it holds some.',
];

List<String> _fileProblems(AssetId id, int bytes, int limit) => [
  if (bytes > limit) '"$id" is $bytes bytes, over the limit of $limit.',
  for (final segment in id.toString().split('/'))
    ...?_segmentProblem(id, segment),
];

List<String>? _segmentProblem(AssetId id, String segment) {
  final String? why;
  if (segment.startsWith('.')) {
    why = 'starts with a dot, so it would write a hidden file';
  } else if (segment.endsWith('.') || segment.endsWith(' ')) {
    why = 'ends with a dot or a space, which Windows drops';
  } else if (_control.hasMatch(segment)) {
    why = 'has a control character in it';
  } else if (_device.hasMatch(segment)) {
    why = 'is a Windows device name';
  } else {
    why = null;
  }
  return why == null ? null : ['"$id" has a part, "$segment", that $why.'];
}

List<String> _clashes(Iterable<AssetId> ids) {
  final seen = <String, AssetId>{};
  final problems = <String>[];
  for (final id in ids) {
    final other = seen[id.toString().toLowerCase()];
    if (other != null) {
      problems.add('"$other" and "$id" differ only in case.');
    }
    seen[id.toString().toLowerCase()] = id;
  }
  return problems;
}

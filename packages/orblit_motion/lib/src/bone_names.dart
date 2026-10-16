import 'package:orblit_rig/orblit_rig.dart' show BoneNaming;

import 'rest.dart';

final RegExp _namespace = RegExp(r'[:|]');
final RegExp _sideWord = RegExp(
  r'^(left|right)[_ -]?|[_ -]?(left|right)$|[_ -]([lr])$',
);
final RegExp _punctuation = RegExp(r'[^a-z0-9]');

/// What is left of a bone's name once the things that vary between tools are
/// taken off, and which side it is on.
///
/// Off go a namespace such as `mixamorig:`, case, punctuation, and the side
/// however it is written: `.L`, `_L` or `Left`. The side is `L`, `R` or null.
({String base, String? side}) cleanName(String name) {
  final bare = name.substring(name.lastIndexOf(_namespace) + 1);
  var side = BoneNaming.sideOf(bare)?.suffix.substring(1);
  var base = BoneNaming.baseOf(bare).toLowerCase();
  final word = _sideWord.firstMatch(base);
  if (word != null) {
    side ??= (word[1] ?? word[2] ?? word[3])![0].toUpperCase();
    base = base.replaceRange(word.start, word.end, '');
  }
  return (base: base.replaceAll(_punctuation, ''), side: side);
}

/// Which bone of [from] moves each bone of [to], by name.
///
/// A bone with the same name in both is matched first. The rest are matched
/// by what is left of the name once the things that vary between tools are
/// taken off: a namespace such as `mixamorig:`, a `DEF-` or `ORG-` prefix,
/// case, punctuation, and which side it is on, whether written `.L`, `_L` or
/// `Left`. So `mixamorig:LeftHand` and `hand.L` are the same bone.
///
/// A name that two bones of one skeleton share once cleaned matches nothing,
/// since guessing would drive a bone from the wrong side of the body. The
/// bones this leaves out are the ones to name by hand, in the `bones` of
/// [retargetClip], as target bone to source bone.
Map<String, String> matchBones(RestSkeleton from, RestSkeleton to) {
  final matched = <String, String>{
    for (final name in to.names)
      if (from.indexOf(name) != null) name: name,
  };
  final taken = matched.values.toSet();
  final sources = _byKey([
    for (final name in from.names)
      if (!taken.contains(name)) name,
  ]);
  final targets = _byKey([
    for (final name in to.names)
      if (!matched.containsKey(name)) name,
  ]);
  for (final MapEntry(:key, :value) in targets.entries) {
    final source = sources[key];
    if (source != null) matched[value] = source;
  }
  return matched;
}

/// [names] by their cleaned name, without any that share one.
Map<String, String> _byKey(List<String> names) {
  final seen = <String, String>{};
  final shared = <String>{};
  for (final name in names) {
    final key = _keyOf(name);
    if (seen.containsKey(key)) shared.add(key);
    seen[key] = name;
  }
  return {
    for (final MapEntry(:key, :value) in seen.entries)
      if (!shared.contains(key)) key: value,
  };
}

String _keyOf(String name) {
  final (:base, :side) = cleanName(name);
  return '$base/${side ?? ''}';
}

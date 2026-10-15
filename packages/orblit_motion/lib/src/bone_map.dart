import 'dart:convert';

import 'package:orblit_scene/orblit_scene.dart' show Values;

import 'clip.dart';
import 'format.dart';
import 'rest.dart';
import 'retarget.dart';
import 'retarget_plan.dart';

/// The extension a bone map file carries.
const String boneMapExtension = '.obonemap';

/// A file that is not a bone map, or is one from a newer Orblit.
class BoneMapFormatException implements Exception {
  const BoneMapFormatException(this.message);

  final String message;

  @override
  String toString() => 'BoneMapFormatException: $message';
}

/// A bone map read back off disk, with anything that could not be read.
class BoneMapLoad {
  const BoneMapLoad({required this.map, this.problems = const []});

  final BoneMap map;

  final List<String> problems;
}

/// How the bones of one skeleton move the bones of another, kept as a file
/// so the same pairing carries every clip made for the one onto the other.
///
/// It holds what [retargetClip] is given besides the skeletons and the clip:
/// the [bones] named by hand, the [unpaired] bones left alone, and whether
/// to pair by [humanoid] and to fix the rest pose. [fromAsset] and [toAsset]
/// are what the two skeletons came from, such as the paths of two models.
/// They are kept for whoever opens the file and are never read here.
final class BoneMap {
  BoneMap({
    this.fromAsset = '',
    this.toAsset = '',
    Map<String, String> bones = const {},
    Set<String> unpaired = const {},
    this.humanoid = false,
    this.fixRest = false,
  }) : bones = Map.unmodifiable(bones),
       unpaired = Set.unmodifiable(unpaired) {
    for (final name in unpaired) {
      if (bones.containsKey(name)) {
        throw ArgumentError.value(
          unpaired,
          'unpaired',
          '"$name" is paired by hand, so it cannot be left unpaired.',
        );
      }
    }
  }

  static const String marker = 'orblit.bonemap';

  /// The shape of the file. Bumped with a migration whenever it changes.
  static const int formatVersion = 1;

  static const FileFormat _format = FileFormat(
    marker: marker,
    noun: 'bone map',
    version: formatVersion,
    fail: BoneMapFormatException.new,
  );

  final String fromAsset;
  final String toAsset;

  /// Bone of the target to the bone of the source that moves it, over
  /// whatever names and body parts pair.
  final Map<String, String> bones;

  /// Bones of the target that nothing moves, whatever names would pair.
  final Set<String> unpaired;

  /// Whether bones are also paired by the part of a person each is.
  final bool humanoid;

  /// Whether each bone is first turned to point the way its driver does at
  /// rest.
  final bool fixRest;

  BoneMap copyWith({
    String? fromAsset,
    String? toAsset,
    Map<String, String>? bones,
    Set<String>? unpaired,
    bool? humanoid,
    bool? fixRest,
  }) => BoneMap(
    fromAsset: fromAsset ?? this.fromAsset,
    toAsset: toAsset ?? this.toAsset,
    bones: bones ?? this.bones,
    unpaired: unpaired ?? this.unpaired,
    humanoid: humanoid ?? this.humanoid,
    fixRest: fixRest ?? this.fixRest,
  );

  /// This map with [bone] of the target moved by [source], by hand.
  ///
  /// Whatever left [bone] unpaired is undone, so a bone is never both.
  BoneMap withPair(String bone, String source) => copyWith(
    bones: {...bones, bone: source},
    unpaired: {...unpaired}..remove(bone),
  );

  /// This map with [bone] of the target moved by nothing, whatever a name or
  /// the body would pair it with.
  BoneMap withUnpaired(String bone) =>
      copyWith(bones: {...bones}..remove(bone), unpaired: {...unpaired, bone});

  /// This map with [bone] of the target paired as names and the body say.
  BoneMap withAuto(String bone) => copyWith(
    bones: {...bones}..remove(bone),
    unpaired: {...unpaired}..remove(bone),
  );

  /// The bones this map names that [from] or [to] lacks, sorted. A map made
  /// for another model has some, and [apply] throws until they are out.
  List<String> stale({required RestSkeleton from, required RestSkeleton to}) =>
      [
        for (final MapEntry(:key, :value) in bones.entries)
          if (!_fits(key, value, from: from, to: to)) key,
        for (final name in unpaired)
          if (to.indexOf(name) == null) name,
      ]..sort();

  /// This map without the names [stale] answers.
  BoneMap fitted({required RestSkeleton from, required RestSkeleton to}) =>
      copyWith(
        bones: {
          for (final MapEntry(:key, :value) in bones.entries)
            if (_fits(key, value, from: from, to: to)) key: value,
        },
        unpaired: {
          for (final name in unpaired)
            if (to.indexOf(name) != null) name,
        },
      );

  static bool _fits(
    String bone,
    String source, {
    required RestSkeleton from,
    required RestSkeleton to,
  }) => to.indexOf(bone) != null && from.indexOf(source) != null;

  /// The bone of [from] that moves each bone of [to] under this map, by name,
  /// and what could not be paired. Names and the body are read as [apply]
  /// reads them, so this is what a clip would get.
  ///
  /// Throws [ArgumentError] as [apply] does, for a name [stale] answers.
  ({Map<String, String> drivers, List<String> problems}) pairing({
    required RestSkeleton from,
    required RestSkeleton to,
  }) {
    final plan = planRetarget(
      from: from,
      to: to,
      bones: bones,
      unpaired: unpaired,
      humanoid: humanoid,
      fixRest: fixRest,
    );
    return (
      drivers: {
        for (final MapEntry(:key, :value) in plan.drivers.entries)
          to.names[key]: from.names[value],
      },
      problems: plan.problems,
    );
  }

  /// [clip], made for [from], made to move [to], paired as this map says.
  ///
  /// Throws [ArgumentError] as [retargetClip] does, for a bone the map names
  /// that the skeleton it is named in lacks.
  ClipRetargeted apply(
    ClipDocument clip, {
    required RestSkeleton from,
    required RestSkeleton to,
    String target = '',
  }) => retargetClip(
    clip,
    from: from,
    to: to,
    bones: bones,
    unpaired: unpaired,
    target: target,
    humanoid: humanoid,
    fixRest: fixRest,
  );

  Map<String, Object?> toJson() => {
    'kind': marker,
    'formatVersion': formatVersion,
    if (fromAsset.isNotEmpty) 'from': fromAsset,
    if (toAsset.isNotEmpty) 'to': toAsset,
    'humanoid': humanoid,
    'fixRest': fixRest,
    'bones': {
      for (final name in bones.keys.toList()..sort()) name: bones[name],
    },
    'unpaired': unpaired.toList()..sort(),
  };

  /// The file's text, one pair to a line so two maps diff as the pairs that
  /// changed.
  String encode() =>
      '${const JsonEncoder.withIndent('  ').convert(toJson())}\n';

  /// A bone map out of a file's text, with whatever could not be read.
  ///
  /// Lenient about the parts, strict about the whole, like a clip: a pair
  /// that is not two names is left out with a note, and a file that is not a
  /// bone map or is one from a newer Orblit throws [BoneMapFormatException].
  static BoneMapLoad decode(String text) {
    final problems = <String>[];
    final json = _format.open(text, problems);
    final bones = _pairs(json['bones'], problems);
    final unpaired = _names(json['unpaired'], problems);
    for (final name in unpaired.toList()) {
      if (bones.containsKey(name)) {
        problems.add(
          '"$name" is paired by hand and also left unpaired. The pair was '
          'kept.',
        );
        unpaired.remove(name);
      }
    }
    return BoneMapLoad(
      map: BoneMap(
        fromAsset: Values.text(json, 'from') ?? '',
        toAsset: Values.text(json, 'to') ?? '',
        bones: bones,
        unpaired: unpaired,
        humanoid: Values.flag(json, 'humanoid', fallback: false),
        fixRest: Values.flag(json, 'fixRest', fallback: false),
      ),
      problems: problems,
    );
  }

  static Map<String, String> _pairs(Object? raw, List<String> problems) {
    final out = <String, String>{};
    if (raw is! Map<String, Object?>) return out;
    for (final MapEntry(:key, :value) in raw.entries) {
      if (value is String && value.isNotEmpty && key.isNotEmpty) {
        out[key] = value;
      } else {
        problems.add(
          'The pair for "$key" is not a bone name, so it was left out.',
        );
      }
    }
    return out;
  }

  static Set<String> _names(Object? raw, List<String> problems) {
    final out = <String>{};
    for (final name in raw is List ? raw : const []) {
      if (name is String && name.isNotEmpty) {
        out.add(name);
      } else {
        problems.add('An unpaired bone that is not a name was left out.');
      }
    }
    return out;
  }
}

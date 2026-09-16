import 'dart:convert';
import 'dart:math' as math;
import 'dart:typed_data';

import 'package:vector_math/vector_math_64.dart';

import 'key_list.dart';
import 'scene.dart' show OrblitLight, OrblitLightKind, OrblitObject;

/// One of a model file's own animation clips, playing on an object.
///
/// The clip is the file's; this says which, and where it is. The renderer
/// samples it at the moment each frame is drawn, measured from the moment the
/// scene carrying it was sent — so between two sends it carries on at
/// [speed], and a host that holds its clock still gets exactly [seconds]
/// every frame, which is what makes a frame check repeatable.
///
/// A host that has stopped sending scenes has stopped its clips too: a clip
/// runs on for at most a quarter of a second past the last scene, the same
/// allowance the camera has, and then holds.
class OrblitAnimation {
  const OrblitAnimation({
    required this.clip,
    this.seconds = 0,
    this.speed = 1,
    this.loop = true,
    this.from,
    this.fade = 1,
  });

  /// Which of the file's clips, by its position in [OrblitAssetInfo.clips].
  /// A clip the file does not have is a scene note, and nothing plays.
  final int clip;

  /// Where the clip is at the moment the scene is sent, in its own seconds.
  final double seconds;

  /// How many of the clip's seconds pass per second of the host's clock.
  /// Nought holds it, which is how a clip is scrubbed.
  final double speed;

  /// Whether it wraps past its end, or stops on its last frame.
  final bool loop;

  /// The clip being left, while a change of clip fades in. Its own [from] is
  /// not read: a fade is between two clips, not a chain of them.
  final OrblitAnimation? from;

  /// How far the fade from [from] has gone: nought is all [from], one is all
  /// this clip. Ignored without a [from].
  final double fade;

  /// Whole numbers a pose packs: the clip, the clip being left, the flags
  /// below and the object's material variant. Must match kPoseInts in the
  /// renderer and poseIntStride in the plugins.
  static const intStride = 4;

  /// Floats a pose packs: this clip's seconds and speed, [from]'s seconds and
  /// speed, and [fade]. Must match kPoseFloats and poseStride.
  static const stride = 5;

  static const _loops = 1 << 0;
  static const _fromLoops = 1 << 1;
}

/// A joint of a model's skin, set by hand.
///
/// What lets something other than the file's clips move a character: a rig
/// solved in Dart, a head turned toward whatever it is looking at, a foot put
/// on the ground it is standing on. It replaces the joint's local transform
/// after any clip has been applied, so a hand-set joint always wins.
class OrblitJointPose {
  const OrblitJointPose({
    required this.skin,
    required this.joint,
    required this.transform,
  });

  /// Which skin, and which of its joints, by their positions in
  /// [OrblitAssetInfo.skins] and [OrblitSkinInfo.joints].
  final int skin;
  final int joint;

  /// The joint's transform relative to its parent, column-major.
  final Matrix4 transform;
}

/// Packs the poses of a scene's objects into the arrays the renderer reads.
///
/// Only objects with something to say are sent, and nothing at all when none
/// has — so a scene with no models out of files sends exactly what it sent
/// before. An object that stops being posed is simply not named, and the
/// renderer puts it back as the file had it.
Map<String, Object>? packPoses(List<OrblitObject> objects) {
  final posed = [
    for (final object in objects)
      if (object.animation != null ||
          object.variant != null ||
          (object.joints?.isNotEmpty ?? false))
        object,
  ];
  if (posed.isEmpty) return null;

  final count = posed.length;
  final keys = makeKeyList(count);
  final ints = Int32List(count * OrblitAnimation.intStride);
  final floats = Float32List(count * OrblitAnimation.stride);
  final jointCounts = Int32List(count);
  final joints = <int>[];
  final transforms = <double>[];

  for (var i = 0; i < count; i++) {
    final object = posed[i];
    keys[i] = object.key;
    final animation = object.animation;
    final from = animation?.from;

    final row = i * OrblitAnimation.intStride;
    ints[row] = animation?.clip ?? -1;
    ints[row + 1] = from?.clip ?? -1;
    ints[row + 2] =
        (animation != null && animation.loop ? OrblitAnimation._loops : 0) |
        (from != null && from.loop ? OrblitAnimation._fromLoops : 0);
    ints[row + 3] = object.variant ?? -1;

    final at = i * OrblitAnimation.stride;
    floats[at] = animation?.seconds ?? 0;
    floats[at + 1] = animation?.speed ?? 0;
    floats[at + 2] = from?.seconds ?? 0;
    floats[at + 3] = from?.speed ?? 0;
    floats[at + 4] = from == null ? 1 : (animation?.fade ?? 1);

    final set = object.joints ?? const <OrblitJointPose>[];
    jointCounts[i] = set.length;
    for (final joint in set) {
      joints
        ..add(joint.skin)
        ..add(joint.joint);
      transforms.addAll(joint.transform.storage);
    }
  }

  return {
    'poseKeys': keys,
    'poseInts': ints,
    'poseFloats': floats,
    'poseJointCounts': jointCounts,
    'poseJoints': Int32List.fromList(joints),
    'poseJointTransforms': Float32List.fromList(transforms),
  };
}

/// What a model file holds, as the renderer loaded it.
///
/// Reported back through `OrblitView.onAssetInfo` whenever something new is
/// made of a file — from the loaded model rather than from the file, so an
/// FBX that became a glTF on the way in is described as what the renderer
/// actually has: the clips it can play, the joints it can set.
class OrblitAssetInfo {
  const OrblitAssetInfo({
    required this.path,
    this.clips = const [],
    this.skins = const [],
    this.variants = const [],
    this.materials = const [],
    this.lights = const [],
    this.cameras = const [],
    required this.boundsMin,
    required this.boundsMax,
    this.unsupported = const [],
  });

  /// The path the scene named the file by.
  final String path;

  /// Its animation clips, in the order [OrblitAnimation.clip] counts them.
  final List<OrblitClipInfo> clips;

  /// Its skins, in the order [OrblitJointPose.skin] counts them.
  final List<OrblitSkinInfo> skins;

  /// The names of its material variants, in the order
  /// [OrblitObject.variant] counts them.
  final List<String> variants;

  /// The names of its materials.
  final List<String> materials;

  /// The lights it carries. The renderer does not draw these itself — see
  /// [lightsFor] for why, and for how to.
  final List<OrblitFileLight> lights;

  /// The cameras it carries, by name.
  final List<OrblitFileCamera> cameras;

  /// The box its geometry fills, in the file's own units, as loaded.
  final Vector3 boundsMin;
  final Vector3 boundsMax;

  /// glTF extensions it uses that the renderer does not draw. The parts that
  /// need them are drawn without them, and the scene notes say so too.
  final List<String> unsupported;

  /// The start of a scene note's key that carries one of these rather than a
  /// problem: the rest of the key is the path, and the note is JSON. The
  /// renderer's kModelInfoPrefix.
  static const notePrefix = 'orblit.model:';

  /// The clip called [name], or null.
  int? clipNamed(String name) {
    for (var i = 0; i < clips.length; i++) {
      if (clips[i].name == name) return i;
    }
    return null;
  }

  /// The skin and joint called [name], searching every skin, or null.
  ({int skin, int joint})? jointNamed(String name) {
    for (var s = 0; s < skins.length; s++) {
      final joint = skins[s].joints.indexOf(name);
      if (joint >= 0) return (skin: s, joint: joint);
    }
    return null;
  }

  /// The file's lights as ordinary scene lights, for an object standing at
  /// [transform].
  ///
  /// The renderer takes a file's lights out rather than drawing them as the
  /// file made them: those would cast no shadows, be counted against nothing,
  /// and a directional one would fight the scene's sun for the one slot
  /// Filament has. Stated as [OrblitLight]s they share one lighting path with
  /// everything else. [keyOf] gives each light its scene key from its
  /// position in [lights], and has to keep clear of every other key in the
  /// scene.
  List<OrblitLight> lightsFor(
    Matrix4 transform, {
    required int Function(int index) keyOf,
    bool castShadows = true,
  }) {
    return [
      for (var i = 0; i < lights.length; i++)
        lights[i].toLight(transform, key: keyOf(i), castShadows: castShadows),
    ];
  }

  /// Reads the description the renderer sent for [path].
  factory OrblitAssetInfo.fromJson(String path, Map<String, Object?> json) {
    List<Object?> list(String key) => json[key] as List<Object?>? ?? const [];
    final bounds = json['bounds'] as Map<String, Object?>? ?? const {};
    return OrblitAssetInfo(
      path: path,
      clips: [
        for (final clip in list('clips').cast<Map<String, Object?>>())
          OrblitClipInfo(
            name: clip['name'] as String? ?? '',
            seconds: _number(clip['seconds']),
          ),
      ],
      skins: [
        for (final skin in list('skins').cast<Map<String, Object?>>())
          OrblitSkinInfo(
            name: skin['name'] as String? ?? '',
            joints: [
              for (final joint in skin['joints'] as List<Object?>? ?? const [])
                joint as String? ?? '',
            ],
            parents: [
              for (final parent
                  in skin['parents'] as List<Object?>? ?? const [])
                (parent as num? ?? -1).toInt(),
            ],
            rest: [
              for (final matrix in skin['rest'] as List<Object?>? ?? const [])
                _matrix(matrix),
            ],
            local: [
              for (final matrix in skin['local'] as List<Object?>? ?? const [])
                _matrix(matrix),
            ],
          ),
      ],
      variants: [for (final name in list('variants')) name as String? ?? ''],
      materials: [for (final name in list('materials')) name as String? ?? ''],
      lights: [
        for (final light in list('lights').cast<Map<String, Object?>>())
          OrblitFileLight._fromJson(light),
      ],
      cameras: [
        for (final camera in list('cameras').cast<Map<String, Object?>>())
          OrblitFileCamera._fromJson(camera),
      ],
      boundsMin: _vector(bounds['min']),
      boundsMax: _vector(bounds['max']),
      unsupported: [
        for (final name in list('unsupported')) name as String? ?? '',
      ],
    );
  }

  /// Separates the descriptions from the problems in a publish's notes.
  ///
  /// The renderer hands both back in one map, because every host already
  /// carries that map home and a second one would have meant changing all of
  /// them. A description that does not parse is dropped rather than thrown:
  /// it describes, and nothing depends on it arriving.
  static ({Map<String, String> notes, List<OrblitAssetInfo> models}) split(
    Map<String, String> all,
  ) {
    final notes = <String, String>{};
    final models = <OrblitAssetInfo>[];
    all.forEach((key, value) {
      if (!key.startsWith(notePrefix)) {
        notes[key] = value;
        return;
      }
      try {
        final json = jsonDecode(value);
        if (json is Map<String, Object?>) {
          models.add(
            OrblitAssetInfo.fromJson(key.substring(notePrefix.length), json),
          );
        }
      } on FormatException {
        // Described badly is not described; the scene still draws.
      } on TypeError {
        // Nor is described in a shape this version does not read.
      }
    });
    return (notes: notes, models: models);
  }
}

/// One of a file's animation clips.
class OrblitClipInfo {
  const OrblitClipInfo({required this.name, required this.seconds});

  /// What the file calls it, which may be empty.
  final String name;

  /// How long it is.
  final double seconds;
}

/// One of a file's skins: its joints in order, how they hang together, and
/// where each stands at rest.
class OrblitSkinInfo {
  const OrblitSkinInfo({
    required this.name,
    required this.joints,
    this.parents = const [],
    this.rest = const [],
    this.local = const [],
  });

  final String name;

  /// The joints' names, in the order [OrblitJointPose.joint] counts them.
  final List<String> joints;

  /// Which joint each hangs from, by its position in [joints], or -1 for one
  /// that hangs from something that is not a joint of this skin.
  final List<int> parents;

  /// Where each joint stands at rest, relative to the model's root.
  final List<Matrix4> rest;

  /// Where each joint stands at rest relative to its own parent node — the
  /// transform an [OrblitJointPose] replaces.
  final List<Matrix4> local;
}

/// A light a file carries, in [OrblitLight]'s own units: lux for a
/// directional light, lumens for a point or a spot.
class OrblitFileLight {
  const OrblitFileLight({
    required this.name,
    required this.kind,
    required this.colour,
    required this.intensity,
    required this.falloff,
    required this.innerConeAngle,
    required this.outerConeAngle,
    required this.transform,
  });

  final String name;
  final OrblitLightKind kind;
  final Vector3 colour;
  final double intensity;

  /// How far it reaches, in metres; nought for a directional light.
  final double falloff;

  /// A spot's cone, as half-angles in radians.
  final double innerConeAngle;
  final double outerConeAngle;

  /// Where it stands relative to the model's root. It shines along its own
  /// minus Z, as glTF has it.
  final Matrix4 transform;

  /// This light as a scene light, for a model standing at [placement].
  OrblitLight toLight(
    Matrix4 placement, {
    required int key,
    bool castShadows = true,
  }) {
    final world = placement.multiplied(transform);
    final direction =
        world.transform3(Vector3(0, 0, -1)) - world.transform3(Vector3.zero());
    return OrblitLight(
      key: key,
      kind: kind,
      colour: colour,
      intensity: intensity,
      position: world.getTranslation(),
      direction: direction.length2 > 0
          ? direction.normalized()
          : Vector3(0, -1, 0),
      falloffRadius: falloff > 0 ? falloff : reachOf(intensity),
      innerConeAngle: innerConeAngle,
      outerConeAngle: outerConeAngle,
      castShadows: castShadows,
    );
  }

  /// Where a light of [lumens] with no stated range stops mattering: where it
  /// has fallen to a tenth of a lux, as orblit_light's own influence radius
  /// has it. A glTF light with no range is meant to reach forever, which
  /// Filament's lights cannot, so it reaches this far.
  static double reachOf(double lumens) =>
      math.sqrt(lumens / (4 * math.pi) / 0.1);

  factory OrblitFileLight._fromJson(Map<String, Object?> json) {
    final kind = (json['kind'] as num? ?? 1).toInt();
    return OrblitFileLight(
      name: json['name'] as String? ?? '',
      kind: OrblitLightKind.values[kind.clamp(0, 2)],
      colour: _vector(json['colour'], fallback: 1),
      intensity: _number(json['intensity']),
      falloff: _number(json['falloff']),
      innerConeAngle: _number(json['inner']),
      outerConeAngle: _number(json['outer']),
      transform: _matrix(json['transform']),
    );
  }
}

/// A camera a file carries.
class OrblitFileCamera {
  const OrblitFileCamera({
    required this.name,
    required this.orthographic,
    required this.fieldOfView,
    required this.viewHeight,
    required this.near,
    required this.far,
    required this.transform,
  });

  final String name;
  final bool orthographic;

  /// The vertical field of view in degrees, for a perspective camera.
  final double fieldOfView;

  /// How much fits from top to bottom, for an orthographic one.
  final double viewHeight;

  final double near;

  /// Where it stops seeing; infinite when the file gives no far plane.
  final double far;

  /// Where it stands relative to the model's root, looking along its own
  /// minus Z.
  final Matrix4 transform;

  factory OrblitFileCamera._fromJson(Map<String, Object?> json) {
    return OrblitFileCamera(
      name: json['name'] as String? ?? '',
      orthographic: json['orthographic'] as bool? ?? false,
      fieldOfView: _number(json['fieldOfView']),
      viewHeight: _number(json['viewHeight']),
      near: _number(json['near']),
      far: json['far'] == null ? double.infinity : _number(json['far']),
      transform: _matrix(json['transform']),
    );
  }
}

double _number(Object? value) => (value as num?)?.toDouble() ?? 0;

Vector3 _vector(Object? value, {double fallback = 0}) {
  final list = value is List ? value : const [];
  double at(int i) => i < list.length ? _number(list[i]) : fallback;
  return Vector3(at(0), at(1), at(2));
}

Matrix4 _matrix(Object? value) {
  final list = value is List ? value : const [];
  if (list.length != 16) return Matrix4.identity();
  return Matrix4.fromList([for (final v in list) _number(v)]);
}

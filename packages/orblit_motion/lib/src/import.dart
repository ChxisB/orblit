import 'dart:typed_data';

import 'package:orblit_mesh/orblit_mesh.dart' show GltfAccessors, gltfParts;
import 'package:orblit_rig/orblit_rig.dart' show BoneNaming;
import 'package:orblit_sequence/orblit_sequence.dart';
import 'package:vector_math/vector_math_64.dart';

import 'clip.dart';
import 'kind.dart';

/// The clips a glTF file carried, and what of it could not be carried.
class ClipsImported {
  const ClipsImported({required this.clips, this.problems = const []});

  /// One for each of the file's animations, in the file's order.
  final List<ClipDocument> clips;

  final List<String> problems;
}

/// Every animation in a `.glb` or a `.gltf`, as a clip.
///
/// A joint of a skin becomes a bone of the model playing the clip, named the
/// way the model's skeleton names it, so the clip plays on the model it came
/// out of and on any other with the same bones. Any other node becomes an
/// entity, under the id a scene imported from the same file gives it.
///
/// Nothing is resampled: a key in the file is a key in the clip, at the time
/// the file has it, and the file's easing comes across as it is. A linear
/// key stays linear, a step stays a step, and a cubic spline keeps its
/// tangents. Numbers are written as short as they can be while still reading
/// back as the float the file had, so a clip is not three times the size of
/// the data it came from.
///
/// What the file gives no word for is guessed the way an artist would
/// expect: the frame rate is the common one every key sits on, and an
/// animation whose name starts or ends with "loop" or "cycle" loops.
///
/// [files] supplies whatever the document names by URI, the `.bin` beside a
/// `.gltf`. Throws [ClipFormatException] when the bytes are not glTF at all.
ClipsImported clipsFromGltf(
  Uint8List bytes, {
  Map<String, Uint8List> files = const {},
}) {
  final ({Map<String, Object?> json, Uint8List? binary}) parts;
  try {
    parts = gltfParts(bytes, files: files);
  } on FormatException catch (error) {
    throw ClipFormatException(error.message);
  }
  return _Importer(parts.json, parts.binary).read();
}

class _Importer {
  _Importer(this.json, Uint8List? binary)
    : nodes = _maps(json['nodes']),
      accessors = GltfAccessors(json, binary, problems: []);

  final Map<String, Object?> json;
  final List<Map<String, Object?>> nodes;
  final GltfAccessors accessors;
  List<String> get problems => accessors.problems;

  /// The bone each joint node is, by the first skin that has it.
  late final Map<int, String> bones = () {
    final out = <int, String>{};
    for (final skin in _maps(json['skins'])) {
      final joints = _ints(skin['joints']);
      final names = BoneNaming.unique([for (final at in joints) _nameOf(at)]);
      for (var i = 0; i < joints.length; i++) {
        out.putIfAbsent(joints[i], () => names[i]);
      }
    }
    return out;
  }();

  ClipsImported read() {
    final animations = _maps(json['animations']);
    final clips = [
      for (var at = 0; at < animations.length; at++) _clip(animations[at], at),
    ];
    return ClipsImported(clips: clips, problems: List.of(problems));
  }

  ClipDocument _clip(Map<String, Object?> animation, int index) {
    final raw = animation['name'];
    final name = raw is String && raw.trim().isNotEmpty
        ? raw.trim()
        : 'Animation ${index + 1}';
    final samplers = _maps(animation['samplers']);
    final channels = <ClipChannel<Object>>[];
    final times = <double>[];

    for (final channel in _maps(animation['channels'])) {
      final target = channel['target'];
      if (target is! Map<String, Object?>) continue;
      final node = target['node'];
      final path = target['path'];
      final at = channel['sampler'];
      if (node is! int || node < 0 || node >= nodes.length) {
        problems.add(
          'A channel of "$name" moves something that is not a node.',
        );
        continue;
      }
      if (at is! int || at < 0 || at >= samplers.length) {
        problems.add('A channel of "$name" has no sampler.');
        continue;
      }
      if (path == 'weights') {
        problems.add(
          'Morph target weights in "$name" were not carried across; clips '
          'do not move them yet.',
        );
        continue;
      }
      final property = switch (path) {
        'translation' => 'position',
        'rotation' => 'rotation',
        'scale' => 'scale',
        _ => null,
      };
      if (property == null) {
        problems.add('A channel of "$name" moves "$path", which is not read.');
        continue;
      }

      final bone = bones[node];
      final address = (
        target: bone == null ? _entityOf(node) : '',
        bone: bone,
        property: bone == null ? 'transform.$property' : property,
      );
      final made = property == 'rotation'
          ? _channel(address, ChannelKind.rotation, samplers[at], name)
          : _channel(address, ChannelKind.vector, samplers[at], name);
      if (made == null) continue;
      if (channels.any(made.sameAddress)) {
        problems.add(
          'Two channels of "$name" move $path on one node; the second was '
          'left out.',
        );
        continue;
      }
      channels.add(made);
      times.addAll([for (final key in made.keys) key.at]);
    }

    var duration = 0.0;
    for (final at in times) {
      if (at > duration) duration = at;
    }
    final lower = name.toLowerCase();
    final loops = [
      'loop',
      'cycle',
    ].any((word) => lower.startsWith(word) || lower.endsWith(word));
    return ClipDocument(
      name: name,
      duration: duration,
      whenDone: loops ? WhenDone.loop : WhenDone.hold,
      rate: _rateOf(times),
      channels: channels,
    );
  }

  ClipChannel<T>? _channel<T extends Object>(
    ({String target, String? bone, String property}) address,
    ChannelKind<T> kind,
    Map<String, Object?> sampler,
    String clip,
  ) {
    final width = kind == ChannelKind.rotation ? 4 : 3;
    final times = accessors.floats(_index(sampler['input']), 1);
    final values = accessors.floats(_index(sampler['output']), width);
    final hold = switch (sampler['interpolation']) {
      'STEP' => Hold.step,
      'CUBICSPLINE' => Hold.curve,
      _ => Hold.linear,
    };
    // A cubic spline stores three things a key: the slope arriving, the
    // value, and the slope leaving.
    final stride = hold == Hold.curve ? 3 : 1;
    final count = times.length;
    if (count == 0 || values.length < count * stride * width) {
      problems.add(
        'A channel of "$clip" has fewer values than times, so it was left '
        'out.',
      );
      return null;
    }

    T element(int at) {
      final numbers = [
        for (var c = 0; c < width; c++) _short(values[at * width + c]),
      ];
      return _value(kind, numbers);
    }

    final keys = <Key<T>>[
      for (var k = 0; k < count; k++)
        if (times[k].isFinite)
          hold == Hold.curve
              ? Key<T>(
                  _short(times[k]),
                  _unit(element(k * 3 + 1)),
                  hold: hold,
                  slopeIn: element(k * 3),
                  slopeOut: element(k * 3 + 2),
                )
              : Key<T>(_short(times[k]), _unit(element(k)), hold: hold),
    ];
    if (keys.isEmpty) return null;
    return ClipChannel<T>(
      target: address.target,
      bone: address.bone,
      property: address.property,
      kind: kind,
      keys: keys,
    );
  }

  /// The id a scene read from this file gives node [at].
  String _entityOf(int at) {
    final extras = nodes[at]['extras'];
    final ours = extras is Map<String, Object?> ? extras['orblit'] : null;
    final id = ours is Map<String, Object?> ? ours['id'] : null;
    return id is String && id.isNotEmpty ? id : 'node-$at';
  }

  /// A node's name as the renderer reads it, and so as a model's skeleton
  /// names its joints: its own, or the name of the mesh, light or camera it
  /// carries, or `<unknown>`. Anything else and an unnamed joint would be one
  /// bone to the clip and another to the model it plays on.
  String _nameOf(int at) {
    if (at < 0 || at >= nodes.length) return _unnamed;
    final node = nodes[at];
    if (node['name'] case final String name) return name;
    final extensions = node['extensions'];
    final lights = extensions is Map<String, Object?>
        ? extensions['KHR_lights_punctual']
        : null;
    final carried = [
      (_index(node['mesh']), _maps(json['meshes'])),
      (
        lights is Map<String, Object?> ? _index(lights['light']) : null,
        _maps(_lightsOf(json)),
      ),
      (_index(node['camera']), _maps(json['cameras'])),
    ];
    for (final (index, all) in carried) {
      if (index != null && index < all.length) {
        if (all[index]['name'] case final String name) return name;
      }
    }
    return _unnamed;
  }

  static const String _unnamed = '<unknown>';

  static Object? _lightsOf(Map<String, Object?> json) {
    final extensions = json['extensions'];
    if (extensions is! Map<String, Object?>) return null;
    final lights = extensions['KHR_lights_punctual'];
    return lights is Map<String, Object?> ? lights['lights'] : null;
  }
}

/// [numbers] as a value of [kind]. Only the vector and rotation kinds are
/// ever asked for.
T _value<T extends Object>(ChannelKind<T> kind, List<double> numbers) =>
    switch (kind) {
          ChannelKind<Quaternion>() => Quaternion(
            numbers[0],
            numbers[1],
            numbers[2],
            numbers[3],
          ),
          _ => Vector3(numbers[0], numbers[1], numbers[2]),
        }
        as T;

/// [value] made a rotation that turns without scaling, when it is one.
T _unit<T extends Object>(T value) {
  if (value is! Quaternion) return value;
  final length = value.length;
  if (length < 1e-9 || (length - 1).abs() < 1e-6) return value;
  return (value.clone()..normalize()) as T;
}

/// The frame rate every one of [times] sits on, or thirty.
///
/// Thirty first, then the film and video rates, then their doubles: a clip
/// keyed on whole seconds fits them all, and thirty is the likeliest guess.
double _rateOf(List<double> times) {
  for (final rate in const [30.0, 24.0, 25.0, 60.0, 50.0, 48.0]) {
    if (times.every((at) => ((at * rate).round() / rate - at).abs() < 1e-3)) {
      return rate;
    }
  }
  return 30;
}

/// The shortest decimal that is still the same float.
///
/// The file's numbers are floats, and a double printed in full carries
/// seventeen digits of which nine mean anything.
double _short(double value) {
  if (!value.isFinite) return value;
  for (var digits = 6; digits <= 9; digits++) {
    final shorter = double.parse(value.toStringAsPrecision(digits));
    if (_float(shorter) == _float(value)) return shorter;
  }
  return value;
}

final Float32List _one = Float32List(1);

double _float(double value) {
  _one[0] = value;
  return _one[0];
}

List<Map<String, Object?>> _maps(Object? raw) => [
  if (raw is List)
    for (final item in raw)
      if (item is Map<String, Object?>) item,
];

List<int> _ints(Object? raw) => [
  if (raw is List)
    for (final item in raw)
      if (item is int) item,
];

int? _index(Object? raw) => raw is int && raw >= 0 ? raw : null;

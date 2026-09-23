import 'dart:convert';
import 'dart:io';
import 'dart:typed_data';

import 'package:orblit_motion/orblit_motion.dart';
import 'package:test/test.dart';
import 'package:vector_math/vector_math_64.dart';

/// A sample model, when it has been fetched. CI does not fetch them.
Uint8List? sample(String name) {
  final file = File('../../assets/samples/$name.glb');
  return file.existsSync() ? file.readAsBytesSync() : null;
}

/// A `.gltf` put together in memory, its data carried in the file itself.
class Built {
  final BytesBuilder _data = BytesBuilder();
  final List<Map<String, Object?>> _views = [];
  final List<Map<String, Object?>> _accessors = [];

  /// [numbers] as an accessor of [type], and its index.
  int floats(List<double> numbers, String type) {
    final width = switch (type) {
      'VEC3' => 3,
      'VEC4' => 4,
      _ => 1,
    };
    final bytes = Float32List.fromList(numbers).buffer.asUint8List();
    _views.add({
      'buffer': 0,
      'byteOffset': _data.length,
      'byteLength': bytes.length,
    });
    _data.add(bytes);
    _accessors.add({
      'bufferView': _views.length - 1,
      'componentType': 5126,
      'type': type,
      'count': numbers.length ~/ width,
    });
    return _accessors.length - 1;
  }

  /// The file, with [uri] naming its data when it is kept beside it.
  Uint8List file(Map<String, Object?> document, {String? uri}) {
    final data = _data.toBytes();
    return utf8.encode(
      jsonEncode({
        'asset': {'version': '2.0'},
        ...document,
        'buffers': [
          {
            'byteLength': data.length,
            'uri':
                uri ??
                'data:application/octet-stream;base64,${base64Encode(data)}',
          },
        ],
        'bufferViews': _views,
        'accessors': _accessors,
      }),
    );
  }

  Uint8List get data => _data.toBytes();
}

Map<String, Object?> sampler(int input, int output, [String? interpolation]) =>
    {'input': input, 'output': output, 'interpolation': ?interpolation};

Map<String, Object?> channel(int sampler, int node, String path) => {
  'sampler': sampler,
  'target': {'node': node, 'path': path},
};

/// An armature with two joints, one of them unnamed, a door the scene gave
/// an id, and a face with morph targets, moved by three animations.
({Uint8List bytes, Uint8List data}) armature({String? uri}) {
  final built = Built();
  final half = built.floats([0, 0.5], 'SCALAR');
  final quarter = built.floats([0, 0.25], 'SCALAR');
  final frames = built.floats([0, 1 / 24, 2 / 24], 'SCALAR');
  final tenth = built.floats([0, 0.1], 'SCALAR');

  // Arriving slope, value, leaving slope, for each of two keys. The second
  // value is twice as long as a rotation should be.
  final turn = built.floats([
    0, 0, 0, 0, 0, 0, 0, 1, 0, 0.5, 0, 0, //
    0, 0.25, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0,
  ], 'VEC4');
  final lift = built.floats([0, 0, 0, 0, 1, 0], 'VEC3');
  final slide = built.floats([0, 0, 0, 1, 0, 0], 'VEC3');
  final grow = built.floats([1, 1, 1, 2, 2, 2], 'VEC3');
  final stride = built.floats([0, 0, 0, 0, 0, 0.1, 0, 0, 0.2], 'VEC3');
  final lone = built.floats([1, 1, 1], 'VEC3');

  final bytes = built.file({
    'nodes': [
      {
        'name': 'Armature',
        'children': [1, 3, 4],
      },
      {
        'name': 'hips',
        'children': [2],
      },
      <String, Object?>{},
      {
        'name': 'Door',
        'extras': {
          'orblit': {'id': 'house/door'},
        },
      },
      {'name': 'Face', 'mesh': 0},
    ],
    'meshes': [
      {'name': 'Face', 'primitives': <Object>[]},
    ],
    'skins': [
      {
        'joints': [1, 2],
      },
    ],
    'animations': [
      {
        'name': 'Walk Loop',
        'samplers': [
          sampler(half, turn, 'CUBICSPLINE'),
          sampler(quarter, lift, 'STEP'),
          sampler(half, slide),
          sampler(half, grow, 'LINEAR'),
        ],
        'channels': [
          channel(0, 1, 'rotation'),
          channel(1, 2, 'translation'),
          channel(2, 3, 'translation'),
          channel(3, 0, 'scale'),
          channel(2, 4, 'weights'),
          channel(3, 3, 'translation'),
        ],
      },
      {
        'name': 'cycle run',
        'samplers': [sampler(frames, stride)],
        'channels': [channel(0, 1, 'translation')],
      },
      {
        'samplers': [sampler(tenth, slide), sampler(tenth, lone)],
        'channels': [
          channel(0, 3, 'translation'),
          channel(0, 9, 'translation'),
          channel(5, 3, 'rotation'),
          channel(0, 3, 'pointer'),
          channel(1, 1, 'scale'),
        ],
      },
    ],
  }, uri: uri);
  return (bytes: bytes, data: built.data);
}

void expectRoundTrip(ClipDocument clip) {
  final again = ClipDocument.decode(clip.encode());
  expect(again.problems, isEmpty);
  expect(again.clip.encode(), clip.encode());
}

void main() {
  group('a file put together by hand', () {
    final imported = clipsFromGltf(armature().bytes);
    final walk = imported.clips[0];

    test('gives a clip for each animation, in order, named', () {
      expect(
        [for (final clip in imported.clips) clip.name],
        ['Walk Loop', 'cycle run', 'Animation 3'],
      );
    });

    test("moves a joint as a bone, by the skeleton's name for it", () {
      final hips = walk.channelFor('', 'rotation', bone: 'hips')!;
      expect(hips.kind, ChannelKind.rotation);
      final unnamed = walk.channelFor('', 'position', bone: '<unknown>')!;
      expect(unnamed.kind, ChannelKind.vector);
    });

    test('moves any other node as an entity, by the id a scene gives it', () {
      final door = walk.channelFor('house/door', 'transform.position')!;
      expect(door.bone, isNull);
      expect(door.valueAt(0.25), Vector3(0.5, 0, 0));
      expect(walk.channelFor('node-0', 'transform.scale'), isNotNull);
    });

    test('keeps each key where the file has it, eased as the file has it', () {
      final hips =
          walk.channelFor('', 'rotation', bone: 'hips')!
              as ClipChannel<Quaternion>;
      expect([for (final key in hips.keys) key.hold], [Hold.curve, Hold.curve]);
      expect(hips.keys[0].slopeOut, Quaternion(0, 0.5, 0, 0));
      expect(hips.keys[1].slopeIn, Quaternion(0, 0.25, 0, 0));
      // A value is made a rotation that does not scale; a slope is not.
      expect(hips.keys[1].value, Quaternion(0, 1, 0, 0));

      final unnamed = walk.channelFor('', 'position', bone: '<unknown>')!;
      expect(unnamed.keys.first.hold, Hold.step);
      expect(unnamed.valueAt(0.2), Vector3.zero());
      final door = walk.channelFor('house/door', 'transform.position')!;
      expect(door.keys.first.hold, Hold.linear);
    });

    test('says what it could not carry, and carries the rest', () {
      expect(walk.channels, hasLength(4));
      bool noted(String words) =>
          imported.problems.any((problem) => problem.contains(words));
      expect(noted('Morph target weights in "Walk Loop"'), isTrue);
      expect(noted('Two channels of "Walk Loop"'), isTrue);
      expect(noted('not a node'), isTrue);
      expect(noted('has no sampler'), isTrue);
      expect(noted('moves "pointer"'), isTrue);
      expect(noted('fewer values than times'), isTrue);
      expect(imported.clips[2].channels, hasLength(1));
    });

    test('guesses the rate every key sits on, and thirty for none', () {
      expect(walk.rate, 24);
      expect(imported.clips[1].rate, 24);
      expect(imported.clips[2].rate, 30);
    });

    test('loops what is named as a loop or a cycle', () {
      expect(walk.whenDone, WhenDone.loop);
      expect(imported.clips[1].whenDone, WhenDone.loop);
      expect(imported.clips[2].whenDone, WhenDone.hold);
    });

    test('lasts as long as its last key', () {
      expect(walk.duration, 0.5);
      expect(imported.clips[1].duration, closeTo(2 / 24, 1e-7));
    });

    test('writes a float as short as still reads back as it', () {
      final third = imported.clips[2];
      final door = third.channelFor('house/door', 'transform.position')!;
      expect(door.keys.last.at, 0.1);
      expect(third.encode(), contains('"at":0.1,'));
    });

    test('reads the data from beside the file too', () {
      final apart = armature(uri: 'walk%20cycle.bin');
      final beside = clipsFromGltf(
        apart.bytes,
        files: {'walk cycle.bin': apart.data},
      );
      expect(beside.clips.first.encode(), walk.encode());
    });

    test('goes to a clip file and comes back the same', () {
      imported.clips.forEach(expectRoundTrip);
    });
  });

  test('refuses what is not glTF at all', () {
    expect(
      () => clipsFromGltf(utf8.encode('not a model')),
      throwsA(isA<ClipFormatException>()),
    );
  });

  group('the sample models', () {
    final fox = sample('Fox');
    final cesium = sample('CesiumMan');
    final rigged = sample('RiggedSimple');

    test('the fox surveys, walks and runs, on its bones', () {
      final imported = clipsFromGltf(fox!);
      expect(imported.problems, isEmpty);
      expect(
        [for (final clip in imported.clips) clip.name],
        ['Survey', 'Walk', 'Run'],
      );
      for (final clip in imported.clips) {
        expect(clip.channels, isNotEmpty);
        for (final channel in clip.channels) {
          expect(channel.target, '');
          expect(channel.bone, startsWith('b_'));
        }
        expectRoundTrip(clip);
      }
      final walk = imported.clips[1];
      expect(walk.rate, 24);
      expect(walk.duration, closeTo(17 / 24, 1e-6));
      expect(walk.sampleAt(0.3).bones['']!.length, greaterThan(10));
    }, skip: fox == null ? 'Fox.glb is not in assets/samples' : false);

    test('an unnamed animation is named for where it is', () {
      final man = clipsFromGltf(cesium!).clips.single;
      expect(man.name, 'Animation 1');
      expect(man.duration, 2);
      expectRoundTrip(man);
    }, skip: cesium == null ? 'CesiumMan.glb is not in assets/samples' : false);

    test(
      'a skin moves the bones it moves, and no others',
      () {
        final simple = clipsFromGltf(rigged!).clips.single;
        expect(
          {for (final channel in simple.channels) channel.bone},
          {'Bone.001'},
        );
        expectRoundTrip(simple);
      },
      skip: rigged == null
          ? 'RiggedSimple.glb is not in assets/samples'
          : false,
    );
  });
}

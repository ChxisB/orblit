import 'dart:convert';

import 'package:orblit_motion/orblit_motion.dart';
import 'package:test/test.dart';
import 'package:vector_math/vector_math_64.dart';

/// A clip with one of most things in it, to send round the trip.
ClipDocument populated() => ClipDocument(
  name: 'Lamp flicker',
  duration: 2,
  whenDone: WhenDone.loop,
  rate: 24,
  rootMotion: RootMotion(bone: 'hips', turns: true),
  channels: [
    ClipChannel<double>(
      target: 'bulb',
      property: 'light.power',
      kind: ChannelKind.number,
      keys: const [
        Key(0, 60, hold: Hold.linear),
        Key(0.5, 20, hold: Hold.linear),
        Key(1, 60, hold: Hold.shaped, shape: Easing.bounce),
        Key(2, 60, hold: Hold.linear),
      ],
    ),
    ClipChannel<Vector3>(
      target: '',
      property: 'transform.position',
      kind: ChannelKind.vector,
      keys: [Key(0, Vector3(0, 0, 0)), Key(2, Vector3(1, 2, 3))],
    ),
    ClipChannel<bool>(
      target: 'bulb/glow',
      property: 'mesh.visible',
      kind: ChannelKind.flag,
      keys: const [
        Key(0, true, hold: Hold.step),
        Key(1.5, false),
      ],
    ),
    ClipChannel<Quaternion>(
      target: '',
      bone: 'hips',
      property: 'rotation',
      kind: ChannelKind.rotation,
      keys: [
        Key(
          0,
          Quaternion.identity(),
          hold: Hold.curve,
          slopeOut: Quaternion(0, 0.5, 0, 0),
        ),
        Key(
          1,
          Quaternion.axisAngle(Vector3(0, 1, 0), 0.5),
          hold: Hold.curve,
          slopeIn: Quaternion(0, 0.25, 0, -0.1),
        ),
      ],
    ),
  ],
  marks: const [
    Mark(1, 'buzz', payload: {'volume': 0.5}),
    Mark(0.25, 'spark'),
  ],
);

Map<String, Object?> file(Map<String, Object?> body) => {
  'kind': ClipDocument.marker,
  'formatVersion': ClipDocument.formatVersion,
  'name': 'Test',
  ...body,
};

ClipLoad load(Map<String, Object?> body) =>
    ClipDocument.decode(jsonEncode(file(body)));

List<double> numbers(Quaternion rotation) => [
  rotation.x,
  rotation.y,
  rotation.z,
  rotation.w,
];

void main() {
  group('the file', () {
    test('reads back as the clip it was written from', () {
      final text = populated().encode();
      final loaded = ClipDocument.decode(text);

      expect(loaded.problems, isEmpty);
      expect(loaded.clip.encode(), text);

      final clip = loaded.clip;
      expect(clip.name, 'Lamp flicker');
      expect(clip.duration, 2);
      expect(clip.whenDone, WhenDone.loop);
      expect(clip.rate, 24);
      expect(clip.rootMotion?.bone, 'hips');
      expect(clip.rootMotion?.turns, isTrue);
      expect(clip.rootMotion?.rises, isFalse);
      expect(clip.channels, hasLength(4));
      expect([for (final mark in clip.marks) mark.name], ['spark', 'buzz']);
      expect(clip.marks.last.payload, {'volume': 0.5});
    });

    test("keeps each key's hold, shape and slopes", () {
      final clip = ClipDocument.decode(populated().encode()).clip;

      final power = clip.channelFor('bulb', 'light.power')!;
      expect(
        [for (final key in power.keys) key.hold],
        [Hold.linear, Hold.linear, Hold.shaped, Hold.linear],
      );
      expect(power.keys[2].shape, Easing.bounce);

      final hips = clip.channelFor('', 'rotation', bone: 'hips')!;
      expect(numbers(hips.keys.first.slopeOut! as Quaternion), [0, 0.5, 0, 0]);
      expect(numbers(hips.keys.last.slopeIn! as Quaternion), [
        0,
        0.25,
        0,
        -0.1,
      ]);
    });

    test('writes one key to a line', () {
      final lines = populated().encode().split('\n');
      final keys = lines.where((line) => line.trimLeft().startsWith('{"at"'));
      // Four, two, two and two keys, and two marks.
      expect(keys, hasLength(12));
      for (final line in keys) {
        expect(jsonDecode(line.trim().replaceAll(RegExp(r',$'), '')), isMap);
      }
    });

    test("says a channel's usual hold once, and only the odd key its own", () {
      final json = populated().toJson();
      final channels = json['channels']! as List;
      final power = channels.first as Map<String, Object?>;
      expect(power['hold'], 'linear');
      final holds = [
        for (final key in power['keys']! as List)
          (key as Map<String, Object?>)['hold'],
      ];
      expect(holds, [null, null, 'shaped', null]);

      // Smooth is what a key does when nothing says otherwise.
      final position = channels[1] as Map<String, Object?>;
      expect(position.containsKey('hold'), isFalse);
    });

    test("sorts keys written out of order, keeping a cut's order", () {
      final clip = load({
        'channels': [
          {
            'property': 'light.power',
            'kind': 'number',
            'keys': [
              {'at': 1, 'value': 5},
              {'at': 0, 'value': 1},
              {'at': 1, 'value': 9},
            ],
          },
        ],
      }).clip;
      final keys = clip.channels.single.keys;
      expect([for (final key in keys) key.value], [1, 5, 9]);
      // The later of two keys at one moment is what it says from then on.
      expect(clip.channels.single.valueAt(1), 9);
    });
  });

  group('reading leniently', () {
    test('drops a key it cannot read, and says so', () {
      final loaded = load({
        'channels': [
          {
            'property': 'transform.position',
            'kind': 'vector',
            'keys': [
              {
                'at': 0,
                'value': [0, 0, 0],
              },
              {
                'at': 1,
                'value': [1, 2],
              },
              {
                'value': [1, 1, 1],
              },
              {
                'at': 2,
                'value': [2, 2, 2],
              },
            ],
          },
        ],
      });
      expect(loaded.clip.channels.single.keys, hasLength(2));
      expect(loaded.problems.single, contains('2 keys'));
    });

    test('drops a second channel moving the same thing', () {
      final channel = {
        'target': 'bulb',
        'property': 'light.power',
        'kind': 'number',
        'keys': [
          {'at': 0, 'value': 1},
        ],
      };
      final loaded = load({
        'channels': [channel, channel],
      });
      expect(loaded.clip.channels, hasLength(1));
      expect(loaded.problems.single, contains('second was left out'));
    });

    test('drops a channel of a kind it does not know', () {
      final loaded = load({
        'channels': [
          {
            'property': 'light.colour',
            'kind': 'colour',
            'keys': [
              {'at': 0, 'value': '#ffffff'},
            ],
          },
        ],
      });
      expect(loaded.clip.channels, isEmpty);
      expect(loaded.problems.single, contains('kind'));
    });

    test('drops a channel moving something its target does not have', () {
      final loaded = load({
        'channels': [
          {
            'bone': 'hips',
            'property': 'rotation',
            'kind': 'vector',
            'keys': [
              {
                'at': 0,
                'value': [0, 0, 0],
              },
            ],
          },
          {
            'property': 'power',
            'kind': 'number',
            'keys': [
              {'at': 0, 'value': 1},
            ],
          },
        ],
      });
      expect(loaded.clip.channels, isEmpty);
      expect(loaded.problems, hasLength(2));
      expect(loaded.problems.first, contains('not something a bone has'));
      expect(loaded.problems.last, contains('names no component'));
    });

    test('drops a mark with no name', () {
      final loaded = load({
        'marks': [
          {'at': 1},
          {'at': 0.5, 'name': 'step'},
        ],
      });
      expect(loaded.clip.marks.single.name, 'step');
      expect(loaded.problems, hasLength(1));
    });

    test('takes the last key or mark as the length when none is given', () {
      final clip = load({
        'channels': [
          {
            'property': 'light.power',
            'kind': 'number',
            'keys': [
              {'at': 0, 'value': 1},
              {'at': 1.5, 'value': 2},
            ],
          },
        ],
        'marks': [
          {'at': 1.75, 'name': 'end'},
        ],
      }).clip;
      expect(clip.duration, 1.75);
      expect(clip.rate, 30);
      expect(clip.whenDone, WhenDone.hold);
    });

    test('keeps a stated length shorter than the keys', () {
      final clip = load({
        'duration': 1,
        'channels': [
          {
            'property': 'light.power',
            'kind': 'number',
            'keys': [
              {'at': 0, 'value': 1},
              {'at': 2, 'value': 2},
            ],
          },
        ],
      }).clip;
      expect(clip.duration, 1);
    });
  });

  group('refusing', () {
    test('a file that is not a clip', () {
      for (final text in ['{"kind": "orblit.scene"}', 'not json', '[1, 2]']) {
        expect(
          () => ClipDocument.decode(text),
          throwsA(isA<ClipFormatException>()),
          reason: text,
        );
      }
    });

    test('a clip from a newer Orblit', () {
      final text = jsonEncode({
        'kind': ClipDocument.marker,
        'formatVersion': ClipDocument.formatVersion + 1,
      });
      expect(
        () => ClipDocument.decode(text),
        throwsA(
          isA<ClipFormatException>().having(
            (error) => error.message,
            'message',
            contains('newer'),
          ),
        ),
      );
    });
  });

  group('rotations', () {
    ClipLoad rotations(List<Map<String, Object?>> keys, {String? hold}) =>
        load({
          'channels': [
            {
              'bone': 'hips',
              'property': 'rotation',
              'kind': 'rotation',
              'hold': ?hold,
              'keys': keys,
            },
          ],
        });

    Quaternion readOne(List<num> value) =>
        rotations([
              {'at': 0, 'value': value},
            ]).clip.channels.single.keys.single.value
            as Quaternion;

    test('are made unit length on the way in', () {
      expect(numbers(readOne([0, 0, 0, 2])), [0, 0, 0, 1]);
    });

    test('already unit length as near as a float gets are left alone', () {
      // One, as a float, but not as a double.
      const value = [0.18257418, 0.36514837, 0.5477226, 0.73029673];
      expect(numbers(readOne(value)), value);
    });

    test('of no length are refused', () {
      final loaded = rotations([
        {
          'at': 0,
          'value': [0, 0, 0, 0],
        },
        {
          'at': 1,
          'value': [0, 0, 0, 1],
        },
      ]);
      expect(loaded.clip.channels.single.keys.single.at, 1);
      expect(loaded.problems, hasLength(1));
    });

    test('keep their slopes as written, of any length', () {
      final keys = rotations(hold: 'curve', [
        {
          'at': 0,
          'value': [0, 0, 0, 1],
          'out': [0, 0, 0, 0],
        },
        {
          'at': 1,
          'value': [0, 0, 0, 1],
          'in': [0, 3, 0, 0],
        },
      ]).clip.channels.single.keys;
      expect(numbers(keys.first.slopeOut! as Quaternion), [0, 0, 0, 0]);
      expect(numbers(keys.last.slopeIn! as Quaternion), [0, 3, 0, 0]);
      expect(keys.first.hold, Hold.curve);
    });
  });

  group('sampling', () {
    test("puts an entity's values by target and property", () {
      final frame = populated().sampleAt(0.25);
      expect(frame.at, 0.25);
      expect(frame.valueOf('bulb', 'light.power'), closeTo(40, 1e-9));
      expect(frame.valueOf('bulb/glow', 'mesh.visible'), isTrue);
      final position = frame.valueOf('', 'transform.position')! as Vector3;
      expect(position.z, greaterThan(0));
      expect(frame.valueOf('bulb', 'light.colour'), isNull);
    });

    test("puts a bone's parts on the bone", () {
      final frame = populated().sampleAt(1);
      final hips = frame.boneOf('', 'hips')!;
      expect(hips.position, isNull);
      expect(hips.scale, isNull);
      final expected = Quaternion.axisAngle(Vector3(0, 1, 0), 0.5);
      expect(hips.rotation!.w, closeTo(expected.w, 1e-12));
      expect(hips.rotation!.y, closeTo(expected.y, 1e-12));
      expect(frame.values.containsKey('hips'), isFalse);
    });

    test('holds the first and last keys past either end', () {
      final clip = populated();
      expect(clip.sampleAt(-1).valueOf('bulb', 'light.power'), 60);
      expect(clip.sampleAt(5).valueOf('bulb/glow', 'mesh.visible'), isFalse);
    });
  });

  test('a mark fires in the step that reaches it, and only that one', () {
    final clip = populated();
    expect(clip.marksBetween(0, 0.25).single.name, 'spark');
    expect(clip.marksBetween(0.25, 1).single.name, 'buzz');
    expect(clip.marksBetween(1, 2), isEmpty);
  });

  test('a channel needs a key', () {
    expect(
      () => ClipChannel<double>(
        target: '',
        property: 'light.power',
        kind: ChannelKind.number,
        keys: const [],
      ),
      throwsArgumentError,
    );
  });

  group('held without knowing the kind', () {
    test('rekeyed takes keys of anything and keeps what they carry', () {
      final ClipChannel<Object> channel = populated().channels[1];
      final moved = channel.rekeyed([
        Key<Object>(
          0.5,
          Vector3(4, 5, 6),
          hold: Hold.curve,
          slopeOut: Vector3(1, 0, 0),
        ),
        ...channel.keys,
      ]);

      expect(moved, isA<ClipChannel<Vector3>>());
      expect(moved.keys.map((key) => key.at), [0, 0.5, 2]);
      expect(moved.keys[1].hold, Hold.curve);
      expect(moved.keys[1].slopeOut, Vector3(1, 0, 0));
      expect(moved.sameAddress(channel), isTrue);
    });

    test('a channel read from a file is typed as what it carries', () {
      final read = ClipDocument.decode(populated().encode()).clip.channels;
      expect(read[0], isA<ClipChannel<double>>());
      expect(read[1], isA<ClipChannel<Vector3>>());
      expect(read[2], isA<ClipChannel<bool>>());
    });

    test('rekeyed refuses a value the channel does not carry', () {
      final ClipChannel<Object> channel = populated().channels[0];
      expect(
        () => channel.rekeyed([Key<Object>(0, Vector3.zero())]),
        throwsA(isA<TypeError>()),
      );
    });

    test('ofKind makes a channel of a kind chosen at run time', () {
      final ChannelKind<Object> kind = ChannelKind.named('number')!;
      final channel = ClipChannel.ofKind(
        target: 'bulb',
        property: 'light.power',
        kind: kind,
        keys: const [Key<Object>(1, 30.0), Key<Object>(0, 10.0)],
      );

      expect(channel, isA<ClipChannel<double>>());
      expect(channel.keys.map((key) => key.at), [0, 1]);
      expect(channel.valueAt(0.5), closeTo(20, 1e-9));
    });
  });
}

import 'package:orblit_input/orblit_input.dart';
import 'package:test/test.dart';

const chromeDualShock =
    'Wireless Controller (STANDARD GAMEPAD Vendor: 054c Product: 09cc)';

class FakeGamepads implements GamepadSource {
  List<GamepadReading?> readings = [];
  final List<({int index, double strong, double weak, Duration duration})>
  rumbles = [];

  @override
  List<GamepadReading?> read() => readings;

  @override
  void rumble(
    int index, {
    required double strong,
    required double weak,
    required Duration duration,
  }) => rumbles.add((
    index: index,
    strong: strong,
    weak: weak,
    duration: duration,
  ));
}

// A standard-order pad: seventeen buttons and four axes.
GamepadReading reading({
  int index = 0,
  String id = chromeDualShock,
  Set<int> pressed = const {},
  Map<int, double> values = const {},
  List<double> axes = const [0, 0, 0, 0],
  bool connected = true,
  bool canRumble = false,
  int buttons = 17,
}) => GamepadReading(
  index: index,
  id: id,
  connected: connected,
  standard: true,
  canRumble: canRumble,
  axes: axes,
  buttons: [
    for (var i = 0; i < buttons; i++)
      (
        pressed: pressed.contains(i),
        value: values[i] ?? (pressed.contains(i) ? 1 : 0),
      ),
  ],
);

void main() {
  late FakeGamepads source;
  late Pads pads;

  setUp(() {
    source = FakeGamepads();
    pads = Pads(backend: GamepadApiPads(source));
  });

  group('the standard order', () {
    test('every button lands on the named position', () {
      const expected = {
        0: PadButton.south,
        1: PadButton.east,
        2: PadButton.west,
        3: PadButton.north,
        4: PadButton.leftShoulder,
        5: PadButton.rightShoulder,
        8: PadButton.select,
        9: PadButton.start,
        10: PadButton.leftStick,
        11: PadButton.rightStick,
        12: PadButton.dpadUp,
        13: PadButton.dpadDown,
        14: PadButton.dpadLeft,
        15: PadButton.dpadRight,
        16: PadButton.guide,
      };
      for (final entry in expected.entries) {
        source.readings = [
          reading(pressed: {entry.key}),
        ];
        final held = pads.poll().first.held.toSet();
        expect(held, {entry.value}, reason: 'button ${entry.key}');
      }
    });

    test('a press is an edge for one poll, then a hold, then a release', () {
      source.readings = [reading()];
      pads.poll();

      source.readings = [
        reading(pressed: {0}),
      ];
      final down = pads.poll();
      expect(down.first.wasPressed(PadButton.south), isTrue);
      expect(down.events.whereType<PadPressed>(), hasLength(1));
      expect(pads.poll().first.wasPressed(PadButton.south), isFalse);

      source.readings = [reading()];
      final up = pads.poll();
      expect(up.first.wasReleased(PadButton.south), isTrue);
      expect(up.events.whereType<PadReleased>(), hasLength(1));
    });

    test('a stick at rest is exactly nought and up is positive', () {
      source.readings = [reading()];
      expect(pads.poll().first.stick(PadStick.left).length, 0);

      // The browser's Y runs down the screen, the same as the hardware's.
      source.readings = [
        reading(axes: [1, -1, 0, 0]),
      ];
      final stick = pads.poll().first.stick(PadStick.left);
      expect(stick.x, closeTo(0.7, 0.3));
      expect(stick.y, greaterThan(0));
    });

    test('a trigger is read from the button value, and is also a button', () {
      source.readings = [
        reading(values: {7: 0.9}, pressed: {7}),
      ];
      final pad = pads.poll().first;
      expect(pad.trigger(PadSide.right), greaterThan(0.5));
      expect(pad.trigger(PadSide.left), 0);
      expect(pad.isDown(PadButton.rightTrigger), isTrue);
    });

    test('a number that is not one is a stick at rest', () {
      source.readings = [
        reading(axes: [double.nan, double.nan, 0, 0]),
      ];
      expect(pads.poll().first.stick(PadStick.left).length, 0);
    });
  });

  group('who the browser says it is', () {
    test('Chrome tags the numbers on the end', () {
      final identity = padIdentityOf(chromeDualShock, 2);
      expect(identity.name, 'Wireless Controller');
      expect(identity.vendor, 0x054c);
      expect(identity.product, 0x09cc);
      expect(identity.path, 'gamepad:2');
    });

    test('Firefox puts them in front', () {
      final identity = padIdentityOf('054c-09cc-Wireless Controller', 0);
      expect(identity.name, 'Wireless Controller');
      expect(identity.vendor, 0x054c);
      expect(identity.product, 0x09cc);
    });

    test('a pad with no numbers keeps its name and gets nought', () {
      final identity = padIdentityOf(
        'Xbox 360 Controller (XInput STANDARD GAMEPAD)',
        0,
      );
      expect(identity.name, 'Xbox 360 Controller');
      expect(identity.vendor, 0);
    });

    test('the numbers choose the labels', () {
      source.readings = [reading()];
      final connected = pads.poll().events.whereType<PadConnected>().single;
      expect(connected.layout.name, 'DualShock 4');
      expect(connected.layout.labels.south, 'Cross');
    });
  });

  group('hot-plug', () {
    test('a browser that shows no pad until a button is pressed', () {
      source.readings = [null, null];
      expect(pads.poll().count, 0);

      source.readings = [reading()];
      final arrived = pads.poll();
      expect(arrived.events.whereType<PadConnected>(), hasLength(1));
      expect(arrived.count, 1);
    });

    for (final gone in <String, List<GamepadReading?>>{
      'drops out of the list': [null],
      'reports itself disconnected': [reading(connected: false)],
    }.entries) {
      test('a pad that ${gone.key} lets go of what it held', () {
        source.readings = [reading()];
        pads.poll();
        source.readings = [
          reading(pressed: {0}, axes: [1, 0, 0, 0]),
        ];
        expect(pads.poll().first.isDown(PadButton.south), isTrue);

        source.readings = gone.value;
        final frame = pads.poll();

        expect(frame.events.map((e) => e.runtimeType), [
          PadReleased,
          PadDisconnected,
        ]);
        expect(frame.pad(0).connected, isFalse);
        expect(frame.pad(0).stick(PadStick.left).length, 0);
      });
    }

    test('the same pad coming back takes its player number again', () {
      source.readings = [reading(index: 0), reading(index: 1, id: 'other')];
      pads.poll();

      source.readings = [null, reading(index: 1, id: 'other')];
      pads.poll();
      source.readings = [reading(index: 0), reading(index: 1, id: 'other')];
      final back = pads.poll();

      expect(back.pad(0).connected, isTrue);
      expect(back.pad(0).identity.path, 'gamepad:0');
    });

    test('a different pad in the same index is a new arrival', () {
      source.readings = [reading()];
      pads.poll();

      source.readings = [reading(id: 'Another Pad')];
      final swapped = pads.poll().events;

      expect(swapped.whereType<PadDisconnected>(), hasLength(1));
      expect(swapped.whereType<PadConnected>(), hasLength(1));
    });
  });

  group('a pad the browser does not recognise', () {
    const odd = 'Odd Pad (Vendor: 1234 Product: 5678)';
    final key = PadMapping.keyOf(0x1234, 0x5678);

    test('is read through the table entry for its numbers', () {
      pads = Pads(
        backend: GamepadApiPads(
          source,
          mappings: {
            key: const PadMapping(buttons: {0: 0x131, 30: 0x130}),
          },
        ),
      );
      source.readings = [
        reading(id: odd, pressed: {0}),
      ];

      // Index 30 is past the end of this pad's buttons, and is no fault.
      expect(pads.poll().first.held.toSet(), {PadButton.east});
    });

    test('is read in the standard order when nobody has written an entry', () {
      source.readings = [
        reading(id: odd, pressed: {0}),
      ];
      expect(pads.poll().first.held.toSet(), {PadButton.south});
    });
  });

  group('rumble', () {
    test('reaches the browser by the pad index', () {
      source.readings = [
        reading(),
        reading(index: 1, id: 'two', canRumble: true),
      ];
      pads.poll();

      expect(pads.canRumble(0), isFalse);
      expect(pads.canRumble(1), isTrue);
      pads.rumble(
        1,
        strong: 0.5,
        weak: 0.25,
        duration: const Duration(milliseconds: 300),
      );
      pads.rumble(0, strong: 1, weak: 1, duration: const Duration(seconds: 1));

      expect(source.rumbles, [
        (
          index: 1,
          strong: 0.5,
          weak: 0.25,
          duration: const Duration(milliseconds: 300),
        ),
      ]);
    });
  });
}

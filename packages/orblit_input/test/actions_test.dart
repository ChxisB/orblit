import 'dart:math' as math;

import 'package:orblit_input/orblit_input.dart';
import 'package:test/test.dart';

import 'action_fixture.dart';

void main() {
  group('Control', () {
    test('is the same control when it is the same kind and name', () {
      expect(const KeyControl('A'), const KeyControl('A'));
      expect(const KeyControl('A').hashCode, const KeyControl('A').hashCode);
      expect(const KeyControl('A'), isNot(const TouchControl('A')));
      expect(
        const PadAxisControl(PadAxis.leftX),
        isNot(const PadAxisControl(PadAxis.leftY)),
      );
    });

    test('goes through JSON and comes back', () {
      const controls = <Control>[
        KeyControl('KeyW'),
        PadButtonControl(PadButton.dpadLeft),
        PadAxisControl(PadAxis.rightTrigger),
        PadAxisControl(PadAxis.leftX, side: AxisSide.positive),
        PadAxisControl(PadAxis.leftX, side: AxisSide.negative),
        PadStickControl(PadStick.right),
        TouchControl('fire'),
      ];

      for (final control in controls) {
        expect(Control.fromJson(control.toJson()), control);
      }
    });

    test('tells the halves of an axis apart', () {
      const both = PadAxisControl(PadAxis.leftX);
      const positive = PadAxisControl(PadAxis.leftX, side: AxisSide.positive);
      const negative = PadAxisControl(PadAxis.leftX, side: AxisSide.negative);

      expect({both, positive, negative}, hasLength(3));
      expect(
        positive,
        const PadAxisControl(PadAxis.leftX, side: AxisSide.positive),
      );
      expect(positive.toJson(), {'padAxisPositive': 'leftX'});
      expect(negative.toJson(), {'padAxisNegative': 'leftX'});
      expect(both.toJson(), {'padAxis': 'leftX'});
    });

    test('rejects JSON that is not a control', () {
      const bad = <Object?>[
        null,
        'KeyW',
        <String, Object?>{},
        {'key': 1},
        {'key': 'A', 'touch': 'B'},
        {'padButton': 'nope'},
        {'padAxis': 'nope'},
        {'padAxisPositive': 'nope'},
        {'padAxisNegative': 3},
        {'padStick': 'nope'},
        {'wat': 'KeyW'},
      ];

      for (final json in bad) {
        expect(
          () => Control.fromJson(json),
          throwsFormatException,
          reason: '$json',
        );
      }
    });
  });

  group('FrameControls', () {
    test('reads each kind of control', () {
      final controls = frame(
        keys: {'KeyW'},
        touch: {'fire'},
        down: {PadButton.south},
        axes: {PadAxis.leftX: 0.6, PadAxis.leftY: 0.8, PadAxis.rightTrigger: 1},
      );

      expect(controls.read(const KeyControl('KeyW')), 1);
      expect(controls.read(const KeyControl('KeyS')), 0);
      expect(controls.read(const TouchControl('fire')), 1);
      expect(controls.read(const TouchControl('jump')), 0);
      expect(controls.read(const PadButtonControl(PadButton.south)), 1);
      expect(controls.read(const PadButtonControl(PadButton.east)), 0);
      expect(controls.read(const PadAxisControl(PadAxis.leftX)), 0.6);
      expect(
        controls.read(const PadStickControl(PadStick.left)),
        closeTo(1, 1e-9),
      );
      expect(controls.stick(PadStick.left).y, 0.8);
    });

    test('reads one half of an axis as 0 to 1', () {
      final controls = frame(axes: {PadAxis.leftX: 0.6, PadAxis.leftY: -0.7});
      const rightOfX = PadAxisControl(PadAxis.leftX, side: AxisSide.positive);
      const leftOfX = PadAxisControl(PadAxis.leftX, side: AxisSide.negative);
      const upOfY = PadAxisControl(PadAxis.leftY, side: AxisSide.positive);
      const downOfY = PadAxisControl(PadAxis.leftY, side: AxisSide.negative);

      expect(controls.read(rightOfX), 0.6);
      expect(controls.read(leftOfX), 0);
      expect(controls.read(upOfY), 0);
      expect(controls.read(downOfY), 0.7);
      expect(controls.read(const PadAxisControl(PadAxis.leftY)), -0.7);
    });

    test('lists what is pressed: keys, buttons, sticks, axes, touch', () {
      final controls = frame(
        keys: {'KeyW'},
        touch: {'fire'},
        down: {PadButton.south},
        axes: {
          PadAxis.leftX: 0.8,
          PadAxis.rightTrigger: 0.9,
          PadAxis.rightX: 0.2,
        },
      );

      expect(controls.active, const [
        KeyControl('KeyW'),
        PadButtonControl(PadButton.south),
        PadStickControl(PadStick.left),
        PadAxisControl(PadAxis.leftX, side: AxisSide.positive),
        PadAxisControl(PadAxis.rightTrigger, side: AxisSide.positive),
        TouchControl('fire'),
      ]);
    });

    test('lists the half of an axis that was pushed', () {
      final controls = frame(axes: {PadAxis.leftX: -0.9});

      expect(controls.active, const [
        PadStickControl(PadStick.left),
        PadAxisControl(PadAxis.leftX, side: AxisSide.negative),
      ]);
    });

    test('lists a trigger once, as the axis that travels', () {
      const triggers = {
        PadButton.leftTrigger: PadAxis.leftTrigger,
        PadButton.rightTrigger: PadAxis.rightTrigger,
      };

      for (final MapEntry(key: button, value: axis) in triggers.entries) {
        final travelled = frame(down: {button}, axes: {axis: 0.9});
        final clicked = frame(down: {button});

        expect(travelled.active, [
          PadAxisControl(axis, side: AxisSide.positive),
        ]);
        expect(clicked.active, [PadButtonControl(button)]);
      }
    });

    test('has nothing pressed when nothing is', () {
      expect(frame().active, isEmpty);
      expect(FrameControls().active, isEmpty);
    });
  });

  group('buttons', () {
    test('go down, stay down and come up, with an edge for each', () {
      final map = actionMap();

      map.update(frame(keys: {'Space'}), now: ms(0));
      expect([map.isDown('jump'), map.wasPressed('jump')], [true, true]);

      map.update(frame(keys: {'Space'}), now: ms(16));
      expect([map.isDown('jump'), map.wasPressed('jump')], [true, false]);

      map.update(frame(), now: ms(32));
      expect([map.isDown('jump'), map.wasReleased('jump')], [false, true]);

      map.update(frame(), now: ms(48));
      expect(map.wasReleased('jump'), isFalse);
    });

    test('are down while any one of their bindings is', () {
      final map = actionMap();
      final both = frame(keys: {'Space'}, down: {PadButton.south});

      map.update(both, now: ms(0));
      map.update(frame(down: {PadButton.south}), now: ms(16));
      expect(map.isDown('jump'), isTrue);
      expect(map.wasReleased('jump'), isFalse);

      map.update(frame(), now: ms(32));
      expect(map.wasReleased('jump'), isTrue);
    });

    test('can be a trigger, which is down from halfway', () {
      final map = ActionMap(
        actions: const [InputAction.button('gas')],
        contexts: [
          InputContext(
            'play',
            bindings: const {
              'gas': [
                Binding(
                  PadAxisControl(PadAxis.rightTrigger),
                  shaping: Shaping.none,
                ),
              ],
            },
          ),
        ],
        active: const ['play'],
      );

      map.update(frame(axes: {PadAxis.rightTrigger: 0.4}), now: ms(0));
      expect(map.isDown('gas'), isFalse);
      map.update(frame(axes: {PadAxis.rightTrigger: 0.6}), now: ms(16));
      expect(map.isDown('gas'), isTrue);
    });

    test('can be a stick pushed one way, with a negative scale', () {
      final map = ActionMap(
        actions: const [InputAction.button('crouch')],
        contexts: [
          InputContext(
            'play',
            bindings: const {
              'crouch': [
                Binding(
                  PadAxisControl(PadAxis.leftY),
                  scale: -1,
                  shaping: Shaping.none,
                ),
              ],
            },
          ),
        ],
        active: const ['play'],
      );

      map.update(frame(axes: {PadAxis.leftY: 0.8}), now: ms(0));
      expect(map.isDown('crouch'), isFalse);
      map.update(frame(axes: {PadAxis.leftY: -0.8}), now: ms(16));
      expect(map.isDown('crouch'), isTrue);
    });
  });

  group('axes', () {
    test('add up what their bindings give', () {
      final map = actionMap();

      map.update(frame(keys: {'KeyD'}), now: ms(0));
      expect(map.value('steer'), 1);
      map.update(frame(keys: {'KeyA'}), now: ms(16));
      expect(map.value('steer'), -1);
      map.update(frame(keys: {'KeyA', 'KeyD'}), now: ms(32));
      expect(map.value('steer'), 0);
      map.update(frame(), now: ms(48));
      expect(map.value('steer'), 0);
    });

    test('stay between -1 and 1', () {
      final map = ActionMap(
        actions: const [InputAction.axis('zoom')],
        contexts: [
          InputContext(
            'play',
            bindings: const {
              'zoom': [
                Binding(KeyControl('KeyP'), scale: 3),
                Binding(KeyControl('KeyQ'), scale: 3),
                Binding(KeyControl('KeyN'), scale: -5),
              ],
            },
          ),
        ],
        active: const ['play'],
      );

      map.update(frame(keys: {'KeyP', 'KeyQ'}), now: ms(0));
      expect(map.value('zoom'), 1);
      map.update(frame(keys: {'KeyN'}), now: ms(16));
      expect(map.value('zoom'), -1);
    });

    test('drop what a binding\'s dead zone takes out', () {
      final map = ActionMap(
        actions: const [InputAction.axis('steer')],
        contexts: [
          InputContext(
            'play',
            bindings: const {
              'steer': [
                Binding(
                  PadAxisControl(PadAxis.leftX),
                  shaping: Shaping(inner: 0.2, outer: 1),
                ),
              ],
            },
          ),
        ],
        active: const ['play'],
      );

      map.update(frame(axes: {PadAxis.leftX: 0.1}), now: ms(0));
      expect(map.value('steer'), 0);
      map.update(frame(axes: {PadAxis.leftX: 1}), now: ms(16));
      expect(map.value('steer'), 1);
    });
  });

  group('two-axis actions', () {
    test('add keys on each half', () {
      final map = actionMap();

      map.update(frame(keys: {'KeyW'}), now: ms(0));
      expect(map.vector('move').storage, [0, 1]);
      map.update(frame(keys: {'KeyA', 'KeyS'}), now: ms(16));
      expect(map.vector('move').length, closeTo(1, 1e-9));
      expect(map.vector('move').x, lessThan(0));
      expect(map.vector('move').y, lessThan(0));
    });

    test('are no faster on the diagonal', () {
      final map = actionMap();

      map.update(frame(keys: {'KeyW', 'KeyD'}), now: ms(0));

      expect(map.vector('move').x, closeTo(math.sqrt1_2, 1e-9));
      expect(map.vector('move').y, closeTo(math.sqrt1_2, 1e-9));
    });

    test('read a whole stick', () {
      final map = actionMap();

      map.update(
        frame(axes: {PadAxis.leftX: 0.3, PadAxis.leftY: 0.4}),
        now: ms(0),
      );

      expect(map.vector('move').x, closeTo(0.3, 1e-9));
      expect(map.vector('move').y, closeTo(0.4, 1e-9));
    });

    test('add a stick and keys, then bring the total back to 1', () {
      final map = actionMap();

      map.update(
        frame(keys: {'KeyD'}, axes: {PadAxis.leftX: 0.9}),
        now: ms(0),
      );

      expect(map.vector('move').x, closeTo(1, 1e-9));
      expect(map.vector('move').y, 0);
    });

    test('shape a stick as a stick', () {
      final map = ActionMap(
        actions: const [InputAction.axis2('look')],
        contexts: [
          InputContext(
            'play',
            bindings: const {
              'look': [
                Binding(
                  PadStickControl(PadStick.right),
                  shaping: Shaping(inner: 0.2, outer: 1),
                ),
              ],
            },
          ),
        ],
        active: const ['play'],
      );

      map.update(
        frame(axes: {PadAxis.rightX: 0.15, PadAxis.rightY: 0.15}),
        now: ms(0),
      );
      final gentle = map.vector('look');
      expect(gentle.length, greaterThan(0), reason: '0.21 is past 0.2');
      expect(gentle.x, closeTo(gentle.y, 1e-9), reason: 'still a diagonal');

      map.update(
        frame(axes: {PadAxis.rightX: 0.1, PadAxis.rightY: 0.1}),
        now: ms(16),
      );
      expect(map.vector('look').length, 0, reason: '0.14 is inside');
    });

    test('hand out a copy', () {
      final map = actionMap();
      map.update(frame(keys: {'KeyW'}), now: ms(0));

      map.vector('move').setValues(5, 5);

      expect(map.vector('move').storage, [0, 1]);
    });
  });

  group('asking', () {
    test('about an action that does not exist is an argument error', () {
      final map = actionMap();

      expect(() => map.isDown('nope'), throwsArgumentError);
      expect(() => map.value('nope'), throwsArgumentError);
      expect(() => map.vector('nope'), throwsArgumentError);
    });

    test('in the wrong way is a state error', () {
      final map = actionMap();

      expect(() => map.isDown('steer'), throwsStateError);
      expect(() => map.wasTapped('move'), throwsStateError);
      expect(() => map.value('jump'), throwsStateError);
      expect(() => map.value('move'), throwsStateError);
      expect(() => map.vector('steer'), throwsStateError);
    });
  });

  group('building', () {
    test('refuses two actions of one name', () {
      expect(
        () => ActionMap(
          actions: const [InputAction.button('a'), InputAction.axis('a')],
          contexts: const [],
        ),
        throwsArgumentError,
      );
    });

    test('refuses two contexts of one name', () {
      expect(
        () => ActionMap(
          actions: const [],
          contexts: [
            InputContext('a', bindings: const {}),
            InputContext('a', bindings: const {}),
          ],
        ),
        throwsArgumentError,
      );
    });

    test('refuses a binding for an action that does not exist', () {
      expect(
        () => ActionMap(
          actions: const [],
          contexts: [
            InputContext(
              'a',
              bindings: const {
                'jump': [Binding(KeyControl('Space'))],
              },
            ),
          ],
        ),
        throwsArgumentError,
      );
    });

    test('refuses a whole stick on anything but a two-axis action', () {
      for (final action in const [
        InputAction.button('x'),
        InputAction.axis('x'),
      ]) {
        expect(
          () => ActionMap(
            actions: [action],
            contexts: [
              InputContext(
                'a',
                bindings: const {
                  'x': [Binding(PadStickControl(PadStick.left))],
                },
              ),
            ],
          ),
          throwsArgumentError,
        );
      }
    });

    test('refuses a scale that is not a number', () {
      expect(
        () => ActionMap(
          actions: const [InputAction.axis('x')],
          contexts: [
            InputContext(
              'a',
              bindings: const {
                'x': [Binding(KeyControl('K'), scale: double.nan)],
              },
            ),
          ],
        ),
        throwsArgumentError,
      );
    });

    test('refuses to start in a context that does not exist', () {
      expect(
        () => ActionMap(actions: const [], contexts: const [], active: ['a']),
        throwsArgumentError,
      );
    });
  });

  group('contexts', () {
    test('start off unless asked for', () {
      final map = actionMap(active: const []);

      map.update(frame(keys: {'Space'}), now: ms(0));

      expect(map.isActive('play'), isFalse);
      expect(map.isDown('jump'), isFalse);
    });

    test('shadow only the actions they bind', () {
      final map = actionMap(active: const ['play', 'vehicle']);

      map.update(frame(keys: {'Space', 'KeyD'}), now: ms(0));
      expect(map.isDown('jump'), isFalse, reason: 'the vehicle took jump');
      expect(map.value('steer'), 1, reason: 'the vehicle left steer alone');

      map.update(frame(keys: {'KeyE'}), now: ms(16));
      expect(map.isDown('jump'), isTrue);

      map.deactivate('vehicle');
      map.update(frame(), now: ms(32));
      map.update(frame(keys: {'Space'}), now: ms(48));
      expect(map.isDown('jump'), isTrue);
    });

    test('that block hide everything under them', () {
      final map = actionMap(active: const ['play', 'menu']);

      map.update(frame(keys: {'KeyD', 'KeyF', 'Enter'}), now: ms(0));

      expect(map.isDown('confirm'), isTrue);
      expect(map.isDown('fire'), isFalse);
      expect(map.value('steer'), 0);
      expect(map.vector('move').length, 0);

      map.deactivate('menu');
      map.update(frame(keys: {'KeyD', 'KeyF'}), now: ms(16));
      expect(map.value('steer'), 1, reason: 'an axis follows the key at once');
      map.update(frame(), now: ms(32));
      map.update(frame(keys: {'KeyF'}), now: ms(48));
      expect(map.isDown('fire'), isTrue);
    });

    test('that block still let a context above them speak', () {
      final map = actionMap(active: const ['play', 'menu', 'vehicle']);

      map.update(frame(keys: {'KeyE', 'KeyD'}), now: ms(0));

      expect(map.isDown('jump'), isTrue, reason: 'above the menu');
      expect(map.value('steer'), 0, reason: 'below the menu');
    });

    test('do not press what was already held when a menu closes', () {
      final map = actionMap(active: const ['play', 'menu']);
      map.update(frame(keys: {'Space'}), now: ms(0));
      map.update(frame(keys: {'Space'}), now: ms(16));

      map.deactivate('menu');
      map.update(frame(keys: {'Space'}), now: ms(32));
      map.update(frame(keys: {'Space'}), now: ms(48));
      expect([map.isDown('jump'), map.wasPressed('jump')], [false, false]);

      map.update(frame(), now: ms(64));
      map.update(frame(keys: {'Space'}), now: ms(80));
      expect([map.isDown('jump'), map.wasPressed('jump')], [true, true]);
    });

    test('release a button that is hidden while it is down', () {
      final map = actionMap();
      map.update(frame(keys: {'Space'}), now: ms(0));

      map.activate('menu');
      map.update(frame(keys: {'Space'}), now: ms(16));

      expect([map.isDown('jump'), map.wasReleased('jump')], [false, true]);
      expect(map.wasTapped('jump'), isFalse, reason: 'it was cut off');
    });

    test('release a button whose binding is taken over while it is down', () {
      final map = actionMap(active: const ['play']);
      map.update(frame(keys: {'Space'}), now: ms(0));
      map.update(frame(keys: {'Space'}), now: ms(16));

      map.activate('vehicle');
      map.update(frame(keys: {'Space'}), now: ms(32));
      expect(
        [map.isDown('jump'), map.wasReleased('jump'), map.wasTapped('jump')],
        [false, true, false],
      );

      map.deactivate('vehicle');
      map.update(frame(keys: {'Space'}), now: ms(48));
      expect(
        [map.isDown('jump'), map.wasPressed('jump')],
        [false, false],
        reason: 'Space has been down all along',
      );

      map.update(frame(), now: ms(64));
      map.update(frame(keys: {'Space'}), now: ms(80));
      expect(map.wasPressed('jump'), isTrue);
    });

    test('release a button whose key is rebound while it is down', () {
      final map = actionMap();
      map.update(frame(keys: {'Space'}), now: ms(0));

      map.bindings.rebind('jump', const KeyControl('KeyJ'), context: 'play');
      map.update(frame(keys: {'Space'}), now: ms(16));

      expect(
        [map.isDown('jump'), map.wasReleased('jump'), map.wasTapped('jump')],
        [false, true, false],
      );
    });

    test('go to the top when activated again', () {
      final map = actionMap(active: const ['play', 'vehicle']);

      map.activate('play');
      map.update(frame(keys: {'Space'}), now: ms(0));
      expect(map.isDown('jump'), isTrue);

      map.update(frame(keys: {'KeyE'}), now: ms(16));
      expect(map.isDown('jump'), isFalse, reason: 'play now shadows vehicle');
    });

    test('are on once however often they are activated', () {
      final map = actionMap();

      map.activate('play');
      map.activate('play');
      map.deactivate('play');

      expect(map.isActive('play'), isFalse);
    });

    test('are refused when they do not exist', () {
      final map = actionMap();

      expect(() => map.activate('nope'), throwsArgumentError);
      map.deactivate('nope');
    });
  });

  group('gestures', () {
    test('a short press is a tap when it comes up', () {
      final map = actionMap();

      map.update(frame(keys: {'Space'}), now: ms(0));
      expect(map.wasTapped('jump'), isFalse);
      map.update(frame(), now: ms(100));
      expect(map.wasTapped('jump'), isTrue);
      map.update(frame(), now: ms(116));
      expect(map.wasTapped('jump'), isFalse);
    });

    test('a long press is not a tap', () {
      final map = actionMap();

      map.update(frame(keys: {'Space'}), now: ms(0));
      map.update(frame(), now: ms(300));

      expect(map.wasTapped('jump'), isFalse);
      expect(map.wasReleased('jump'), isTrue);
    });

    test('a press that lasts is held once', () {
      final map = actionMap();
      map.update(frame(keys: {'Space'}), now: ms(0));

      map.update(frame(keys: {'Space'}), now: ms(399));
      expect(map.wasHeld('jump'), isFalse);
      map.update(frame(keys: {'Space'}), now: ms(400));
      expect(map.wasHeld('jump'), isTrue);
      map.update(frame(keys: {'Space'}), now: ms(500));
      expect(map.wasHeld('jump'), isFalse);

      map.update(frame(), now: ms(516));
      map.update(frame(keys: {'Space'}), now: ms(532));
      map.update(frame(keys: {'Space'}), now: ms(932));
      expect(map.wasHeld('jump'), isTrue, reason: 'the next press is new');
    });

    test('a second press soon after a tap is a double tap', () {
      final map = actionMap();
      map.update(frame(keys: {'Space'}), now: ms(0));
      map.update(frame(), now: ms(100));

      map.update(frame(keys: {'Space'}), now: ms(250));

      expect(map.wasDoubleTapped('jump'), isTrue);
      expect(map.wasPressed('jump'), isTrue);
    });

    test('a second press that is too late is not', () {
      final map = actionMap();
      map.update(frame(keys: {'Space'}), now: ms(0));
      map.update(frame(), now: ms(100));

      map.update(frame(keys: {'Space'}), now: ms(450));

      expect(map.wasDoubleTapped('jump'), isFalse);
    });

    test('a double tap does not start another', () {
      final map = actionMap();
      map.update(frame(keys: {'Space'}), now: ms(0));
      map.update(frame(), now: ms(50));
      map.update(frame(keys: {'Space'}), now: ms(100));
      map.update(frame(), now: ms(150));

      map.update(frame(keys: {'Space'}), now: ms(200));

      expect(map.wasDoubleTapped('jump'), isFalse);
    });

    test('use the times an action was given', () {
      final map = ActionMap(
        actions: const [
          InputAction.button(
            'charge',
            gestures: Gestures(hold: Duration(seconds: 1)),
          ),
        ],
        contexts: [
          InputContext(
            'play',
            bindings: const {
              'charge': [Binding(KeyControl('KeyC'))],
            },
          ),
        ],
        active: const ['play'],
      );
      map.update(frame(keys: {'KeyC'}), now: ms(0));

      map.update(frame(keys: {'KeyC'}), now: ms(999));
      expect(map.wasHeld('charge'), isFalse);
      map.update(frame(keys: {'KeyC'}), now: ms(1000));
      expect(map.wasHeld('charge'), isTrue);
    });
  });
}

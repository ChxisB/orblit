@TestOn('browser')
library;

import 'dart:js_interop';
import 'dart:js_interop_unsafe';

import 'package:orblit_input/orblit_input.dart';
import 'package:orblit_input/src/web.dart';
import 'package:test/test.dart';
import 'package:web/web.dart' as web;

// Run with `dart test -p chrome test/web_test.dart`. The browser has no pad
// to offer a test, so `navigator.getGamepads` is replaced by one that returns
// objects shaped as the specification says. That proves the code reads the
// real object model and the right properties of it; it does not prove any
// make of pad.

final JSObject navigator = web.window.navigator as JSObject;

/// A gamepad object as the specification describes it.
JSObject gamepad({
  int index = 0,
  String id = 'Test Pad (STANDARD GAMEPAD Vendor: 054c Product: 09cc)',
  Set<int> pressed = const {},
  bool actuator = true,
  List<Map<String, Object?>>? effects,
}) {
  final pad =
      {
            'index': index,
            'id': id,
            'connected': true,
            'mapping': 'standard',
            'axes': [0.0, 0.0, 0.0, 0.0],
            'buttons': [
              for (var i = 0; i < 17; i++)
                {
                  'pressed': pressed.contains(i),
                  'value': pressed.contains(i) ? 1.0 : 0.0,
                },
            ],
          }.jsify()!
          as JSObject;
  if (actuator) {
    final motors = {'type': 'dual-rumble'}.jsify()! as JSObject;
    motors['playEffect'] = ((JSString type, JSObject parameters) {
      effects?.add({
        'type': type.toDart,
        'duration': (parameters['duration']! as JSNumber).toDartInt,
        'strong': (parameters['strongMagnitude']! as JSNumber).toDartDouble,
        'weak': (parameters['weakMagnitude']! as JSNumber).toDartDouble,
      });
      return Future<JSString>.value('complete'.toJS).toJS;
    }).toJS;
    pad['vibrationActuator'] = motors;
  }
  return pad;
}

void offer(List<JSObject?> pads) {
  navigator['getGamepads'] = (() => pads.map((pad) => pad).toList().toJS).toJS;
}

void main() {
  tearDown(() => navigator.delete('getGamepads'.toJS));

  test('a browser pad is read as plain data', () {
    offer([
      gamepad(pressed: {0}),
      null,
    ]);

    final readings = const WebGamepads().read();

    expect(readings, hasLength(2));
    expect(readings[1], isNull);
    final first = readings[0]!;
    expect(first.id, startsWith('Test Pad'));
    expect(first.standard, isTrue);
    expect(first.canRumble, isTrue);
    expect(first.buttons, hasLength(17));
    expect(first.buttons[0].pressed, isTrue);
    expect(first.axes, [0, 0, 0, 0]);
  });

  test('Pads.open finds the browser pad and its button', () {
    offer([
      gamepad(pressed: {0}),
    ]);

    final frame = Pads.open().poll();

    expect(frame.count, 1);
    expect(frame.first.isDown(PadButton.south), isTrue);
    expect(frame.first.identity.vendor, 0x054c);
  });

  test('a pad pulled out of the browser lets go and disconnects', () {
    offer([
      gamepad(pressed: {0}),
    ]);
    final pads = Pads.open();
    pads.poll();

    offer([null]);
    final events = pads.poll().events;

    expect(events.map((e) => e.runtimeType), [PadReleased, PadDisconnected]);
  });

  test('rumble plays a dual-rumble effect on the pad', () {
    final effects = <Map<String, Object?>>[];
    offer([gamepad(effects: effects)]);
    final pads = Pads.open();
    pads.poll();

    expect(pads.canRumble(0), isTrue);
    pads.rumble(
      0,
      strong: 0.5,
      weak: 0.25,
      duration: const Duration(milliseconds: 300),
    );

    expect(effects, [
      {'type': 'dual-rumble', 'duration': 300, 'strong': 0.5, 'weak': 0.25},
    ]);
  });

  test('a pad with no actuator says it cannot rumble', () {
    offer([gamepad(actuator: false)]);
    final pads = Pads.open();
    pads.poll();

    expect(pads.canRumble(0), isFalse);
  });
}

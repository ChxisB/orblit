import 'dart:convert';

import 'package:orblit_input/orblit_input.dart';
import 'package:orblit_parity_tool/fixture.dart';
import 'package:vector_math/vector_math_64.dart';

const _nan = double.nan;
const _inf = double.infinity;

/// The generator the audio exporter uses, so a case list is the same on every
/// run.
final class _Rng {
  _Rng(this._seed);

  int _seed;

  int next(int below) {
    _seed = (_seed * 1103515245 + 12345) & 0x7fffffff;
    return (_seed >> 8) % below;
  }

  T pick<T>(List<T> from) => from[next(from.length)];

  bool chance(int outOf) => next(outOf) == 0;
}

const _actions = [
  InputAction.button('jump'),
  InputAction.button('fire'),
  InputAction.button('confirm'),
  InputAction.axis('steer'),
  InputAction.axis2('move'),
];

const _buttonNames = ['jump', 'fire', 'confirm'];
const _contextNames = ['play', 'vehicle', 'menu'];
const _keys = [
  'Space',
  'KeyA',
  'KeyD',
  'KeyS',
  'KeyW',
  'KeyF',
  'KeyE',
  'Enter',
  'KeyJ',
  'KeyX',
];
const _regions = ['jump', 'fire', 'pad'];

final _controls = <Control>[
  for (final key in _keys) KeyControl(key),
  for (final button in PadButton.values) PadButtonControl(button),
  for (final axis in PadAxis.values)
    for (final side in AxisSide.values) PadAxisControl(axis, side: side),
  for (final stick in PadStick.values) PadStickControl(stick),
  for (final region in _regions) TouchControl(region),
];

/// Gameplay on the keyboard and a pad, a vehicle that rebinds the jump key and
/// leaves the rest, and a menu that blocks.
List<InputContext> _contexts(String jumpKey) => [
  InputContext(
    'play',
    bindings: {
      'jump': [
        Binding(KeyControl(jumpKey)),
        const Binding(PadButtonControl(PadButton.south)),
      ],
      'fire': const [Binding(KeyControl('KeyF'))],
      'steer': const [
        Binding(KeyControl('KeyA'), scale: -1),
        Binding(KeyControl('KeyD')),
      ],
      'move': const [
        Binding(KeyControl('KeyA'), scale: -1),
        Binding(KeyControl('KeyD')),
        Binding(KeyControl('KeyS'), scale: -1, part: AxisPart.y),
        Binding(KeyControl('KeyW'), part: AxisPart.y),
        Binding(PadStickControl(PadStick.left), shaping: Shaping.none),
      ],
    },
  ),
  InputContext(
    'vehicle',
    bindings: const {
      'jump': [Binding(KeyControl('KeyE'))],
    },
  ),
  InputContext(
    'menu',
    blocks: true,
    bindings: const {
      'confirm': [Binding(KeyControl('Enter')), Binding(KeyControl('Space'))],
    },
  ),
];

ActionMap _actionMap(List<String> active, String jumpKey) =>
    ActionMap(actions: _actions, contexts: _contexts(jumpKey), active: active);

/// A value as a case holds it. JSON has no NaN or infinity, so a double that
/// has none travels as `{"n": "NaN"}`, which the C# side reads back. A string
/// "NaN" stays a string.
Object? _spec(Object? value) => switch (value) {
  final double d when !d.isFinite => {'n': number(d)},
  final List<Object?> l => [for (final e in l) _spec(e)],
  _ => value,
};

/// The kind of error [body] throws, or null when it does not throw.
///
/// The kinds are the ones a caller can tell apart: the C# side maps each to
/// its own exception type.
String? _failure(void Function() body) {
  try {
    body();
    return null;
  } on BindingClash {
    return 'clash';
  } on RangeError {
    return 'range';
  } on ArgumentError {
    return 'argument';
  } on FormatException {
    return 'format';
  } on StateError {
    return 'state';
  }
}

Map<String, Object?> _run(Object? Function() body) {
  Object? value;
  final error = _failure(() => value = tree(body()));
  return error == null ? {'value': value} : {'error': error};
}

Object? _decoded(Object? json) => jsonDecode(jsonEncode(json));

List<Map<String, Object?>> _controlCases() {
  const bad = <Object?>[
    null,
    42,
    'key',
    [],
    {},
    {'key': 1},
    {'key': 'A', 'touch': 'x'},
    {'padButton': 'nope'},
    {'padButton': 3},
    {'padAxis': 'nope'},
    {'padAxisPositive': 'leftX', 'padAxisNegative': 'leftY'},
    {'padAxisNegative': 'guide'},
    {'padStick': 'middle'},
    {'touch': 3},
    {'unknown': 'x'},
    {'key': ''},
    {'key': null},
  ];
  return [
    for (final control in _controls)
      {
        'op': 'control',
        'json': control.toJson(),
        'expect': _run(() => Control.fromJson(control.toJson()).toJson()),
      },
    for (final json in bad.map(_decoded))
      {
        'op': 'control',
        'json': json,
        'expect': _run(() => Control.fromJson(json).toJson()),
      },
  ];
}

const _levels = <double>[0, 0.3, 0.5, 0.6, 0.9, 1, -0.3, -0.5, -0.6, -1, _nan];
const _vectors = <List<double>>[
  [0.5, 0],
  [0, 1],
  [0.3, 0.4],
  [1, 1],
  [-0.7, 0.1],
  [3, 4],
  [0, 0],
];

/// What the devices hold, changing a little each frame so that presses last.
final class _Devices {
  final keys = <String>{};
  final touch = <String>{};
  final down = <PadButton>{};
  final axes = <PadAxis, double>{};
  final sticks = <PadStick, List<double>>{};

  void drift(_Rng rng) {
    _toggle(rng, keys, _keys, 5);
    _toggle(rng, touch, _regions, 4);
    _toggle(rng, down, PadButton.values, 8);
    for (final axis in PadAxis.values) {
      if (rng.chance(3)) axes[axis] = rng.pick(_levels);
    }
    for (final stick in PadStick.values) {
      if (!rng.chance(6)) continue;
      if (rng.chance(3)) {
        sticks.remove(stick);
      } else {
        sticks[stick] = rng.pick(_vectors);
      }
    }
  }

  void _toggle<T>(_Rng rng, Set<T> held, List<T> pool, int outOf) {
    for (final item in pool) {
      if (rng.chance(outOf) && !held.remove(item)) held.add(item);
    }
  }

  Map<String, Object?> spec() => {
    'keys': keys.toList(),
    'touch': touch.toList(),
    'down': [for (final button in down) button.name],
    'axes': {for (final e in axes.entries) e.key.name: number(e.value)},
    'sticks': {
      for (final e in sticks.entries) e.key.name: [e.value[0], e.value[1]],
    },
  };

  FrameControls frame() => FrameControls(
    keys: {...keys},
    touch: {...touch},
    sticks: {
      for (final e in sticks.entries) e.key: Vector2(e.value[0], e.value[1]),
    },
    pad: PadState(
      slot: 0,
      identity: const PadIdentity(name: 'test'),
      down: {...down},
      pressed: const {},
      released: const {},
      axes: {...axes},
      raw: {...axes},
    ),
  );
}

List<Map<String, Object?>> _frameCases(_Rng rng) {
  final devices = _Devices();
  return [
    for (var i = 0; i < 150; i++)
      () {
        devices.drift(rng);
        final frame = devices.frame();
        return {
          'op': 'frame',
          'frame': devices.spec(),
          'expect': {
            'reads': [for (final c in _controls) number(frame.read(c))],
            'sticks': [
              for (final stick in PadStick.values)
                [number(frame.stick(stick).x), number(frame.stick(stick).y)],
            ],
            'active': [for (final c in frame.active) c.toJson()],
          },
        };
      }(),
  ];
}

const _gaps = <int>[16, 16, 16, 50, 120, 260, 310, 450, 900];

Map<String, Object?> _snapshot(ActionMap map) => {
  for (final name in _buttonNames)
    name: [
      map.isDown(name),
      map.wasPressed(name),
      map.wasReleased(name),
      map.wasHeld(name),
      map.wasTapped(name),
      map.wasDoubleTapped(name),
    ],
  'steer': number(map.value('steer')),
  'move': [number(map.vector('move').x), number(map.vector('move').y)],
};

Map<String, Object?> _bindings(BindingTable table) => {
  for (final context in _contextNames)
    context: {
      for (final e in table.bindingsIn(context).entries)
        e.key: [
          for (final b in e.value)
            {
              'control': b.control.toJson(),
              'scale': number(b.scale),
              'part': b.part.name,
            },
        ],
    },
};

String _someContext(_Rng rng) =>
    rng.chance(12) ? 'nope' : rng.pick(_contextNames);

String _someAction(_Rng rng) => rng.chance(12)
    ? 'nope'
    : rng.pick(['jump', 'fire', 'confirm', 'steer', 'move']);

const _asks = ['isDown', 'value', 'vector'];

Object? _ask(ActionMap map, String action, String ask) => switch (ask) {
  'isDown' => map.isDown(action),
  'value' => number(map.value(action)),
  _ => [number(map.vector(action).x), number(map.vector(action).y)],
};

/// One scripted run of an action map: frames, context switches, rebindings
/// and clash questions, with what each gave.
Map<String, Object?> _session(_Rng rng, List<Object?> profiles) {
  final active = [
    for (var i = rng.next(4); i > 0; i--) rng.pick(_contextNames),
  ];
  final jumpKey = rng.pick(['Space', 'KeyJ']);
  final map = _actionMap(active, jumpKey);
  final devices = _Devices();
  var now = 0;
  final steps = <Map<String, Object?>>[];
  final results = <Map<String, Object?>>[];
  for (var i = 8 + rng.next(14); i > 0; i--) {
    final roll = rng.next(100);
    if (roll < 62) {
      devices.drift(rng);
      now += rng.pick(_gaps);
      steps.add({'op': 'update', 'frame': devices.spec(), 'ms': now});
      map.update(devices.frame(), now: Duration(milliseconds: now));
      results.add({'value': _snapshot(map)});
    } else if (roll < 72) {
      final context = _someContext(rng);
      final on = rng.chance(2);
      steps.add({'op': on ? 'activate' : 'deactivate', 'context': context});
      results.add(
        _run(() {
          on ? map.activate(context) : map.deactivate(context);
          return null;
        }),
      );
    } else if (roll < 76) {
      final action = _someAction(rng);
      final ask = rng.pick(_asks);
      steps.add({'op': 'ask', 'action': action, 'ask': ask});
      results.add(_run(() => _ask(map, action, ask)));
    } else {
      final action = _someAction(rng);
      final control = rng.pick(
        rng.chance(2) ? _controls.sublist(0, 10) : _controls,
      );
      final context = _someContext(rng);
      final slot = rng.chance(8) ? rng.next(5) - 1 : rng.next(2);
      if (roll < 84) {
        steps.add({
          'op': 'clash',
          'action': action,
          'control': control.toJson(),
          'context': context,
          'slot': slot,
        });
        results.add(
          _run(
            () => map.bindings.clash(
              action,
              control,
              context: context,
              slot: slot,
            ),
          ),
        );
        continue;
      }
      final swap = rng.chance(3);
      final scale = rng.chance(2)
          ? null
          : rng.pick(const [-1.0, 0.5, 2.0, 0.0, _nan, _inf]);
      final part = rng.chance(2) ? null : rng.pick(AxisPart.values);
      steps.add({
        'op': 'rebind',
        'action': action,
        'control': control.toJson(),
        'context': context,
        'slot': slot,
        'swap': swap,
        'scale': _spec(scale),
        'part': part?.name,
      });
      results.add(
        _run(() {
          map.bindings.rebind(
            action,
            control,
            context: context,
            slot: slot,
            swap: swap,
            scale: scale,
            part: part,
          );
          return null;
        }),
      );
    }
  }
  profiles.add(_decoded(map.bindings.toJson()));
  return {
    'op': 'session',
    'jumpKey': jumpKey,
    'active': active,
    'steps': steps,
    'expect': {
      'steps': results,
      'saved': tree(map.bindings.toJson()),
      'bindings': _bindings(map.bindings),
    },
  };
}

const _junk = <Object?>[
  null,
  3,
  'x',
  [],
  {},
  true,
  {'key': 'KeyZ'},
  {'padStick': 'left'},
  {'key': 'KeyZ', 'scale': 2},
  {'key': 'KeyZ', 'scale': 'big'},
  {'key': 'KeyZ', 'part': 'y'},
  {'key': 'KeyZ', 'part': 'z'},
  {'padAxisNegative': 'leftX', 'scale': -1},
];

/// [json] with one value somewhere inside it replaced, or one name changed.
Object? _mutate(Object? json, _Rng rng) {
  switch (json) {
    case final Map<String, Object?> map when map.isNotEmpty:
      final key = rng.pick(map.keys.toList());
      if (rng.chance(6)) {
        return {
          for (final e in map.entries) e.key == key ? 'zzz' : e.key: e.value,
        };
      }
      return {
        ...map,
        key: rng.chance(3) ? rng.pick(_junk) : _mutate(map[key], rng),
      };
    case final List<Object?> list when list.isNotEmpty:
      final index = rng.next(list.length);
      return [...list]
        ..[index] = rng.chance(3) ? rng.pick(_junk) : _mutate(list[index], rng);
    default:
      return rng.pick(_junk);
  }
}

Map<String, Object?> _loadCase(Object? written) {
  // A literal's static type is not what a decoded file has, and the loader checks it.
  final profile = _decoded(written);
  final table = _actionMap(const [], 'Space').bindings
    ..rebind('jump', const KeyControl('KeyZ'), context: 'play');
  final error = _failure(() => table.load(profile));
  return {
    'op': 'load',
    'profile': profile,
    'expect': {
      'error': error,
      'saved': tree(table.toJson()),
      'bindings': _bindings(table),
    },
  };
}

List<Map<String, Object?>> _loadCases(_Rng rng, List<Object?> saved) {
  const handWritten = <Object?>[
    null,
    [],
    'x',
    {},
    {'contexts': 3},
    {'contexts': <Object?>[]},
    {
      'contexts': {'play': 3},
    },
    {
      'contexts': {
        'play': {'jump': 3},
      },
    },
    {
      'contexts': {
        'play': {
          'jump': [3],
        },
      },
    },
    {
      'contexts': {
        'play': {
          'jump': [
            {'key': 'KeyZ'},
            null,
          ],
        },
      },
    },
    {
      'contexts': {
        'play': {
          'jump': [
            {'key': 'KeyQ', 'scale': 'x'},
          ],
        },
      },
    },
    {
      'contexts': {
        'play': {
          'steer': [
            {'padStick': 'left'},
          ],
        },
      },
    },
    {
      'contexts': {
        'nowhere': {
          'jump': [
            {'key': 'KeyZ'},
          ],
        },
      },
    },
    {
      'contexts': {
        'play': {
          'nothing': [
            {'key': 'KeyZ'},
          ],
        },
      },
    },
  ];
  return [
    for (final profile in handWritten) _loadCase(profile),
    for (final profile in saved) _loadCase(profile),
    for (var i = 0; i < 140; i++) _loadCase(_mutate(rng.pick(saved), rng)),
  ];
}

Map<String, Object?> _nextCase(_Rng rng) {
  const pool = <Control>[
    KeyControl('Space'),
    KeyControl('KeyA'),
    PadButtonControl(PadButton.south),
    PadStickControl(PadStick.left),
    PadAxisControl(PadAxis.leftX, side: AxisSide.negative),
    PadAxisControl(PadAxis.leftX, side: AxisSide.positive),
    PadAxisControl(PadAxis.leftTrigger),
    TouchControl('jump'),
  ];
  final kind = rng.pick(ActionKind.values);
  final next = NextInput(kind);
  final held = <Control>[];
  final frames = <List<Object?>>[];
  final captured = <Object?>[];
  for (var i = 4 + rng.next(8); i > 0; i--) {
    for (final control in pool) {
      if (rng.chance(3) && !held.remove(control)) held.add(control);
    }
    frames.add([for (final c in held) c.toJson()]);
    next.update(_Pressed([...held]));
    captured.add(next.captured?.toJson());
  }
  return {
    'op': 'next',
    'kind': kind.name,
    'frames': frames,
    'expect': captured,
  };
}

final class _Pressed implements PressedControls {
  _Pressed(this.active);

  @override
  final Iterable<Control> active;
}

const _payloads = <Object?>[
  true,
  false,
  null,
  'yes',
  1,
  0,
  [0.5, -0.25],
  [1, 0],
  [3, 4],
  [0, 0],
  [0.0, 0.0],
  [1],
  [1, 2, 3],
  ['a', 1],
  [0.5, true],
  [_nan, 0],
  [0, _inf],
  [-_inf, 0],
  [1e-300, 0],
  [0.6, 0.8],
  [10, -20],
  [-0.0, 0],
];

Map<String, Object?> _touchCase(_Rng rng) {
  final touch = TouchControls();
  final steps = <Map<String, Object?>>[];
  final results = <Map<String, Object?>>[];
  for (var i = 6 + rng.next(10); i > 0; i--) {
    final payload = rng.pick(_payloads);
    switch (rng.next(5)) {
      case 0 || 1:
        final region = rng.pick(_regions);
        steps.add({'op': 'hold', 'region': region, 'payload': _spec(payload)});
        touch.hold(region, payload);
      case 2:
        final stick = rng.pick(PadStick.values);
        steps.add({
          'op': 'moveStick',
          'stick': stick.name,
          'payload': _spec(payload),
        });
        touch.moveStick(stick, payload);
      case 3:
        steps.add({'op': 'swipe', 'payload': _spec(payload)});
        touch.swipe(payload);
      default:
        steps.add({'op': 'endFrame'});
        touch.endFrame();
    }
    results.add({
      'held': touch.held.toList(),
      'sticks': {
        for (final e in touch.sticks.entries)
          e.key.name: [number(e.value.x), number(e.value.y)],
      },
      'swiped': [number(touch.swiped.x), number(touch.swiped.y)],
    });
  }
  return {'op': 'touch', 'steps': steps, 'expect': results};
}

void main(List<String> args) {
  final root = args.isEmpty ? '../..' : args.first;
  final rng = _Rng(31337);
  final profiles = <Object?>[];
  final sessions = [for (var i = 0; i < 60; i++) _session(rng, profiles)];
  writeFixture(
    root: root,
    package: 'orblit_input',
    name: 'actions',
    cases: [
      ..._controlCases(),
      ..._frameCases(rng),
      ...sessions,
      ..._loadCases(rng, profiles),
      for (var i = 0; i < 60; i++) _nextCase(rng),
      for (var i = 0; i < 60; i++) _touchCase(rng),
    ],
  );
}

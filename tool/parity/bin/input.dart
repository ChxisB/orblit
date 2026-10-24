import 'dart:typed_data';

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

const _inners = <double>[0, 0.06, 0.15, 0.3, 0.9, 1];
const _outers = <double>[0.1, 0.5, 0.95, 0.98, 1];
const _curves = <double>[1, 0.5, 2, 3];

const _scalars = <double>[
  -2, -1, -0.5, -0.2, -0.1, 0, 0.05, 0.1, 0.15, 0.2, 0.5, 0.95, 1, 1.5, //
  _nan, _inf, -_inf,
];

const _sticks = <List<double>>[
  [0, 0],
  [1, 0],
  [0, -1],
  [0.1, 0.1],
  [0.2, 0.2],
  [0.5, -0.5],
  [0.7, 0.7],
  [1, 1],
  [-1, 1],
  [0.3, 0],
  [3, 4],
  [_nan, 0],
  [0, _inf],
  [-_inf, _inf],
  [1e-300, 1e-300],
];

Map<String, Object?> _shapingSpec(double inner, double outer, double curve) => {
  'inner': inner,
  'outer': outer,
  'curve': curve,
};

List<Map<String, Object?>> _scalarCases() => [
  for (final inner in _inners)
    for (final outer in _outers)
      for (final curve in _curves)
        for (final value in _scalars)
          {
            'op': 'scalar',
            'spec': {..._shapingSpec(inner, outer, curve), 'value': number(value)},
            'expect': number(
              Shaping(inner: inner, outer: outer, curve: curve).scalar(value),
            ),
          },
];

List<Map<String, Object?>> _stickCases() => [
  for (final inner in _inners)
    for (final outer in _outers)
      for (final curve in _curves)
        for (final v in _sticks)
          {
            'op': 'stick',
            'spec': {
              ..._shapingSpec(inner, outer, curve),
              'x': number(v[0]),
              'y': number(v[1]),
            },
            'expect': _pair(
              Shaping(inner: inner, outer: outer, curve: curve).stick(
                Vector2(v[0], v[1]),
              ),
            ),
          },
];

List<Object> _pair(Vector2 v) => [number(v.x), number(v.y)];

const _ranges = <List<int>>[
  [-32768, 32767, 0],
  [0, 255, 0],
  [-1, 1, 0],
  [0, 1023, 12],
  [-128, 127, 0],
  [5, 5, 0],
  [10, 0, 0],
  [-100, 0, 0],
];

List<Map<String, Object?>> _rangeCases() {
  final cases = <Map<String, Object?>>[];
  for (final r in _ranges) {
    final range = PadAxisRange(minimum: r[0], maximum: r[1], flat: r[2]);
    final values = <int>{
      r[0],
      r[1],
      (r[0] + r[1]) ~/ 2,
      r[0] - 1,
      r[1] + 1,
      0,
      1,
      -1,
      r[0] + 7,
      r[1] - 7,
    };
    for (final value in values) {
      cases.add({
        'op': 'range',
        'spec': {'min': r[0], 'max': r[1], 'flat': r[2], 'value': value},
        'expect': {
          'signed': number(range.toSigned(value)),
          'unsigned': number(range.toUnsigned(value)),
          'unipolar': range.isUnipolar,
        },
      });
    }
  }
  return cases;
}

const _bitmaps = <String>[
  '',
  '   ',
  '0',
  '1',
  'b',
  '3',
  '30003',
  '3f',
  'ffffffffffffffff',
  '8000000000000000',
  '7fffffffffffffff',
  '1 0',
  '0 1',
  '3000000000000 0 0 0 0',
  '10000 0 0',
  '1  2',
  ' ab cd ',
  'ffffffffffffffffff',
  'xyz',
  '1 xyz 2',
  '-1',
  '+f',
  '0x10',
  '100000000',
  'ffffffff',
  '1ffffffff',
  'deadbeefcafebabe',
  '0000000000000000 0000000000000001',
  'A',
  'FFFF',
];

List<Map<String, Object?>> _bitmapCases() => [
  for (final line in _bitmaps)
    {
      'op': 'bitmap',
      'spec': {'line': line},
      'expect': (EvdevCapabilities.parseBitmap(line).toList()..sort()),
    },
];

List<int> _triple(EvdevEvent e) => [e.type, e.code, e.value];

List<Map<String, Object?>> _decodeCases() {
  final rng = _Rng(7);
  final cases = <Map<String, Object?>>[];
  for (var i = 0; i < 120; i++) {
    final timeBytes = rng.chance(4) ? 8 : 16;
    final events = [
      for (var n = rng.next(6); n > 0; n--)
        [
          rng.pick(<int>[0, 1, 2, 3, 0xffff]),
          rng.pick(<int>[0, 1, 3, 0x10, 0x130, 0xffff]),
          rng.pick(<int>[0, 1, 2, -1, 32767, -32768, 0x7fffffff, -0x80000000]),
        ],
    ];
    final bytes = BytesBuilder();
    for (final e in events) {
      bytes.add(encodeEvdevEvent(e[0], e[1], e[2], timeBytes: timeBytes));
    }
    final all = bytes.takeBytes();
    final cuts = <int>{
      for (var n = rng.next(4); n > 0; n--)
        if (all.isNotEmpty) rng.next(all.length + 1),
    }.toList()
      ..sort();
    final decoder = EvdevDecoder(timeBytes: timeBytes);
    final chunks = <Map<String, Object?>>[];
    var from = 0;
    for (final end in [...cuts, all.length]) {
      final out = decoder.decode(all.sublist(from, end));
      from = end;
      chunks.add({'events': out.map(_triple).toList(), 'partial': decoder.hasPartial});
    }
    cases.add({
      'op': 'decode',
      'spec': {'timeBytes': timeBytes, 'events': events, 'cuts': cuts},
      'expect': chunks,
    });
  }
  return cases;
}

const _names = <String>[
  '',
  'Xbox Wireless Controller',
  'Microsoft X-Box 360 pad',
  'Sony Interactive Entertainment DualSense Wireless Controller',
  'PS4 DualShock 4',
  'DUALSHOCK',
  'Nintendo Switch Pro Controller',
  'Steam Deck Controller',
  'Valve Steam Controller',
  'Generic USB Joystick',
  'NINTENDO',
  'Dualsense Edge',
  'Controller (XBOX 360 For Windows)',
];

const _identities = <List<int>>[
  [0, 0],
  [0x28de, 0x1205],
  [0x28de, 0x11ff],
  [0x28de, 0x1102],
  [0x045e, 0x028e],
  [0x045e, 0x02d1],
  [0x045e, 0x02ea],
  [0x045e, 0x0b12],
  [0x045e, 0x0b13],
  [0x054c, 0x05c4],
  [0x054c, 0x09cc],
  [0x054c, 0x0ce6],
  [0x054c, 0x0df2],
  [0x057e, 0x2009],
  [0x057e, 0x2017],
  [0x1234, 0x5678],
  [0x045e, 0x0001],
];

const _absLines = <String?>[null, '3', '25', '3f', '27', '37', '0', '30003', '7'];

Map<String, Object?> _layoutJson(PadLayout layout) => {
  'name': layout.name,
  'labels': [
    layout.labels.south,
    layout.labels.east,
    layout.labels.west,
    layout.labels.north,
  ],
  'buttons': {
    for (final k in (layout.buttons.keys.toList()..sort()))
      '$k': layout.buttons[k]!.name,
  },
  'axes': {
    for (final k in (layout.axes.keys.toList()..sort()))
      '$k': layout.axes[k]!.name,
  },
  'sticks': _shapingJson(layout.sticks),
  'triggers': _shapingJson(layout.triggers),
};

List<Object> _shapingJson(Shaping s) => [
  number(s.inner),
  number(s.outer),
  number(s.curve),
];

List<Map<String, Object?>> _layoutCases() {
  final cases = <Map<String, Object?>>[];
  void add(String name, int vendor, int product, String? abs) {
    final identity = PadIdentity(name: name, vendor: vendor, product: product);
    final layout = PadLayout.of(
      identity,
      capabilities: abs == null
          ? null
          : EvdevCapabilities.parse(ev: 'b', key: '', abs: abs),
    );
    cases.add({
      'op': 'layout',
      'spec': {
        'name': name,
        'vendor': vendor,
        'product': product,
        'abs': abs,
      },
      'expect': _layoutJson(layout),
    });
  }

  for (final id in _identities) {
    add('Unknown Pad', id[0], id[1], null);
  }
  for (final name in _names) {
    for (final abs in _absLines) {
      add(name, 0, 0, abs);
    }
  }
  // A table hit wins over a name, and a name over the axis guess.
  add('Nintendo Switch Pro Controller', 0x045e, 0x028e, '25');
  add('Xbox Wireless Controller', 0x1234, 0x5678, '25');
  add('Generic', 0x1234, 0x5678, '25');
  add('Generic', 0x1234, 0x5678, '3f');
  // U+0130 and U+212A, which Dart folds to plain "i" and "k".
  add('NİNTENDO', 0, 0, null);
  add('DualShocK', 0, 0, null);
  add('DUALSHOCK', 0, 0, null);
  add('İ', 0, 0, null);
  return cases;
}

const _paths = <String>['p0', 'p1', 'p2', 'p3'];

const _codes = <int>[
  0x130, 0x131, 0x133, 0x134, 0x136, 0x137, 0x138, 0x139, 0x13a, 0x13b, //
  0x13c, 0x13d, 0x13e, 0x220, 0x221, 0x222, 0x223, 0x999,
];

const _axisCodes = <int>[0, 1, 2, 3, 4, 5, 0x10, 0x11, 0x77];

const _axisValues = <int>[
  -32768, -20000, -5000, -100, 0, 100, 5000, 20000, 32767, -1, 1, 255, 128, //
  40000, -40000,
];

/// A pad the script can attach: always the same hardware, so a case is a list
/// of steps and nothing else.
RecordedPad _device(int i, Map<String, Object?> spec) {
  final held = (spec['held']! as List).cast<int>().toSet();
  return RecordedPad(
    identity: PadIdentity(
      name: spec['name']! as String,
      vendor: spec['vendor']! as int,
      product: spec['product']! as int,
      uniq: spec['uniq']! as String,
      path: spec['path']! as String,
    ),
    capabilities: EvdevCapabilities.parse(
      ev: 'b',
      key: '3000000000000 0 0 0 0',
      abs: spec['abs']! as String,
    ),
    ranges: {
      0: PadAxisRange.stick,
      1: PadAxisRange.stick,
      2: spec['unipolarZ']! as bool
          ? PadAxisRange.trigger
          : PadAxisRange.stick,
      3: PadAxisRange.stick,
      4: PadAxisRange.stick,
      5: PadAxisRange.trigger,
      0x10: const PadAxisRange(minimum: -1, maximum: 1),
      0x11: const PadAxisRange(minimum: -1, maximum: 1),
    },
    held: held,
    canRumble: spec['canRumble']! as bool,
  );
}

Map<String, Object?> _padSpec(_Rng rng, int i) => {
  'name': rng.pick(<String>['Orblit Virtual Pad', 'Xbox Controller', 'DualSense']),
  'vendor': rng.pick(<int>[0x28de, 0x054c, 0, 0x1234]),
  'product': rng.pick(<int>[0x1205, 0x0ce6, 0, 0x5678]),
  'uniq': rng.chance(3) ? 'u$i' : '',
  'path': _paths[i],
  'abs': rng.pick(<String>['30003', '3f', '25', '3']),
  'unipolarZ': rng.chance(2),
  'canRumble': rng.chance(2),
  'held': [if (rng.chance(3)) _codes[rng.next(14)]],
};

Map<String, Object?> _step(_Rng rng, int pads) {
  final pad = rng.next(pads);
  return switch (rng.next(16)) {
    0 || 1 || 2 => {
      'step': 'button',
      'pad': pad,
      'code': rng.pick(_codes),
      'down': rng.chance(2),
    },
    3 || 4 || 5 => {
      'step': 'axis',
      'pad': pad,
      'code': rng.pick(_axisCodes),
      'value': rng.pick(_axisValues),
    },
    6 => {
      'step': 'send',
      'pad': pad,
      'type': 1,
      'code': rng.pick(_codes),
      'value': 2,
    },
    7 => {'step': 'send', 'pad': pad, 'type': 0, 'code': 3, 'value': 0},
    8 => {'step': 'send', 'pad': pad, 'type': 3, 'code': 0x2f, 'value': 9},
    9 => {
      'step': 'drift',
      'pad': pad,
      'buttons': [for (var n = rng.next(3); n > 0; n--) rng.pick(_codes)],
      'axes': {
        for (var n = rng.next(3); n > 0; n--)
          '${rng.pick(_axisCodes)}': rng.pick(_axisValues),
      },
    },
    10 => {'step': 'detach', 'pad': pad},
    11 => {'step': 'attach', 'pad': pad},
    12 => {
      'step': 'rumble',
      'slot': rng.next(pads + 1),
      'strong': number(rng.pick(<double>[-1, 0, 0.3, 1, 2, _nan])),
      'weak': number(rng.pick(<double>[-1, 0, 0.7, 1, 5, _nan])),
      'ms': rng.pick(<int>[-5, 0, 100, 2000]),
    },
    _ => {'step': 'poll'},
  };
}

List<Map<String, Object?>> _padsCases() {
  final cases = <Map<String, Object?>>[];
  for (var seed = 0; seed < 150; seed++) {
    final rng = _Rng(1000 + seed);
    final count = 1 + rng.next(3);
    final specs = [for (var i = 0; i < count; i++) _padSpec(rng, i)];
    final attached = [for (var i = 0; i < count; i++) rng.chance(4) ? false : true];
    final max = rng.pick(<int>[8, 8, 8, 1, 2]);
    final steps = [
      for (var n = 10 + rng.next(30); n > 0; n--) _step(rng, count),
      {'step': 'poll'},
    ];
    cases.add({
      'op': 'pads',
      'spec': {
        'pads': specs,
        'attached': attached,
        'max': max,
        'steps': steps,
      },
      'expect': _runPads(specs, attached, steps, max),
    });
  }
  return cases;
}

double _double(Object? value) =>
    value is double ? value : double.parse('$value');

Map<String, Object?> _stateJson(PadState s) => {
  'slot': s.slot,
  'connected': s.connected,
  'name': s.identity.name,
  'held': s.held.map((b) => b.name).toList(),
  'pressed': [for (final b in PadButton.values) if (s.wasPressed(b)) b.name],
  'released': [for (final b in PadButton.values) if (s.wasReleased(b)) b.name],
  'axes': [for (final a in PadAxis.values) number(s.axis(a))],
  'raw': [for (final a in PadAxis.values) number(s.rawAxis(a))],
};

List<Object?> _runPads(
  List<Map<String, Object?>> specs,
  List<bool> attached,
  List<Map<String, Object?>> steps,
  int max,
) {
  final devices = [for (var i = 0; i < specs.length; i++) _device(i, specs[i])];
  final backend = RecordedPads([
    for (var i = 0; i < devices.length; i++)
      if (attached[i]) devices[i],
  ]);
  final pads = Pads(backend: backend, maxPads: max);
  final out = <Object?>[];
  for (final step in steps) {
    final device = devices[(step['pad'] ?? 0) as int];
    switch (step['step']) {
      case 'button':
        device.button(step['code']! as int, down: step['down']! as bool);
      case 'axis':
        device.axis(step['code']! as int, step['value']! as int);
      case 'send':
        device.send(
          step['type']! as int,
          step['code']! as int,
          step['value']! as int,
        );
      case 'drift':
        device.drift(
          buttons: (step['buttons']! as List).cast<int>().toSet(),
          axes: {
            for (final e in (step['axes']! as Map).entries)
              int.parse(e.key as String): e.value! as int,
          },
        );
      case 'detach':
        if (backend.devices.contains(device)) backend.detach(device);
      case 'attach':
        if (!backend.devices.contains(device) && device.isAlive) {
          backend.attach(device);
        }
      case 'rumble':
        final before = [for (final d in devices) d.rumbles.length];
        pads.rumble(
          step['slot']! as int,
          strong: _double(step['strong']),
          weak: _double(step['weak']),
          duration: Duration(milliseconds: step['ms']! as int),
        );
        out.add({
          'rumble': [
            for (var i = 0; i < devices.length; i++)
              for (final r in devices[i].rumbles.skip(before[i]))
                [i, number(r.strong), number(r.weak), r.duration.inMilliseconds],
          ],
          'can': pads.canRumble(step['slot']! as int),
        });
      default:
        final frame = pads.poll();
        out.add({
          'events': frame.events.map((e) => e.toString()).toList(),
          'pads': frame.pads.map(_stateJson).toList(),
          'count': frame.count,
          'active': frame.isActive,
        });
    }
  }
  return out;
}

void main(List<String> args) {
  final root = args.isEmpty ? '../..' : args.first;
  writeFixture(
    root: root,
    package: 'orblit_input',
    name: 'input',
    cases: [
      ..._scalarCases(),
      ..._stickCases(),
      ..._rangeCases(),
      ..._bitmapCases(),
      ..._decodeCases(),
      ..._layoutCases(),
      ..._padsCases(),
    ],
  );
}

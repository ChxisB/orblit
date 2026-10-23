import 'dart:convert';
import 'dart:typed_data';

import 'package:orblit_motion/orblit_motion.dart';
import 'package:orblit_parity_tool/fixture.dart';
import 'package:orblit_parity_tool/motion_clips.dart';

typedef _Json = Map<String, Object?>;

/// The data a glTF document keeps in its one buffer, and the views and
/// accessors that name the parts of it.
final class _Gltf {
  final BytesBuilder _bytes = BytesBuilder();
  final List<Object?> _views = [];
  final List<Object?> _accessors = [];

  Uint8List get data => _bytes.toBytes();

  int _add(Uint8List part, int component, String type, int count, bool norm) {
    _views.add({
      'buffer': 0,
      'byteOffset': _bytes.length,
      'byteLength': part.length,
    });
    _bytes.add(part);
    while (_bytes.length % 4 != 0) {
      _bytes.addByte(0);
    }
    _accessors.add({
      'bufferView': _views.length - 1,
      'componentType': component,
      'type': type,
      'count': count,
      if (norm) 'normalized': true,
    });
    return _accessors.length - 1;
  }

  static int _width(String type) => switch (type) {
    'VEC3' => 3,
    'VEC4' => 4,
    _ => 1,
  };

  /// [numbers] as float32 values of [type], and the accessor's index.
  int floats(List<double> numbers, String type) {
    final part = ByteData(numbers.length * 4);
    for (var i = 0; i < numbers.length; i++) {
      part.setFloat32(i * 4, numbers[i], Endian.little);
    }
    return _add(
      part.buffer.asUint8List(),
      5126,
      type,
      numbers.length ~/ _width(type),
      false,
    );
  }

  /// [numbers] as normalized signed 16 bit values, which is how an exporter
  /// keeps a rotation small.
  int shorts(List<int> numbers, String type) {
    final part = ByteData(numbers.length * 2);
    for (var i = 0; i < numbers.length; i++) {
      part.setInt16(i * 2, numbers[i], Endian.little);
    }
    return _add(
      part.buffer.asUint8List(),
      5122,
      type,
      numbers.length ~/ _width(type),
      true,
    );
  }

  /// [document] with the buffer, views and accessors added to it. Its data is
  /// in the file, unless [uri] names where it is kept.
  _Json file(_Json document, {String? uri, bool buffer = true}) => {
    ...document,
    'asset': {'version': '2.0'},
    if (buffer) ...{
      'buffers': [
        {
          'byteLength': _bytes.length,
          'uri':
              uri ??
              'data:application/octet-stream;base64,${base64Encode(data)}',
        },
      ],
      'bufferViews': _views,
      'accessors': _accessors,
    },
  };

  /// The same as a binary container, with the data in its second chunk.
  Uint8List glb(_Json document) {
    final whole = file(document, buffer: false)
      ..['buffers'] = [
        {'byteLength': _bytes.length},
      ]
      ..['bufferViews'] = _views
      ..['accessors'] = _accessors;
    final json = utf8.encode(jsonEncode(whole));
    final text = Uint8List.fromList([
      ...json,
      ...List.filled((4 - json.length % 4) % 4, 0x20),
    ]);
    final binary = Uint8List.fromList([
      ...data,
      ...List.filled((4 - data.length % 4) % 4, 0),
    ]);
    final head = ByteData(12)
      ..setUint32(0, 0x46546C67, Endian.little)
      ..setUint32(4, 2, Endian.little)
      ..setUint32(8, 12 + 8 + text.length + 8 + binary.length, Endian.little);
    ByteData chunk(int length, int type) => ByteData(8)
      ..setUint32(0, length, Endian.little)
      ..setUint32(4, type, Endian.little);
    return Uint8List.fromList([
      ...head.buffer.asUint8List(),
      ...chunk(text.length, 0x4E4F534A).buffer.asUint8List(),
      ...text,
      ...chunk(binary.length, 0x004E4942).buffer.asUint8List(),
      ...binary,
    ]);
  }
}

_Json _sampler(int input, int output, [Object? interpolation]) => {
  'input': input,
  'output': output,
  if (interpolation != null) 'interpolation': interpolation,
};

_Json _channel(Object? sampler, Object? node, Object? path) => {
  'sampler': sampler,
  'target': {'node': node, 'path': path},
};

Case _of(
  String label,
  String op,
  List<int> bytes, [
  Map<String, List<int>> files = const {},
]) => {
  'label': label,
  ...caseOf(op, [
    b64(bytes),
    {for (final entry in files.entries) entry.key: b64(entry.value)},
  ], (input) => op == 'clips' ? _clips(input) : _skeletons(input)),
};

Uint8List _bytes(Object? text) => base64Decode(text! as String);

Map<String, Uint8List> _files(Object? files) => {
  for (final entry in (files! as _Json).entries) entry.key: _bytes(entry.value),
};

Object? _clips(List<Object?> input) {
  final result = clipsFromGltf(_bytes(input[0]), files: _files(input[1]));
  return {
    'clips': [for (final clip in result.clips) tree(jsonDecode(clip.encode()))],
    'problems': result.problems,
  };
}

Object? _skeletons(List<Object?> input) {
  final found = restSkeletonsFromGltf(
    _bytes(input[0]),
    files: _files(input[1]),
  );
  return [
    for (final skeleton in found)
      {
        'names': skeleton.names,
        'parents': skeleton.parents,
        'reach': number(skeleton.reach),
        'positions': [
          for (var i = 0; i < skeleton.length; i++)
            vecDump(skeleton.positionOf(i)),
        ],
        'rotations': [
          for (var i = 0; i < skeleton.length; i++)
            quatDump(skeleton.rotationOf(i)),
        ],
        'scales': [
          for (var i = 0; i < skeleton.length; i++)
            vecDump(skeleton.scaleOf(i)),
        ],
        'parentBodies': [
          for (var i = 0; i < skeleton.length; i++)
            [for (final v in skeleton.parentWorldOf(i).storage) number(v)],
        ],
      },
  ];
}

Case _clipOf(
  String label,
  _Json document,
  _Gltf data, {
  String? uri,
  Map<String, List<int>> files = const {},
}) => _of(
  label,
  'clips',
  utf8.encode(jsonEncode(data.file(document, uri: uri))),
  files,
);

Case _skeletonOf(String label, _Json document) => _of(
  label,
  'skeletons',
  utf8.encode(jsonEncode(_Gltf().file(document, buffer: false))),
);

/// An armature with two joints, one of them unnamed, a door a scene gave an
/// id, and a face with morph targets, moved by three animations.
(_Json, _Gltf) _armature() {
  final data = _Gltf();
  final half = data.floats([0, 0.5], 'SCALAR');
  final quarter = data.floats([0, 0.25], 'SCALAR');
  final frames = data.floats([0, 1 / 24, 2 / 24], 'SCALAR');
  final tenth = data.floats([0, 0.1], 'SCALAR');
  final turn = data.floats([
    0, 0, 0, 0, 0, 0, 0, 1, 0, 0.5, 0, 0, //
    0, 0.25, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0,
  ], 'VEC4');
  final lift = data.floats([0, 0, 0, 0, 1, 0], 'VEC3');
  final slide = data.floats([0, 0, 0, 1, 0, 0], 'VEC3');
  final grow = data.floats([1, 1, 1, 2, 2, 2], 'VEC3');
  final stride = data.floats([0, 0, 0, 0, 0, 0.1, 0, 0, 0.2], 'VEC3');
  final lone = data.floats([1, 1, 1], 'VEC3');
  final document = <String, Object?>{
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
      {'name': 'Face', 'primitives': <Object?>[]},
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
          _sampler(half, turn, 'CUBICSPLINE'),
          _sampler(quarter, lift, 'STEP'),
          _sampler(half, slide),
          _sampler(half, grow, 'LINEAR'),
        ],
        'channels': [
          _channel(0, 1, 'rotation'),
          _channel(1, 2, 'translation'),
          _channel(2, 3, 'translation'),
          _channel(3, 0, 'scale'),
          _channel(2, 4, 'weights'),
          _channel(3, 3, 'translation'),
        ],
      },
      {
        'name': 'cycle run',
        'samplers': [_sampler(frames, stride)],
        'channels': [_channel(0, 1, 'translation')],
      },
      {
        'samplers': [_sampler(tenth, slide), _sampler(tenth, lone)],
        'channels': [
          _channel(0, 3, 'translation'),
          _channel(0, 9, 'translation'),
          _channel(5, 3, 'rotation'),
          _channel(0, 3, 'pointer'),
          _channel(1, 1, 'scale'),
        ],
      },
    ],
  };
  return (document, data);
}

/// One unnamed node moved by one animation, for the cases that change a single
/// thing about it.
Case _moves(
  String label, {
  List<double> times = const [0, 0.5],
  List<double> values = const [0, 0, 0, 1, 0, 0],
  String type = 'VEC3',
  Object? interpolation,
  Object? name = 'Move',
  Object? node = 0,
  Object? path = 'translation',
  _Json Function(int input, int output)? sampler,
  Object? Function(int index)? channel,
}) {
  final data = _Gltf();
  final input = data.floats(times, 'SCALAR');
  final output = data.floats(values, type);
  return _clipOf(label, {
    'nodes': [<String, Object?>{}],
    'animations': [
      {
        if (name != null) 'name': name,
        'samplers': [
          sampler?.call(input, output) ??
              _sampler(input, output, interpolation),
        ],
        'channels': [channel?.call(0) ?? _channel(0, node, path)],
      },
    ],
  }, data);
}

List<Case> _containerCases() {
  final (document, data) = _armature();
  final (beside, apart) = _armature();
  return [
    _clipOf('armature, data in the file', document, data),
    _clipOf(
      'armature, data beside the file, escaped',
      beside,
      apart,
      uri: 'walk%20cycle.bin',
      files: {'walk cycle.bin': apart.data},
    ),
    _clipOf(
      'armature, data beside the file',
      beside,
      apart,
      uri: 'walk.bin',
      files: {'walk.bin': apart.data},
    ),
    _clipOf('armature, data missing', beside, apart, uri: 'gone.bin'),
    _clipOf(
      'armature, data percent encoded',
      beside,
      apart,
      uri: 'data:application/octet-stream,abc',
    ),
    _clipOf(
      'armature, data not base64',
      beside,
      apart,
      uri: 'data:application/octet-stream;base64,!!!',
    ),
    _clipOf('armature, data uri with no comma', beside, apart, uri: 'data:x'),
    _of('armature, binary container', 'clips', apart.glb(beside)),
    _of('not a model', 'clips', utf8.encode('not a model')),
    _of('a list', 'clips', utf8.encode('[1, 2]')),
    _of('nothing', 'clips', const []),
    _of('a number', 'clips', utf8.encode('5')),
    _of('a bare container header', 'clips', utf8.encode('glTF\u0002\u0000')),
    _of('an empty document', 'clips', utf8.encode('{}')),
  ];
}

List<Case> _nameCases() {
  const names = <Object?>[
    ' Walk ',
    'Loop',
    'LOOP',
    'Cycle 2',
    'a loop',
    'a CYCLE',
    'looper',
    'noloop',
    'Loo p',
    'Strolling',
    '',
    '   ',
    '  ',
    '\u0085padded\u0085',
    '᠎padded',
    '\u001fpadded',
    '​padded',
    ' loop ',
    '﻿ Loop me\t',
    'Walk﻿',
    5,
    true,
    null,
  ];
  return [
    for (final name in names) _moves('name ${jsonEncode(name)}', name: name),
  ];
}

List<Case> _rateCases() {
  const rates = <String, List<double>>{
    'whole seconds': [0, 1, 2],
    'thirty': [0, 1 / 30, 2 / 30],
    'twenty five': [0, 0.04, 0.08],
    'twenty four': [0, 1 / 24, 2 / 24],
    'sixty': [0, 1 / 60, 2 / 60],
    'fifty': [0, 0.02, 0.04],
    'forty eight': [0, 1 / 48, 2 / 48],
    'none of them': [0, 0.123],
    'a hair off thirty': [0, 0.0334],
    'near zero': [0, 0.0005],
    'a thousandth': [0, 0.001],
    'a thousandth and a frame': [0, 1 / 24, 0.001],
    'a thousandth off': [0, 1 / 24 + 0.0009],
    'a thousandth over': [0, 1 / 24 + 0.0011],
    'half a frame': [0, 0.5 / 30],
    'one key': [0.25],
    'one key at nought': [0],
    'long': [0, 10, 3600.5],
  };
  return [
    for (final entry in rates.entries)
      _moves(
        'rate: ${entry.key}',
        times: entry.value,
        values: [
          for (final _ in entry.value) ...const [0.0, 1.0, 2.0],
        ],
      ),
  ];
}

List<Case> _interpolationCases() => [
  _moves('interpolation: step', interpolation: 'STEP'),
  _moves('interpolation: linear', interpolation: 'LINEAR'),
  _moves('interpolation: none'),
  _moves('interpolation: unknown word', interpolation: 'BOGUS'),
  _moves('interpolation: lower case', interpolation: 'step'),
  _moves('interpolation: a number', interpolation: 7),
  _moves(
    'interpolation: cubic vector',
    interpolation: 'CUBICSPLINE',
    values: [0, 0, 0, 1, 2, 3, 0, 1, 0, 0.5, 0, 0, 4, 5, 6, 0, 0, 1],
  ),
  _moves(
    'interpolation: cubic with too few values',
    interpolation: 'CUBICSPLINE',
    values: [0, 0, 0, 1, 2, 3, 0, 1, 0, 0.5, 0, 0, 4, 5, 6],
  ),
  _moves(
    'interpolation: cubic rotation',
    interpolation: 'CUBICSPLINE',
    path: 'rotation',
    type: 'VEC4',
    values: [
      0,
      0,
      0,
      0,
      0,
      0,
      0,
      1,
      0,
      0,
      0,
      0.5,
      0,
      0,
      0,
      3,
      0,
      1,
      0,
      0,
      0,
      0,
      0,
      0,
    ],
  ),
];

List<Case> _rotationCases() {
  const rows = <String, List<double>>{
    'unit': [0, 0, 0, 1],
    'nought': [0, 0, 0, 0],
    'a little long': [0, 0, 0, 1.0000005],
    'a little short': [0, 0, 0, 0.9999995],
    'too long': [0, 0, 0, 1.000002],
    'too short': [0, 0, 0, 0.999998],
    'half': [0.5, 0, 0, 0],
    'tiny': [1e-10, 0, 0, 0],
    'small': [3e-9, 0, 0, 0],
    'not a number': [double.nan, 0, 0, 1],
    'infinite': [double.infinity, 0, 0, 1],
    'big': [100, 200, 300, 400],
  };
  return [
    for (final entry in rows.entries)
      _moves(
        'rotation: ${entry.key}',
        path: 'rotation',
        type: 'VEC4',
        values: [...entry.value, 0, 0, 0, 1],
      ),
    _moves(
      'rotation: three numbers a key',
      path: 'rotation',
      values: [0, 0, 0, 0, 0, 0],
    ),
    _moves('translation: four numbers a key', type: 'VEC4'),
  ];
}

List<Case> _shortCases() {
  const numbers = <double>[
    0.1,
    1 / 3,
    2 / 3,
    16777216,
    1e-8,
    3e38,
    1.17549435e-38,
    1.4e-45,
    -0.0,
    123456.789,
    0.30000001192092896,
    1e-5,
    5e-5,
    -7.5e-7,
    999999.94,
    0.0099999,
  ];
  return [
    _moves(
      'short: values',
      times: [for (var i = 0; i < numbers.length; i++) i / 4],
      values: [
        for (var i = 0; i < numbers.length; i += 1) ...[
          numbers[i],
          numbers[(i + 5) % numbers.length],
          numbers[(i + 9) % numbers.length],
        ],
      ],
    ),
    _moves(
      'short: times',
      times: <double>[0, ...numbers.where((n) => n > 0)]..sort(),
      values: List.filled(3 * 15, 1.0),
    ),
    _moves(
      'rate: an enormous time',
      times: [0, 3e38],
      values: [0, 0, 0, 1, 0, 0],
    ),
  ];
}

List<Case> _keyCases() => [
  _moves(
    'keys: not a number time',
    times: [0, double.nan, 0.5],
    values: [0, 0, 0, 1, 0, 0, 2, 0, 0],
  ),
  _moves(
    'keys: infinite time',
    times: [0, double.infinity],
    values: [0, 0, 0, 1, 0, 0],
  ),
  _moves(
    'keys: only not a number times',
    times: [double.nan, double.nan],
    values: [0, 0, 0, 1, 0, 0],
  ),
  _moves('keys: out of order', times: [0.5, 0.2], values: [0, 0, 0, 1, 0, 0]),
  _moves('keys: on one time', times: [0.2, 0.2], values: [0, 0, 0, 1, 0, 0]),
  _moves('keys: before nought', times: [-1, 0.5], values: [0, 0, 0, 1, 0, 0]),
  _moves(
    'keys: more values than times',
    times: [0, 0.5],
    values: [0, 0, 0, 1, 0, 0, 5, 5, 5],
  ),
  _moves(
    'keys: fewer values than times',
    times: [0, 0.5, 1],
    values: [0, 0, 0, 1, 0, 0],
  ),
  _moves('keys: no times', times: [], values: [0, 0, 0]),
  _moves('keys: no values', values: []),
  _moves('keys: a value not a number', values: [0, double.nan, 0, 1, 0, 0]),
  _moves('keys: a value infinite', values: [0, 0, double.infinity, 1, 0, 0]),
];

List<Case> _reachCases() {
  const lone = <double>[0, 0, 0];
  return [
    _moves('reach: channel not a map', channel: (_) => 5),
    _moves('reach: channel without a target', channel: (_) => {'sampler': 0}),
    _moves(
      'reach: target a list',
      channel: (_) => {
        'sampler': 0,
        'target': [0],
      },
    ),
    _moves(
      'reach: node missing',
      channel: (_) => {
        'sampler': 0,
        'target': {'path': 'translation'},
      },
    ),
    _moves('reach: node negative', node: -1),
    _moves('reach: node past the end', node: 1),
    _moves('reach: node far past the end', node: 99),
    _moves('reach: node a double', node: 1.5),
    _moves('reach: node a whole double', node: 0.0),
    _moves('reach: node a string', node: '0'),
    _moves('reach: node null', node: null),
    _moves(
      'reach: sampler missing',
      channel: (_) => {
        'target': {'node': 0, 'path': 'translation'},
      },
    ),
    _moves(
      'reach: sampler negative',
      channel: (_) => _channel(-1, 0, 'translation'),
    ),
    _moves(
      'reach: sampler past the end',
      channel: (_) => _channel(1, 0, 'translation'),
    ),
    _moves(
      'reach: sampler a double',
      channel: (_) => _channel(0.5, 0, 'translation'),
    ),
    _moves(
      'reach: sampler a string',
      channel: (_) => _channel('0', 0, 'translation'),
    ),
    _moves('reach: path weights', path: 'weights'),
    _moves('reach: path pointer', path: 'pointer'),
    _moves('reach: path upper case', path: 'Translation'),
    _moves('reach: path empty', path: ''),
    _moves('reach: path a number', path: 5),
    _moves('reach: path a double', path: 1.5),
    _moves('reach: path a whole double', path: 2.0),
    _moves('reach: path true', path: true),
    _moves('reach: path null', path: null),
    _moves('reach: path a list', path: [1]),
    _moves('reach: path a map', path: <String, Object?>{}),
    _moves('reach: path a quote', path: 'say "hi"'),
    _moves('reach: input missing', sampler: (_, output) => {'output': output}),
    _moves(
      'reach: input negative',
      sampler: (_, output) => _sampler(-1, output),
    ),
    _moves(
      'reach: input past the end',
      sampler: (_, output) => _sampler(9, output),
    ),
    _moves(
      'reach: input a double',
      sampler: (_, output) => _sampler(0, output)..['input'] = 0.5,
    ),
    _moves(
      'reach: input a string',
      sampler: (_, output) => _sampler(0, output)..['input'] = '0',
    ),
    _moves(
      'reach: input huge',
      sampler: (_, output) => _sampler(0, output)..['input'] = 1e20,
    ),
    _moves(
      'reach: input past the int range',
      sampler: (_, output) => _sampler(0, output)..['input'] = 4294967296,
    ),
    _moves('reach: output missing', sampler: (input, _) => {'input': input}),
    _moves(
      'reach: output is the times',
      sampler: (input, _) => _sampler(input, input),
    ),
    _moves('reach: values of one only', values: lone),
  ];
}

List<Case> _animationCases() {
  final data = _Gltf();
  final time = data.floats([0, 0.5], 'SCALAR');
  final value = data.floats([0, 0, 0, 1, 0, 0], 'VEC3');
  _Json animation(Object? name) => {
    'name': name,
    'samplers': [_sampler(time, value)],
    'channels': [_channel(0, 0, 'translation')],
  };
  final nodes = [<String, Object?>{}];
  return [
    _clipOf('animations: two', {
      'nodes': nodes,
      'animations': [animation('A'), animation('B')],
    }, data),
    _clipOf('animations: one not a map', {
      'nodes': nodes,
      'animations': [5, animation(null), 'x', animation(null)],
    }, data),
    _clipOf('animations: not a list', {
      'nodes': nodes,
      'animations': animation('A'),
    }, data),
    _clipOf('animations: none', {'nodes': nodes}, data),
    _clipOf('animations: no nodes', {
      'animations': [animation('A')],
    }, data),
    _clipOf('animations: no samplers or channels', {
      'nodes': nodes,
      'animations': [
        {'name': 'Empty'},
      ],
    }, data),
    _clipOf('animations: channels but no samplers', {
      'nodes': nodes,
      'animations': [
        {
          'name': 'Empty',
          'channels': [_channel(0, 0, 'translation')],
        },
      ],
    }, data),
    _clipOf('animations: two channels one address', {
      'nodes': nodes,
      'animations': [
        {
          'name': 'Twice',
          'samplers': [_sampler(time, value)],
          'channels': [
            _channel(0, 0, 'translation'),
            _channel(0, 0, 'translation'),
            _channel(0, 0, 'scale'),
          ],
        },
      ],
    }, data),
  ];
}

List<Case> _targetCases() {
  final data = _Gltf();
  final time = data.floats([0, 0.5], 'SCALAR');
  final value = data.floats([0, 0, 0, 1, 0, 0], 'VEC3');
  final turn = data.floats([0, 0, 0, 1, 0, 0, 0, 1], 'VEC4');
  _Json moved(List<Object?> nodes, List<Object?> skins, [List<int>? at]) => {
    'nodes': nodes,
    if (skins.isNotEmpty) 'skins': skins,
    'animations': [
      {
        'name': 'Move',
        'samplers': [_sampler(time, value), _sampler(time, turn)],
        'channels': [
          for (final node in at ?? [0, 1, 2]) ...[
            _channel(0, node, 'translation'),
            _channel(1, node, 'rotation'),
          ],
        ],
      },
    ],
  };
  Map<String, Object?> id(Object? value) => {
    'extras': {
      'orblit': {'id': value},
    },
  };
  return [
    _clipOf('target: ids', moved([id('a/b'), id(''), id(5)], const []), data),
    _clipOf(
      'target: extras not maps',
      moved([
        {'extras': 5},
        {'extras': <Object?>[]},
        {
          'extras': {'orblit': 'x'},
        },
      ], const []),
      data,
    ),
    _clipOf(
      'target: a id with white space',
      moved([id(' a b '), id('x'), {}], const []),
      data,
    ),
    _clipOf(
      'target: a bone and entities',
      moved(
        [
          {'name': 'Root'},
          {'name': 'Free'},
          id('free/two'),
        ],
        [
          {
            'joints': [0],
          },
        ],
      ),
      data,
    ),
    _clipOf(
      'target: skins share a joint',
      moved(
        [
          {'name': 'First'},
          {'name': 'Second'},
          {'name': 'Third'},
        ],
        [
          {
            'joints': [1, 0],
          },
          {
            'joints': [0, 2],
          },
        ],
      ),
      data,
    ),
    _clipOf(
      'target: skin joints strange',
      moved(
        [
          {'name': 'A'},
          {'name': 'A'},
          {'name': 'A'},
        ],
        [
          {
            'joints': [0, 1.5, 'x', -1, 2, 99, 1],
          },
        ],
      ),
      data,
    ),
    _clipOf(
      'target: skin without joints',
      moved(
        [{}, {}, {}],
        [
          {'name': 'Empty'},
          {'joints': 5},
          5,
          {
            'joints': [0],
          },
        ],
      ),
      data,
    ),
    _clipOf(
      'target: joint names',
      moved(
        [
          {'name': 'Plain'},
          {'name': 5, 'mesh': 0},
          {'mesh': 1},
          {'camera': 0},
          {
            'extensions': {
              'KHR_lights_punctual': {'light': 1},
            },
          },
          {'mesh': 9},
          {'mesh': -1},
          {'name': ''},
          {'mesh': 2},
          {
            'extensions': {
              'KHR_lights_punctual': {'light': 0},
            },
          },
          {'extensions': 5},
          {
            'extensions': {'KHR_lights_punctual': 5},
          },
          {'mesh': 3},
        ],
        [
          {
            'joints': [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12],
          },
        ],
        [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12],
      )..addAll({
        'meshes': [
          {'name': 'Face'},
          {'primitives': <Object?>[]},
          {'name': 5},
        ],
        'cameras': [
          {'name': 'Lens'},
        ],
        'extensions': {
          'KHR_lights_punctual': {
            'lights': [
              {'name': 'Lamp'},
              {'name': 'Sun'},
            ],
          },
        },
      }),
      data,
    ),
  ];
}

List<Case> _packedCases() {
  final data = _Gltf();
  final time = data.floats([0, 0.5], 'SCALAR');
  final turn = data.shorts([0, 0, 0, 32767, 0, 0, 16384, 16384], 'VEC4');
  final back = data.shorts([-32768, 0, 0, 32767, 0, 0, 0, 0], 'VEC4');
  return [
    _clipOf('packed: a rotation in shorts', {
      'nodes': [<String, Object?>{}],
      'animations': [
        {
          'name': 'Turn',
          'samplers': [_sampler(time, turn), _sampler(time, back)],
          'channels': [_channel(0, 0, 'rotation')],
        },
      ],
    }, data),
    _clipOf('packed: a rotation in shorts, second', {
      'nodes': [<String, Object?>{}],
      'animations': [
        {
          'name': 'Turn',
          'samplers': [_sampler(time, turn), _sampler(time, back)],
          'channels': [_channel(1, 0, 'rotation')],
        },
      ],
    }, data),
  ];
}

/// The cases for the clips of a glTF file.
List<Case> importCases() => [
  ..._containerCases(),
  ..._nameCases(),
  ..._rateCases(),
  ..._interpolationCases(),
  ..._rotationCases(),
  ..._shortCases(),
  ..._keyCases(),
  ..._reachCases(),
  ..._animationCases(),
  ..._targetCases(),
  ..._packedCases(),
];

Map<String, Object?> _node(
  String name, {
  List<int>? children,
  List<double>? translation,
  List<double>? rotation,
  List<double>? scale,
  List<double>? matrix,
}) => {
  'name': name,
  if (children != null) 'children': children,
  if (translation != null) 'translation': translation,
  if (rotation != null) 'rotation': rotation,
  if (scale != null) 'scale': scale,
  if (matrix != null) 'matrix': matrix,
};

_Json _skin(List<Object?> nodes, List<Object?> joints, [Object? more]) => {
  'nodes': nodes,
  'skins': [
    {'joints': joints},
    ?more,
  ],
};

/// The cases for the skeletons of a glTF file.
List<Case> skeletonCases() => [
  _skeletonOf(
    'skeletons: an armature, hips and a spine',
    _skin(
      [
        _node(
          'Armature',
          rotation: [-0.7071068, 0, 0, 0.7071068],
          scale: [0.01, 0.01, 0.01],
          children: [1],
        ),
        _node('Hips', translation: [0, 100, 0], children: [2]),
        _node(
          'Spine',
          matrix: [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 20, 0, 1],
        ),
      ],
      [2, 1],
    ),
  ),
  _skeletonOf(
    'skeletons: names that repeat',
    _skin(
      [
        _node('A', children: [1]),
        _node('A'),
      ],
      [0, 1],
    ),
  ),
  _skeletonOf('skeletons: no skin', {
    'nodes': [<String, Object?>{}],
  }),
  _skeletonOf('skeletons: no nodes', {
    'skins': [
      {
        'joints': [0],
      },
    ],
  }),
  _skeletonOf('skeletons: nothing', <String, Object?>{}),
  _skeletonOf('skeletons: a skin with no joints', {
    'nodes': [_node('A')],
    'skins': [
      <String, Object?>{},
      {'joints': <Object?>[]},
      {'joints': 5},
      5,
    ],
  }),
  _skeletonOf(
    'skeletons: two skins',
    _skin(
      [
        _node('A', children: [1], translation: [1, 0, 0]),
        _node('B', translation: [0, 2, 0]),
        _node('C', translation: [0, 0, 3]),
      ],
      [0, 1],
      {
        'joints': [2],
      },
    ),
  ),
  _skeletonOf(
    'skeletons: a node between two joints',
    _skin(
      [
        _node('Root', children: [1], translation: [0, 1, 0]),
        _node(
          'Between',
          children: [2],
          translation: [0, 5, 0],
          scale: [2, 2, 2],
        ),
        _node('Tip', translation: [0, 1, 0]),
      ],
      [0, 2],
    ),
  ),
  _skeletonOf(
    'skeletons: children that loop',
    _skin(
      [
        _node('A', children: [1]),
        _node('B', children: [0]),
      ],
      [0, 1],
    ),
  ),
  _skeletonOf(
    'skeletons: a node under two parents',
    _skin(
      [
        _node('A', children: [2], translation: [1, 0, 0]),
        _node('B', children: [2], translation: [0, 1, 0]),
        _node('C', translation: [0, 0, 1]),
      ],
      [0, 1, 2],
    ),
  ),
  _skeletonOf(
    'skeletons: children that are not nodes',
    _skin(
      [
        _node('A', children: [1, -1, 9, 0]),
        _node('B'),
      ],
      [0, 1],
    ),
  ),
  _skeletonOf('skeletons: a joint past the end', _skin([_node('A')], [0, 99])),
  _skeletonOf('skeletons: a joint below nought', _skin([_node('A')], [-1, 0])),
  _skeletonOf(
    'skeletons: a joint twice',
    _skin(
      [
        _node('A', children: [1]),
        _node('B', translation: [0, 1, 0]),
      ],
      [1, 0, 1],
    ),
  ),
  _skeletonOf(
    'skeletons: joints that are not whole numbers',
    _skin(
      [
        _node('A', children: [1]),
        _node('B', translation: [0, 1, 0]),
      ],
      [1, 'x', 0, 0.5, null],
    ),
  ),
  _skeletonOf(
    'skeletons: children first, three deep',
    _skin(
      [
        _node('Top', children: [1, 3], translation: [0, 1, 0]),
        _node('Mid', children: [2], translation: [0, 2, 0]),
        _node('Low', translation: [0, 3, 0]),
        _node('Side', translation: [1, 0, 0]),
      ],
      [2, 3, 1, 0],
    ),
  ),
  _skeletonOf(
    'skeletons: a grandparent joint',
    _skin(
      [
        _node('Top', children: [1], translation: [0, 1, 0]),
        _node('Gap', children: [2], translation: [0, 2, 0]),
        _node('Low', translation: [0, 3, 0]),
        _node('Other', translation: [3, 3, 3]),
      ],
      [2, 3, 0],
    ),
  ),
  _skeletonOf(
    'skeletons: transforms strange',
    _skin(
      [
        _node('Zero', rotation: [0, 0, 0, 0], translation: [1, 2, 3]),
        _node('Long', rotation: [0, 0, 0, 2], scale: [1, 2, 3]),
        _node('Short', translation: [1, 2], rotation: [0, 0, 1]),
        _node(
          'Matrix',
          matrix: [2, 0, 0, 0, 0, 3, 0, 0, 0, 0, 4, 0, 5, 6, 7, 1],
        ),
        _node(
          'Short matrix',
          matrix: [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 20, 0],
          translation: [9, 9, 9],
        ),
        _node(
          'Both',
          matrix: [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 1, 1, 1, 1],
          translation: [9, 9, 9],
        ),
        {
          'name': 'Not numbers',
          'translation': ['a', 0, 0],
          'rotation': [0, 0, 0, null],
          'scale': 5,
        },
        _node('Whole numbers', translation: [1, 2, 3], scale: [1, 1, 1]),
        _node('Edge', rotation: [1e-9, 0, 0, 0]),
      ],
      [0, 1, 2, 3, 4, 5, 6, 7, 8],
    ),
  ),
  _skeletonOf(
    'skeletons: the root hangs from a chain',
    _skin(
      [
        _node(
          'Scene',
          children: [1],
          translation: [0, 1, 0],
          rotation: [0, 0.7071068, 0, 0.7071068],
        ),
        _node(
          'Armature',
          children: [2],
          scale: [2, 2, 2],
          translation: [1, 0, 0],
        ),
        _node('Hips', translation: [0, 3, 0]),
      ],
      [2],
    ),
  ),
  _skeletonOf(
    'skeletons: a root that loops back on its parents',
    _skin(
      [
        _node('A', children: [1]),
        _node('B', children: [2]),
        _node('C', children: [1], translation: [0, 4, 0]),
      ],
      [2],
    ),
  ),
  _skeletonOf('skeletons: names from meshes', {
    ..._skin(
      [
        {
          'mesh': 0,
          'children': [1],
        },
        {'mesh': 0},
        {
          'translation': [1, 1, 1],
        },
      ],
      [0, 1, 2],
    ),
    'meshes': [
      {'name': 'Body'},
    ],
  }),
];

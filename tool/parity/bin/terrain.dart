import 'dart:convert';
import 'dart:math' as math;
import 'dart:typed_data';

import 'package:orblit_parity_tool/fixture.dart';
import 'package:orblit_terrain/orblit_terrain.dart';
import 'package:vector_math/vector_math_64.dart';

typedef Case = Map<String, Object?>;

/// An int JSON can carry exactly. A reader hands back a double past 2^53, so
/// those travel as strings.
Object big(int value) => value > 9007199254740992 || value < -9007199254740992
    ? value.toString()
    : value;

/// What a call gave, or that it threw. The kind of error is not compared:
/// Dart's `RangeError`, `FormatException` and the format exceptions have no
/// one .NET twin, and a refusal is what the callers see.
Case attempt(Object? Function() body) {
  try {
    return {'value': body()};
  } on Object {
    return {'throws': true};
  }
}

String b64(List<int> bytes) => base64Encode(bytes);

String dataB64(TypedData data) =>
    b64(data.buffer.asUint8List(data.offsetInBytes, data.lengthInBytes));

Object? tree(Object? value) => switch (value) {
  final double d => number(d),
  final int i => big(i),
  final Map<Object?, Object?> m => {
    for (final e in m.entries) '${e.key}': tree(e.value),
  },
  final List<Object?> l => [for (final e in l) tree(e)],
  _ => value,
};

Object? vec(Vector3? v) =>
    v == null ? null : [number(v.x), number(v.y), number(v.z)];

List<Object> keyJson(RegionKey key) => [big(key.x), big(key.z)];

Object? pick(math.Random r, List<Object?> pool) => pool[r.nextInt(pool.length)];

// --- Cover ------------------------------------------------------------------

Case coverState(Cover c) => {
  'word': c.word,
  'base': c.base,
  'overlay': c.overlay,
  'blendStep': c.blendStep,
  'blend': number(c.blend),
  'angle': c.angle,
  'radians': number(c.radians),
  'scaleCode': c.scaleCode,
  'scale': c.scale,
  'uv': number(c.uvMultiplier),
  'hole': c.hole,
  'navigation': c.navigation,
  'automatic': c.automatic,
};

Cover coverWith(Cover c, String field, Object value) => switch (field) {
  'base' => c.withBase(value as int),
  'overlay' => c.withOverlay(value as int),
  'blend' => c.withBlend(value as double),
  'angle' => c.withAngle(value as int),
  'scale' => c.withScale(value as int),
  'hole' => c.withHole(value as bool),
  'navigation' => c.withNavigation(value as bool),
  _ => c.withAutomatic(value as bool),
};

Object withValue(Object value) => value is double ? number(value) : value;

Case coverCase({
  int base = 0,
  int overlay = 0,
  double blend = 0,
  int angle = 0,
  int scale = 0,
  bool hole = false,
  bool navigation = false,
  bool automatic = false,
  List<(String, Object)> withs = const [],
}) => {
  'op': 'cover',
  'args': {
    'base': base,
    'overlay': overlay,
    'blend': number(blend),
    'angle': angle,
    'scale': scale,
    'hole': hole,
    'navigation': navigation,
    'automatic': automatic,
  },
  'withs': [
    for (final (field, value) in withs) [field, withValue(value)],
  ],
  'expect': attempt(() {
    var c = Cover.of(
      base: base,
      overlay: overlay,
      blend: blend,
      angle: angle,
      scale: scale,
      hole: hole,
      navigation: navigation,
      automatic: automatic,
    );
    final states = [coverState(c)];
    for (final (field, value) in withs) {
      c = coverWith(c, field, value);
      states.add(coverState(c));
    }
    return states;
  }),
};

List<Case> coverCases() {
  final r = math.Random(11);
  final blends = <double>[
    0,
    1,
    0.5,
    1 / 510,
    3 / 510,
    0.999,
    -0.0,
    -0.25,
    1.5,
    double.nan,
    double.infinity,
    double.negativeInfinity,
    1e-300,
  ];
  double blend() => r.nextBool()
      ? blends[r.nextInt(blends.length)]
      : r.nextDouble() * 1.4 - 0.2;
  final cases = <Case>[
    coverCase(),
    coverCase(hole: true),
    coverCase(navigation: true, automatic: true),
    for (final scale in [
      -100,
      -60,
      -50,
      -30,
      -10,
      0,
      10,
      20,
      30,
      40,
      50,
      60,
      70,
      80,
      90,
      1000,
    ])
      coverCase(scale: scale, withs: [('scale', scale + 5)]),
  ];
  for (var n = 0; n < 150; n++) {
    cases.add(
      coverCase(
        base: r.nextInt(80) - 20,
        overlay: r.nextInt(80) - 20,
        blend: blend(),
        angle: r.nextInt(40) - 10,
        scale: r.nextInt(240) - 120,
        hole: r.nextBool(),
        navigation: r.nextBool(),
        automatic: r.nextBool(),
        withs: [
          ('base', r.nextInt(50) - 5),
          ('blend', blend()),
          ('scale', r.nextInt(240) - 120),
          ('angle', r.nextInt(40) - 10),
          ('overlay', r.nextInt(50) - 5),
          ('hole', r.nextBool()),
          ('navigation', r.nextBool()),
          ('automatic', r.nextBool()),
        ],
      ),
    );
  }
  return cases;
}

// --- Ground colour ----------------------------------------------------------

Case colourState(GroundColour c) => {
  'rgba': c.rgba,
  'red': c.red,
  'green': c.green,
  'blue': c.blue,
  'roughnessByte': c.roughnessByte,
  'roughness': number(c.roughness),
};

GroundColour colourWith(GroundColour c, List<Object> change) =>
    change[0] == 'colour'
    ? c.withColour(change[1] as int, change[2] as int, change[3] as int)
    : c.withRoughness(change[1] as double);

List<Object> colourChange(math.Random r, double Function() roughness) =>
    r.nextBool()
    ? ['colour', r.nextInt(400) - 60, r.nextInt(400) - 60, r.nextInt(400) - 60]
    : ['roughness', roughness()];

Case colourCase(String kind, List<Object> args, List<List<Object>> changes) => {
  'op': 'colour',
  'kind': kind,
  'args': [for (final a in args) withValue(a)],
  'changes': [
    for (final change in changes) [for (final c in change) withValue(c)],
  ],
  'expect': attempt(() {
    var c = kind == 'of'
        ? GroundColour.of(
            red: args[0] as int,
            green: args[1] as int,
            blue: args[2] as int,
            roughness: args[3] as double,
          )
        : GroundColour.bytes(
            args[0] as int,
            args[1] as int,
            args[2] as int,
            args[3] as int,
          );
    final states = [colourState(c)];
    for (final change in changes) {
      c = colourWith(c, change);
      states.add(colourState(c));
    }
    return states;
  }),
};

List<Case> colourCases() {
  final r = math.Random(12);
  final roughnesses = <double>[
    -1,
    1,
    0,
    -0.0,
    0.5,
    -0.5,
    1 / 255,
    -2,
    3,
    double.nan,
    double.infinity,
    double.negativeInfinity,
    1e-300,
  ];
  double roughness() => r.nextBool()
      ? roughnesses[r.nextInt(roughnesses.length)]
      : r.nextDouble() * 2.6 - 1.3;
  final cases = <Case>[
    colourCase('of', [255, 255, 255, 0.0], const []),
    colourCase('bytes', [255, 255, 128, 128], const []),
  ];
  for (var n = 0; n < 80; n++) {
    final changes = [for (var c = 0; c < 4; c++) colourChange(r, roughness)];
    cases.add(
      colourCase('of', [
        r.nextInt(400) - 60,
        r.nextInt(400) - 60,
        r.nextInt(400) - 60,
        roughness(),
      ], changes),
    );
    cases.add(
      colourCase('bytes', [
        r.nextInt(600) - 200,
        r.nextInt(600) - 200,
        r.nextInt(600) - 200,
        r.nextInt(600) - 200,
      ], changes),
    );
  }
  return cases;
}

// --- Distance policy --------------------------------------------------------

Case policyCase({
  required int ceiling,
  required double high,
  required double low,
  int floor = 1,
  double settle = 2,
  List<(double, double)> updates = const [],
}) => {
  'op': 'policy',
  'ceiling': ceiling,
  'high': number(high),
  'low': number(low),
  'floor': floor,
  'settle': number(settle),
  'updates': [
    for (final (cost, seconds) in updates) [number(cost), number(seconds)],
  ],
  'expect': attempt(() {
    final policy = DistancePolicy(
      ceiling: ceiling,
      high: high,
      low: low,
      floor: floor,
      settle: settle,
    );
    final steps = [policy.steps];
    for (final (cost, seconds) in updates) {
      policy.update(cost, seconds);
      steps.add(policy.steps);
    }
    return {'steps': steps, 'fraction': number(policy.fraction)};
  }),
};

List<Case> policyCases() {
  final r = math.Random(13);
  final cases = <Case>[
    policyCase(ceiling: 4, high: 16, low: 10),
    policyCase(ceiling: 0, high: 16, low: 10),
    policyCase(ceiling: 4, high: 16, low: 10, floor: 0),
    policyCase(ceiling: 4, high: 16, low: 10, floor: 5),
    policyCase(ceiling: 4, high: 10, low: 10),
    policyCase(ceiling: 4, high: 10, low: 12),
    policyCase(ceiling: 4, high: 16, low: double.nan),
    policyCase(ceiling: 4, high: double.nan, low: 1),
    policyCase(ceiling: 4, high: 16, low: 10, settle: 0),
    policyCase(ceiling: 4, high: 16, low: 10, settle: -1),
    policyCase(ceiling: 4, high: 16, low: 10, settle: double.nan),
    policyCase(
      ceiling: 3,
      high: 16,
      low: 10,
      updates: [
        (20, 0),
        (20, 1),
        (double.nan, 2),
        (20, 4),
        (double.infinity, 6),
        (1, 8),
      ],
    ),
    // A mean exactly on a bound moves nothing, whichever side it came from.
    policyCase(
      ceiling: 3,
      high: 16,
      low: 10,
      updates: [(20, 0), (20, 2), (10, 4), (16, 6), (14, 7), (18, 8), (10, 10)],
    ),
  ];
  for (var n = 0; n < 60; n++) {
    final ceiling = 1 + r.nextInt(8);
    final settle = pick(r, [0.5, 1, 2, 3.5]) as num;
    var seconds = r.nextDouble() * 3;
    final updates = <(double, double)>[];
    for (var u = 0; u < 40; u++) {
      seconds += r.nextBool() ? 0 : r.nextDouble() * 1.5;
      updates.add((r.nextDouble() * 30, seconds));
    }
    cases.add(
      policyCase(
        ceiling: ceiling,
        high: 14 + r.nextDouble() * 4,
        low: 6 + r.nextDouble() * 6,
        floor: 1 + r.nextInt(ceiling),
        settle: settle.toDouble(),
        updates: updates,
      ),
    );
  }
  return cases;
}

// --- Region keys ------------------------------------------------------------

List<Case> keyCases() {
  final r = math.Random(14);
  final names = <String>[
    'x0_z0.oregion',
    'x1_z2.oregion',
    'x-3_z4.oregion',
    'x5_z-6.oregion',
    'x-0_z-0.oregion',
    'x007_z0.oregion',
    'x1_z2.OREGION',
    'x1_z2.oregion\n',
    '\nx1_z2.oregion',
    'x1_z2.oregion ',
    'x+1_z2.oregion',
    'x1_z2',
    'x1z2.oregion',
    'x_z.oregion',
    'x1_z2.oregion.bak',
    'X1_z2.oregion',
    'x9223372036854775807_z-9223372036854775808.oregion',
    'x9223372036854775808_z0.oregion',
    'x0_z-9223372036854775809.oregion',
    'x99999999999999999999999_z0.oregion',
    'x١_z2.oregion',
    '',
    for (var n = 0; n < 40; n++)
      'x${r.nextInt(2000) - 1000}_z${r.nextInt(2000) - 1000}.oregion',
  ];
  return [
    for (final name in names)
      {
        'op': 'keyName',
        'name': name,
        'expect': attempt(() {
          final key = RegionKey.fromFileName(name);
          return key == null
              ? null
              : {'key': keyJson(key), 'name': key.fileName};
        }),
      },
    for (var n = 0; n < 80; n++)
      () {
        final size = [1, 2, 3, 4, 8, 256, 4096, 0, -4][r.nextInt(9)];
        final i = n.isEven
            ? r.nextInt(2000) - 1000
            : (r.nextInt(2000) - 1000) * 4611686018427387;
        final j = r.nextInt(2000) - 1000;
        return <String, Object?>{
          'op': 'keyContaining',
          'i': big(i),
          'j': big(j),
          'size': size,
          'expect': attempt(() => keyJson(RegionKey.containing(i, j, size))),
        };
      }(),
    {
      'op': 'keyContaining',
      'i': big(-9223372036854775808),
      'j': big(9223372036854775807),
      'size': 4,
      'expect': attempt(
        () => keyJson(
          RegionKey.containing(-9223372036854775808, 9223372036854775807, 4),
        ),
      ),
    },
  ];
}

// --- Regions ----------------------------------------------------------------

TerrainRegion hills(
  math.Random r,
  RegionKey key,
  int size,
  double spacing, {
  double amplitude = 6,
  double holes = 0.03,
}) {
  final region = TerrainRegion(key, size);
  for (var j = 0; j < size; j++) {
    for (var i = 0; i < size; i++) {
      final at = j * size + i;
      final wx = (key.x * size + i) * spacing;
      final wz = (key.z * size + j) * spacing;
      region.heights[at] =
          amplitude * math.sin(wx * 0.31) * math.cos(wz * 0.23) +
          (r.nextDouble() - 0.5) * 0.6 +
          (wx > 6 ? 4 : 0);
      if (r.nextDouble() < holes) {
        region.cover[at] = Cover.of(hole: true).word;
      } else if (r.nextDouble() < 0.3) {
        region.cover[at] = Cover.of(
          base: r.nextInt(4),
          overlay: r.nextInt(4),
          blend: r.nextDouble(),
          angle: r.nextInt(16),
          scale: r.nextInt(8) * 20 - 60,
          navigation: r.nextBool(),
        ).word;
      }
      if (r.nextDouble() < 0.4) {
        final colour = GroundColour.bytes(
          r.nextInt(256),
          r.nextInt(256),
          r.nextInt(256),
          r.nextInt(256),
        );
        region.setColour(i, j, colour);
      }
    }
  }
  region.touch();
  return region;
}

Case regionEncodeCase(TerrainRegion region) => {
  'op': 'regionEncode',
  'x': big(region.key.x),
  'z': big(region.key.z),
  'size': region.size,
  'heights': dataB64(region.heights),
  'cover': dataB64(region.cover),
  'colour': dataB64(region.colour),
  'expect': attempt(() => b64(region.encode())),
};

/// A decode case, or nothing when the file decodes to a NaN height. A NaN
/// keeps whatever payload the platform gave it, so such a file cannot be
/// compared byte for byte.
Case? regionDecodeCase(Uint8List bytes) {
  final attempted = attempt(() {
    final load = TerrainRegion.decode(bytes);
    final region = load.region;
    return {
      'nan': region.heights.any((h) => h.isNaN),
      'x': big(region.key.x),
      'z': big(region.key.z),
      'size': region.size,
      'heights': dataB64(region.heights),
      'cover': dataB64(region.cover),
      'colour': dataB64(region.colour),
      'problems': load.problems,
    };
  });
  final value = attempted['value'];
  if (value is Map<String, Object?>) {
    if (value['nan']! as bool) {
      return null;
    }
    value.remove('nan');
  }
  return {'op': 'regionDecode', 'bytes': b64(bytes), 'expect': attempted};
}

Uint8List regionFile(String header, List<List<int>> maps) {
  final head = utf8.encode('$header\n');
  final start = (head.length + 3) & ~3;
  final out = BytesBuilder()
    ..add(head)
    ..add(Uint8List(start - head.length));
  for (final map in maps) {
    out.add(map);
  }
  return out.takeBytes();
}

/// Fixed map bytes for a size-4 region that read as finite floats whatever
/// order they are taken in.
List<List<int>> fuzzBody() => [
  List<int>.filled(64, 0x3C),
  List<int>.generate(64, (n) => (n * 5) % 100),
  List<int>.generate(64, (n) => (n * 3 + 1) % 90),
  List<int>.filled(8, 0x11),
];

/// A header map with some keys swapped for values a hand or a bug could write.
String fuzzHeader(
  math.Random r,
  Map<String, Object?> base,
  Map<String, List<Object?>> pools,
) {
  final map = {...base};
  for (var edit = 1 + r.nextInt(2); edit > 0; edit--) {
    final key = pools.keys.elementAt(r.nextInt(pools.length));
    if (r.nextInt(6) == 0) {
      map.remove(key);
    } else {
      map[key] = pick(r, pools[key]!);
    }
  }
  return jsonEncode(map);
}

void addDecode(List<Case> cases, Uint8List bytes) {
  final decoded = regionDecodeCase(bytes);
  if (decoded != null) {
    cases.add(decoded);
  }
}

List<Case> regionCases() {
  final r = math.Random(15);
  final cases = <Case>[];
  final files = <Uint8List>[];

  void encoded(TerrainRegion region) {
    cases.add(regionEncodeCase(region));
    files.add(region.encode());
  }

  for (final size in [2, 4, 8, 16]) {
    for (final key in [
      const RegionKey(0, 0),
      const RegionKey(-5, 7),
      const RegionKey(1099511627776, -1099511627776),
      const RegionKey(9223372036854775807, -9223372036854775808),
    ]) {
      encoded(hills(r, key, size, 1));
    }
  }
  final flat = TerrainRegion(const RegionKey(2, 3), 4);
  encoded(flat);
  final onlyNegativeZero = TerrainRegion(const RegionKey(2, 3), 4)
    ..heights[3] = -0.0;
  encoded(onlyNegativeZero);
  encoded(
    TerrainRegion(const RegionKey(2, 3), 4)
      ..heights[0] = double.infinity
      ..heights[1] = double.negativeInfinity
      ..heights[2] = 1e30
      ..heights[3] = 1e39
      ..heights[4] = 1e-45
      ..heights[5] = -1e-45
      ..heights[6] = 3.4028234663852886e38
      ..heights[7] = 16777217,
  );
  encoded(
    TerrainRegion(const RegionKey(2, 3), 4)
      ..cover[5] = 0
      ..cover[6] = 0xFFFFFFFF,
  );
  encoded(
    TerrainRegion(const RegionKey(2, 3), 4)
      ..setColour(1, 1, GroundColour.bytes(0, 0, 0, 0)),
  );

  // The sizes a region may be, and the lengths it checks.
  for (final size in [0, 1, 2, 3, 4, 6, 8, 12, 4097, 8192, -4, 1 << 40]) {
    cases.add({
      'op': 'regionNew',
      'size': big(size),
      'lengths': null,
      'expect': attempt(() {
        TerrainRegion(const RegionKey(0, 0), size);
        return true;
      }),
    });
  }
  for (final lengths in [
    [15, 16, 64],
    [16, 15, 64],
    [16, 16, 63],
    [16, 16, 65],
    [17, 16, 64],
    [0, 16, 64],
    [16, 16, 64],
  ]) {
    cases.add({
      'op': 'regionNew',
      'size': 4,
      'lengths': lengths,
      'expect': attempt(() {
        TerrainRegion(
          const RegionKey(0, 0),
          4,
          heights: Float32List(lengths[0]),
          cover: Uint32List(lengths[1]),
          colour: Uint8List(lengths[2]),
        );
        return true;
      }),
    });
  }
  for (final size in [
    -1,
    0,
    1,
    2,
    3,
    4,
    5,
    8,
    100,
    4096,
    4097,
    8192,
    1 << 40,
  ]) {
    cases.add({
      'op': 'validSize',
      'size': big(size),
      'expect': TerrainRegion.validSize(size),
    });
  }

  // Valid files first, then the ways a file goes wrong.
  for (final bytes in files) {
    addDecode(cases, bytes);
  }
  final header = <String, Object?>{
    'kind': 'orblit.region',
    'formatVersion': 1,
    'x': 1,
    'z': -2,
    'size': 4,
    'maps': [
      {'name': 'height', 'bytes': 64},
      {'name': 'cover', 'bytes': 64},
      {'name': 'colour', 'bytes': 64},
    ],
  };
  final pools = <String, List<Object?>>{
    'kind': ['orblit.region', 'orblit.terrain', '', 5, false],
    'formatVersion': [1, 2, 0, -1, '1', 1.0, 99999999999],
    'x': [
      0,
      3,
      -4,
      1.0,
      1.5,
      '1',
      false,
      9223372036854775807,
      1e30,
      4611686018427387904,
    ],
    'z': [0, 3, -4, 1.0, 1.5, '1', false, -9223372036854775808, 1e30],
    'size': [4, 2, 3, 0, 8192, '4', 4.0, 4.5, false, 8, 1, -4],
    'maps': [
      <Object>[],
      'x',
      5,
      <Object>[
        {'name': 'height', 'bytes': 64},
      ],
      <Object>[
        {'name': 'height', 'bytes': 64},
        {'name': 'cover', 'bytes': 64},
      ],
      <Object>[
        {'name': 'cover', 'bytes': 64},
        {'name': 'height', 'bytes': 64},
      ],
      <Object>[
        {'name': 'colour', 'bytes': 64},
        {'name': 'mystery', 'bytes': 8},
      ],
      <Object>[
        {'name': 'mystery', 'bytes': 8},
        {'name': 'height', 'bytes': 64},
      ],
      <Object>[
        {'name': 'height', 'bytes': -1},
      ],
      <Object>[
        {'name': 5, 'bytes': 4},
      ],
      <Object>[
        {'name': 'height', 'bytes': '4'},
      ],
      <Object>[
        {'name': 'height', 'bytes': 4.0},
      ],
      <Object>[
        {'name': 'height'},
      ],
      <Object>[
        {'bytes': 4},
      ],
      <Object>[
        {'name': 'height', 'bytes': 1000},
      ],
      <Object>[
        {'name': 'height', 'bytes': 60},
      ],
      <Object>[5],
      <Object?>[null],
      <Object>[
        {'name': 'height', 'bytes': 64},
        {'name': 'height', 'bytes': 64},
      ],
      <Object>[
        {'name': 'height', 'bytes': 64},
        {'name': 'cover', 'bytes': 64},
        {'name': 'colour', 'bytes': 64},
        {'name': 'mystery', 'bytes': 8},
        {'name': 'more', 'bytes': 0},
      ],
    ],
    'extra': ['x', 1, null],
  };
  for (var n = 0; n < 160; n++) {
    addDecode(cases, regionFile(fuzzHeader(r, header, pools), fuzzBody()));
  }

  final good = regionFile(jsonEncode(header), fuzzBody());
  final headerEnd = good.indexOf(0x0A);
  final texts = <String, Uint8List>{
    'empty': Uint8List(0),
    'one newline': Uint8List.fromList([0x0A]),
    'no newline': Uint8List.fromList(utf8.encode(jsonEncode(header))),
    'not json': Uint8List.fromList(utf8.encode('hello\n')),
    'json list': Uint8List.fromList(utf8.encode('[]\n')),
    'json null': Uint8List.fromList(utf8.encode('null\n')),
    'blank header': Uint8List.fromList(utf8.encode('\n')),
    'bom header': Uint8List.fromList([0xEF, 0xBB, 0xBF, ...good]),
    'bad utf8': Uint8List.fromList([
      ...good.sublist(0, 12),
      0xFF,
      ...good.sublist(12),
    ]),
    'lone continuation': Uint8List.fromList([
      ...good.sublist(0, 12),
      0x80,
      ...good.sublist(12),
    ]),
    'two byte char in a name': Uint8List.fromList(
      utf8.encode('${jsonEncode({...header, 'extra': 'é'})}\n'),
    ),
    'crlf': Uint8List.fromList(
      utf8.encode('${jsonEncode(header)}\r\n') +
          fuzzBody().expand((m) => m).toList(),
    ),
    'pad is not zero': Uint8List.fromList([
      ...good.sublist(0, headerEnd + 1),
      ...List<int>.filled(good.length - headerEnd - 1, 0x7),
    ]),
  };
  for (final bytes in texts.values) {
    addDecode(cases, bytes);
  }
  for (var n = 0; n < 40; n++) {
    addDecode(cases, good.sublist(0, r.nextInt(good.length)));
  }
  for (var n = 0; n < 60; n++) {
    final copy = Uint8List.fromList(good);
    copy[r.nextInt(headerEnd + 8)] = r.nextInt(256);
    addDecode(cases, copy);
  }
  for (final extra in [1, 4, 100]) {
    addDecode(
      cases,
      Uint8List.fromList([...good, ...List<int>.filled(extra, 0x42)]),
    );
  }
  return cases;
}

// --- Worlds -----------------------------------------------------------------

const List<TerrainSet> worldSets = [
  TerrainSet(name: 'grass', albedo: 'grass.png', tileSize: 3),
  TerrainSet(name: 'scree', normal: 'scree_n.png', triplanar: true),
  TerrainSet(name: 'sand'),
];

Terrain makeWorld(
  math.Random r, {
  int size = 8,
  double spacing = 1,
  int reach = 1,
  List<ScatterLayer> scatter = const [],
  AutoCover autoCover = const AutoCover(),
  double sharpness = 0.87,
  double amplitude = 6,
  double holes = 0.03,
  double dropped = 0.15,
}) {
  final terrain = Terrain(
    regionSize: size,
    spacing: spacing,
    sets: worldSets,
    autoCover: autoCover,
    blendSharpness: sharpness,
    scatter: scatter,
  );
  for (var z = -reach; z <= reach; z++) {
    for (var x = -reach; x <= reach; x++) {
      if ((x != 0 || z != 0) && r.nextDouble() < dropped) {
        continue;
      }
      terrain.putRegion(
        hills(
          r,
          RegionKey(x, z),
          size,
          spacing,
          amplitude: amplitude,
          holes: holes,
        ),
      );
    }
  }
  return terrain;
}

List<TerrainRegion> sortedRegions(Terrain terrain) =>
    terrain.regions.toList()..sort(
      (a, b) => a.key.z != b.key.z
          ? a.key.z.compareTo(b.key.z)
          : a.key.x.compareTo(b.key.x),
    );

List<Case> regionFiles(Terrain terrain) => [
  for (final region in sortedRegions(terrain))
    {
      'x': big(region.key.x),
      'z': big(region.key.z),
      'file': b64(region.encode()),
    },
];

/// What a stroke left in each region, with the heights as numbers. A NaN
/// height has a bit pattern the CPU chooses, so the file's bytes cannot be
/// compared across machines.
List<Case> regionDumps(Terrain terrain) => [
  for (final region in sortedRegions(terrain))
    {
      'x': big(region.key.x),
      'z': big(region.key.z),
      'heights': [for (final h in region.heights) number(h)],
      'cover': dataB64(region.cover),
      'colour': dataB64(region.colour),
    },
];

Case worldJson(Terrain terrain) => {
  'terrain': terrain.encode(),
  'regions': regionFiles(terrain),
};

// --- Terrain files ----------------------------------------------------------

Case terrainDecodeCase(String text) => {
  'op': 'terrainDecode',
  'text': text,
  'expect': attempt(() {
    final load = Terrain.decode(text);
    return {
      'terrain': tree(load.terrain.toJson()),
      'regions': [for (final key in load.regions) keyJson(key)],
      'problems': load.problems,
      'encoded': load.terrain.encode(),
    };
  }),
};

ScatterLayer sampleLayer(String name, {int seed = 0}) => ScatterLayer(
  name: name,
  seed: seed,
  density: 0.5,
  sets: const [0, 2],
  maxSlope: 40,
  size: (0.2, 0.4, 0.2),
  minScale: 0.5,
  maxScale: 1.5,
  colour: 0x4A8C2A,
  colourVariation: 0.2,
  groundTint: 0.5,
  mesh: name == 'tree' ? 'tree.glb' : null,
);

List<Case> terrainFileCases() {
  final r = math.Random(16);
  final base =
      Terrain(
          regionSize: 8,
          spacing: 0.5,
          sets: worldSets.sublist(0, 2),
          autoCover: const AutoCover(
            steep: 1,
            flat: 0,
            slope: 0.7,
            heightFalloff: 0.2,
          ),
          blendSharpness: 0.6,
          scatter: [
            sampleLayer('grass', seed: 3),
            sampleLayer('tree', seed: -1),
          ],
        )
        ..addRegion(const RegionKey(0, 0))
        ..addRegion(const RegionKey(-1, 2))
        ..addRegion(const RegionKey(2, -1));
  final text = base.encode();
  final json = jsonDecode(text) as Map<String, Object?>;
  final pools = <String, List<Object?>>{
    'kind': ['orblit.terrain', 'orblit.region', '', 5],
    'formatVersion': [1, 2, 0, -1, '1', 1.0, 99999999999],
    'regionSize': [4, 8, 3, 0, 4096, 8192, 4.0, 4.5, '8', -8, 1e300, 16],
    'spacing': [1, 0.5, 0, -1, 1e-300, 'x', 2, 1e300, -0.0],
    'blendSharpness': [0.5, 2, -1, 'x', 0, 1, 1e300],
    'autoCover': [
      <String, Object>{
        'steep': 2,
        'flat': 3,
        'slope': 0.5,
        'heightFalloff': 0.3,
      },
      <String, Object>{
        'steep': -1,
        'flat': 1.5,
        'slope': 0,
        'heightFalloff': -2,
      },
      <String, Object>{'steep': 99999999999, 'flat': '1'},
      <String, Object>{},
      'x',
      <Object>[],
    ],
    'sets': [
      <Object>[],
      'x',
      5,
      <Object>[
        {'name': 'a'},
        {
          'name': 'b',
          'albedo': 'x.png',
          'normal': 'y.png',
          'tileSize': 2.5,
          'triplanar': true,
        },
      ],
      <Object>[
        {'name': 'a', 'tileSize': -1},
        {'name': 'b', 'tileSize': 0},
        {'name': 'c', 'tileSize': 'x'},
        {'name': 'd', 'albedo': 5, 'normal': '', 'triplanar': 1},
      ],
      <Object?>[
        {},
        {'name': 5},
        5,
        null,
        {'name': ''},
        {'name': 'ok'},
      ],
      <Object>[
        for (var n = 0; n < 33; n++) {'name': 's$n'},
      ],
      <Object>[
        for (var n = 0; n < 32; n++) {'name': 's$n'},
      ],
    ],
    'scatter': [
      <Object>[],
      'x',
      5,
      <Object>[
        {'name': 'g', 'density': 500},
        {'name': 'h', 'density': 0},
        {'density': 1},
        5,
        {
          'name': 'i',
          'sets': [0, 40, 'a'],
          'size': [1, 2],
          'colour': 'red',
        },
      ],
      <Object>[
        {
          'name': 'j',
          'minSlope': 80,
          'maxSlope': 10,
          'minScale': 5,
          'maxScale': 2,
          'lean': 4,
        },
      ],
    ],
    'regions': [
      <Object>[],
      'x',
      5,
      <Object>[
        [0, 0],
        [1, 1],
      ],
      <Object>[
        [0, 0],
        [0, 0],
      ],
      <Object>[
        [0],
        [0, 1, 2],
        'a',
        5,
      ],
      <Object>[
        [0.0, 1],
        [1, 2.5],
        ['a', 'b'],
        [null, null],
      ],
      <Object>[
        [9223372036854775807, -9223372036854775808],
      ],
      <Object>[
        [1e30, 1],
      ],
    ],
    'extra': ['x', 1, null],
  };
  final cases = <Case>[
    terrainDecodeCase(text),
    terrainDecodeCase(Terrain().encode()),
    terrainDecodeCase('not json'),
    terrainDecodeCase(''),
    terrainDecodeCase('[]'),
    terrainDecodeCase('null'),
    terrainDecodeCase('{}'),
    terrainDecodeCase('{"kind":"orblit.terrain"}'),
    terrainDecodeCase('{"kind":"orblit.terrain","regionSize":8}'),
    terrainDecodeCase('{"kind":"orblit.terrain","regionSize":8} trailing'),
    terrainDecodeCase('\u{FEFF}$text'),
    terrainDecodeCase(' \n\t$text\n\n'),
    terrainDecodeCase(text.replaceAll('\n', '\r\n')),
    terrainDecodeCase(
      '{"kind":"orblit.terrain","regionSize":8,"regionSize":16}',
    ),
    terrainDecodeCase(
      '{"kind":"orblit.terrain","regionSize":8,"spacing":1e999}',
    ),
    terrainDecodeCase(
      '{"kind":"orblit.terrain","regionSize":8,"spacing":-1e999}',
    ),
    terrainDecodeCase('{"kind":"orblit.terrain","regionSize":8e0}'),
    terrainDecodeCase('{"kind":"orblit.terrain","regionSize":8.0}'),
    terrainDecodeCase('{"kind":"orblit.terrain","regionSize":"8"}'),
    terrainDecodeCase(
      '{"kind":"orblit.terrain","regionSize":8,"sets":[{"name":"é中"}]}',
    ),
  ];
  for (var n = 0; n < 160; n++) {
    cases.add(terrainDecodeCase(fuzzHeader(r, json, pools)));
  }
  const replacements = '{}[],:"0123456789-.eE+tfnx ';
  for (var n = 0; n < 60; n++) {
    final chars = text.split('');
    final at = r.nextInt(chars.length);
    chars[at] = replacements[r.nextInt(replacements.length)];
    cases.add(terrainDecodeCase(chars.join()));
  }
  for (var n = 0; n < 20; n++) {
    cases.add(terrainDecodeCase(text.substring(0, r.nextInt(text.length))));
  }
  return cases;
}

// --- Scatter layers ---------------------------------------------------------

Case layerCase(String text) => {
  'op': 'layer',
  'text': text,
  'expect': attempt(() {
    final problems = <String>[];
    final layer = ScatterLayer.fromJson(jsonDecode(text), problems);
    return {'layer': tree(layer?.toJson()), 'problems': problems};
  }),
};

List<Case> layerCases() {
  final r = math.Random(17);
  final base = sampleLayer('grass', seed: 4).toJson();
  final pools = <String, List<Object?>>{
    'name': ['grass', '', 5, false, 'é'],
    'seed': [
      0,
      7,
      -3,
      1.0,
      2.5,
      9007199254740993,
      1e3,
      'x',
      1e30,
      -9223372036854775808,
    ],
    'density': [0.5, 100, 101, 0, -1, 1e-9, 'a', 5, 1e300],
    'sets': [
      <Object>[],
      <Object>[0, 1],
      <Object>[1, 1, 2],
      <Object>[-1, 32, 3.0, 'a', false, 31],
      'x',
    ],
    'minSlope': [0, 10, 45, 90, 100, -5, 'x', 30.5],
    'maxSlope': [0, 10, 45, 90, 100, -5, 'x', 30.5],
    'minHeight': [-5, 0, 3, 1e9, 'x', -1e300],
    'maxHeight': [-5, 0, 3, 1e9, 'x', 1e300],
    'size': [
      <Object>[1, 2, 3],
      <Object>[1, 2],
      <Object>[0, 1, 1],
      <Object>[-1, 1, 1],
      <Object>[1.5, 2, 3],
      'x',
      <Object>[1, 'a', 3],
      <Object>[1e300, 1, 1],
      <Object>[1, 1, 1, 1],
    ],
    'minScale': [0.001, 1, 5, 1000, 1001, 0, -1, 'x'],
    'maxScale': [0.001, 1, 5, 1000, 1001, 0, -1, 'x'],
    'lean': [0, 0.5, 1, 2, -1, 'x'],
    'turn': [true, false, 0, 'false'],
    'lift': [0, 1.5, -2, 'x', 1e300],
    'colour': [
      '#ff8800',
      '#FF8800',
      '#12345',
      '#1234567',
      'ff8800',
      '#-12345',
      '# 12345',
      '#12345 ',
      '#zzzzzz',
      '#+ff880',
      '#١٢٣٤٥٦',
      5,
      '#ffffff',
      '#000000',
    ],
    'colourVariation': [0, 0.3, 1, 2, -1, 'x'],
    'groundTint': [0, 0.3, 1, 2, -1, 'x'],
    'range': [0, 50, -1, 1e308, 'x'],
    'castShadows': [true, false, 1, 'x'],
    'mesh': ['rock.glb', '', 5, false],
    'material': ['rock.mat', '', 5, false],
    'extra': ['x', 1],
  };
  return [
    layerCase('{"name":"a"}'),
    layerCase('{}'),
    layerCase('[]'),
    layerCase('5'),
    layerCase('null'),
    layerCase('{"name":5}'),
    layerCase(jsonEncode(base)),
    for (var n = 0; n < 200; n++) layerCase(fuzzHeader(r, base, pools)),
  ];
}

// --- Queries ----------------------------------------------------------------

List<Object> point(math.Random r, double lo, double hi) {
  final pool = <double>[
    lo + r.nextDouble() * (hi - lo),
    lo + r.nextDouble() * (hi - lo),
    lo + r.nextDouble() * (hi - lo),
    (lo + r.nextDouble() * (hi - lo)).roundToDouble(),
    (lo + r.nextDouble() * (hi - lo)).roundToDouble() + 0.5,
    double.nan,
    double.infinity,
    -double.infinity,
    -0.0,
  ];
  return [number(pool[r.nextInt(pool.length)])];
}

double d(Object value) =>
    value is String ? double.parse(value) : (value as num).toDouble();

List<Case> queryCases() {
  final r = math.Random(18);
  final cases = <Case>[];
  for (final (size, spacing, reach, holes) in [
    (4, 1.0, 1, 0.0),
    (8, 1.0, 1, 0.05),
    (8, 0.5, 1, 0.02),
    (4, 2.5, 2, 0.05),
    (16, 0.25, 0, 0.0),
    (2, 1.0, 2, 0.1),
  ]) {
    final terrain = makeWorld(
      r,
      size: size,
      spacing: spacing,
      reach: reach,
      holes: holes,
    );
    final across = size * spacing;
    final lo = -(reach + 0.6) * across;
    final hi = (reach + 1.6) * across;
    final heights = [
      for (var n = 0; n < 120; n++) [point(r, lo, hi)[0], point(r, lo, hi)[0]],
    ];
    final texels = [
      for (var n = 0; n < 80; n++)
        [
          big(
            n % 10 == 0
                ? (r.nextInt(2000) - 1000) * 1099511627776
                : r.nextInt(60) - 30,
          ),
          r.nextInt(60) - 30,
        ],
    ];
    final rays = <Case>[];
    for (var n = 0; n < 50; n++) {
      final from = [
        number(lo + r.nextDouble() * (hi - lo)),
        number(r.nextDouble() * 30 - 5),
        number(lo + r.nextDouble() * (hi - lo)),
      ];
      final shape = r.nextInt(8);
      final aim = switch (shape) {
        0 => [0.0, 0.0, 0.0],
        1 => [0.0, -1.0, 0.0],
        2 => [r.nextDouble() - 0.5, 0.0, r.nextDouble() - 0.5],
        3 => [r.nextDouble() - 0.5, r.nextDouble(), r.nextDouble() - 0.5],
        _ => [
          r.nextDouble() - 0.5,
          -r.nextDouble() * 0.6,
          r.nextDouble() - 0.5,
        ],
      };
      rays.add({
        'origin': from,
        'direction': [for (final a in aim) number(a)],
        'maxDistance': number(
          r.nextBool() ? double.infinity : 5 + r.nextDouble() * 120,
        ),
      });
    }
    cases.add({
      'op': 'query',
      'world': worldJson(terrain),
      'heights': heights,
      'texels': texels,
      'rays': rays,
      'expect': {
        'height': [
          for (final p in heights) number2(terrain.heightAt(d(p[0]), d(p[1]))),
        ],
        'normal': [
          for (final p in heights) vec(terrain.normalAt(d(p[0]), d(p[1]))),
        ],
        'cover': [
          for (final p in heights) terrain.coverAt(d(p[0]), d(p[1]))?.word,
        ],
        'texelHeight': [
          for (final t in texels)
            number2(terrain.texelHeight(texelInt(t[0]), t[1] as int)),
        ],
        'texelCover': [
          for (final t in texels)
            terrain.texelCover(texelInt(t[0]), t[1] as int)?.word,
        ],
        'texelColour': [
          for (final t in texels)
            terrain.texelColour(texelInt(t[0]), t[1] as int)?.rgba,
        ],
        'ray': [
          for (final ray in rays)
            vec(
              terrain.raycast(
                Vector3(
                  d((ray['origin']! as List<Object>)[0]),
                  d((ray['origin']! as List<Object>)[1]),
                  d((ray['origin']! as List<Object>)[2]),
                ),
                Vector3(
                  d((ray['direction']! as List<Object>)[0]),
                  d((ray['direction']! as List<Object>)[1]),
                  d((ray['direction']! as List<Object>)[2]),
                ),
                maxDistance: d(ray['maxDistance']!),
              ),
            ),
        ],
        'encoded': terrain.encode(),
      },
    });
  }
  return cases;
}

Object? number2(double? value) => value == null ? null : number(value);

int texelInt(Object value) => value is String ? int.parse(value) : value as int;

// --- Strokes ----------------------------------------------------------------

Case strokeCase(
  Terrain terrain, {
  required BrushTool tool,
  Brush brush = const Brush(size: 4),
  bool invert = false,
  int set = 0,
  GroundColour colour = GroundColour.none,
  double roughness = 0,
  double? height,
  int tileSize = 32,
  required List<(double, double)> moves,
}) {
  final world = worldJson(terrain);
  return {
    'op': 'stroke',
    'world': world,
    'tool': tool.name,
    'brush': {
      'size': number(brush.size),
      'strength': number(brush.strength),
      'falloff': number(brush.falloff),
      'jitter': number(brush.jitter),
      'spacing': number(brush.spacing),
    },
    'invert': invert,
    'set': set,
    'colour': colour.rgba,
    'roughness': number(roughness),
    'height': height == null ? null : number(height),
    'tileSize': tileSize,
    'moves': [
      for (final (x, z) in moves) [number(x), number(z)],
    ],
    'expect': attempt(() {
      final stroke = TerrainStroke(
        terrain,
        tool: tool,
        brush: brush,
        invert: invert,
        set: set,
        colour: colour,
        roughness: roughness,
        height: height,
        tileSize: tileSize,
      );
      final patches = <Case>[];
      for (final (x, z) in moves) {
        final patch = stroke.moveTo(x, z);
        final regions = patch.regions.toList()
          ..sort(
            (a, b) => a.z != b.z ? a.z.compareTo(b.z) : a.x.compareTo(b.x),
          );
        patches.add({
          'empty': patch.isEmpty,
          'tiles': patch.tileCount,
          'bytes': patch.byteCount,
          'regions': [for (final key in regions) keyJson(key)],
        });
      }
      final start = stroke.start;
      return {
        'dabs': stroke.dabs,
        'start': start == null ? null : [number(start.$1), number(start.$2)],
        'patches': patches,
        'regions': regionDumps(terrain),
      };
    }),
  };
}

List<Case> strokeCases() {
  final r = math.Random(19);
  final cases = <Case>[];
  for (final tool in BrushTool.values) {
    for (var variant = 0; variant < 6; variant++) {
      final size = variant.isEven ? 4 : 8;
      final spacing = const [1.0, 0.5, 2.0][variant % 3];
      final terrain = makeWorld(r, size: size, spacing: spacing, reach: 1);
      final across = size * spacing;
      final lo = -0.7 * across;
      final hi = 2.2 * across;
      final steps = 1 + r.nextInt(4);
      cases.add(
        strokeCase(
          terrain,
          tool: tool,
          brush: Brush(
            size: 1 + r.nextDouble() * across * 1.2,
            strength: 0.1 + r.nextDouble() * 0.9,
            falloff: r.nextDouble(),
            spacing: 0.1 + r.nextDouble() * 0.4,
          ),
          invert: variant == 1 || variant == 4,
          set: r.nextInt(4),
          colour: GroundColour.bytes(
            r.nextInt(256),
            r.nextInt(256),
            r.nextInt(256),
            r.nextInt(256),
          ),
          roughness: r.nextDouble() * 2 - 1,
          height: variant == 2 ? r.nextDouble() * 8 - 2 : null,
          tileSize: const [2, 4, 32][variant % 3],
          moves: [
            for (var n = 0; n < steps; n++)
              (
                lo + r.nextDouble() * (hi - lo),
                lo + r.nextDouble() * (hi - lo),
              ),
          ],
        ),
      );
    }
  }

  // The edges: a brush with nothing to do, one that is huge, and moves that
  // do not go anywhere.
  for (final brush in [
    const Brush(size: 0),
    const Brush(size: -3),
    const Brush(size: 4, strength: 0),
    const Brush(size: 4, strength: 5),
    const Brush(size: 4, spacing: 0),
    const Brush(size: 4, spacing: 0.001),
    const Brush(size: 4, spacing: 3),
    const Brush(size: 4, falloff: 0),
    const Brush(size: 4, falloff: 1),
    const Brush(size: 4, falloff: 9),
    const Brush(size: 300),
    const Brush(size: 4, spacing: double.nan),
    const Brush(size: double.infinity),
    const Brush(size: double.nan),
  ]) {
    for (final tool in [BrushTool.raise, BrushTool.smooth, BrushTool.cover]) {
      cases.add(
        strokeCase(
          makeWorld(r, size: 4, reach: 1),
          tool: tool,
          brush: brush,
          moves: [(2, 2), (6, 5), (6, 5), (-3, 9)],
        ),
      );
    }
  }
  for (final tool in [BrushTool.raise, BrushTool.slope, BrushTool.flatten]) {
    cases.add(
      strokeCase(
        makeWorld(r, size: 4, reach: 1),
        tool: tool,
        moves: [(double.nan, 2), (3, 3)],
      ),
    );
    cases.add(
      strokeCase(
        makeWorld(r, size: 4, reach: 1),
        tool: tool,
        moves: [(3, 3), (double.nan, 2)],
      ),
    );
    cases.add(
      strokeCase(
        makeWorld(r, size: 4, reach: 1),
        tool: tool,
        moves: [(3, 3), (double.infinity, 2)],
      ),
    );
    cases.add(
      strokeCase(
        makeWorld(r, size: 4, reach: 0),
        tool: tool,
        moves: [(-50, -50), (-40, -40), (1, 1), (3, 3)],
      ),
    );
  }
  return cases;
}

// --- Scatter ----------------------------------------------------------------

Case groupJson(ScatterGroup g) => {
  'key': keyJson(g.key),
  'layer': g.layer,
  'id': g.id,
  'count': g.count,
  'transforms': dataB64(g.transforms),
  'colours': dataB64(g.colours),
  'minimum': vec(g.minimum),
  'maximum': vec(g.maximum),
};

Case placerJson(ScatterPlacer placer, {required bool changed}) => {
  'changed': changed,
  'count': placer.count,
  'groups': [for (final g in placer.groups) groupJson(g)],
};

Case editJson(
  String op,
  int x,
  int z, {
  int i = 0,
  int j = 0,
  double value = 0,
}) => {'op': op, 'x': x, 'z': z, 'i': i, 'j': j, 'value': number(value)};

void applyEdit(Terrain terrain, Case edit) {
  final key = RegionKey(edit['x']! as int, edit['z']! as int);
  switch (edit['op']) {
    case 'height':
      terrain
          .regionAt(key)!
          .setHeight(edit['i']! as int, edit['j']! as int, d(edit['value']!));
    case 'remove':
      terrain.removeRegion(key);
    default:
      terrain.addRegion(key);
  }
}

Case scatterCase(Terrain terrain, List<Case> edits) {
  final world = worldJson(terrain);
  final placer = ScatterPlacer();
  final first = placer.update(terrain);
  final firstJson = placerJson(placer, changed: first);
  final again = placer.update(terrain);
  for (final edit in edits) {
    applyEdit(terrain, edit);
  }
  final after = placer.update(terrain);
  return {
    'op': 'scatter',
    'world': world,
    'edits': edits,
    'expect': {
      'first': firstJson,
      'again': again,
      'after': placerJson(placer, changed: after),
    },
  };
}

List<Case> scatterCases() {
  final r = math.Random(20);
  const grass = ScatterLayer(
    name: 'grass',
    seed: 5,
    density: 0.8,
    sets: [0],
    maxSlope: 35,
    size: (0.1, 0.5, 0.1),
    minScale: 0.6,
    maxScale: 1.4,
    lean: 0.4,
    colour: 0x4A8C2A,
    colourVariation: 0.3,
    groundTint: 0.5,
  );
  const rocks = ScatterLayer(
    name: 'rocks',
    seed: -9,
    density: 0.15,
    sets: [1, 2],
    minSlope: 5,
    minHeight: -2,
    maxHeight: 4,
    size: (1, 0.7, 1.2),
    minScale: 0.5,
    maxScale: 2,
    lift: -0.1,
    colour: 0x808080,
    colourVariation: 0.1,
  );
  const tree = ScatterLayer(
    name: 'tree',
    seed: 123456789012,
    density: 0.05,
    mesh: 'tree.glb',
    lean: 1,
    lift: 0.2,
    turn: false,
    range: 80,
    castShadows: true,
  );
  const dust = ScatterLayer(
    name: 'dust',
    density: 1.5,
    groundTint: 1,
    colourVariation: 1,
    colour: 0x112233,
    size: (0.05, 0.05, 0.05),
  );
  final cases = <Case>[];
  for (final (size, spacing, reach, layers) in [
    (8, 1.0, 1, [grass, rocks]),
    (4, 1.0, 1, [grass, rocks, tree]),
    (8, 0.5, 1, [dust, grass]),
    (4, 2.0, 1, [tree, rocks]),
    (8, 1.0, 0, [grass]),
    (4, 1.0, 1, <ScatterLayer>[]),
  ]) {
    final terrain = makeWorld(
      r,
      size: size,
      spacing: spacing,
      reach: reach,
      scatter: layers,
      autoCover: const AutoCover(
        steep: 1,
        flat: 0,
        slope: 0.7,
        heightFalloff: 0.05,
      ),
      dropped: 0.1,
    );
    final edits = <Case>[
      editJson('height', 0, 0, i: 1, j: 2, value: 3.5),
      editJson('height', 0, 0, i: size - 1, j: size - 1, value: -4),
      editJson('add', reach + 1, 0),
      if (reach > 0) editJson('remove', -reach, 0),
    ];
    cases.add(scatterCase(terrain, edits));
  }
  return cases;
}

void main(List<String> args) {
  writeFixture(
    root: args.single,
    package: 'orblit_terrain',
    name: 'terrain',
    cases: [
      ...coverCases(),
      ...colourCases(),
      ...policyCases(),
      ...keyCases(),
      ...regionCases(),
      ...terrainFileCases(),
      ...layerCases(),
      ...queryCases(),
      ...strokeCases(),
      ...scatterCases(),
    ],
  );
}

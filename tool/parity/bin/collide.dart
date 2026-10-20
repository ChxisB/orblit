// Usage: `dart run bin/collide.dart <repository root>`
import 'package:orblit_collide/orblit_collide.dart';
import 'package:orblit_parity_tool/fixture.dart';
import 'package:vector_math/vector_math_64.dart' hide Ray, Sphere;

const _nan = double.nan;
const _inf = double.infinity;

/// The generator the other exporters use, so a case list is the same on every
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

  /// A value with two decimals, so a fixture line reads as a number.
  double between(int low, int high) => low + next((high - low) * 100 + 1) / 100;
}

/// Every double becomes what JSON can hold, however deep it sits.
Object? _encode(Object? v) => switch (v) {
  double() => number(v),
  List() => [for (final item in v) _encode(item)],
  Map() => <String, Object?>{
    for (final MapEntry(:key, :value) in v.entries)
      key as String: _encode(value),
  },
  _ => v,
};

List<double> _list(Vector3 v) => [v.x, v.y, v.z];

List<double> _list2(Vector2 v) => [v.x, v.y];

Vector3 _vec(Object? spec) {
  final v = (spec! as List).cast<double>();
  return Vector3(v[0], v[1], v[2]);
}

Vector2 _vec2(Object? spec) {
  final v = (spec! as List).cast<double>();
  return Vector2(v[0], v[1]);
}

/// A coordinate on a half-metre grid half the time, so centres coincide and
/// faces meet, and a two-decimal value the rest.
double _coord(_Rng r, int span) => r.chance(2)
    ? (r.next(span * 4 + 1) - span * 2) / 2
    : r.between(-span, span);

double _size(_Rng r, List<double> grid, int top) =>
    r.chance(2) ? r.pick(grid) : r.between(0, top);

List<double> _point(_Rng r, int span) => [
  _coord(r, span),
  _coord(r, span),
  _coord(r, span),
];

List<double> _point2(_Rng r, int span) => [_coord(r, span), _coord(r, span)];

Map<String, Object?> _shapeSpec(_Rng r, int span) {
  final p = _point(r, span);
  return switch (r.next(3)) {
    0 => <String, Object?>{
      't': 'sphere',
      'p': p,
      'r': _size(r, [0.0, 0.25, 0.5, 1.0, 1.5, 2.0], 3),
    },
    1 => <String, Object?>{
      't': 'box',
      'p': p,
      'size': [
        for (var i = 0; i < 3; i++) _size(r, [0.0, 1.0, 2.0, 4.0], 6),
      ],
    },
    _ => <String, Object?>{
      't': 'capsule',
      'p': p,
      'height': _size(r, [0.0, 0.5, 1.0, 2.0, 4.0], 6),
      'radius': _size(r, [0.0, 0.25, 0.4, 0.5, 1.0], 2),
    },
  };
}

Shape _shape(Object? spec) {
  final s = spec! as Map<String, Object?>;
  final p = _vec(s['p']);
  return switch (s['t']) {
    'sphere' => Sphere(p, s['r']! as double),
    'box' => Box(p, _vec(s['size'])),
    'capsule' => Capsule(
      p,
      height: s['height']! as double,
      radius: s['radius']! as double,
    ),
    _ => throw ArgumentError('unknown shape $s'),
  };
}

Map<String, Object?> _hitboxSpec(_Rng r, int span) {
  final p = _point2(r, span);
  if (r.chance(2)) {
    return <String, Object?>{
      't': 'circle',
      'p': p,
      'r': _size(r, [0.0, 0.25, 0.5, 1.0, 1.5], 3),
    };
  }
  return <String, Object?>{
    't': 'rect',
    'p': p,
    'size': [for (var i = 0; i < 2; i++) _size(r, [0.0, 1.0, 2.0, 4.0], 6)],
  };
}

Hitbox _hitbox(Object? spec) {
  final s = spec! as Map<String, Object?>;
  final p = _vec2(s['p']);
  return switch (s['t']) {
    'circle' => Circle(p, s['r']! as double),
    'rect' => Rectangle(p, _vec2(s['size'])),
    _ => throw ArgumentError('unknown hitbox $s'),
  };
}

Object? _contactJson(Contact? c) => c == null
    ? null
    : <String, Object?>{
        'point': _list(c.point),
        'normal': _list(c.normal),
        'depth': c.depth,
      };

Object? _touchJson(Touch? t) => t == null
    ? null
    : <String, Object?>{
        'point': _list2(t.point),
        'normal': _list2(t.normal),
        'depth': t.depth,
      };

Object? _hitJson(Hit? h) => h == null
    ? null
    : <String, Object?>{
        'distance': h.distance,
        'point': _list(h.point),
        'normal': _list(h.normal),
        'inside': h.inside,
      };

List<List<double>> _box(Aabb a) => [_list(a.minimum), _list(a.maximum)];

List<List<double>> _rect(Rect2 a) => [_list2(a.minimum), _list2(a.maximum)];

/// Two shapes, a third of the time stacked or touching on purpose.
Map<String, Object?> _contactCase(_Rng r) {
  final a = _shapeSpec(r, 3);
  final b = _shapeSpec(r, 3);
  if (r.chance(6)) {
    b['p'] = a['p'];
  } else if (r.chance(6)) {
    final p = (a['p']! as List).cast<double>();
    b['p'] = [for (final c in p) c + r.pick([0.0, 0.0, 0.5, 1.0])];
  }
  return _contactOf(a, b);
}

Map<String, Object?> _contactOf(Map<String, Object?> a, Map<String, Object?> b) {
  final x = _shape(a);
  final y = _shape(b);
  return <String, Object?>{
    'op': 'contact',
    'a': a,
    'b': b,
    'forward': _contactJson(contact(x, y)),
    'backward': _contactJson(contact(y, x)),
    'overlaps': overlaps(x, y),
  };
}

/// Shapes with NaN or infinity in them, which nothing downstream should crash
/// on and which both sides must answer the same way.
List<Map<String, Object?>> _contactEdges() {
  Map<String, Object?> sphere(List<double> p, double radius) =>
      <String, Object?>{'t': 'sphere', 'p': p, 'r': radius};
  Map<String, Object?> box(List<double> p, List<double> size) =>
      <String, Object?>{'t': 'box', 'p': p, 'size': size};
  Map<String, Object?> capsule(List<double> p, double height, double radius) =>
      <String, Object?>{
        't': 'capsule',
        'p': p,
        'height': height,
        'radius': radius,
      };

  final shapes = [
    sphere([_nan, 0.0, 0.0], 1.0),
    sphere([0.0, 0.0, 0.0], _inf),
    sphere([0.0, 0.0, 0.0], _nan),
    box([0.0, 0.0, 0.0], [_inf, 2.0, 2.0]),
    box([_nan, 0.0, 0.0], [2.0, 2.0, 2.0]),
    capsule([0.0, 0.0, 0.0], _inf, 0.5),
    capsule([0.0, 0.0, 0.0], 2.0, _nan),
    capsule([0.0, _inf, 0.0], 2.0, 0.5),
    sphere([0.5, 0.0, 0.0], 1.0),
    box([0.5, 0.0, 0.0], [2.0, 2.0, 2.0]),
    capsule([0.5, 0.0, 0.0], 2.0, 0.5),
  ];
  return [
    for (final a in shapes)
      for (final b in shapes) _contactOf(a, b),
  ];
}

Map<String, Object?> _probeCase(_Rng r) {
  final spec = _shapeSpec(r, 3);
  final s = _shape(spec);
  final q = _point(r, 4);
  return <String, Object?>{
    'op': 'shape',
    's': spec,
    'q': q,
    'bounds': _box(s.bounds),
    'nearest': _list(s.nearestTo(_vec(q))),
    'contains': s.contains(_vec(q)),
    'moved': _list(s.movedTo(_vec(q)).position),
    'movedBounds': _box(s.movedTo(_vec(q)).bounds),
    if (s is Box) 'boxSize': _list(s.size),
    if (s is Capsule) 'segment': [_list(s.segment.$1), _list(s.segment.$2)],
  };
}

/// Boxes a few of which are inverted, to see both sides refuse a clamp the
/// same way.
Map<String, Object?> _aabbCase(_Rng r) {
  List<List<double>> corners() {
    final low = _point(r, 4);
    final extent = [for (var i = 0; i < 3; i++) _size(r, [0.0, 1.0, 2.0], 5)];
    final high = [for (var i = 0; i < 3; i++) low[i] + extent[i]];
    return r.chance(8) ? [high, low] : [low, high];
  }

  final a = corners();
  final b = corners();
  final q = _point(r, 5);
  final centre = _point(r, 3);
  final size = [for (var i = 0; i < 3; i++) _size(r, [0.0, 1.0, 2.0], 5)];
  final margin = r.pick([0.0, 0.5, 1.0, -0.5]);

  final boxA = Aabb(_vec(a[0]), _vec(a[1]));
  final boxB = Aabb(_vec(b[0]), _vec(b[1]));
  Object? nearest;
  try {
    nearest = _list(boxA.nearestTo(_vec(q)));
  } on ArgumentError {
    nearest = 'throws';
  }
  return <String, Object?>{
    'op': 'aabb',
    'a': a,
    'b': b,
    'q': q,
    'centre': centre,
    'size': size,
    'margin': margin,
    'centreOf': _list(boxA.centre),
    'sizeOf': _list(boxA.size),
    'overlaps': boxA.overlaps(boxB),
    'contains': boxA.contains(_vec(q)),
    'nearest': nearest,
    'union': _box(boxA.union(boxB)),
    'grown': _box(boxA.grownBy(margin)),
    'around': _box(Aabb.around(_vec(centre), _vec(size))),
  };
}

/// Segments that are degenerate, parallel or collinear on purpose.
Map<String, Object?> _segmentCase(_Rng r) {
  final a0 = _point(r, 3);
  final a1 = r.chance(8) ? a0 : _point(r, 3);
  List<double> second() {
    final b0 = _point(r, 3);
    if (r.chance(5)) {
      return b0;
    }
    final k = r.pick([-2.0, -0.5, 0.5, 1.0, 2.0]);
    return [for (var i = 0; i < 3; i++) b0[i] + (a1[i] - a0[i]) * k];
  }

  final b0 = _point(r, 3);
  final b1 = r.chance(8) ? b0 : second();
  final (pa, pb) = nearestBetweenSegments(
    _vec(a0),
    _vec(a1),
    _vec(b0),
    _vec(b1),
  );
  return <String, Object?>{
    'op': 'segments',
    'a0': a0,
    'a1': a1,
    'b0': b0,
    'b1': b1,
    'onSegment': _list(nearestOnSegment(_vec(a0), _vec(a1), _vec(b0))),
    'between': [_list(pa), _list(pb)],
  };
}

List<double> _direction(_Rng r) {
  final axis = r.pick([
    [1.0, 0.0, 0.0],
    [-1.0, 0.0, 0.0],
    [0.0, 1.0, 0.0],
    [0.0, -1.0, 0.0],
    [0.0, 0.0, 1.0],
    [0.0, 0.0, -1.0],
  ]);
  return switch (r.next(10)) {
    0 => [0.0, 0.0, 0.0],
    1 => [1e-13, 0.0, 0.0],
    2 => [1e-11, 0.0, 0.0],
    3 || 4 || 5 => axis,
    6 => [
      axis[0] + r.pick([0.0, 1e-13]),
      axis[1] + r.pick([0.0, 1e-13]),
      axis[2] + 1e-13,
    ],
    _ => [r.between(-3, 3), r.between(-3, 3), r.between(-3, 3)],
  };
}

Map<String, Object?> _raySpec(_Rng r, Map<String, Object?> target) {
  final centre = (target['p']! as List).cast<double>();
  final List<double> from;
  List<double> direction;
  if (r.next(10) < 6) {
    final offset = [for (var i = 0; i < 3; i++) r.pick([-6.0, -4.0, 0.0, 3.0, 5.0])];
    from = [for (var i = 0; i < 3; i++) centre[i] + offset[i]];
    direction = r.chance(2)
        ? [for (final o in offset) -o]
        : [for (final o in offset) -o + r.pick([0.0, 0.0, 0.5])];
  } else {
    from = _point(r, 6);
    direction = _direction(r);
  }
  if (r.chance(5)) {
    direction = _direction(r);
  }
  return <String, Object?>{
    'from': from,
    'dir': direction,
    'distance': r.chance(2) ? _inf : r.pick([0.0, 1.0, 5.0, 12.0, 30.0, -1.0]),
  };
}

Ray _ray(Object? spec) {
  final s = spec! as Map<String, Object?>;
  return Ray(
    _vec(s['from']),
    _vec(s['dir']),
    distance: s['distance']! as double,
  );
}

Map<String, Object?> _rayCase(_Rng r) {
  final target = _shapeSpec(r, 3);
  final spec = _raySpec(r, target);
  final ray = _ray(spec);
  final along = r.between(-2, 12);
  return <String, Object?>{
    'op': 'ray',
    's': target,
    'ray': spec,
    'hit': _hitJson(raycast(ray, _shape(target))),
    'direction': _list(ray.direction),
    'along': along,
    'at': _list(ray.at(along)),
  };
}

/// Several shapes on one ray, a few of them twins, so two hits share a
/// distance and the order of ties shows.
Map<String, Object?> _raysCase(_Rng r) {
  final specs = <Map<String, Object?>>[
    for (var i = 0; i < 2 + r.next(10); i++) _shapeSpec(r, 4),
  ];
  if (r.chance(2)) {
    specs.add(specs[r.next(specs.length)]);
  }
  final ray = _raySpec(r, specs.first);
  final shapes = [for (final s in specs) _shape(s)];
  final all = raycastAll(_ray(ray), shapes);
  final first = raycastFirst(_ray(ray), shapes);
  return <String, Object?>{
    'op': 'rays',
    'ray': ray,
    'shapes': specs,
    'all': [
      for (final found in all)
        <String, Object?>{'i': found.index, 'hit': _hitJson(found.hit)},
    ],
    'first': first == null
        ? null
        : <String, Object?>{'i': first.index, 'hit': _hitJson(first.hit)},
  };
}

Map<String, Object?> _broadphaseCase(_Rng r) {
  final cell = r.pick([0.5, 1.0, 2.0, 4.0, 8.0]);
  final items = <Map<String, Object?>>[];
  for (var i = 0; i < 2 + r.next(24); i++) {
    if (r.chance(6)) {
      final low = _point(r, 8);
      items.add(<String, Object?>{
        't': 'bounds',
        'min': low,
        'max': [for (final c in low) c + r.pick([0.0, 1.0, 3.0, 9.0])],
      });
    } else if (r.chance(20)) {
      final far = _shapeSpec(r, 3);
      final p = (far['p']! as List).cast<double>();
      far['p'] = [p[0] + r.pick([-1e7, 1e7, 3e5]), p[1], p[2]];
      items.add(far);
    } else {
      items.add(_shapeSpec(r, 8));
    }
  }
  final grid = Broadphase(cellSize: cell);
  final indexes = <int>[];
  for (final item in items) {
    if (item['t'] == 'bounds') {
      indexes.add(
        grid.addBounds(Aabb(_vec(item['min']), _vec(item['max']))),
      );
    } else {
      indexes.add(grid.add(_shape(item)));
    }
  }
  final queries = [
    for (var i = 0; i < 4; i++)
      () {
        final low = _point(r, 9);
        return [low, [for (final c in low) c + r.pick([0.0, 1.0, 4.0, 12.0])]];
      }(),
  ];
  final result = <String, Object?>{
    'op': 'broadphase',
    'cell': cell,
    'items': items,
    'queries': queries,
    'indexes': indexes,
    'length': grid.length,
    'pairs': [
      for (final (a, b) in grid.pairs()) [a, b],
    ],
    'near': [
      for (final q in queries) grid.near(Aabb(_vec(q[0]), _vec(q[1]))),
    ],
  };
  grid.clear();
  result['lengthAfterClear'] = grid.length;
  result['pairsAfterClear'] = grid.pairs().length;
  return result;
}

/// Bounds with NaN or infinity in them. A cell index needs a finite number, so
/// Dart refuses and the fixture says whether it did.
List<Map<String, Object?>> _broadphaseRefusals() {
  final out = <Map<String, Object?>>[];
  for (final bad in [_nan, _inf, -_inf]) {
    for (final (low, high) in [
      ([bad, 0.0, 0.0], [1.0, 1.0, 1.0]),
      ([0.0, 0.0, 0.0], [1.0, 1.0, bad]),
    ]) {
      var threw = false;
      try {
        Broadphase().addBounds(Aabb(_vec(low), _vec(high)));
      } on UnsupportedError {
        threw = true;
      }
      out.add(<String, Object?>{
        'op': 'broadphaseRefuses',
        'min': low,
        'max': high,
        'throws': threw,
      });
    }
  }
  return out;
}

const _masks = [0, 1, 2, 3, 4, 8, 0x100, 0x7FFFFFFF, 0x80000000, 0xFFFFFFFF];

Map<String, Object?> _layersCase(_Rng r) {
  final a = Layers(is_: r.pick(_masks), cares: r.pick(_masks));
  final b = Layers(is_: r.pick(_masks), cares: r.pick(_masks));
  return <String, Object?>{
    'op': 'layers',
    'a': [a.is_, a.cares],
    'b': [b.is_, b.cares],
    'interact': Layers.interact(a, b),
  };
}

Map<String, Object?> _touchingCase(_Rng r) {
  final a = _hitboxSpec(r, 3);
  final b = _hitboxSpec(r, 3);
  if (r.chance(6)) {
    b['p'] = a['p'];
  }
  final x = _hitbox(a);
  final y = _hitbox(b);
  final q = _point2(r, 4);
  return <String, Object?>{
    'op': 'touching',
    'a': a,
    'b': b,
    'q': q,
    'forward': _touchJson(touching(x, y)),
    'backward': _touchJson(touching(y, x)),
    'boundsA': _rect(x.bounds),
    'containsA': x.contains(_vec2(q)),
    'positionA': _list2(x.position),
  };
}

Map<String, Object?> _rect2Case(_Rng r) {
  List<List<double>> corners() {
    final low = _point2(r, 4);
    final high = [for (final c in low) c + _size(r, [0.0, 1.0, 2.0], 5)];
    return r.chance(8) ? [high, low] : [low, high];
  }

  final a = corners();
  final b = corners();
  final q = _point2(r, 5);
  final centre = _point2(r, 3);
  final size = [for (var i = 0; i < 2; i++) _size(r, [0.0, 1.0, 2.0], 5)];
  final boxA = Rect2(_vec2(a[0]), _vec2(a[1]));
  final boxB = Rect2(_vec2(b[0]), _vec2(b[1]));
  Object? nearest;
  try {
    nearest = _list2(boxA.nearestTo(_vec2(q)));
  } on ArgumentError {
    nearest = 'throws';
  }
  return <String, Object?>{
    'op': 'rect2',
    'a': a,
    'b': b,
    'q': q,
    'centre': centre,
    'size': size,
    'centreOf': _list2(boxA.centre),
    'sizeOf': _list2(boxA.size),
    'overlaps': boxA.overlaps(boxB),
    'contains': boxA.contains(_vec2(q)),
    'nearest': nearest,
    'around': _rect(Rect2.around(_vec2(centre), _vec2(size))),
  };
}

Object? _collisionJson(Collision c) => <String, Object?>{
  'a': c.a,
  'b': c.b,
  'touch': _touchJson(c.touch),
};

/// A world driven by a list of operations, with what each step said written
/// beside it so the other side can replay the list and compare.
final class _Script {
  _Script(double cell) : cell = cell, world = World2(cellSize: cell);

  final double cell;
  final World2 world;
  final ops = <Map<String, Object?>>[];
  final log = <Object?>[];
  final live = <int>{};

  List<int> get ids => live.toList()..sort();

  void add(Map<String, Object?> spec) {
    final id = spec['id']! as int;
    world.add(
      Body(
        id: id,
        hitbox: _hitbox(spec['hitbox']),
        passthrough: spec['passthrough']! as bool,
        is_: spec['is']! as int,
        cares: spec['cares']! as int,
      ),
    );
    live.add(id);
    ops.add(<String, Object?>{'op': 'add', 'body': spec});
    log.add(null);
  }

  void move(int id, Map<String, Object?> hitbox) {
    world[id]!.hitbox = _hitbox(hitbox);
    ops.add(<String, Object?>{'op': 'move', 'id': id, 'hitbox': hitbox});
    log.add(null);
  }

  void remove(int id) {
    world.remove(id);
    live.remove(id);
    ops.add(<String, Object?>{'op': 'remove', 'id': id});
    log.add(null);
  }

  void step({required bool separate}) {
    final result = world.step();
    if (separate) {
      world.separate(result.touching);
    }
    ops.add(<String, Object?>{'op': 'step', 'separate': separate});
    log.add(<String, Object?>{
      'touching': [for (final c in result.touching) _collisionJson(c)],
      'began': [for (final c in result.began) _collisionJson(c)],
      'ended': [
        for (final (a, b) in result.ended) [a, b],
      ],
      'length': world.length,
      'bodies': [
        for (final id in ids)
          <String, Object?>{
            'id': id,
            'p': _list2(world[id]!.hitbox.position),
          },
      ],
    });
  }

  void at(List<double> p) {
    ops.add(<String, Object?>{'op': 'at', 'p': p});
    log.add(world.at(_vec2(p)));
  }

  void inside(List<double> low, List<double> high) {
    ops.add(<String, Object?>{'op': 'inside', 'min': low, 'max': high});
    log.add(world.inside(Rect2(_vec2(low), _vec2(high))));
  }
}

List<T> _shuffled<T>(_Rng r, List<T> from) {
  final out = [...from];
  for (var i = out.length - 1; i > 0; i--) {
    final j = r.next(i + 1);
    final held = out[i];
    out[i] = out[j];
    out[j] = held;
  }
  return out;
}

Map<String, Object?> _bodySpec(_Rng r, int id) => <String, Object?>{
  'id': id,
  'hitbox': _hitboxSpec(r, 4),
  'passthrough': r.chance(5),
  'is': r.chance(4) ? r.pick(_masks) : 1,
  'cares': r.chance(4) ? r.pick(_masks) : 0xFFFFFFFF,
};

Map<String, Object?> _worldCase(_Rng r, List<int> pool) {
  final s = _Script(r.pick([1.0, 2.0, 8.0]));
  final ids = _shuffled(r, pool);
  final count = 3 + r.next(5);
  for (var i = 0; i < count; i++) {
    s.add(_bodySpec(r, ids[i]));
  }
  for (var round = 0; round < 8; round++) {
    for (var k = r.next(3); k > 0; k--) {
      s.move(r.pick(s.ids), _hitboxSpec(r, 4));
    }
    if (r.chance(6) && s.live.length > 2) {
      s.remove(r.pick(s.ids));
    }
    if (r.chance(6)) {
      s.add(_bodySpec(r, ids[count + round]));
    }
    s.step(separate: r.chance(3));
    if (r.chance(2)) {
      s.at(_point2(r, 4));
    }
    if (r.chance(2)) {
      final low = _point2(r, 5);
      s.inside(low, [for (final c in low) c + r.pick([0.0, 2.0, 6.0])]);
    }
  }
  return <String, Object?>{
    'op': 'world',
    'cell': s.cell,
    'ops': s.ops,
    'log': s.log,
  };
}

void main(List<String> args) {
  if (args.length != 1) {
    throw ArgumentError('Usage: dart run bin/collide.dart <repository root>');
  }
  final r = _Rng(31415);
  final plain = [for (var i = 0; i < 20; i++) i * 3];
  // Ids that the pairing key a * 1000003 + b gets wrong, which the port keeps.
  final odd = [
    -1000003,
    -7,
    -3,
    -1,
    0,
    1,
    2,
    7,
    1000002,
    1000003,
    1000004,
    2000006,
    2000007,
    3000009,
    -2000006,
    5,
    6,
    8,
    9,
    10,
  ];
  final cases = <Map<String, Object?>>[
    for (var i = 0; i < 500; i++) _contactCase(r),
    ..._contactEdges(),
    for (var i = 0; i < 250; i++) _probeCase(r),
    for (var i = 0; i < 120; i++) _aabbCase(r),
    for (var i = 0; i < 300; i++) _segmentCase(r),
    for (var i = 0; i < 700; i++) _rayCase(r),
    for (var i = 0; i < 120; i++) _raysCase(r),
    for (var i = 0; i < 120; i++) _broadphaseCase(r),
    ..._broadphaseRefusals(),
    for (var i = 0; i < 100; i++) _layersCase(r),
    for (var i = 0; i < 300; i++) _touchingCase(r),
    for (var i = 0; i < 100; i++) _rect2Case(r),
    for (var i = 0; i < 100; i++) _worldCase(r, plain),
    for (var i = 0; i < 25; i++) _worldCase(r, odd),
  ];
  writeFixture(
    root: args[0],
    package: 'orblit_collide',
    name: 'collide',
    cases: [for (final c in cases) _encode(c)! as Map<String, Object?>],
  );
}

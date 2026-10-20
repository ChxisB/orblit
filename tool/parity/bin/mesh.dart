import 'dart:convert';
import 'dart:math' as math;
import 'dart:typed_data';

import 'package:orblit_mesh/orblit_mesh.dart';
import 'package:orblit_parity_tool/fixture.dart';
import 'package:vector_math/vector_math_64.dart';

typedef Json = Map<String, Object?>;
typedef Gen = Json Function(math.Random r, Mesh m);

// reading a spec

double d(Object? v) => v is String ? double.parse(v) : (v! as num).toDouble();

double? maybeD(Object? v) => v == null ? null : d(v);

int? maybeInt(Object? v) => v as int?;

List<Object?> list(Object? v) => v! as List<Object?>;

Vector3 v3(Object? j) {
  final l = list(j);
  return Vector3(d(l[0]), d(l[1]), d(l[2]));
}

Vector2 v2(Object? j) {
  final l = list(j);
  return Vector2(d(l[0]), d(l[1]));
}

Object vec3(Vector3 v) => [number(v.x), number(v.y), number(v.z)];

Object vec2(Vector2 v) => [number(v.x), number(v.y)];

Object? boxOf(({Vector3 min, Vector3 max})? box) =>
    box == null ? null : {'min': vec3(box.min), 'max': vec3(box.max)};

Shape shapeOf(Json s) =>
    Shape.of(ShapeKind.values.byName(s['kind']! as String)).copyWith(
      width: maybeD(s['width']),
      height: maybeD(s['height']),
      depth: maybeD(s['depth']),
      sides: maybeInt(s['sides']),
      rings: maybeInt(s['rings']),
      columns: maybeInt(s['columns']),
      steps: maybeInt(s['steps']),
      subdivisions: maybeInt(s['subdivisions']),
      widthCuts: maybeInt(s['widthCuts']),
      heightCuts: maybeInt(s['heightCuts']),
      thickness: maybeD(s['thickness']),
      tubeRadius: maybeD(s['tubeRadius']),
      circumference: maybeD(s['circumference']),
      pedimentHeight: maybeD(s['pedimentHeight']),
      sideWidth: maybeD(s['sideWidth']),
      capped: s['capped'] as bool?,
      smooth: s['smooth'] as bool?,
      byCount: s['byCount'] as bool?,
      stepHeight: maybeD(s['stepHeight']),
    );

Json faceSpec(Face f) => {
  'v': f.vertices,
  'm': f.material,
  's': f.smooth,
  if (f.uv.toJson().isNotEmpty) 'uv': tree(f.uv.toJson()),
};

Json specOf(Mesh m) => {
  'p': [
    for (final at in m.positions) ...[number(at.x), number(at.y), number(at.z)],
  ],
  'f': [for (final f in m.faces) faceSpec(f)],
};

Mesh meshOf(Object? spec) {
  final s = spec! as Json;
  final p = list(s['p']).map(d).toList();
  return Mesh(
    positions: [
      for (var i = 0; i + 2 < p.length; i += 3)
        Vector3(p[i], p[i + 1], p[i + 2]),
    ],
    faces: [
      for (final raw in list(s['f']))
        Face(
          [for (final i in list((raw! as Json)['v'])) i! as int],
          material: (raw as Json)['m']! as int,
          smooth: raw['s']! as bool,
          uv: FaceUv.fromJson(raw['uv']),
        ),
    ],
  );
}

GlbMaterial materialOf(Object? spec) {
  final s = spec! as Json;
  return GlbMaterial(
    name: s['name']! as String,
    colour: list(s['colour']).map(d).toList(),
    metallic: d(s['metallic']),
    roughness: d(s['roughness']),
    emissive: list(s['emissive']).map(d).toList(),
    doubleSided: s['doubleSided']! as bool,
    cutout: s['cutout']! as bool,
  );
}

ReadLimits limitsOf(Object? spec) {
  if (spec == null) return const ReadLimits();
  final s = spec as Json;
  return ReadLimits(
    bytes: s['bytes'] as int? ?? 128 << 20,
    vertices: s['vertices'] as int? ?? 4000000,
    faces: s['faces'] as int? ?? 4000000,
    corners: s['corners'] as int? ?? 1024,
    indices: s['indices'] as int? ?? 16000000,
    names: s['names'] as int? ?? 100000,
    line: s['line'] as int? ?? 1 << 20,
  );
}

Map<String, Uint8List> filesOf(Object? spec) => {
  if (spec != null)
    for (final e in (spec as Json).entries)
      e.key: base64Decode(e.value! as String),
};

// cases

/// A case whose inputs went through JSON before [run] saw them, so Dart and C#
/// read the same values.
Json made(String op, Json fields, Object? Function(Json spec) run) {
  final spec = jsonDecode(jsonEncode(fields)) as Json;
  return {'op': op, ...spec, 'expect': attempt(() => run(spec))};
}

T pick<T>(math.Random r, List<T> pool) => pool[r.nextInt(pool.length)];

Object numOf(math.Random r, List<double> pool) => number(pick(r, pool));

// the shapes

Object? sizeOf(Mesh m) {
  final box = m.bounds;
  var area = 0.0;
  for (final f in m.faces) {
    area += m.areaOf(f);
  }
  return {
    'faces': m.faces.length,
    'positions': m.positions.length,
    'box': boxOf(box),
    'area': number(area),
  };
}

const dims = [0.0, 1.0, 2.0, 0.5, -1.0, 1e-12, 1e12, 3.0, 7.0, -0.0];
const wild = [double.nan, double.infinity, double.negativeInfinity];
const counts = [-5, 0, 1, 2, 3, 4, 7, 12];
const fractions = [0.0, 0.1, 0.25, 0.5, 1.0, 2.0, 99.0, -0.3];

const pools = <String, List<Object>>{
  'width': [0.0, 1.0, 3.0, 0.2, -2.0, 1e-9, 1e9, 4.0],
  'height': [0.0, 1.0, 2.0, 5.0, -3.0, 1e-9, 1e9],
  'depth': [0.0, 1.0, 7.0, -1.0, 1e-9, 1e9],
  'sides': counts,
  'rings': counts,
  'columns': counts,
  'steps': counts,
  'subdivisions': [-5, 0, 1, 2, 3, 4],
  'widthCuts': counts,
  'heightCuts': counts,
  'thickness': fractions,
  'tubeRadius': fractions,
  'circumference': [0.0, 90.0, 180.0, 360.0, 720.0, -90.0, 1e9],
  'pedimentHeight': fractions,
  'sideWidth': fractions,
  'stepHeight': [0.0, 0.25, 0.2, 1e-300, 1e300, -1.0, 0.07],
  'capped': [true, false],
  'smooth': [true, false],
  'byCount': [true, false],
};

Json randomShape(math.Random r) {
  final spec = <String, Object?>{'kind': pick(r, ShapeKind.values).name};
  for (final e in pools.entries) {
    if (r.nextInt(4) == 0) {
      final value = pick(r, e.value);
      spec[e.key] = value is double ? number(value) : value;
    }
  }
  return spec;
}

List<Json> shapeSpecs() {
  final r = math.Random(11);
  final specs = <Json>[];
  for (final kind in ShapeKind.values) {
    final k = kind.name;
    specs.add({'kind': k});
    specs.add({'kind': k, 'height': 2.0});
    specs.add({'kind': k, 'width': 3.0, 'height': 5.0, 'depth': 7.0});
    for (final n in counts) {
      specs.add({
        'kind': k,
        'sides': n,
        'rings': n,
        'columns': n,
        'steps': n,
        'subdivisions': n < 4 ? n : 3,
        'widthCuts': n,
        'heightCuts': n,
      });
    }
    for (final field in ['width', 'height', 'depth']) {
      for (final x in [...dims, ...wild]) {
        specs.add({'kind': k, field: number(x)});
      }
    }
  }
  for (var i = 0; i < 160; i++) {
    specs.add(randomShape(r));
  }
  return specs;
}

List<Json> shapeCases() {
  final cases = <Json>[];
  for (final spec in shapeSpecs()) {
    cases.add(made('shapeSize', spec, (s) => sizeOf(shapeOf(s).build())));
    final small = attempt(() => shapeOf(spec).build().faces.length <= 40);
    if (small['value'] != false) {
      cases.add(made('shape', spec, (s) => specOf(shapeOf(s).build())));
    }
  }
  const huge = [100, 4096];
  for (final kind in ShapeKind.values) {
    for (final n in huge) {
      cases.add(
        made('shapeSize', {
          'kind': kind.name,
          'sides': n,
          'rings': n,
          'columns': n,
          'steps': n,
          'subdivisions': n,
          'widthCuts': n,
          'heightCuts': n,
        }, (s) => sizeOf(shapeOf(s).build())),
      );
    }
  }
  return cases;
}

// base meshes

Mesh built(ShapeKind kind, [Shape Function(Shape s)? change]) {
  final shape = Shape.of(kind);
  return (change == null ? shape : change(shape)).build();
}

Mesh handMade(List<List<double>> points, List<List<int>> faces) => Mesh(
  positions: [for (final p in points) Vector3(p[0], p[1], p[2])],
  faces: [for (final f in faces) Face(f)],
);

List<(String, Mesh)> bases() => [
  ('cube', built(ShapeKind.cube)),
  (
    'plane',
    built(ShapeKind.plane, (s) => s.copyWith(widthCuts: 2, heightCuts: 2)),
  ),
  ('cylinder', built(ShapeKind.cylinder, (s) => s.copyWith(sides: 5))),
  ('cone', built(ShapeKind.cone, (s) => s.copyWith(sides: 4))),
  ('prism', built(ShapeKind.prism)),
  ('pipe', built(ShapeKind.pipe, (s) => s.copyWith(sides: 4))),
  ('door', built(ShapeKind.door)),
  ('stairs', built(ShapeKind.stairs, (s) => s.copyWith(steps: 3))),
  ('sphere', built(ShapeKind.sphere, (s) => s.copyWith(subdivisions: 1))),
  ('torus', built(ShapeKind.torus, (s) => s.copyWith(rings: 4, columns: 4))),
  (
    'ngons',
    handMade(
      [
        [0, 0, 0],
        [2, 0, 0],
        [3, 1, 0],
        [2, 2, 0],
        [0, 2, 0],
        [-1, 1, 0],
        [2, 0, 1],
        [0, 0, 1],
      ],
      [
        [0, 1, 2, 3, 4, 5],
        [0, 7, 6, 1],
      ],
    ),
  ),
  (
    'strip',
    handMade(
      [
        [0, 0, 0],
        [1, 0, 0],
        [2, 0, 0],
        [3, 0, 0],
        [0, 0, 1],
        [1, 0, 1],
        [2, 0, 1],
        [3, 0, 1],
      ],
      [
        [0, 4, 5, 1],
        [1, 5, 6, 2],
        [2, 6, 7, 3],
      ],
    ),
  ),
  (
    'triangle',
    handMade(
      [
        [0, 0, 0],
        [1, 0, 0],
        [0, 1, 0],
      ],
      [
        [0, 1, 2],
      ],
    ),
  ),
  (
    'pair',
    handMade(
      [
        [0, 0, 0],
        [1, 0, 0],
        [0, 1, 0],
        [1, 1, 0],
        [0, 0, 0],
      ],
      [
        [0, 1, 2],
        [1, 3, 2],
        [4, 3, 2],
      ],
    ),
  ),
  ('empty', Mesh()),
  (
    'odd',
    handMade(
      [
        [0, 0, 0],
        [1, 0, 0],
        [2, 0, 0],
        [0, 1, 0],
      ],
      [
        [0, 1, 2],
        [0, 0, 1],
        [0, 1, 3, 3],
        [0, 1],
      ],
    ),
  ),
];

// Dart's List.sort is stable up to this many items only. The C# port keeps
// a stable sort, so a mesh with more faces and mixed materials would be
// written in an order Dart does not promise (decision 75).
const stableSortLimit = 32;

/// Materials, smoothing and coordinates for some of the faces, so the cases
/// reach the code that carries them.
Mesh dressed(math.Random r, Mesh m) {
  final out = m.copy();
  final mixable = out.faces.length <= stableSortLimit;
  for (final f in out.faces) {
    if (mixable && r.nextInt(3) == 0) f.material = r.nextInt(3);
    if (r.nextInt(3) == 0) f.smooth = r.nextBool();
    if (r.nextInt(3) == 0) f.uv = randomUv(r, f.vertices.length);
  }
  return out;
}

FaceUv randomUv(math.Random r, int corners) => FaceUv(
  fit: pick(r, UvFit.values),
  offset: Vector2(pick(r, [0.0, 0.5, -1.0]), pick(r, [0.0, 0.25])),
  scale: Vector2(pick(r, [1.0, 2.0, 0.5, -1.0]), pick(r, [1.0, 3.0])),
  rotation: pick(r, [0.0, 90.0, 45.0, -30.0, 360.0]),
  flipU: r.nextInt(4) == 0,
  flipV: r.nextInt(4) == 0,
  swap: r.nextInt(4) == 0,
  manual: r.nextInt(3) == 0
      ? [
          for (var i = 0; i < corners; i++)
            Vector2(r.nextDouble(), r.nextDouble()),
        ]
      : null,
);

// steps

List<Face> which(Mesh m, Object? ids) => ids == 'all'
    ? [...m.faces]
    : [for (final i in list(ids)) m.faces[i! as int]];

List<int> verts(Object? ids) => [for (final i in list(ids)) i! as int];

MeshEdge edge(Object? e) {
  final l = list(e);
  return (l[0]! as int, l[1]! as int);
}

List<MeshEdge> edgeList(Object? es) => [for (final e in list(es)) edge(e)];

List<int> ids(Mesh m, Iterable<Face> faces) => [
  for (final f in faces) m.faces.indexOf(f),
];

Object edges(Iterable<MeshEdge> es) => [
  for (final e in es) [e.$1, e.$2],
];

Object? faceOrNull(Mesh m, Face? f) => f == null ? null : m.faces.indexOf(f);

Object? step(Mesh m, Json s) {
  switch (s['do']! as String) {
    case 'extrude':
      return ids(m, m.extrude(which(m, s['faces']), d(s['n'])));
    case 'inset':
      return ids(m, m.inset(which(m, s['faces']), d(s['n'])));
    case 'subdivide':
      return ids(m, m.subdivide(which(m, s['faces'])));
    case 'deleteFaces':
      m.deleteFaces(which(m, s['faces']));
      return null;
    case 'flipFaces':
      m.flipFaces(which(m, s['faces']));
      return null;
    case 'weld':
      return s['within'] == null ? m.weld() : m.weld(within: d(s['within']));
    case 'compact':
      return m.compact();
    case 'moveVertices':
      m.moveVertices(verts(s['verts']), v3(s['by']));
      return null;
    case 'verticesOf':
      return [...m.verticesOf(which(m, s['faces']))]..sort();
    case 'edgesOf':
      return edges(m.edgesOf(which(m, s['faces'])));
    case 'openFaces':
      return ids(m, m.openFaces);
    case 'movePoints':
      m.movePoints(verts(s['verts']), v3(s['by']));
      return null;
    case 'turnPoints':
      m.turnPoints(
        verts(s['verts']),
        Matrix3.fromList(list(s['matrix']).map(d).toList()),
        v3(s['about']),
      );
      return null;
    case 'scalePoints':
      m.scalePoints(verts(s['verts']), v3(s['by']), v3(s['about']));
      return null;
    case 'grown':
      return specOf(m.grown(d(s['n'])));
    case 'measure':
      return {
        'faces': [
          for (final f in m.faces)
            {
              'n': vec3(m.normalOf(f)),
              'c': vec3(m.centreOf(f)),
              'a': number(m.areaOf(f)),
            },
        ],
        'box': boxOf(m.bounds),
      };
    case 'flip':
      m.flip();
      return null;
    case 'merge':
      m.merge(meshOf(s['other']));
      return null;
    case 'transform':
      m.transform(Matrix4.fromList(list(s['matrix']).map(d).toList()));
      return null;
    case 'facesOn':
      return ids(m, m.facesOn(edge(s['edge'])));
    case 'allEdges':
      return edges(m.allEdges);
    case 'openEdges':
      return edges(m.openEdges);
    case 'facesAt':
      return ids(m, m.facesAt(s['v']! as int));
    case 'grow':
      return ids(
        m,
        m.grow(which(m, s['faces']), withinAngle: maybeD(s['angle'])),
      );
    case 'shrink':
      return ids(m, m.shrink(which(m, s['faces'])));
    case 'edgeLoop':
      return edges(m.edgeLoop(edge(s['edge'])));
    case 'edgeRing':
      return edges(m.edgeRing(edge(s['edge'])));
    case 'faceLoop':
      return ids(m, m.faceLoop(m.faces[s['face']! as int]));
    case 'collapse':
      return m.collapse(verts(s['verts']), toFirst: s['toFirst'] == true);
    case 'split':
      return m.split(verts(s['verts']));
    case 'fillHole':
      return ids(m, m.fillHole(verts(s['verts'])));
    case 'bevel':
      return ids(m, m.bevel(edgeList(s['edges']), d(s['n'])));
    case 'bridge':
      return faceOrNull(m, m.bridge(edge(s['a']), edge(s['b'])));
    case 'subdivideEdges':
      return s['into'] == null
          ? m.subdivideEdges(edgeList(s['edges']))
          : m.subdivideEdges(edgeList(s['edges']), into: s['into']! as int);
    case 'detach':
      return specOf(m.detach(which(m, s['faces'])));
    case 'duplicateFaces':
      return specOf(m.duplicateFaces(which(m, s['faces'])));
    case 'mergeFaces':
      return faceOrNull(m, m.mergeFaces(which(m, s['faces'])));
    case 'triangulateFaces':
      return ids(m, m.triangulateFaces(which(m, s['faces'])));
    case 'conformNormals':
      return s['faces'] == null
          ? m.conformNormals()
          : m.conformNormals(which(m, s['faces']));
    case 'centrePivotOn':
      return vec3(m.centrePivotOn(verts(s['verts'])));
    case 'mirrored':
      return specOf(
        m.mirrored(x: s['x'] == true, y: s['y'] == true, z: s['z'] == true),
      );
    case 'freezeUvs':
      m.freezeUvs(which(m, s['faces']));
      return null;
    case 'releaseUvs':
      m.releaseUvs(which(m, s['faces']));
      return null;
    case 'nudgeUvs':
      m.nudgeUvs(which(m, s['faces']), v2(s['by']));
      return null;
    case 'scaleUvs':
      m.scaleUvs(
        which(m, s['faces']),
        v2(s['by']),
        about: s['about'] == null ? null : v2(s['about']),
      );
      return null;
    case 'turnUvs':
      m.turnUvs(
        which(m, s['faces']),
        d(s['n']),
        about: s['about'] == null ? null : v2(s['about']),
      );
      return null;
    case 'projectPlanar':
      m.projectPlanar(which(m, s['faces']));
      return null;
    case 'projectBox':
      m.projectBox(which(m, s['faces']));
      return null;
    case 'fitUvs':
      m.fitUvs(which(m, s['faces']), keepShape: s['keepShape'] != false);
      return null;
    case 'flipUvs':
      m.flipUvs(which(m, s['faces']), u: s['u'] == true, v: s['v'] == true);
      return null;
    case 'uvsOf':
      return [for (final at in m.uvsOf(m.faces[s['face']! as int])) vec2(at)];
    case 'uvBoundsOf':
      final box = m.uvBoundsOf(which(m, s['faces']));
      return box == null ? null : {'min': vec2(box.min), 'max': vec2(box.max)};
    case 'whereOn':
      final where = s['reach'] == null
          ? m.whereOn(m.faces[s['face']! as int], v3(s['at']))
          : m.whereOn(
              m.faces[s['face']! as int],
              v3(s['at']),
              reach: d(s['reach']),
            );
      return switch (where) {
        AtCorner(:final corner) => ['corner', corner],
        AlongEdge(:final corner, :final along) => [
          'along',
          corner,
          number(along),
        ],
        InsideFace() => ['inside'],
      };
    case 'cutFace':
      final path = [for (final at in list(s['path'])) v3(at)];
      final face = m.faces[s['face']! as int];
      return ids(
        m,
        s['reach'] == null
            ? m.cutFace(face, path)
            : m.cutFace(face, path, reach: d(s['reach'])),
      );
    default:
      throw ArgumentError('no step ${s['do']}');
  }
}

Object? runSteps(Json spec) {
  final mesh = meshOf(spec['mesh']);
  final results = <Object?>[];
  for (final raw in list(spec['steps'])) {
    results.add(step(mesh, raw! as Json));
  }
  return {'results': results, 'mesh': specOf(mesh)};
}

// generating steps

/// Face numbers, mostly real ones and now and then one that is not there.
List<int> pickFaces(math.Random r, Mesh m) {
  final n = m.faces.length;
  if (n == 0) return r.nextInt(6) == 0 ? [0] : [];
  final chosen = <int>{
    for (var i = 0, c = 1 + r.nextInt(math.min(n, 4)); i < c; i++) r.nextInt(n),
  };
  if (r.nextInt(30) == 0) chosen.add(n + r.nextInt(2));
  return [...chosen];
}

List<int> pickVerts(math.Random r, Mesh m) {
  final n = m.positions.length;
  if (n == 0) return r.nextInt(6) == 0 ? [0] : [];
  final chosen = <int>{
    for (var i = 0, c = 1 + r.nextInt(math.min(n, 5)); i < c; i++) r.nextInt(n),
  };
  if (r.nextInt(30) == 0) chosen.add(-1 - r.nextInt(2));
  if (r.nextInt(30) == 0) chosen.add(n + r.nextInt(2));
  return [...chosen];
}

List<int> pickEdge(math.Random r, Mesh m) {
  final all = m.allEdges.toList();
  if (all.isEmpty || r.nextInt(25) == 0) {
    final n = math.max(m.positions.length, 2);
    return [r.nextInt(n), r.nextInt(n)];
  }
  final e = pick(r, all);
  return r.nextBool() ? [e.$1, e.$2] : [e.$2, e.$1];
}

List<List<int>> pickEdges(math.Random r, Mesh m) => [
  for (var i = 0, c = 1 + r.nextInt(3); i < c; i++) pickEdge(r, m),
];

const distances = [-1.0, 0.0, 0.1, 0.5, 1.0, 2.5, 1e-12, 1e6];
const smalls = [0.0, 0.05, 0.1, 0.3, 0.5, 0.9, 1.0, 1.7, -0.2];
const angles = [0.0, 10.0, 45.0, 90.0, 180.0, 360.0, -45.0];

Object by3(math.Random r) => [
  numOf(r, distances),
  numOf(r, distances),
  numOf(r, distances),
];

Object by2(math.Random r) => [numOf(r, smalls), numOf(r, smalls)];

Object rotationMatrix(math.Random r) {
  final turn = Matrix3.identity();
  final angle = pick(r, [0.0, 0.5, 1.5707963267948966, 3.0, -1.0]);
  switch (r.nextInt(3)) {
    case 0:
      turn.setRotationX(angle);
    case 1:
      turn.setRotationY(angle);
    default:
      turn.setRotationZ(angle);
  }
  return [for (final x in turn.storage) number(x)];
}

final editGens = <Gen>[
  (r, m) => {
    'do': 'extrude',
    'faces': pickFaces(r, m),
    'n': numOf(r, distances),
  },
  (r, m) => {'do': 'inset', 'faces': pickFaces(r, m), 'n': numOf(r, smalls)},
  (r, m) => {'do': 'subdivide', 'faces': pickFaces(r, m)},
  (r, m) => {'do': 'deleteFaces', 'faces': pickFaces(r, m)},
  (r, m) => {'do': 'flipFaces', 'faces': pickFaces(r, m)},
  (r, m) => {
    'do': 'weld',
    if (r.nextBool()) 'within': numOf(r, [0.0, 1e-4, 0.5, 1.5, 1e300, -1.0]),
  },
  (r, m) => {'do': 'compact'},
  (r, m) => {'do': 'moveVertices', 'verts': pickVerts(r, m), 'by': by3(r)},
  (r, m) => {'do': 'movePoints', 'verts': pickVerts(r, m), 'by': by3(r)},
  (r, m) => {
    'do': 'turnPoints',
    'verts': pickVerts(r, m),
    'matrix': rotationMatrix(r),
    'about': by3(r),
  },
  (r, m) => {
    'do': 'scalePoints',
    'verts': pickVerts(r, m),
    'by': by3(r),
    'about': by3(r),
  },
  (r, m) => {
    'do': 'grown',
    'n': numOf(r, [0.0, 0.1, -0.1, 1.0, 1e300]),
  },
  (r, m) => {'do': 'verticesOf', 'faces': pickFaces(r, m)},
  (r, m) => {'do': 'edgesOf', 'faces': pickFaces(r, m)},
  (r, m) => {'do': 'openFaces'},
  (r, m) => {'do': 'measure'},
  (r, m) => {'do': 'flip'},
  (r, m) => {
    'do': 'transform',
    'matrix': [
      for (var i = 0; i < 16; i++) numOf(r, [0.0, 1.0, 2.0, -1.0, 0.5]),
    ],
  },
];

final actionGens = <Gen>[
  (r, m) => {'do': 'facesOn', 'edge': pickEdge(r, m)},
  (r, m) => {'do': 'allEdges'},
  (r, m) => {'do': 'openEdges'},
  (r, m) => {'do': 'facesAt', 'v': r.nextInt(math.max(m.positions.length, 1))},
  (r, m) => {
    'do': 'grow',
    'faces': pickFaces(r, m),
    if (r.nextBool()) 'angle': numOf(r, angles),
  },
  (r, m) => {'do': 'shrink', 'faces': pickFaces(r, m)},
  (r, m) => {'do': 'edgeLoop', 'edge': pickEdge(r, m)},
  (r, m) => {'do': 'edgeRing', 'edge': pickEdge(r, m)},
  (r, m) => {'do': 'faceLoop', 'face': r.nextInt(math.max(m.faces.length, 1))},
  (r, m) => {
    'do': 'collapse',
    'verts': pickVerts(r, m),
    'toFirst': r.nextBool(),
  },
  (r, m) => {'do': 'split', 'verts': pickVerts(r, m)},
  (r, m) => {'do': 'fillHole', 'verts': pickVerts(r, m)},
  (r, m) => {'do': 'bevel', 'edges': pickEdges(r, m), 'n': numOf(r, smalls)},
  (r, m) => {'do': 'bridge', 'a': pickEdge(r, m), 'b': pickEdge(r, m)},
  (r, m) => {
    'do': 'subdivideEdges',
    'edges': pickEdges(r, m),
    if (r.nextBool()) 'into': pick(r, [-1, 0, 1, 2, 3]),
  },
  (r, m) => {'do': 'detach', 'faces': pickFaces(r, m)},
  (r, m) => {'do': 'duplicateFaces', 'faces': pickFaces(r, m)},
  (r, m) => {'do': 'mergeFaces', 'faces': pickFaces(r, m)},
  (r, m) => {'do': 'triangulateFaces', 'faces': pickFaces(r, m)},
  (r, m) => {
    'do': 'conformNormals',
    if (r.nextBool()) 'faces': pickFaces(r, m),
  },
  (r, m) => {'do': 'centrePivotOn', 'verts': pickVerts(r, m)},
  (r, m) => {
    'do': 'mirrored',
    'x': r.nextBool(),
    'y': r.nextBool(),
    'z': r.nextBool(),
  },
  (r, m) => {'do': 'merge', 'other': specOf(pick(r, bases()).$2)},
];

final uvGens = <Gen>[
  (r, m) => {'do': 'freezeUvs', 'faces': pickFaces(r, m)},
  (r, m) => {'do': 'releaseUvs', 'faces': pickFaces(r, m)},
  (r, m) => {'do': 'nudgeUvs', 'faces': pickFaces(r, m), 'by': by2(r)},
  (r, m) => {
    'do': 'scaleUvs',
    'faces': pickFaces(r, m),
    'by': by2(r),
    if (r.nextBool()) 'about': by2(r),
  },
  (r, m) => {
    'do': 'turnUvs',
    'faces': pickFaces(r, m),
    'n': numOf(r, angles),
    if (r.nextBool()) 'about': by2(r),
  },
  (r, m) => {'do': 'projectPlanar', 'faces': pickFaces(r, m)},
  (r, m) => {'do': 'projectBox', 'faces': pickFaces(r, m)},
  (r, m) => {
    'do': 'fitUvs',
    'faces': pickFaces(r, m),
    'keepShape': r.nextBool(),
  },
  (r, m) => {
    'do': 'flipUvs',
    'faces': pickFaces(r, m),
    'u': r.nextBool(),
    'v': r.nextBool(),
  },
  (r, m) => {'do': 'uvsOf', 'face': r.nextInt(math.max(m.faces.length, 1))},
  (r, m) => {'do': 'uvBoundsOf', 'faces': pickFaces(r, m)},
];

/// A point on, beside or away from a face, the places a cut path goes.
Object pathPoint(math.Random r, Mesh m, Face f) {
  final points = m.pointsOf(f);
  if (points.isEmpty) return vec3(Vector3.zero());
  final a = pick(r, points);
  final b = pick(r, points);
  final c = pick(r, points);
  final t = r.nextDouble();
  final u = r.nextDouble() * (1 - t);
  final inside = a * t + b * u + c * (1 - t - u);
  switch (r.nextInt(7)) {
    case 0:
      return vec3(a);
    case 1:
      return vec3(a + (b - a) * pick(r, [0.0, 0.25, 0.5, 0.999, 1.0]));
    case 2:
      return vec3(inside + m.normalOf(f) * pick(r, [0.0, 0.01, 0.5]));
    case 3:
      return vec3(a + (b - a) * 0.5 + Vector3(0.001, 0.0, -0.001));
    case 4:
      return vec3(inside + Vector3(5, 5, 5));
    default:
      return vec3(inside);
  }
}

final cutGens = <Gen>[
  (r, m) {
    final face = r.nextInt(m.faces.length);
    return {
      'do': 'whereOn',
      'face': face,
      'at': pathPoint(r, m, m.faces[face]),
      if (r.nextBool()) 'reach': numOf(r, [0.0, 0.02, 0.1, 1.0, 1e-9]),
    };
  },
  (r, m) {
    final face = r.nextInt(m.faces.length);
    return {
      'do': 'cutFace',
      'face': face,
      'path': [
        for (var i = 0, c = 2 + r.nextInt(3); i < c; i++)
          pathPoint(r, m, m.faces[face]),
      ],
      if (r.nextBool()) 'reach': numOf(r, [0.0, 0.02, 0.1, 1.0, 1e-9]),
    };
  },
];

List<Json> sequence(math.Random r, Mesh base, List<Gen> gens, int length) {
  final work = meshOf(specOf(base));
  final steps = <Json>[];
  for (var i = 0; i < length; i++) {
    if (work.faces.isEmpty && gens == cutGens) break;
    final s = jsonDecode(jsonEncode(pick(r, gens)(r, work))) as Json;
    steps.add(s);
    try {
      step(work, s);
    } on Object {
      break;
    }
  }
  return steps;
}

List<Json> stepCases(String op, int seed, List<Gen> gens, int perMesh) {
  final r = math.Random(seed);
  final cases = <Json>[];
  for (final (_, base) in bases()) {
    for (var i = 0; i < perMesh; i++) {
      final start = r.nextBool() ? dressed(r, base) : base;
      final steps = sequence(r, start, gens, 1 + r.nextInt(4));
      if (steps.isEmpty) continue;
      cases.add(made(op, {'mesh': specOf(start), 'steps': steps}, runSteps));
    }
  }
  return cases;
}

// triangles

Object floats(List<double> values) => [for (final x in values) number(x)];

Object? trianglesOf(Json spec) {
  final t = meshOf(spec['mesh']).triangulate();
  return {
    'positions': floats(t.positions),
    'normals': floats(t.normals),
    'uvs': floats(t.uvs),
    'indices': t.indices,
    'groups': [
      for (final g in t.groups) [g.material, g.start, g.count],
    ],
    'tangents': floats(tangentsOf(t)),
  };
}

List<Json> triangleCases() {
  final r = math.Random(21);
  final cases = <Json>[];
  for (final (_, base) in bases()) {
    cases.add(made('triangulate', {'mesh': specOf(base)}, trianglesOf));
    for (var i = 0; i < 3; i++) {
      cases.add(
        made('triangulate', {'mesh': specOf(dressed(r, base))}, trianglesOf),
      );
    }
  }
  final negative = built(ShapeKind.cube)..faces.first.material = -1;
  cases.add(made('triangulate', {'mesh': specOf(negative)}, trianglesOf));
  final mixed = built(ShapeKind.pipe);
  for (final f in mixed.faces) {
    f.material = r.nextInt(3);
    f.smooth = r.nextBool();
  }
  cases.add(made('triangulate', {'mesh': specOf(mixed)}, trianglesOf));
  return cases;
}

List<List<Vector3>> polygons(math.Random r) {
  final out = <List<Vector3>>[];
  for (var i = 0; i < 100; i++) {
    final n = 3 + r.nextInt(8);
    final points = <Vector3>[];
    final kind = r.nextInt(5);
    for (var k = 0; k < n; k++) {
      final angle = k / n * 2 * math.pi;
      final radius = switch (kind) {
        0 => 1.0,
        1 => k.isEven ? 1.0 : 0.4,
        2 => 0.5 + r.nextDouble(),
        3 => 1.0,
        _ => r.nextDouble() * 2,
      };
      final at = Vector3(
        math.cos(angle) * radius,
        kind == 3 ? 0.0 : math.sin(angle) * radius,
        kind == 3 ? math.sin(angle) * radius : 0.0,
      );
      if (kind == 4 && r.nextInt(3) == 0 && points.isNotEmpty) {
        points.add(points.last.clone());
      } else {
        points.add(at);
      }
    }
    out.add(points);
  }
  out.add([Vector3.zero(), Vector3.zero(), Vector3.zero()]);
  out.add([Vector3(0, 0, 0), Vector3(1, 0, 0), Vector3(2, 0, 0)]);
  out.add([
    Vector3(0, 0, 0),
    Vector3(1, 1, 0),
    Vector3(1, 0, 0),
    Vector3(0, 1, 0),
  ]);
  return out;
}

List<Json> polygonCases() {
  final r = math.Random(31);
  final cases = <Json>[];
  for (final points in polygons(r)) {
    final normals = [
      Vector3(0, 0, 1),
      Vector3(0, 1, 0),
      Vector3(0, 0, -1),
      Vector3.zero(),
    ];
    final normal = pick(r, normals);
    cases.add(
      made(
        'fn',
        {
          'f': 'cutUp',
          'points': [for (final p in points) vec3(p)],
          'normal': vec3(normal),
        },
        (s) =>
            cutUp([for (final p in list(s['points'])) v3(p)], v3(s['normal'])),
      ),
    );
    cases.add(
      made(
        'fn',
        {
          'f': 'signedLoopArea',
          'points': [for (final p in points) vec3(p)],
          'normal': vec3(normal),
        },
        (s) => number(
          signedLoopArea([
            for (final p in list(s['points'])) v3(p),
          ], v3(s['normal'])),
        ),
      ),
    );
    cases.add(
      made(
        'fn',
        {
          'f': 'signedAreaOf',
          'points': [for (final p in points) vec3(p)],
          'normal': vec3(normal),
        },
        (s) => number(
          signedAreaOf([
            for (final p in list(s['points'])) v3(p),
          ], v3(s['normal'])),
        ),
      ),
    );
    cases.add(
      made(
        'fn',
        {
          'f': 'outlineCrosses',
          'points': [for (final p in points) vec3(p)],
          'normal': vec3(normal),
        },
        (s) => outlineCrosses([
          for (final p in list(s['points'])) v3(p),
        ], v3(s['normal'])),
      ),
    );
    cases.add(
      made(
        'fn',
        {
          'f': 'flattenFace',
          'points': [for (final p in points) vec3(p)],
          'normal': vec3(normal),
        },
        (s) {
          final flat = flattenFace([
            for (final p in list(s['points'])) v3(p),
          ], v3(s['normal']));
          return {
            'flat': [for (final at in flat.flat) vec2(at)],
            'u': vec3(flat.u),
            'v': vec3(flat.v),
          };
        },
      ),
    );
    final at =
        pick(r, points) +
        Vector3(r.nextDouble() - 0.5, r.nextDouble() - 0.5, 0);
    cases.add(
      made(
        'fn',
        {
          'f': 'closesOutline',
          'points': [for (final p in points) vec3(p)],
          'at': vec3(at),
          if (r.nextBool()) 'reach': numOf(r, [0.0, 0.15, 1.0, 1e-9]),
        },
        (s) {
          final pts = [for (final p in list(s['points'])) v3(p)];
          return s['reach'] == null
              ? closesOutline(pts, v3(s['at']))
              : closesOutline(pts, v3(s['at']), reach: d(s['reach']));
        },
      ),
    );
    cases.add(
      made(
        'fn',
        {
          'f': 'isNewPoint',
          'points': [for (final p in points) vec3(p)],
          'at': vec3(r.nextBool() ? points.last : at),
        },
        (s) =>
            isNewPoint([for (final p in list(s['points'])) v3(p)], v3(s['at'])),
      ),
    );
    final outline = [for (final p in points) Vector2(p.x, p.y)];
    cases.add(
      made(
        'fn',
        {
          'f': 'pointInsideOutline',
          'outline': [for (final p in outline) vec2(p)],
          'at': vec2(Vector2(r.nextDouble() * 2 - 1, r.nextDouble() * 2 - 1)),
        },
        (s) => pointInsideOutline([
          for (final p in list(s['outline'])) v2(p),
        ], v2(s['at'])),
      ),
    );
    cases.add(
      made('fn', {
        'f': 'distanceToSegment',
        'at': vec2(Vector2(r.nextDouble() * 4 - 2, r.nextDouble() * 4 - 2)),
        'a': vec2(outline.first),
        'b': vec2(outline.last),
      }, (s) => number(distanceToSegment(v2(s['at']), v2(s['a']), v2(s['b'])))),
    );
    cases.add(
      made('fn', {
        'f': 'turnBetween',
        'a': vec2(outline.first),
        'b': vec2(outline.last),
      }, (s) => number(turnBetween(v2(s['a']), v2(s['b'])))),
    );
    cases.add(
      made('fn', {
        'f': 'angleBetween',
        'a': vec3(points.first),
        'b': vec3(points.last),
      }, (s) => number(angleBetween(v3(s['a']), v3(s['b'])))),
    );
    cases.add(
      made(
        'fn',
        {
          'f': 'onPlane',
          'origin': vec3(points.first),
          'direction': vec3(points.last - points.first),
          'planePoint': vec3(Vector3.zero()),
          'planeNormal': vec3(normal),
        },
        (s) {
          final hit = PolyShape.onPlane(
            v3(s['origin']),
            v3(s['direction']),
            v3(s['planePoint']),
            v3(s['planeNormal']),
          );
          return hit == null ? null : vec3(hit);
        },
      ),
    );
    cases.add(
      made(
        'poly',
        {
          'points': [for (final p in points) vec3(p)],
          'height': numOf(r, [2.0, 0.0, -1.5, 1e-12, 4.0]),
          'flipped': r.nextBool(),
        },
        (s) {
          final shape = PolyShape(
            points: [for (final p in list(s['points'])) v3(p)],
            height: d(s['height']),
            flipped: s['flipped']! as bool,
          );
          return {
            'drawable': shape.isDrawable,
            'centre': vec3(shape.plane.centre),
            'normal': vec3(shape.plane.normal),
            'mesh': specOf(shape.build()),
          };
        },
      ),
    );
  }
  for (final e in [
    [0, 1],
    [3, 3],
    [-2, 5],
    [5, -2],
  ]) {
    cases.add(
      made('fn', {'f': 'edgeOf', 'a': e[0], 'b': e[1]}, (s) {
        final pair = edgeOf(s['a']! as int, s['b']! as int);
        return [pair.$1, pair.$2];
      }),
    );
  }
  return cases;
}

// face coordinates

Json uvSpec(FaceUv uv) => tree(uv.toJson())! as Json;

List<Json> faceUvCases() {
  final r = math.Random(41);
  final cases = <Json>[];
  for (var i = 0; i < 150; i++) {
    final corners = 3 + r.nextInt(3);
    final uv = randomUv(r, r.nextInt(5) == 0 ? corners + 1 : corners);
    final points = [
      for (var k = 0; k < corners; k++)
        Vector3(
          r.nextDouble() * 4 - 2,
          r.nextDouble() * 4 - 2,
          r.nextDouble() * 4 - 2,
        ),
    ];
    final normal = pick(r, [
      Vector3(0, 1, 0),
      Vector3(0, 0, 1),
      Vector3(1, 0, 0),
      Vector3(1, 1, 1).normalized(),
      Vector3(0, -1, 0),
      Vector3.zero(),
      Vector3(1e-9, 1, 0),
    ]);
    cases.add(
      made(
        'faceUv',
        {
          'uv': uvSpec(uv),
          'points': [for (final p in points) vec3(p)],
          'normal': vec3(normal),
        },
        (s) {
          final made = FaceUv.fromJson(s['uv'])!;
          final axes = FaceUv.axesFor(v3(s['normal']));
          return {
            'coordinates': [
              for (final at in made.forFace([
                for (final p in list(s['points'])) v3(p),
              ], v3(s['normal'])))
                vec2(at),
            ],
            'again': tree(made.toJson()),
            'u': vec3(axes.u),
            'v': vec3(axes.v),
          };
        },
      ),
    );
  }
  return cases;
}

const oddValues = <Object?>[
  null,
  'nonsense',
  3,
  [],
  {},
  {'fit': 'stretch'},
  {'fit': 'nonsense'},
  {'fit': 4},
  {'rotation': 'x'},
  {'rotation': 90},
  {
    'offset': [1, 2],
    'scale': [3, 4],
  },
  {
    'offset': [1],
  },
  {'offset': 'x'},
  {
    'scale': [1, 'x'],
  },
  {
    'manual': [
      [0, 0],
      [1, 0],
      [1, 1],
    ],
  },
  {
    'manual': [
      [0, 0],
      [1],
    ],
  },
  {'manual': 'x'},
  {'manual': <Object?>[]},
  {'flipU': true, 'flipV': 1, 'swap': true},
];

const oddMeshes = <Object?>[
  null,
  'nonsense',
  {},
  {'positions': <Object?>[]},
  {'positions': 'x'},
  {
    'positions': [0, 0, 0, 1, 0, 0, 0, 1, 0],
    'faces': <Object?>[],
  },
  {
    'positions': [0, 0, 0, 1, 0, 0, 0, 1, 0],
    'faces': [
      {
        'v': [0, 1, 2],
      },
      {
        'v': [0, 1, 99],
      },
      {
        'v': [0, 1, -1],
      },
      {
        'v': [0, 1],
      },
      {'v': 'x'},
      <String, Object?>{},
      3,
      {
        'v': [0, 1, 2],
        'material': 2,
        'smooth': true,
      },
      {
        'v': [0, 1, 2],
        'material': 'x',
      },
      {
        'v': [0, 1, 2.5],
      },
    ],
  },
  {
    'positions': [0, 0, 0, 1, 0],
    'faces': <Object?>[],
  },
  {
    'positions': [0, 'x', 0, 1, 0, 0],
    'faces': <Object?>[],
  },
  {
    'positions': [0, 0, 0, 1, 0, 0, 0, 1, 0],
    'faces': [
      {
        'v': [0, 1, 2],
        'uv': {'fit': 'fit', 'rotation': 45},
      },
    ],
  },
];

const oddShapes = <Object?>[
  null,
  'x',
  {},
  {'kind': 'nonsense'},
  {'kind': 'cube', 'width': 'x'},
  {'kind': 'cube', 'width': 3},
  {'kind': 'stairs', 'steps': 12, 'width': 4, 'byCount': false},
  {'kind': 'sphere', 'subdivisions': 2.7},
  {'kind': 'sphere', 'subdivisions': '2'},
  {'kind': 'torus', 'capped': 'yes', 'smooth': false},
  {'kind': 'pipe', 'sides': 1e30},
];

const oddPolys = <Object?>[
  null,
  'x',
  {},
  {'points': 'x'},
  {
    'points': [0, 0, 0, 1, 0, 0, 0, 1, 0],
  },
  {
    'points': [0, 0, 0, 1, 0, 0, 0, 1, 0],
    'height': 3,
    'flipped': true,
  },
  {
    'points': [0, 0, 0, 1, 'x', 0, 0, 1, 0, 5, 5, 5],
  },
  {
    'points': [0, 0, 0, 1, 0],
    'height': 'x',
  },
];

List<Json> jsonCases() => [
  for (final v in oddValues)
    made(
      'readJson',
      {'what': 'uv', 'value': v},
      (s) => FaceUv.fromJson(s['value'])?.toJson() == null
          ? null
          : tree(FaceUv.fromJson(s['value'])!.toJson()),
    ),
  for (final v in oddMeshes)
    made('readJson', {'what': 'mesh', 'value': v}, (s) {
      final m = Mesh.fromJson(s['value']);
      return m == null ? null : specOf(m);
    }),
  for (final v in oddShapes)
    made('readJson', {'what': 'shape', 'value': v}, (s) {
      final shape = Shape.fromJson(s['value']);
      return shape == null ? null : tree(shape.toJson());
    }),
  for (final v in oddPolys)
    made('readJson', {'what': 'poly', 'value': v}, (s) {
      final poly = PolyShape.fromJson(s['value']);
      return poly == null ? null : tree(poly.toJson());
    }),
];

// writing

Json materialSpec(math.Random r, int i) => {
  'name': pick(r, ['wall', 'two words', 'caf\u00e9', 'm$i', '', 'a/b']),
  'colour': [
    numOf(r, [0.8, 0.0, 1.0, 0.123456789]),
    numOf(r, [0.8, 0.5]),
    numOf(r, [0.8, 1e-9]),
    numOf(r, [1.0, 0.5]),
  ],
  'metallic': numOf(r, [0.0, 1.0, 0.3]),
  'roughness': numOf(r, [0.5, 1.0, 0.05]),
  'emissive': [
    numOf(r, [0.0, 0.2]),
    numOf(r, [0.0, 1.0]),
    numOf(r, [0.0, 0.5]),
  ],
  'doubleSided': r.nextBool(),
  'cutout': r.nextBool(),
};

Object? writtenOf(Json s) {
  final m = meshOf(s['mesh']);
  final materials = [for (final x in list(s['materials'])) materialOf(x)];
  final written = m.writeAs(
    MeshFormat.values.byName(s['format']! as String),
    name: s['name']! as String,
    materials: materials,
    binary: s['binary']! as bool,
  );
  return [
    for (final w in written) {'name': w.name, 'bytes': b64(w.bytes)},
  ];
}

const awkward = <double>[
  -0.0,
  1e9,
  1e21,
  1e30,
  1.5e-7,
  0.1,
  123456789.123456789,
  5e-324,
  1.7976931348623157e308,
  double.nan,
  double.infinity,
  double.negativeInfinity,
  100.0,
  1e20,
  -1e21,
  0.000001,
];

Mesh awkwardMesh(math.Random r) {
  final m = Mesh();
  for (var i = 0; i < 4; i++) {
    m.addVertex(Vector3(pick(r, awkward), pick(r, awkward), pick(r, awkward)));
  }
  m.addFace([0, 1, 2]);
  m.addFace([1, 2, 3]);
  return m;
}

List<Json> writeCases() {
  final r = math.Random(51);
  final cases = <Json>[];
  final pool = <Mesh>[
    for (final (i, (_, base)) in bases().indexed) ...[
      base,
      if (i.isEven) dressed(r, base),
    ],
    for (var i = 0; i < 6; i++) awkwardMesh(r),
  ];
  for (final m in pool) {
    for (final format in MeshFormat.values) {
      for (final binary in [true, false]) {
        if (!binary && format != MeshFormat.stl && format != MeshFormat.ply) {
          continue;
        }
        final count = r.nextInt(3);
        cases.add(
          made('export', {
            'mesh': specOf(m),
            'format': format.name,
            'binary': binary,
            'name': pick(r, ['mesh', 'two words', 'caf\u00e9', 'a.b', '']),
            'materials': [for (var i = 0; i < count; i++) materialSpec(r, i)],
          }, writtenOf),
        );
      }
    }
  }
  return cases;
}

// reading

Uint8List utf(String text) => Uint8List.fromList(utf8.encode(text));

Object? modelOf(Json s) {
  final model = readObj(
    base64Decode(s['bytes']! as String),
    files: filesOf(s['files']),
    limits: limitsOf(s['limits']),
  );
  return {
    'mesh': specOf(model.mesh),
    'groups': [
      for (final g in model.groups) [g.name, g.faces],
    ],
    'materials': [for (final x in model.materials) tree(x.toGltf())],
    'problems': model.problems,
  };
}

const objSnippets = <String>[
  'v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\n',
  'v 0 0 0\nv 1 0 0\nv 0 1 0\nf -3 -2 -1\n',
  'v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1/1 2/2 3/3\n',
  'v 0 0 0\nv 1 0 0\nv 0 1 0\nvt 0 0\nvt 1 0\nvt 0 1\nf 1/1 2/2 3/3\n',
  'v 0 0 0\nv 1 0 0\nv 0 1 0\nvn 0 0 1\nf 1//1 2//1 3//1\n',
  'v 0 0 0\nv 1 0 0\nv 0 1 0\nvt 0 0\nvt 1 0\nvt 0 1\nvn 0 0 1\nf 1/1/1 2/2/1 3/3/1\n',
  'v 0 0 0 1\nv 1 0 0 1\nv 0 1 0 1\nf 1 2 3\n',
  'v 0 0 0 1 0 0\nv 1 0 0 0 1 0\nv 0 1 0 0 0 1\nf 1 2 3\n',
  'v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3 4\n',
  'v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2\n',
  'v 0 0 0\nv 1 0 0\nv 0 1 0\nf 0 1 2\n',
  'v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 9\n',
  'v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 x\n',
  'v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3 \\\n',
  'v 0 0 0\r\nv 1 0 0\r\nv 0 1 0\r\nf 1 2 3\r\n',
  'v\t0\t0\t0\nv 1 0 0\nv 0 1 0\nf 1 2 3\n',
  '# comment\nv 0 0 0 # trailing\nv 1 0 0\nv 0 1 0\nf 1 2 3\n',
  'v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\nf 1 2 3\n',
  'o first\nv 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\no second\nv 0 0 1\nf 1 2 4\n',
  'g a\nv 0 0 0\nv 1 0 0\nv 0 1 0\ng b\nf 1 2 3\ng a\nf 1 2 3\n',
  'g\nv 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\n',
  's 1\nv 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\ns off\nf 1 2 3\n',
  'usemtl red\nv 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\nusemtl blue\nf 1 2 3\nusemtl red\nf 1 2 3\n',
  'v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\nusemtl\nf 1 2 3\n',
  'v 1e999 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\n',
  'v nan 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\n',
  'v 0x10 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\n',
  'v 1,5 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\n',
  'v 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\n',
  'v 0 0 0\nv 1 0 0\nv 0 1 0\nl 1 2\np 1\nf 1 2 3\n',
  'v 0 0 0\nv 1 0 0\nv 0 1 0\nunknown 1 2\nf 1 2 3\n',
  'v 0 0 0\nv 1 0 0\nv 0 1 0\nvt 0\nvt x y\nf 1/1 2/2 3/3\n',
  'v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1/5 2/5 3/5\n',
  'v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1/-1 2/-1 3/-1\n',
  'v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1/1/1/1 2 3\n',
  'v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3',
  '',
  'f 1 2 3\n',
  'v 0 0 0\n',
  '\u00ef\u00bb\u00bfv 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\n',
  'o caf\u00e9\nv 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\n',
  'mtllib a.mtl\nv 0 0 0\nv 1 0 0\nv 0 1 0\nusemtl red\nf 1 2 3\n',
  'mtllib a.mtl b.mtl\nv 0 0 0\nv 1 0 0\nv 0 1 0\nusemtl red\nf 1 2 3\n',
  'mtllib missing.mtl\nv 0 0 0\nv 1 0 0\nv 0 1 0\nusemtl red\nf 1 2 3\n',
  'mtllib dir/a.mtl\nv 0 0 0\nv 1 0 0\nv 0 1 0\nusemtl red\nf 1 2 3\n',
];

const mtlSnippets = <String>[
  'newmtl red\nKd 1 0 0\nNs 100\nd 1\n',
  'newmtl red\nKd 1 0 0\nKe 0.5 0.5 0\nd 0.5\nNs 0\n',
  'newmtl red\nKd 1 0\n',
  'newmtl red\nKd x y z\n',
  'newmtl red\nmap_Kd tex.png\nKd 0.5 0.5 0.5\n',
  'newmtl red\nKd 1 0 0\nnewmtl blue\nKd 0 0 1\nnewmtl red\nKd 0 1 0\n',
  'Kd 1 0 0\nnewmtl red\n',
  'newmtl\nKd 1 0 0\n',
  'newmtl a b\nKd 1 0 0\n',
  'newmtl red\nTr 0.25\nNs 1000\nillum 2\n',
  'newmtl red\nPm 0.5\nPr 0.25\nKe 1 1 1\n',
  'newmtl red\nKd 2 -1 0.5\nd 7\n',
  'newmtl red\nbump b.png\nmap_d d.png\nmap_Ke e.png\n',
  '',
  '\r\nnewmtl red\r\nKd 1 0 0\r\n',
];

Uint8List mutate(math.Random r, Uint8List bytes) {
  if (bytes.isEmpty) return Uint8List.fromList([r.nextInt(256)]);
  final out = bytes.toList();
  switch (r.nextInt(6)) {
    case 0:
      return Uint8List.fromList(out.sublist(0, r.nextInt(out.length)));
    case 1:
      out[r.nextInt(out.length)] ^= 1 << r.nextInt(8);
    case 2:
      out[r.nextInt(out.length)] = r.nextInt(256);
    case 3:
      final at = r.nextInt(out.length);
      final end = math.min(out.length, at + 1 + r.nextInt(16));
      out.removeRange(at, end);
    case 4:
      out.insert(r.nextInt(out.length + 1), r.nextInt(256));
    default:
      final at = r.nextInt(out.length);
      final end = math.min(out.length, at + 1 + r.nextInt(8));
      for (var i = at; i < end; i++) {
        out[i] = r.nextInt(256);
      }
  }
  return Uint8List.fromList(out);
}

Json? limitsFor(math.Random r) {
  if (r.nextInt(5) != 0) return null;
  final all = {
    'bytes': pick(r, [10, 100, 1000]),
    'vertices': pick(r, [0, 1, 3, 5]),
    'faces': pick(r, [0, 1, 2, 5]),
    'corners': pick(r, [0, 2, 3, 4]),
    'indices': pick(r, [0, 3, 6, 10]),
    'names': pick(r, [0, 1, 2]),
    'line': pick(r, [0, 5, 20]),
  };
  final key = pick(r, all.keys.toList());
  return {key: all[key]};
}

List<Json> objCases() {
  final r = math.Random(61);
  final cases = <Json>[];
  Json fields(Uint8List bytes, [Map<String, Uint8List>? files, Json? limits]) =>
      {
        'bytes': b64(bytes),
        if (files != null)
          'files': {for (final e in files.entries) e.key: b64(e.value)},
        if (limits != null) 'limits': limits,
      };
  for (final text in objSnippets) {
    final files = {'a.mtl': utf(mtlSnippets[0]), 'b.mtl': utf(mtlSnippets[5])};
    cases.add(made('readObj', fields(utf(text), files), modelOf));
    cases.add(
      made(
        'readObj',
        fields(utf(text), files, limitsFor(r) ?? {'faces': 1}),
        modelOf,
      ),
    );
  }
  for (final mtl in mtlSnippets) {
    cases.add(
      made(
        'readObj',
        fields(
          utf(
            'mtllib a.mtl\nv 0 0 0\nv 1 0 0\nv 0 1 0\nusemtl red\nf 1 2 3\nusemtl blue\nf 1 2 3\n',
          ),
          {'a.mtl': utf(mtl)},
        ),
        modelOf,
      ),
    );
  }
  final written = <Uint8List>[
    for (final (_, base) in bases().take(11))
      for (final w in dressed(r, base).writeAs(
        MeshFormat.obj,
        materials: [
          const GlbMaterial(name: 'wall'),
          const GlbMaterial(name: 'floor'),
        ],
      ))
        if (w.name.endsWith('.obj')) w.bytes,
  ];
  for (final bytes in written) {
    cases.add(
      made(
        'readObj',
        fields(bytes, {
          'mesh.mtl': utf('newmtl wall\nKd 1 0 0\nnewmtl floor\nKd 0 1 0\n'),
        }),
        modelOf,
      ),
    );
    for (var i = 0; i < 5; i++) {
      cases.add(
        made(
          'readObj',
          fields(mutate(r, bytes), {
            'mesh.mtl': utf(mtlSnippets[1]),
          }, limitsFor(r)),
          modelOf,
        ),
      );
    }
  }
  return cases;
}

Object? readStlOf(Json s) => specOf(
  readStl(base64Decode(s['bytes']! as String), limits: limitsOf(s['limits'])),
);

Object? readPlyOf(Json s) => specOf(
  readPly(base64Decode(s['bytes']! as String), limits: limitsOf(s['limits'])),
);

Uint8List plyOf(
  String format,
  List<String> header,
  String ascii, [
  Uint8List? binary,
]) => Uint8List.fromList([
  ...utf8.encode('ply\nformat $format 1.0\n${header.join('\n')}\nend_header\n'),
  ...(binary ?? utf8.encode(ascii)),
]);

const plyVertex = [
  'element vertex 3',
  'property float x',
  'property float y',
  'property float z',
];

const plyFace = ['element face 1', 'property list uchar int vertex_indices'];

List<Json> plyHandCases() {
  final cases = <Json>[];
  void add(Uint8List bytes, [Json? limits]) => cases.add(
    made('readPly', {
      'bytes': b64(bytes),
      if (limits != null) 'limits': limits,
    }, readPlyOf),
  );
  add(
    plyOf('ascii', [
      ...plyVertex,
      ...plyFace,
    ], '0 0 0\n1 0 0\n0 1 0\n3 0 1 2\n'),
  );
  add(
    plyOf('ascii', [
      ...plyVertex,
      ...plyFace,
    ], '0 0 0\n1 0 0\n0 1 0\n4 0 1 2 0\n'),
  );
  add(
    plyOf('ascii', [
      ...plyVertex,
      ...plyFace,
    ], '0 0 0\n1 0 0\n0 1 0\n3 0 1 9\n'),
  );
  add(
    plyOf('ascii', [...plyVertex, ...plyFace], '0 0 0\n1 0 0\n0 1 0\n3 0 1\n'),
  );
  add(plyOf('ascii', [...plyVertex, ...plyFace], '0 0 0\n1 0 0\n0 1 0\n'));
  add(
    plyOf('ascii', [
      ...plyVertex,
      ...plyFace,
    ], '0 0 0\n1 0 0\n0 x 0\n3 0 1 2\n'),
  );
  add(
    plyOf('ascii', [
      ...plyVertex,
      ...plyFace,
    ], '0 0 0\n1 0 0\n0 1 0\n3 0 1 2\n3 0 1 2\n'),
  );
  add(
    plyOf('ascii', [
      'comment hi',
      'obj_info x',
      ...plyVertex,
      ...plyFace,
    ], '0 0 0\n1 0 0\n0 1 0\n3 0 1 2\n'),
  );
  add(
    plyOf('ascii', [
      'element vertex 0x3',
      'property float x',
      'property float y',
      'property float z',
      ...plyFace,
    ], '0 0 0\n1 0 0\n0 1 0\n3 0 1 2\n'),
  );
  add(
    plyOf('ascii', [
      'element vertex -3',
      'property float x',
      'property float y',
      'property float z',
      ...plyFace,
    ], '3 0 1 2\n'),
  );
  add(
    plyOf('ascii', [
      'element vertex 3',
      'property float x',
      'property float y',
      ...plyFace,
    ], '0 0\n1 0\n0 1\n3 0 1 2\n'),
  );
  add(
    plyOf('ascii', [
      'element vertex 3',
      'property double x',
      'property double y',
      'property double z',
      'property uchar red',
      'property uchar green',
      'property uchar blue',
      'element face 1',
      'property list uchar uint vertex_index',
    ], '0 0 0 255 0 0\n1 0 0 0 255 0\n0 1 0 0 0 255\n3 0 1 2\n'),
  );
  add(
    plyOf('ascii', [
      'element vertex 3',
      'property float x',
      'property float y',
      'property float z',
      'element face 1',
      'property uchar flags',
      'property list uchar int vertex_indices',
    ], '0 0 0\n1 0 0\n0 1 0\n7 3 0 1 2\n'),
  );
  add(
    plyOf('ascii', [
      'element vertex 3',
      'property float x',
      'property float y',
      'property float z',
      'element extra 2',
      'property float q',
      'element face 1',
      'property list uchar int vertex_indices',
    ], '0 0 0\n1 0 0\n0 1 0\n1\n2\n3 0 1 2\n'),
  );
  add(
    plyOf('ascii', [
      'element vertex 3',
      'property float x',
      'property float y',
      'property float z',
      'element vertex 3',
      ...plyFace,
    ], '0 0 0\n1 0 0\n0 1 0\n3 0 1 2\n'),
  );
  add(
    plyOf('ascii', [
      'element vertex 3',
      'property float x',
      'property float y',
      'property float z',
      'element face 1',
      'property list short short vertex_indices',
    ], '0 0 0\n1 0 0\n0 1 0\n3 0 1 2\n'),
  );
  add(
    plyOf('ascii', [
      'element vertex 3',
      'property float x',
      'property float y',
      'property float z',
      'element face 1',
      'property list uchar bogus vertex_indices',
    ], '0 0 0\n1 0 0\n0 1 0\n3 0 1 2\n'),
  );
  add(plyOf('nonsense', [...plyVertex, ...plyFace], ''));
  add(utf('ply\nformat ascii 1.0\nelement vertex 3\n'));
  add(utf('not a ply'));
  add(utf(''));
  add(
    plyOf('ascii', [
      ...plyVertex,
      ...plyFace,
    ], '0 0 0\n1 0 0\n0 1 0\n3 0 1 2\n'),
    {'faces': 0},
  );
  add(
    plyOf('ascii', [
      ...plyVertex,
      ...plyFace,
    ], '0 0 0\n1 0 0\n0 1 0\n3 0 1 2\n'),
    {'vertices': 2},
  );
  add(
    plyOf('ascii', [
      ...plyVertex,
      ...plyFace,
    ], '0 0 0\n1 0 0\n0 1 0\n3 0 1 2\n'),
    {'corners': 2},
  );
  return cases;
}

List<Json> meshReadCases() {
  final r = math.Random(71);
  final cases = <Json>[];
  void add(String op, Uint8List bytes) => cases.add(
    made(op, {
      'bytes': b64(bytes),
      if (limitsFor(r) case final l?) 'limits': l,
    }, op == 'readStl' ? readStlOf : readPlyOf),
  );
  final quad = built(
    ShapeKind.plane,
    (s) => s.copyWith(widthCuts: 0, heightCuts: 0),
  );
  final sources = [
    built(ShapeKind.cube),
    built(ShapeKind.prism),
    quad,
    built(ShapeKind.cone, (s) => s.copyWith(sides: 3)),
    awkwardMesh(r),
    Mesh(),
  ];
  for (final m in sources) {
    for (final binary in [true, false]) {
      final stl = m.writeAs(MeshFormat.stl, binary: binary).single.bytes;
      final ply = m.writeAs(MeshFormat.ply, binary: binary).single.bytes;
      add('readStl', stl);
      add('readPly', ply);
      for (var i = 0; i < 10; i++) {
        add('readStl', mutate(r, stl));
        add('readPly', mutate(r, ply));
      }
    }
  }
  const stlText = [
    'solid x\nfacet normal 0 0 1\nouter loop\nvertex 0 0 0\nvertex 1 0 0\nvertex 0 1 0\nendloop\nendfacet\nendsolid x\n',
    'solid x\nvertex 0 0 0\nvertex 1 0 0\nvertex 0 1 0\nendsolid\n',
    'solid x\nvertex 0 0 0\nvertex 1 0 0\nendsolid\n',
    'solid x\nvertex 0 0 0\nvertex 1 0 x\nvertex 0 1 0\nendsolid\n',
    'solid x\nvertex 0 0 0\nvertex 1 0 nan\nvertex 0 1 0\nendsolid\n',
    'solid\n',
    'solid x\nvertex 0 0 0\nvertex 1 0 0\nvertex 0 1 0\nvertex 0 0 1\nvertex 1 0 1\nvertex 0 1 1\nendsolid\n',
    'solid x\nvertex 0 0 0\nvertex 1 0 0\nvertex 0 1 0\nvertex 5 5 5\nendsolid\n',
    'vertex 0 0 0\nvertex 1 0 0\nvertex 0 1 0\n',
    'solid x\r\nvertex 0 0 0\r\nvertex 1 0 0\r\nvertex 0 1 0\r\nendsolid\r\n',
    'solid x\n vertex   0   0   0 \n vertex 1 0 0\n vertex 0 1 0\n',
    'solid x\nVERTEX 0 0 0\nVERTEX 1 0 0\nVERTEX 0 1 0\n',
  ];
  for (final text in stlText) {
    add('readStl', utf(text));
  }
  add('readStl', Uint8List(0));
  add('readStl', Uint8List(84));
  add('readStl', Uint8List(83));
  add('readStl', Uint8List(134));
  final huge = Uint8List(84 + 50)
    ..buffer.asByteData().setUint32(80, 0xffffffff, Endian.little);
  add('readStl', huge);
  final count = Uint8List(84 + 50 * 2)
    ..buffer.asByteData().setUint32(80, 1, Endian.little);
  add('readStl', count);
  cases.addAll(plyHandCases());
  return cases;
}

// glTF

Object? chunksOf(Json s) {
  final c = glbChunks(base64Decode(s['bytes']! as String));
  return c == null
      ? null
      : {'json': tree(c.json), 'binary': c.binary.isEmpty ? '' : b64(c.binary)};
}

Object? partsOf(Json s) {
  final p = gltfParts(
    base64Decode(s['bytes']! as String),
    files: filesOf(s['files']),
  );
  return {
    'json': tree(p.json),
    'binary': p.binary == null ? null : b64(p.binary!),
  };
}

Object? boundsOf(Json s) => boxOf(switch (s['kind']) {
  'glb' => boundsOfGlb(base64Decode(s['bytes']! as String)),
  'obj' => boundsOfObj(base64Decode(s['bytes']! as String)),
  _ => boundsOfGltf(utf8.decode(base64Decode(s['bytes']! as String))),
});

Object? accessorsOf(Json s) {
  final json = s['json']! as Json;
  final binary = s['binary'] == null
      ? null
      : base64Decode(s['binary']! as String);
  final reader = GltfAccessors(json, binary);
  final results = <Object?>[];
  for (final raw in list(s['calls'])) {
    final call = raw! as Json;
    switch (call['f']) {
      case 'count':
        results.add(reader.count(call['at'] as int?));
      case 'floats':
        results.add(
          floats(reader.floats(call['at'] as int?, call['n']! as int)),
        );
      default:
        results.add(reader.indices(call['at']! as int));
    }
  }
  return {'results': results, 'problems': reader.problems};
}

const accessorPools = <String, List<Object?>>{
  'componentType': [5120, 5121, 5122, 5123, 5125, 5126, 0, -1, 'x', null],
  'count': [0, 1, 2, 3, 24, 1000000, -1, 2.5, 'x', null],
  'type': ['SCALAR', 'VEC2', 'VEC3', 'VEC4', 'MAT4', 'nonsense', 3, null],
  'normalized': [true, false, 'x', null],
  'bufferView': [0, 1, 2, 3, 99, -1, 'x', null],
  'byteOffset': [0, 4, 12, 3, 100000, -4, 'x', null],
  'byteStride': [0, 4, 8, 12, 16, 1, 255, 1000000, -4, null],
  'byteLength': [0, 12, 48, 1000000, -1, null],
  'sparse': [
    {},
    {'count': 1},
    null,
  ],
};

Json mutateDocument(math.Random r, Json doc) {
  final copy = jsonDecode(jsonEncode(doc)) as Json;
  final accessors = (copy['accessors'] as List<Object?>?) ?? [];
  final views = (copy['bufferViews'] as List<Object?>?) ?? [];
  for (var i = 0, n = 1 + r.nextInt(3); i < n; i++) {
    final key = pick(r, accessorPools.keys.toList());
    final value = pick(r, accessorPools[key]!);
    final target = switch (key) {
      'byteStride' || 'byteLength' => views,
      _ => accessors,
    };
    if (target.isEmpty) continue;
    final one = pick(r, target)! as Json;
    if (value == null) {
      one.remove(key);
    } else {
      one[key] = value;
    }
  }
  return copy;
}

List<Json> gltfCases() {
  final r = math.Random(81);
  final cases = <Json>[];
  final sources = [
    built(ShapeKind.cube),
    built(ShapeKind.cylinder, (s) => s.copyWith(sides: 5)),
    built(ShapeKind.sphere, (s) => s.copyWith(subdivisions: 1)),
    Mesh(),
    dressed(r, built(ShapeKind.prism)),
  ];
  final glbs = [for (final m in sources) m.toGlb()];
  for (final glb in glbs) {
    final chunks = glbChunks(glb)!;
    for (final bytes in [
      glb,
      ...[for (var i = 0; i < 10; i++) mutate(r, glb)],
    ]) {
      cases.add(made('glbChunks', {'bytes': b64(bytes)}, chunksOf));
      cases.add(made('gltfParts', {'bytes': b64(bytes)}, partsOf));
      cases.add(made('bounds', {'kind': 'glb', 'bytes': b64(bytes)}, boundsOf));
    }
    for (var i = 0; i < 20; i++) {
      final doc = mutateDocument(r, chunks.json);
      cases.add(
        made('accessors', {
          'json': doc,
          'binary': i % 7 == 0 ? null : b64(chunks.binary),
          'calls': [
            for (var k = 0; k < 4; k++)
              {
                'f': pick(r, ['count', 'floats', 'indices']),
                'at': pick(r, [0, 1, 2, 3, 9, -1]),
                'n': pick(r, [1, 2, 3, 4]),
              },
          ],
        }, accessorsOf),
      );
    }
    final text = gltfText(
      Map<String, Object?>.of(chunks.json),
      buffer: 'mesh.bin',
      byteLength: chunks.binary.length,
    );
    for (final bytes in [
      utf(text),
      ...[for (var i = 0; i < 10; i++) mutate(r, utf(text))],
    ]) {
      cases.add(
        made('gltfParts', {
          'bytes': b64(bytes),
          'files': {'mesh.bin': b64(chunks.binary)},
        }, partsOf),
      );
      cases.add(
        made('bounds', {'kind': 'gltf', 'bytes': b64(bytes)}, boundsOf),
      );
    }
  }
  const uris = [
    'data:application/octet-stream;base64,AAECAw==',
    'data:;base64,AAECAw',
    'data:,plain',
    'data:application/octet-stream;base64,@@@@',
    'data:application/octet-stream;base64,AAECAw==AAECAw==',
    'a%20b.bin',
    'a%2',
    'a%zz',
    'a%C3%A9.bin',
    '../a.bin',
    'sub/a.bin',
    './a.bin',
    'a.bin',
    'missing.bin',
    '',
    'C:\\a.bin',
    'file:///a.bin',
    'http://example.com/a.bin',
    'caf\u00e9.bin',
  ];
  for (final uri in uris) {
    final document = jsonEncode({
      'asset': {'version': '2.0'},
      'buffers': [
        {'byteLength': 4, 'uri': uri},
      ],
    });
    final files = {
      'a.bin': b64([1, 2, 3, 4]),
      'a b.bin': b64([5, 6, 7, 8]),
      'sub/a.bin': b64([9, 9, 9, 9]),
      'caf\u00e9.bin': b64([7, 7, 7, 7]),
      'caf%C3%A9.bin': b64([8, 8, 8, 8]),
    };
    cases.add(
      made('gltfParts', {'bytes': b64(utf(document)), 'files': files}, partsOf),
    );
  }
  const documents = [
    '{}',
    '[]',
    '3',
    'null',
    '',
    '{"buffers": []}',
    '{"buffers": 3}',
    '{"buffers": [3]}',
    '{"buffers": [{}]}',
    '{"buffers": [{"uri": 4}]}',
    '{"asset": {"version": "2.0"}, "accessors": [{"min": [0,0,0], "max": [1,1,1]}]}',
    '{"accessors": [{"min": [0,0,0], "max": [1,1]}]}',
    '{"accessors": [{"min": [0,0,"x"], "max": [1,1,1]}]}',
    '{"accessors": [{"min": [1,1,1], "max": [0,0,0]}]}',
    '{"meshes": [{"primitives": [{"attributes": {"POSITION": 0}}]}], "accessors": [{"min": [0,0,0], "max": [1,2,3]}]}',
    '{"meshes": [{"primitives": [{"attributes": {"POSITION": 5}}]}], "accessors": [{"min": [0,0,0], "max": [1,2,3]}]}',
    '{"meshes": [{"primitives": [{"attributes": {"POSITION": 0}}, {"attributes": {"POSITION": 1}}]}], "accessors": [{"min": [0,0,0], "max": [1,2,3]}, {"min": [-1,-1,-1], "max": [0,0,0]}]}',
    '{"meshes": [{"primitives": []}]}',
    '{"meshes": [{}]}',
    '{"meshes": 3}',
    '{"nodes": [{"mesh": 0, "translation": [1,2,3]}], "meshes": [{"primitives": [{"attributes": {"POSITION": 0}}]}], "accessors": [{"min": [0,0,0], "max": [1,2,3]}]}',
  ];
  for (final text in documents) {
    cases.add(
      made('bounds', {'kind': 'gltf', 'bytes': b64(utf(text))}, boundsOf),
    );
    cases.add(made('gltfParts', {'bytes': b64(utf(text))}, partsOf));
  }
  for (final bytes in written()) {
    cases.add(made('bounds', {'kind': 'obj', 'bytes': b64(bytes)}, boundsOf));
  }
  for (final text in objSnippets) {
    cases.add(
      made('bounds', {'kind': 'obj', 'bytes': b64(utf(text))}, boundsOf),
    );
  }
  return cases;
}

List<Uint8List> written() {
  final r = math.Random(91);
  return [
    for (final (_, base) in bases())
      for (final w in dressed(r, base).writeAs(MeshFormat.obj))
        if (w.name.endsWith('.obj')) w.bytes,
  ];
}

// cases exactly on a bound

Json readOf(String op, Uint8List bytes, Json limits) => made(
  op,
  {'bytes': b64(bytes), 'limits': limits},
  switch (op) {
    'readStl' => readStlOf,
    'readPly' => readPlyOf,
    _ => modelOf,
  },
);

/// Each limit one under, on and one over what the input measures, because a
/// bound is only held when a case sits on it.
List<Json> limitCases() {
  final cases = <Json>[];
  void around(String op, Uint8List bytes, String key, int at) {
    for (final n in [at - 1, at, at + 1]) {
      cases.add(readOf(op, bytes, {key: n}));
    }
  }

  final stl = Uint8List(84 + 50 * 2);
  final data = stl.buffer.asByteData()..setUint32(80, 2, Endian.little);
  const corners = [0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 1, 0, 1, 0, 1, 1];
  for (var i = 0; i < corners.length; i++) {
    data.setFloat32(
      84 + (i ~/ 9) * 50 + 12 + (i % 9) * 4,
      corners[i] * 1.0,
      Endian.little,
    );
  }
  around('readStl', stl, 'bytes', stl.length);
  around('readStl', stl, 'faces', 2);
  const stlText =
      'solid x\nvertex 0 0 0\nvertex 1 0 0\nvertex 0 1 0\nvertex 0 0 1\nvertex 1 0 1\nvertex 0 1 1\nendsolid\n';
  around('readStl', utf(stlText), 'faces', 2);
  around('readStl', utf(stlText), 'bytes', stlText.length);

  const objText = 'v 0 0 0\nv 1 0 0\nv 0 1 0\nv 1 1 0\nf 1 2 3\nf 1 3 4\n';
  around('readObj', utf(objText), 'indices', 6);
  around('readObj', utf(objText), 'vertices', 4);
  around('readObj', utf(objText), 'faces', 2);
  around('readObj', utf(objText), 'line', 7);
  around(
    'readObj',
    utf('v 0 0 0\nv 1 0 0\nv 0 1 0\nv 1 1 0\nf 1 2 3 4\n'),
    'corners',
    4,
  );
  for (final bad in [49, 50, 51, 52]) {
    cases.add(
      readOf(
        'readObj',
        utf('v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\n${'f 1\n' * bad}'),
        {},
      ),
    );
  }
  cases.add(
    readOf(
      'readObj',
      utf(
        'v 0 0 0\nv 1 0 0\nv 0 1 0\ns 0\nf 1 2 3\ns off\nf 1 2 3\ns 1\nf 1 2 3\ns\nf 1 2 3\ns on\nf 1 2 3\n',
      ),
      {},
    ),
  );

  for (final (v, f) in [(3, 1), (2, 1), (4, 1), (3, 0), (3, 2)]) {
    cases.add(
      readOf(
        'readPly',
        plyOf('ascii', [
          ...plyVertex,
          ...plyFace,
        ], '0 0 0\n1 0 0\n0 1 0\n3 0 1 2\n'),
        {'vertices': v, 'faces': f},
      ),
    );
  }
  for (final index in [2, 3, 4]) {
    cases.add(
      readOf(
        'readPly',
        plyOf('ascii', [
          ...plyVertex,
          ...plyFace,
        ], '0 0 0\n1 0 0\n0 1 0\n3 0 1 $index\n'),
        {},
      ),
    );
  }
  for (final length in [39, 40, 41]) {
    final word = '0.${'0' * (length - 2)}';
    cases.add(
      readOf(
        'readPly',
        plyOf('ascii', [
          ...plyVertex,
          ...plyFace,
        ], '$word 0 0\n1 0 0\n0 1 0\n3 0 1 2\n'),
        {},
      ),
    );
  }
  return cases;
}

/// Normalised components at both ends of their range, which is where the
/// scale and the floor at minus one show.
List<Json> normalisedCases() {
  final cases = <Json>[];
  void add(int type, List<int> bytes, int count) {
    cases.add(
      made('accessors', {
        'json': {
          'asset': {'version': '2.0'},
          'buffers': [
            {'byteLength': bytes.length},
          ],
          'bufferViews': [
            {'buffer': 0, 'byteOffset': 0, 'byteLength': bytes.length},
          ],
          'accessors': [
            {
              'bufferView': 0,
              'componentType': type,
              'count': count,
              'type': 'SCALAR',
              'normalized': true,
            },
          ],
        },
        'binary': b64(bytes),
        'calls': [
          {'f': 'floats', 'at': 0, 'n': 1},
        ],
      }, accessorsOf),
    );
  }

  add(5120, [0x80, 0x81, 0x7f, 0, 0x40], 5);
  add(5121, [0, 1, 0x7f, 0x80, 0xff], 5);
  add(5122, [0x00, 0x80, 0x01, 0x80, 0xff, 0x7f, 0, 0, 0x34, 0x12], 5);
  add(5123, [0, 0, 0xff, 0xff, 0, 0x80, 0x34, 0x12], 4);
  return cases;
}

/// Points on the lattice, so some land exactly on a corner or an edge of the
/// outline, and outlines with a corner lying on another corner's edge.
List<Json> latticeCases() {
  final cases = <Json>[];
  final outlines = [
    [
      [0, 0],
      [4, 0],
      [4, 4],
      [0, 4],
    ],
    [
      [0, 0],
      [4, 0],
      [4, 2],
      [2, 2],
      [2, 4],
      [0, 4],
    ],
    [
      [0, 0],
      [2, 0],
      [4, 0],
      [4, 4],
      [0, 4],
    ],
    [
      [0, 0],
      [4, 0],
      [2, 0],
      [2, 4],
    ],
    [
      [0, 0],
      [4, 0],
      [4, 4],
      [2, 2],
      [0, 4],
    ],
  ];
  for (final outline in outlines) {
    for (var x = -1; x <= 5; x++) {
      for (var y = -1; y <= 5; y++) {
        cases.add(
          made(
            'fn',
            {
              'f': 'pointInsideOutline',
              'outline': outline,
              'at': [x, y],
            },
            (s) => pointInsideOutline([
              for (final p in list(s['outline'])) v2(p),
            ], v2(s['at'])),
          ),
        );
      }
    }
    for (final normal in [Vector3(0, 0, 1), Vector3(0, 0, -1)]) {
      cases.add(
        made(
          'fn',
          {
            'f': 'cutUp',
            'points': [
              for (final p in outline) vec3(Vector3(p[0] * 1.0, p[1] * 1.0, 0)),
            ],
            'normal': vec3(normal),
          },
          (s) => cutUp([
            for (final p in list(s['points'])) v3(p),
          ], v3(s['normal'])),
        ),
      );
    }
  }
  return cases;
}

/// A copy of [m] with every position scaled, for the checks that compare a
/// squared length with a tiny limit.
Mesh scaled(Mesh m, double k) {
  final copy = meshOf(specOf(m));
  for (var i = 0; i < copy.positions.length; i++) {
    copy.positions[i] = copy.positions[i] * k;
  }
  return copy;
}

List<Json> tinyCases() {
  final cases = <Json>[];
  final r = math.Random(41);
  for (final k in [1e-3, 1e-4, 1e-5, 1e-6]) {
    for (final kind in [ShapeKind.cube, ShapeKind.cylinder, ShapeKind.prism]) {
      final m = scaled(built(kind, (s) => s.copyWith(smooth: true)), k);
      for (final f in m.faces) {
        f.smooth = true;
      }
      cases.add(made('triangulate', {'mesh': specOf(m)}, trianglesOf));
      cases.add(
        made('export', {
          'mesh': specOf(m),
          'format': 'stl',
          'binary': true,
          'name': 'tiny',
          'materials': <Object?>[],
        }, writtenOf),
      );
      cases.add(
        made('export', {
          'mesh': specOf(m),
          'format': 'stl',
          'binary': false,
          'name': 'tiny',
          'materials': <Object?>[],
        }, writtenOf),
      );
    }
  }
  for (final width in [1e-10, 1e-8, 1e-7, 1e-6, 1e-5]) {
    final quad = {
      'p': [0, 0, 0, width, 0, 0, width, 1, 0, 0, 1, 0],
      'f': [
        {
          'v': [0, 1, 2, 3],
          'm': 0,
          's': false,
        },
      ],
    };
    cases.add(
      made('uvEdit', {
        'mesh': quad,
        'steps': [
          {
            'do': 'fitUvs',
            'faces': [0],
            'keepShape': false,
          },
          {
            'do': 'fitUvs',
            'faces': [0],
            'keepShape': true,
          },
        ],
      }, runSteps),
    );
  }
  for (final gap in [1e-11, 1e-10, 1e-9, 1e-8, 1e-7, 1e-6, 1e-5]) {
    for (final normal in [Vector3(0, 0, 1), Vector3(0, 0, -1)]) {
      for (final points in [
        [
          Vector3(0, 0, 0),
          Vector3(4, 0, 0),
          Vector3(4, 4, 0),
          Vector3(2, 4, 0),
          Vector3(2 + gap, 4, 0),
          Vector3(2 + gap, 1, 0),
          Vector3(2, 1, 0),
          Vector3(2, 4, 0),
          Vector3(0, 4, 0),
        ],
        [
          Vector3(0, 0, 0),
          Vector3(4, 0, 0),
          Vector3(4, 0 + gap, 0),
          Vector3(4, 4, 0),
          Vector3(0, 4, 0),
        ],
      ]) {
        cases.add(
          made(
            'fn',
            {
              'f': 'cutUp',
              'points': [for (final p in points) vec3(p)],
              'normal': vec3(normal),
            },
            (s) => cutUp([
              for (final p in list(s['points'])) v3(p),
            ], v3(s['normal'])),
          ),
        );
      }
    }
  }
  for (var i = 0; i < 300; i++) {
    final n = 4 + r.nextInt(5);
    final points = [
      for (var k = 0; k < n; k++)
        Vector3(r.nextInt(4).toDouble(), r.nextInt(4).toDouble(), 0),
    ];
    cases.add(
      made(
        'fn',
        {
          'f': 'cutUp',
          'points': [for (final p in points) vec3(p)],
          'normal': vec3(Vector3(0, 0, 1)),
        },
        (s) =>
            cutUp([for (final p in list(s['points'])) v3(p)], v3(s['normal'])),
      ),
    );
  }
  return cases;
}

List<Json> smallCases() {
  final cases = <Json>[];
  for (final tube in [0.0005, 0.001, 0.0015, 0.002, 0.003]) {
    cases.add(
      made('shape', {
        'kind': 'torus',
        'tubeRadius': tube,
        'sides': 3,
        'rings': 3,
      }, (s) => specOf(shapeOf(s).build())),
    );
  }
  final quad = {
    'p': [0, 0, 0, 4, 0, 0, 4, 4, 0, 0, 4, 0],
    'f': [
      {
        'v': [0, 1, 2, 3],
        'm': 0,
        's': false,
      },
    ],
  };
  const loop = [
    [1, 1, 0],
    [3, 1, 0],
    [3, 3, 0],
    [1, 1.5, 0],
  ];
  for (final reach in [0.4999, 0.5, 0.5001]) {
    cases.add(
      made('cut', {
        'mesh': quad,
        'steps': [
          {'do': 'cutFace', 'face': 0, 'path': loop, 'reach': reach},
        ],
      }, runSteps),
    );
  }
  return cases;
}

// main

void main(List<String> args) {
  writeFixture(
    root: args.single,
    package: 'orblit_mesh',
    name: 'mesh',
    cases: [
      ...shapeCases(),
      ...stepCases('edit', 101, editGens, 9),
      ...stepCases('action', 102, actionGens, 11),
      ...stepCases('uvEdit', 103, uvGens, 6),
      ...stepCases('cut', 104, cutGens, 8),
      ...triangleCases(),
      ...polygonCases(),
      ...faceUvCases(),
      ...jsonCases(),
      ...writeCases(),
      ...objCases(),
      ...meshReadCases(),
      ...gltfCases(),
      ...limitCases(),
      ...normalisedCases(),
      ...latticeCases(),
      ...smallCases(),
      ...tinyCases(),
    ],
  );
}

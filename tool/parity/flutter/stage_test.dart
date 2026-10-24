// Writes dotnet/fixtures/orblit_stage/document_view.json: the scene a document
// stages to, by first staging and by a diff, with every key the view handed
// out. It also writes particles.json: the populations a view draws frame by
// frame, and the glb and joint poses of a sheet and a ribbon. It needs the
// Flutter package it reads, so tool/parity/flutter/regen.sh copies it into that
// package's test folder, runs it there and removes the copy. The documents come
// from the package's own incremental test.
import 'dart:io';
import 'dart:math';
import 'dart:typed_data';

import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_filament/orblit_filament.dart';
import 'package:orblit_light/orblit_light.dart' show LightType, Tint;
import 'package:orblit_particles/orblit_particles.dart' show Trail, TrailStrip;
import 'package:orblit_scene/orblit_scene.dart';
import 'package:orblit_stage/orblit_stage.dart';
import 'package:orblit_weather/orblit_weather.dart'
    show WeatherCondition, WeatherState;
import 'package:vector_math/vector_math_64.dart';

import 'incremental_test.dart' show randomDocument;
// ignore: avoid_relative_lib_imports, the helper is not in any package
import '../../../tool/parity/lib/fixture.dart';

List<Object> _point(Vector3 v) => [number(v.x), number(v.y), number(v.z)];

List<Object> _cells(Matrix4 m) => [for (final c in m.storage) number(c)];

Map<String, Object?> _dump(OrblitScene scene) => {
  'objects': [
    for (final o in scene.objects)
      {
        'key': big(o.key),
        'transform': _cells(o.transform),
        'mesh': o.mesh,
        'visible': o.visible,
        'colour': _point(o.colour),
        'castShadows': o.castShadows,
        'material': o.material,
      },
  ],
  'lights': [
    for (final l in scene.lights)
      {
        'key': big(l.key),
        'kind': l.kind.index,
        'intensity': number(l.intensity),
        'colour': _point(l.colour),
        'position': _point(l.position),
        'direction': _point(l.direction),
        'falloffRadius': number(l.falloffRadius),
        'innerConeAngle': number(l.innerConeAngle),
        'outerConeAngle': number(l.outerConeAngle),
        'sunAngularRadius': number(l.sunAngularRadius),
        'sourceRadius': number(l.sourceRadius),
        'haloSize': number(l.haloSize),
        'haloFalloff': number(l.haloFalloff),
        'castShadows': l.castShadows,
        'width': number(l.width),
        'height': number(l.height),
      },
  ],
  'sprites': [
    for (final s in scene.sprites)
      {
        'key': big(s.key),
        'transform': _cells(s.transform),
        'blend': s.blend.index,
      },
  ],
  'splats': [
    for (final s in scene.splats)
      {
        'key': big(s.key),
        'path': s.path,
        'transform': _cells(s.transform),
        'harmonics': s.harmonics,
        'limit': s.limit,
      },
  ],
  'sky': {
    'colour': _point(scene.sky.colour),
    'ambient': number(scene.sky.ambient),
  },
  'fog': {
    'colour': _point(scene.fog.colour),
    'density': number(scene.fog.density),
    'height': number(scene.fog.height),
  },
  'precipitation': {
    'amount': number(scene.precipitation.amount),
    'fall': number(scene.precipitation.fall),
    'wind': [
      number(scene.precipitation.wind.x),
      number(scene.precipitation.wind.y),
    ],
  },
  'camera': {
    'position': _point(scene.camera.position),
    'target': _point(scene.camera.target),
    'fieldOfView': number(scene.camera.fieldOfView),
  },
};

SceneEntity _entity(
  String id, {
  String? parent,
  bool visible = true,
  Map<String, SceneComponent> components = const {},
}) => SceneEntity(
  id: id,
  name: id,
  parent: parent,
  visible: visible,
  components: components,
);

TransformComponent _at(double x, double y, double z, {double turn = 0}) =>
    TransformComponent(position: Vector3(x, y, z), rotation: Vector3(0, turn, 0));

/// Documents that reach every part of the staging: each kind of light, an
/// additive sprite, splats with and without a budget, each weather, a hidden
/// group and a camera that is moved.
List<SceneDocument> _chosen() => [
  SceneDocument(
    entities: [
      for (final kind in LightType.values)
        _entity(
          'lamp-${kind.name}',
          components: {
            SceneComponents.transform: _at(1, 2, 3, turn: 90),
            SceneComponents.light: LightComponent(
              kind: kind,
              power: 400,
              spotSize: 30,
              spotBlend: 0.3,
              sourceRadius: 0.4,
              sunAngle: 1,
              castShadows: kind != LightType.point,
            ),
          },
        ),
    ],
  ),
  SceneDocument(
    entities: [
      _entity(
        'glow',
        components: {
          SceneComponents.transform: _at(0, 1, 0),
          SceneComponents.sprite: const SpriteComponent(
            additive: true,
            depth: 2,
          ),
        },
      ),
      _entity(
        'card',
        components: {
          SceneComponents.transform: _at(4, 0, 0, turn: 180),
          SceneComponents.sprite: const SpriteComponent(width: 2, height: 3),
        },
      ),
      _entity(
        'cloud',
        components: {
          SceneComponents.transform: _at(0, 0, 5),
          SceneComponents.splats: const SplatsComponent(
            asset: 'scans/a.ply',
            budget: 5000,
            harmonics: 1,
          ),
        },
      ),
      _entity(
        'wide',
        components: {
          SceneComponents.splats: const SplatsComponent(
            asset: 'scans/b.ply',
            harmonics: 9,
          ),
        },
      ),
      _entity(
        'empty',
        components: {SceneComponents.splats: const SplatsComponent()},
      ),
    ],
  ),
  for (final condition in WeatherCondition.values)
    SceneDocument(
      settings: SceneSettings(
        sky: const Tint.hex(0x6688CC),
        ambient: 3000,
      ),
      entities: [
        _entity(
          'weather',
          components: {
            SceneComponents.weather: WeatherComponent(
              condition: condition,
              air: WeatherState.of(condition),
            ),
          },
        ),
      ],
    ),
  SceneDocument(
    entities: [
      _entity(
        'group',
        visible: false,
        components: {SceneComponents.transform: _at(10, 0, 0, turn: 45)},
      ),
      _entity(
        'inside',
        parent: 'group',
        components: {
          SceneComponents.transform: _at(1, 0, 0),
          SceneComponents.mesh: const MeshComponent(asset: 'm0.glb'),
        },
      ),
      _entity(
        'eye',
        components: {
          SceneComponents.transform: _at(3, 4, 5, turn: 270),
          SceneComponents.camera: const CameraComponent(fieldOfView: 70),
        },
      ),
    ],
  ),
  SceneDocument(entities: const []),
];

// The text a case carries, and the document the exporter stages is read back
// from it, so both sides start from the same bytes.
SceneDocument _read(String text) => SceneDocument.decode(text).document;

Map<String, Object?> _population(OrblitPopulation p) => {
  'key': big(p.key),
  'revision': big(p.revision),
  'count': p.count,
  'transforms': [for (final v in p.transforms) number(v)],
  'colours': [for (final v in p.colours) number(v)],
  'minimum': _point(p.minimum),
  'maximum': _point(p.maximum),
  'fade': p.fade.index,
  'receiveShadows': p.receiveShadows,
};

Map<String, Object?> _frame(OrblitParticles particles) => {
  'populations': [for (final p in particles.populations) _population(p)],
  'rejected': particles.rejected.keys.toList(),
};

SceneEntity _emitterAt(
  String id,
  ParticleEmitterComponent emitter, {
  double x = 0,
  bool visible = true,
}) => _entity(
  id,
  visible: visible,
  components: {
    SceneComponents.transform: _at(x, 0, 0),
    SceneComponents.particles: emitter,
  },
);

final _eye = _entity(
  'eye',
  components: {
    SceneComponents.transform: _at(3, 4, 5, turn: 270),
    SceneComponents.camera: const CameraComponent(),
  },
);

final _spray = ParticleEmitterComponent(
  seed: 7,
  shape: EmitterShape.sphere,
  radius: 0.5,
  rate: 12,
  lifetimeMin: 1,
  lifetimeMax: 2,
  speedMin: 1,
  speedMax: 3,
  spinMin: -1,
  spinMax: 2,
  gravity: Vector3(0, -9.8, 0),
  drag: 0.3,
  size: const [0.2, 0.05],
  colour: const [1, 0.5, 0.2],
);

final _puff = ParticleEmitterComponent(
  seed: 3,
  shape: EmitterShape.cone,
  angle: 0.5,
  rate: 6,
  bursts: const [EmitterBurst(0.5, 5)],
  duration: 2,
  lifetimeMin: 1,
  lifetimeMax: 1.5,
  noiseStrength: 0.4,
  space: EmitterSpace.local,
);

final _dust = ParticleEmitterComponent(
  seed: 11,
  shape: EmitterShape.box,
  extents: Vector3(1, 0.2, 2),
  rate: 8,
  lifetimeMin: 2,
  lifetimeMax: 2,
  speedMin: 0,
  speedMax: 0.5,
);

/// Each case is the documents a view is moved through, one before each frame,
/// and the seconds each frame advances by.
List<Map<String, Object?>> _particleCases() {
  final cases = <Map<String, Object?>>[];
  void add(
    List<SceneDocument> documents,
    List<double> steps, {
    int key = 0,
    int budget = 20000,
  }) {
    final texts = [for (final d in documents) d.encode()];
    final view = OrblitDocumentView(_read(texts.first));
    final particles = OrblitParticles(view, key: key, budget: budget);
    final frames = <Map<String, Object?>>[];
    for (var i = 0; i < steps.length; i++) {
      if (i > 0 && i < texts.length) view.replace(_read(texts[i]));
      particles.advance(steps[i]);
      frames.add(_frame(particles));
    }
    cases.add({
      'op': 'particles',
      'in': {'documents': texts, 'steps': steps, 'key': key, 'budget': budget},
      'expect': frames,
    });
  }

  SceneDocument of(List<SceneEntity> entities) =>
      SceneDocument(entities: entities);

  add(
    [
      of([_emitterAt('spray', _spray), _emitterAt('puff', _puff, x: 4), _eye]),
      of([_emitterAt('spray', _spray, x: 1), _emitterAt('puff', _puff, x: 4)]),
      of([_emitterAt('spray', _spray, x: 3), _emitterAt('puff', _puff, x: 5)]),
      of([
        _emitterAt('spray', _spray, x: 3, visible: false),
        _emitterAt('puff', _puff, x: 5),
      ]),
      of([_emitterAt('spray', _spray, x: 3), _emitterAt('puff', _puff, x: 5)]),
    ],
    [0.5, 0.5, 0.25, 0.25, 1, 0.75],
    key: 40,
  );
  add(
    [
      of([_emitterAt('dust', _dust), _emitterAt('spray', _spray, x: 2), _eye]),
    ],
    [0, 1, 1, 0.5],
    budget: 14,
  );
  add(
    [
      of([
        _emitterAt('spray', _spray),
        _emitterAt('puff', _puff, x: 1),
        _emitterAt('dust', _dust, x: 2),
      ]),
      of([
        _emitterAt('spray', _spray.copyWith(rate: 20)),
        _emitterAt('dust', _dust, x: 2),
      ]),
    ],
    [2, 1, 3],
    key: 5,
    budget: 60,
  );
  return cases;
}

Vector3 _clothAt(int columns, int i) {
  final x = (i % columns) * 0.5;
  final y = -(i ~/ columns) * 0.5;
  return Vector3(x * 1.3 + y * 0.4, y, (i % 5) * 0.07 + (i ~/ 3) * 0.02);
}

Map<String, Object?> _posed(Uint8List glb, List<OrblitJointPose> poses) => {
  'glb': b64(glb),
  'poses': [
    for (final p in poses)
      {'skin': p.skin, 'joint': p.joint, 'transform': _cells(p.transform)},
  ],
};

List<Map<String, Object?>> _clothCases() => [
  for (final (columns, rows, spacing, colour) in [
    (2, 2, 1.0, const [0.8, 0.8, 0.8]),
    (4, 3, 0.25, const [0.9, 0.1, 0.1]),
    (5, 2, 0.5, const [0.2, 0.4, 0.6]),
    (16, 16, 0.1, const [0.8, 0.8, 0.8]),
  ])
    () {
      final sheet = OrblitParticleSheet(
        columns: columns,
        rows: rows,
        spacing: spacing,
        colour: colour,
      );
      return {
        'op': 'sheet',
        'in': {
          'columns': columns,
          'rows': rows,
          'spacing': spacing,
          'colour': colour,
        },
        'expect': _posed(
          sheet.toGlb(),
          sheet.poses((i) => _clothAt(columns, i)),
        ),
      };
    }(),
];

List<Map<String, Object?>> _ribbonCases() => [
  for (final (points, sampled, widths, eye, still) in [
    (6, 6, const [0.4], const [0.0, 0.0, 10.0], false),
    (8, 5, const [0.5, 0.1], const [3.0, 8.0, 1.0], false),
    (4, 4, const [0.0], const [0.0, 0.0, 10.0], false),
    (5, 5, const [0.3], const [0.0, 0.0, 10.0], true),
    (4, 4, const [0.3], const [-5.0, -1.0, -1.5], false),
  ])
    () {
      final strip = TrailStrip(sampled);
      Vector3 path(double t) =>
          still ? Vector3(1, 2, 3) : Vector3(t * 3, t * t * 0.5, t * 0.3);
      Trail(
        life: 1,
        rate: sampled - 1,
        width: widths,
      ).sampleAlong(10, path, strip);
      final ribbon = OrblitTrailStrip(points: points);
      return {
        'op': 'ribbon',
        'in': {
          'points': points,
          'sampled': sampled,
          'widths': widths,
          'eye': eye,
          'still': still,
        },
        'expect': _posed(
          ribbon.toGlb(),
          ribbon.poses(strip, Vector3(eye[0], eye[1], eye[2])),
        ),
      };
    }(),
];

void main() {
  test('writes the particle fixtures', () {
    writeFixture(
      root: Directory('../..').absolute.path,
      package: 'orblit_stage',
      name: 'particles',
      cases: [..._particleCases(), ..._clothCases(), ..._ribbonCases()],
    );
  });

  test('writes the stage fixtures', () {
    final root = Directory('../..').absolute.path;
    final cases = <Map<String, Object?>>[];

    final starts = <SceneDocument>[
      ..._chosen(),
      for (var seed = 0; seed < 40; seed++)
        randomDocument(Random(seed), size: 1 + Random(seed).nextInt(8)),
    ];
    for (final document in starts) {
      final text = document.encode();
      cases.add({
        'op': 'stage',
        'in': text,
        'expect': _dump(OrblitDocumentView(_read(text)).scene),
      });
    }

    for (var seed = 0; seed < 40; seed++) {
      final random = Random(seed);
      final before = randomDocument(random, size: 1 + random.nextInt(8)).encode();
      final after = randomDocument(random, size: 1 + random.nextInt(8)).encode();
      final view = OrblitDocumentView(_read(before))..replace(_read(after));
      cases.add({
        'op': 'replace',
        'in': [before, after],
        'expect': _dump(view.scene),
      });
    }

    for (var seed = 100; seed < 104; seed++) {
      final random = Random(seed);
      final texts = [
        for (var step = 0; step < 10; step++)
          randomDocument(random, size: 1 + random.nextInt(8)).encode(),
      ];
      final view = OrblitDocumentView(_read(texts.first));
      final seen = [_dump(view.scene)];
      for (final text in texts.skip(1)) {
        view.replace(_read(text));
        seen.add(_dump(view.scene));
      }
      cases.add({'op': 'run', 'in': texts, 'expect': seen});
    }

    writeFixture(
      root: root,
      package: 'orblit_stage',
      name: 'document_view',
      cases: cases,
    );
  });
}

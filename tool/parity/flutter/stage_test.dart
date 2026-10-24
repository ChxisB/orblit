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
import 'dart:ui' show Offset, Size;

import 'package:flutter/widgets.dart' show SizedBox;
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_audio/orblit_audio.dart'
    show Audio, AudioCall, AudioStarted, AudioStopped, AudioUpdated, RecordedAudio;
import 'package:orblit_filament/orblit_filament.dart';
import 'package:orblit_light/orblit_light.dart' show LightType, Tint;
import 'package:orblit_motion/orblit_motion.dart'
    show
        ChannelKind,
        ClipChannel,
        ClipDocument,
        CutsceneDocument,
        CutsceneShot,
        CutsceneSound,
        Hold,
        Key,
        Mark,
        WhenDone;
import 'package:orblit_particles/orblit_particles.dart' show Trail, TrailStrip;
import 'package:orblit_scene/orblit_scene.dart';
import 'package:orblit_stage/orblit_stage.dart';
import 'package:orblit_ui/orblit_ui.dart' show UiPanel;
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

SceneEntity _cam(
  String id, {
  required Vector3 at,
  Vector3? turned,
  double fieldOfView = 50,
  bool visible = true,
}) => SceneEntity(
  id: id,
  name: id,
  visible: visible,
  components: {
    SceneComponents.transform: TransformComponent(
      position: at,
      rotation: turned,
    ),
    SceneComponents.camera: CameraComponent(fieldOfView: fieldOfView),
  },
);

/// The game's camera, three more for a cutscene to cut between, and a cube.
/// 'back' looks straight at 'game', so the two cancel when blended evenly.
SceneDocument _cutStage() => SceneDocument(
  entities: [
    _cam('game', at: Vector3(0, 0, 10)),
    _cam('front', at: Vector3(0, 0, 5), fieldOfView: 40, visible: false),
    _cam(
      'side',
      at: Vector3(5, 0, 0),
      turned: Vector3(0, 90, 0),
      fieldOfView: 60,
      visible: false,
    ),
    _cam(
      'back',
      at: Vector3(0, 0, -10),
      turned: Vector3(0, 180, 0),
      fieldOfView: 30,
      visible: false,
    ),
    SceneEntity(
      id: 'cube',
      name: 'cube',
      components: {
        SceneComponents.transform: TransformComponent(),
        SceneComponents.mesh: const MeshComponent(),
      },
    ),
  ],
);

CutsceneDocument _cutIntro(WhenDone whenDone) => CutsceneDocument(
  motion: ClipDocument(
    name: 'Intro',
    duration: 4,
    whenDone: whenDone,
    channels: [
      ClipChannel<Vector3>(
        target: 'cube',
        property: 'transform.position',
        kind: ChannelKind.vector,
        keys: [
          Key(0, Vector3.zero(), hold: Hold.linear),
          Key(4, Vector3(4, 0, 0)),
        ],
      ),
    ],
    marks: const [Mark(3, 'boom')],
  ),
  shots: [
    CutsceneShot(camera: 'front', start: 0, duration: 3),
    CutsceneShot(camera: 'side', start: 2, duration: 2),
  ],
  sounds: [CutsceneSound(sound: 'swell.ogg', start: 0, duration: 4)],
);

CutsceneDocument _cutOutro() => CutsceneDocument(
  motion: ClipDocument(name: 'Outro', duration: 1, channels: const []),
  shots: [CutsceneShot(camera: 'back', start: 0, duration: 1)],
);

Map<String, Object?> _camera(OrblitCamera c) => {
  'position': _point(c.position),
  'target': _point(c.target),
  'fieldOfView': number(c.fieldOfView),
  'aperture': number(c.aperture),
};

Map<String, Object?> _cutFrame(
  OrblitCutscenes cutscenes,
  OrblitDocumentView view,
  OrblitCutsceneStep? step,
) {
  final through = view.through;
  final cube =
      (view.document['cube']![SceneComponents.transform]! as TransformComponent)
          .position;
  return {
    'playing': cutscenes.playing,
    'cube': _point(cube),
    'through': through == null ? null : _camera(through),
    'marks': [
      for (final m in step?.marks ?? const [])
        {'at': number(m.at), 'name': m.name},
    ],
    'sounds': [
      for (final s in step?.sounds ?? const [])
        {
          'sound': s.sound,
          'at': number(s.at),
          'gain': number(s.gain),
          'binding': s.binding,
        },
    ],
    'ended': step?.ended ?? false,
  };
}

/// Each action is one call on the cutscenes, and the frame is what the view
/// shows after it.
Map<String, Object?> _cutCase(
  List<CutsceneDocument> cutscenes,
  List<List<Object?>> script,
) {
  final text = _cutStage().encode();
  final texts = [for (final c in cutscenes) c.encode()];
  final view = OrblitDocumentView(_read(text));
  final player = OrblitCutscenes(view, [
    for (final t in texts) CutsceneDocument.decode(t).cutscene,
  ]);
  final frames = <Map<String, Object?>>[];
  for (final action in script) {
    OrblitCutsceneStep? step;
    switch (action[0]) {
      case 'start':
        player.start(action[1]! as String);
      case 'startFrom':
        player.startFrom([
          for (final name in action[1]! as List) Mark(0, name as String),
        ]);
      case 'stop':
        player.stop();
      case 'advance':
        step = player.advance((action[1]! as num).toDouble());
    }
    frames.add(_cutFrame(player, view, step));
  }
  return {
    'op': 'cutscene',
    'in': {'document': text, 'cutscenes': texts, 'script': script},
    'expect': frames,
  };
}

Map<String, Object?> _blendCase(List<(String, double)> picks) {
  final text = _cutStage().encode();
  final view = OrblitDocumentView(_read(text));
  final blended = blendCameras([
    for (final (id, weight) in picks) (view.cameraOf(id)!, weight),
  ]);
  return {
    'op': 'blend',
    'in': {
      'document': text,
      'cameras': [
        for (final (id, weight) in picks) [id, weight],
      ],
    },
    'expect': blended == null ? null : _camera(blended),
  };
}

List<Map<String, Object?>> _cutsceneCases() => [
  _cutCase(
    [_cutIntro(WhenDone.hold)],
    [
      ['start', 'Intro'],
      ['advance', 1.0],
      ['advance', 1.5],
      ['advance', 0.5],
      ['advance', 1.0],
      ['advance', 1.0],
    ],
  ),
  _cutCase(
    [_cutIntro(WhenDone.release)],
    [
      ['start', 'Intro'],
      ['advance', 2.5],
      ['advance', 2.0],
      ['advance', 1.0],
    ],
  ),
  _cutCase(
    [_cutIntro(WhenDone.loop)],
    [
      ['start', 'Intro'],
      ['advance', 3.5],
      ['advance', 1.0],
      ['advance', 2.0],
    ],
  ),
  _cutCase(
    [_cutIntro(WhenDone.bounce)],
    [
      ['start', 'Intro'],
      ['advance', 3.0],
      ['advance', 2.0],
    ],
  ),
  _cutCase(
    [_cutIntro(WhenDone.release), _cutOutro()],
    [
      ['startFrom', <String>['nothing', 'Outro', 'Intro']],
      ['advance', 0.5],
      ['start', 'Intro'],
      ['advance', 1.0],
      ['start', 'Outro'],
      ['advance', 0.5],
      ['stop'],
      ['advance', 1.0],
    ],
  ),
  _blendCase([('front', 0.5), ('side', 0.5)]),
  _blendCase([('front', 0.25), ('side', 0.75), ('game', 0)]),
  _blendCase([('front', 1), ('side', 0)]),
  _blendCase([('front', 0), ('side', -1)]),
  _blendCase([('game', 1), ('back', 1)]),
  _blendCase([('game', 2), ('back', 1), ('side', 1)]),
];

SceneEntity _thing(
  String id,
  Vector3 at,
  Map<String, SceneComponent> parts, {
  Vector3? turned,
  bool visible = true,
}) => SceneEntity(
  id: id,
  name: id,
  visible: visible,
  components: {
    SceneComponents.transform: TransformComponent(
      position: at,
      rotation: turned,
    ),
    ...parts,
  },
);

SceneEntity _fire(Vector3 at, {bool visible = true}) => _thing(
  'fire',
  at,
  {
    SceneComponents.soundSource: SoundSourceComponent(
      clip: 'fire.ogg',
      bus: 'sfx',
    ),
  },
  visible: visible,
);

SceneEntity _wind(Vector3 at) => _thing('wind', at, {
  SceneComponents.soundSource: SoundSourceComponent(
    clip: 'wind.ogg',
    bus: 'sfx',
    loop: false,
    volume: 0.5,
    near: 2,
    far: 30,
    curve: SoundCurve.linear,
  ),
});

SceneEntity _ear(Vector3 at, Vector3 turned) => _thing('ear', at, {
  SceneComponents.soundListener: const SoundListenerComponent(),
}, turned: turned);

SceneDocument _world(List<SceneEntity> others, {Vector3? eye}) =>
    SceneDocument(
      entities: [
        _thing(
          'game',
          eye ?? Vector3(0, 0, 10),
          {SceneComponents.camera: const CameraComponent()},
        ),
        ...others,
      ],
    );

CutsceneDocument _swell() => CutsceneDocument(
  motion: ClipDocument(name: 'Swell', duration: 1, channels: const []),
  sounds: [CutsceneSound(sound: 'swell.ogg', start: 0, duration: 1)],
);

Map<String, Object?> _audioCall(AudioCall call) => switch (call) {
  AudioStarted s => {
    'kind': 'start',
    'voice': big(s.voice),
    'clip': s.clip,
    'loop': s.loop,
    'gain': number(s.level.gain),
    'pitch': number(s.level.pitch),
    'pan': number(s.level.pan),
    'offset': number(s.offset),
  },
  AudioUpdated u => {
    'kind': 'update',
    'voice': big(u.voice),
    'gain': number(u.level.gain),
    'pitch': number(u.level.pitch),
    'pan': number(u.level.pan),
  },
  AudioStopped x => {'kind': 'stop', 'voice': big(x.voice)},
};

/// Each action is one call on the sound view, and the frame is every call the
/// backend got since the last action, with where the listener stands.
Map<String, Object?> _soundCase(
  List<SceneDocument> documents,
  List<CutsceneDocument> cutscenes,
  List<List<Object?>> script,
) {
  final texts = [for (final d in documents) d.encode()];
  final cutTexts = [for (final c in cutscenes) c.encode()];
  final backend = RecordedAudio();
  final audio = Audio(backend)..mixer.add('sfx');
  final view = OrblitDocumentView(_read(texts.first));
  final sound = OrblitSound(view, audio);
  final player = OrblitCutscenes(view, [
    for (final t in cutTexts) CutsceneDocument.decode(t).cutscene,
  ]);
  final frames = <Map<String, Object?>>[];
  var seen = 0;
  for (final action in script) {
    switch (action[0]) {
      case 'replace':
        view.replace(_read(texts[(action[1]! as num).toInt()]));
      case 'play':
        sound.play(action[1]! as String);
      case 'stop':
        sound.stop();
      case 'start':
        player.start(action[1]! as String);
      case 'frame':
        final seconds = (action[1]! as num).toDouble();
        sound.advance(seconds, player.advance(seconds));
        audio.advance(seconds);
    }
    frames.add({
      'calls': [for (final c in backend.calls.skip(seen)) _audioCall(c)],
      'position': _point(audio.listener.position),
      'forward': _point(audio.listener.forward),
      'up': _point(audio.listener.up),
    });
    seen = backend.calls.length;
  }
  return {
    'op': 'sound',
    'in': {'documents': texts, 'cutscenes': cutTexts, 'script': script},
    'expect': frames,
  };
}

List<Map<String, Object?>> _soundCases() => [
  _soundCase(
    [
      _world([_fire(Vector3(10, 0, 10))]),
    ],
    [],
    [
      ['frame', 1 / 60],
      ['frame', 1 / 60],
      ['play', 'fire'],
      ['frame', 1 / 60],
      ['play', 'nothing'],
    ],
  ),
  _soundCase(
    [
      _world([
        _ear(Vector3(1, 0, 0), Vector3(0, 90, 0)),
        _fire(Vector3(4, 0, 0)),
        _wind(Vector3(-6, 0, 2)),
      ]),
      _world([
        _ear(Vector3(1, 0, 0), Vector3(0, 90, 0)),
        _fire(Vector3(4, 0, 0), visible: false),
        _wind(Vector3(-6, 0, 2)),
      ]),
      _world([
        _ear(Vector3(1, 0, 0), Vector3(0, 90, 0)),
        _fire(Vector3(4, 0, 0)),
        _wind(Vector3(200, 0, 2)),
      ]),
    ],
    [],
    [
      ['frame', 1 / 60],
      ['frame', 0.5],
      ['replace', 1],
      ['frame', 1 / 60],
      ['play', 'fire'],
      ['replace', 2],
      ['frame', 1 / 60],
      ['frame', 1 / 60],
      ['stop'],
      ['replace', 0],
      ['frame', 1 / 60],
    ],
  ),
  _soundCase(
    [
      _world([_fire(Vector3(2, 0, 8))]),
      _world([_fire(Vector3(2, 0, 8))], eye: Vector3(0, 0, 9.95)),
      _world([_fire(Vector3(2, 0, 8))], eye: Vector3(300, 0, 9.95)),
    ],
    [],
    [
      ['frame', 1 / 60],
      ['replace', 1],
      ['frame', 1 / 60],
      ['replace', 2],
      ['frame', 1 / 60],
    ],
  ),
  _soundCase(
    [_world([])],
    [_swell()],
    [
      ['frame', 1 / 60],
      ['start', 'Swell'],
      ['frame', 0.4],
      ['frame', 0.4],
      ['frame', 0.4],
      ['frame', 0.1],
    ],
  ),
];

Vector3 _spread(Random random, double reach) => Vector3(
  (random.nextDouble() * 2 - 1) * reach,
  (random.nextDouble() * 2 - 1) * reach,
  (random.nextDouble() * 2 - 1) * reach,
);

Map<String, Object?> _rayCase(OrblitCamera camera, Offset point, Size view) {
  final ray = PointerRay.fromCamera(camera, point, view);
  return {
    'op': 'ray',
    'in': {
      'position': _point(camera.position),
      'target': _point(camera.target),
      'fieldOfView': number(camera.fieldOfView),
      'orthographic': camera.orthographic,
      'viewHeight': number(camera.viewHeight),
      'point': [number(point.dx), number(point.dy)],
      'view': [number(view.width), number(view.height)],
    },
    'expect': {
      'origin': _point(ray.origin),
      'direction': _point(ray.direction),
    },
  };
}

List<Map<String, Object?>> _rayCases() {
  final random = Random(7);
  const views = [
    Size(100, 100),
    Size(200, 100),
    Size(100, 200),
    Size(640, 480),
    Size(333, 777),
  ];
  Map<String, Object?> random_(int at) {
    final view = views[at % views.length];
    final camera = OrblitCamera(
      position: _spread(random, 10),
      target: _spread(random, 10),
      fieldOfView: 20 + random.nextDouble() * 80,
      orthographic: at % 4 == 3,
      viewHeight: 2 + random.nextDouble() * 18,
    );
    final point = Offset(
      random.nextDouble() * view.width,
      random.nextDouble() * view.height,
    );
    return _rayCase(camera, point, view);
  }

  final front = OrblitCamera(
    position: Vector3(0, 0, 5),
    target: Vector3.zero(),
    fieldOfView: 90,
  );
  return [
    for (final corner in const [
      Offset(0, 0),
      Offset(100, 0),
      Offset(0, 100),
      Offset(100, 100),
      Offset(50, 50),
    ])
      _rayCase(front, corner, const Size(100, 100)),
    _rayCase(front, const Offset(200, 0), const Size(200, 100)),
    _rayCase(front, const Offset(100, 100), const Size(100, 200)),
    _rayCase(
      front.copyWith(orthographic: true, viewHeight: 10),
      const Offset(100, 0),
      const Size(100, 100),
    ),
    // Looking straight down has no right-hand side.
    _rayCase(
      OrblitCamera(position: Vector3(0, 10, 0), target: Vector3.zero()),
      const Offset(30, 70),
      const Size(100, 100),
    ),
    for (var at = 0; at < 40; at++) random_(at),
  ];
}

Map<String, Object?> _pixelCase(
  Matrix4 transform,
  Size size,
  Vector3 origin,
  Vector3 direction,
) {
  final panel = UiPanel(child: const SizedBox(), size: size);
  final wall = WorldPanel(
    id: 'wall',
    panel: panel,
    transform: transform,
    objectKey: 1,
    materialKey: 2,
  );
  final pixel = wall.pixelAt(PointerRay(origin, direction));
  panel.dispose();
  return {
    'op': 'pixel',
    'in': {
      'transform': _cells(transform),
      'size': [number(size.width), number(size.height)],
      'origin': _point(origin),
      'direction': _point(direction),
    },
    'expect': pixel == null ? null : [number(pixel.dx), number(pixel.dy)],
  };
}

Map<String, Object?> _pixelThrough(
  Matrix4 transform,
  Size size,
  Vector3 from,
  Vector3 local,
) {
  final target = transform.transformed3(local);
  return _pixelCase(transform, size, from, (target - from)..normalize());
}

List<Map<String, Object?>> _pixelCases() {
  final wall = Matrix4.identity()
    ..setTranslation(Vector3(2, 1, 0))
    ..rotateY(pi / 2)
    ..scaleByVector3(Vector3(2, 1, 0.05));
  const size = Size(200, 100);
  final eye = Vector3(10, 1, 0);
  final cases = <Map<String, Object?>>[
    for (final local in [
      Vector3(0, 0, 1),
      Vector3(0.5, 0.5, 1),
      Vector3(-0.9, -0.9, 1),
      Vector3(1.5, 0, 1),
      Vector3(0, -2, 1),
    ])
      _pixelThrough(wall, size, eye, local),
    _pixelThrough(wall, size, Vector3(-6, 1, 0), Vector3(0, 0, 1)),
    _pixelCase(wall, size, Vector3(-6, 1, 0), Vector3(-1, 0, 0)),
    _pixelCase(wall, size, Vector3(10, 1, 0), Vector3(0, 0, 1)),
  ];
  final random = Random(11);
  for (var at = 0; at < 24; at++) {
    final turn = Quaternion.axisAngle(
      _spread(random, 1)..normalize(),
      random.nextDouble() * 2 * pi,
    );
    final placed = Matrix4.compose(
      _spread(random, 5),
      turn,
      Vector3(0.5 + random.nextDouble() * 3, 0.5 + random.nextDouble() * 3, 0.05),
    );
    cases.add(
      _pixelThrough(
        placed,
        Size(50 + random.nextDouble() * 400, 50 + random.nextDouble() * 400),
        _spread(random, 12),
        Vector3(
          (random.nextDouble() * 2 - 1) * 1.4,
          (random.nextDouble() * 2 - 1) * 1.4,
          1,
        ),
      ),
    );
  }
  return cases;
}

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  test('writes the pointing fixtures', () {
    writeFixture(
      root: Directory('../..').absolute.path,
      package: 'orblit_stage',
      name: 'pointing',
      cases: [..._rayCases(), ..._pixelCases()],
    );
  });

  test('writes the particle fixtures', () {
    writeFixture(
      root: Directory('../..').absolute.path,
      package: 'orblit_stage',
      name: 'particles',
      cases: [..._particleCases(), ..._clothCases(), ..._ribbonCases()],
    );
  });

  test('writes the sound and cutscene fixtures', () {
    writeFixture(
      root: Directory('../..').absolute.path,
      package: 'orblit_stage',
      name: 'sound_cutscenes',
      cases: [..._cutsceneCases(), ..._soundCases()],
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

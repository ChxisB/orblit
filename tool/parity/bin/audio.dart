import 'dart:math' as math;

import 'package:orblit_audio/orblit_audio.dart';
import 'package:orblit_parity_tool/fixture.dart';
import 'package:vector_math/vector_math_64.dart';

const _nan = double.nan;
const _inf = double.infinity;

/// The generator the light exporter uses, so a case list is the same on every
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

Vector3 _vec(Object? spec) {
  final v = (spec! as List).cast<double>();
  return Vector3(v[0], v[1], v[2]);
}

List<double> _level(Level level) => [level.gain, level.pitch, level.pan];

Map<String, Object?> _callJson(AudioCall call) => switch (call) {
  AudioStarted() => {
    't': 'start',
    'v': call.voice,
    'clip': call.clip,
    'loop': call.loop,
    'offset': call.offset,
    'level': _level(call.level),
  },
  AudioUpdated() => {
    't': 'update',
    'v': call.voice,
    'level': _level(call.level),
  },
  AudioStopped() => {'t': 'stop', 'v': call.voice},
};

Map<String, Object?> _falloffSpec(_Rng r) {
  final near = r.pick(<double>[0.5, 1, 2, 5]);
  return {
    'near': near,
    'far': near + r.pick(<double>[1, 10, 49, 100]),
    'curve': r.next(2),
  };
}

Map<String, Object?> _coneSpec(_Rng r) {
  final inner = r.pick(<double>[0, 0.3, 0.6, 1]);
  return {
    'inner': inner,
    'outer': math.min(inner + r.pick(<double>[0, 0.4, 1, 2]), math.pi),
    'gain': r.pick(<double>[0, 0.2, 0.5, 1]),
  };
}

const _directions = <List<double>>[
  [0, 0, 1],
  [0, 0, -1],
  [1, 0, 0],
  [0, 1, 0],
  [3, 4, 0],
  [0, 0, 0],
  [0.3, -0.2, 0.9],
  [-1, 0, 0],
];

List<double> _spot(_Rng r, {int range = 30}) => [
  r.between(-range, range),
  r.chance(3) ? 0 : r.between(-range ~/ 3, range ~/ 3),
  r.between(-range, range),
];

Map<String, Object?> _emitterSpec(_Rng r) => {
  'pos': _spot(r),
  'vel': r.chance(2) ? <double>[0, 0, 0] : _spot(r, range: 60),
  'falloff': r.chance(3) ? null : _falloffSpec(r),
  'cone': r.chance(3) ? _coneSpec(r) : null,
  'forward': r.chance(2) ? null : r.pick(_directions),
  'doppler': r.pick(<double>[1, 1, 0, 0.5, 2, 4]),
};

Map<String, Object?> _listenerSpec(_Rng r) => {
  'pos': _spot(r, range: 10),
  'vel': r.chance(2) ? <double>[0, 0, 0] : _spot(r, range: 60),
  'forward': r.chance(2) ? <double>[0, 0, -1] : r.pick(_directions),
  'up': r.chance(2) ? <double>[0, 1, 0] : r.pick(_directions),
};

Emitter _emitterFrom(Map<String, Object?> s) {
  final falloff = s['falloff'] as Map<String, Object?>?;
  final cone = s['cone'] as Map<String, Object?>?;
  final forward = s['forward'];
  final emitter = Emitter(
    position: _vec(s['pos']),
    falloff: falloff == null
        ? null
        : Falloff(
            near: falloff['near']! as double,
            far: falloff['far']! as double,
            curve: FalloffCurve.values[falloff['curve']! as int],
          ),
    cone: cone == null
        ? null
        : Cone(
            inner: cone['inner']! as double,
            outer: cone['outer']! as double,
            outerGain: cone['gain']! as double,
          ),
    forward: forward == null ? null : _vec(forward),
    doppler: s['doppler']! as double,
  );
  emitter.velocity = _vec(s['vel']);
  return emitter;
}

void _placeListener(Listener listener, Map<String, Object?> s) {
  listener.position = _vec(s['pos']);
  listener.velocity = _vec(s['vel']);
  listener.forward = _vec(s['forward']);
  listener.up = _vec(s['up']);
}

/// A hand-picked value of each kind: the ones a game gives, then the ones it
/// should not.
double _gain(_Rng r) => r.chance(14)
    ? r.pick(<double>[-1, _nan, _inf])
    : r.pick(<double>[0, 0.25, 0.5, 0.8, 1, 1.5]);

double _seconds(_Rng r) => r.chance(14)
    ? r.pick(<double>[-1, _nan, _inf])
    : r.pick(<double>[0, 0.1, 0.5, 1, 2, 4]);

double _pitch(_Rng r) => r.chance(14)
    ? r.pick(<double>[0, -1, _nan, _inf])
    : r.pick(<double>[0.5, 1, 1.5, 2]);

double _step(_Rng r) => r.chance(12)
    ? r.pick(<double>[-1, _nan])
    : r.pick(<double>[0, 0.016, 0.1, 0.25, 0.5, 1, 2]);

/// What a script runs against: the audio, a recording backend, three buses and
/// a bus from another mixer, and the emitters a step can point at.
final class _World {
  _World(this.kind, Map<String, Object?> script, this.setup)
    : emitters = [
        for (final spec in script['emitters']! as List<Map<String, Object?>>)
          _emitterFrom(spec),
      ] {
    audio = Audio(backend);
    _placeListener(audio.listener, script['listener']! as Map<String, Object?>);
    final sfx = audio.mixer.add('sfx');
    buses = {
      'master': audio.mixer.master,
      'sfx': sfx,
      'fx': audio.mixer.add('fx', under: sfx),
      'foreign': Mixer().add('sfx'),
    };
    _build();
  }

  final String kind;
  final Map<String, Object?> setup;
  final List<Emitter> emitters;
  final RecordedAudio backend = RecordedAudio();
  final List<Voice> voices = [];
  late final Audio audio;
  late final Map<String, Bus> buses;
  Music? music;
  CueBoard? cues;
  ContactSounds? contacts;

  void _build() {
    final bus = _bus(setup['bus'] as String?);
    switch (kind) {
      case 'music':
        music = Music(
          audio,
          tracks: {
            'calm': MusicTrack('calm.ogg'),
            'fight': MusicTrack('fight.ogg', bpm: 60, beatsPerBar: 3),
            'drift': MusicTrack('drift.ogg', bpm: 90.5, beatsPerBar: 5),
          },
          moves: [
            for (final m in setup['moves']! as List<Map<String, Object?>>)
              MusicMove(
                to: m['to']! as String,
                from: m['from'] as String?,
                when: MusicWhen.values[m['when']! as int],
                fade: m['fade']! as double,
              ),
          ],
          bus: bus,
          duck: setup['duck']! as double,
        );
      case 'cues':
        cues = CueBoard(
          audio,
          bus: bus,
          beds: {...(setup['beds']! as List<String>)},
          emitterFor: _emitterFor,
          release: setup['release']! as double,
        );
      case 'contacts':
        contacts = ContactSounds(
          audio,
          rules: [
            for (final rule in setup['rules']! as List<Map<String, Object?>>)
              ContactRule(
                rule['a']! as String,
                rule['b'] as String?,
                rule['clip']! as String,
              ),
          ],
          bus: bus,
          quiet: setup['quiet']! as double,
          loud: setup['loud']! as double,
          limit: setup['limit']! as int,
        );
    }
  }

  Bus? _bus(String? name) => name == null ? null : buses[name];

  Emitter? _emitterAt(int? index) => index == null ? null : emitters[index];

  Emitter? _emitterFor(String binding) {
    final index = const {'a': 0, 'b': 1}[binding];
    return index == null || index >= emitters.length ? null : emitters[index];
  }

  /// Runs one step and writes down what the backend heard and the state.
  Map<String, Object?> run(Map<String, Object?> step) {
    final from = backend.calls.length;
    var threw = false;
    try {
      switch (kind) {
        case 'audio':
          _audioStep(step);
        case 'music':
          _musicStep(step);
        case 'cues':
          _cueStep(step);
        case 'contacts':
          _contactStep(step);
      }
    } on ArgumentError {
      threw = true;
    }
    return {
      'throws': threw,
      'calls': [for (final c in backend.calls.skip(from)) _callJson(c)],
      ..._state(),
    };
  }

  Map<String, Object?> _state() => switch (kind) {
    'audio' => {
      'voices': [
        for (final v in voices) [v.isPlaying, v.volume, v.pitch],
      ],
      'live': audio.voices.length,
    },
    'music' => {'playing': music!.playing, 'waiting': music!.waitingFor},
    _ => {'live': audio.voices.length},
  };

  void _finish(bool Function(AudioStarted) which) {
    for (final call in backend.calls.whereType<AudioStarted>().toList()) {
      if (which(call) && backend.isPlaying(call.voice)) {
        backend.finish(call.voice);
      }
    }
  }

  void _audioStep(Map<String, Object?> s) {
    switch (s['do']) {
      case 'play':
        voices.add(
          audio.play(
            s['clip']! as String,
            bus: _bus(s['bus'] as String?),
            loop: s['loop']! as bool,
            volume: s['volume']! as double,
            pitch: s['pitch']! as double,
            offset: s['offset']! as double,
            at: _emitterAt(s['at'] as int?),
          ),
        );
      case 'fade':
        voices[s['voice']! as int].fadeTo(
          s['volume']! as double,
          seconds: s['seconds']! as double,
        );
      case 'stop':
        voices[s['voice']! as int].stop(fadeSeconds: s['fade']! as double);
      case 'volume':
        voices[s['voice']! as int].volume = s['value']! as double;
      case 'pitch':
        voices[s['voice']! as int].pitch = s['value']! as double;
      case 'advance':
        audio.advance(s['by']! as double);
      case 'busVolume':
        buses[s['bus']! as String]!.volume = s['value']! as double;
      case 'mute':
        buses[s['bus']! as String]!.muted = s['on']! as bool;
      case 'moveEmitter':
        emitters[s['at']! as int].moveTo(
          _vec(s['to']),
          s['seconds']! as double,
        );
      case 'moveListener':
        audio.listener.moveTo(_vec(s['to']), s['seconds']! as double);
      case 'teleportListener':
        audio.listener.teleport(_vec(s['to']));
      case 'finish':
        backend.finish(voices[s['voice']! as int].id);
      default:
        throw StateError('unknown step ${s['do']}');
    }
  }

  void _musicStep(Map<String, Object?> s) {
    switch (s['do']) {
      case 'go':
        music!.go(s['track']! as String);
      case 'stop':
        music!.stop(fade: s['fade']! as double);
      case 'stinger':
        music!.stinger('sting.ogg');
      case 'musicAdvance':
        music!.advance(s['by']! as double);
      case 'audioAdvance':
        audio.advance(s['by']! as double);
      case 'finishStingers':
        _finish((call) => call.clip == 'sting.ogg');
      case 'finishAll':
        _finish((_) => true);
      default:
        throw StateError('unknown step ${s['do']}');
    }
  }

  void _cueStep(Map<String, Object?> s) {
    switch (s['do']) {
      case 'update':
        cues!.update([
          for (final c in s['cues']! as List<Map<String, Object?>>)
            Cue(
              c['clip']! as String,
              c['at']! as double,
              c['gain']! as double,
              binding: c['binding'] as String?,
            ),
        ], seeked: s['seeked']! as bool);
      case 'advance':
        audio.advance(s['by']! as double);
      case 'stop':
        cues!.stop();
      case 'finishAll':
        _finish((_) => true);
      default:
        throw StateError('unknown step ${s['do']}');
    }
  }

  void _contactStep(Map<String, Object?> s) {
    switch (s['do']) {
      case 'hit':
        contacts!.hit(
          s['a']! as String,
          s['b']! as String,
          strength: s['strength']! as double,
          at: _emitterAt(s['at'] as int?),
        );
      case 'advance':
        audio.advance(s['by']! as double);
      case 'finishAll':
        _finish((_) => true);
      default:
        throw StateError('unknown step ${s['do']}');
    }
  }
}

Map<String, Object?> _audioStepFor(_Rng r, _World w) {
  final pick = r.next(100);
  final voice = w.voices.isEmpty ? 0 : r.next(w.voices.length);
  if (pick < 28 || w.voices.isEmpty) {
    return {
      'do': 'play',
      'clip': r.pick(['a.ogg', 'b.ogg', 'c.ogg']),
      'bus': r.pick([null, null, 'master', 'sfx', 'fx', 'sfx', 'fx']),
      'loop': r.chance(2),
      'volume': _gain(r),
      'pitch': _pitch(r),
      'offset': r.chance(4) ? r.pick(<double>[1.5, 12.5, -1, _nan]) : 0.0,
      'at': r.chance(2) || w.emitters.isEmpty
          ? null
          : r.next(w.emitters.length),
    };
  }
  if (pick < 58) return {'do': 'advance', 'by': _step(r)};
  if (pick < 68) {
    return {
      'do': 'fade',
      'voice': voice,
      'volume': _gain(r),
      'seconds': _seconds(r),
    };
  }
  if (pick < 75) return {'do': 'stop', 'voice': voice, 'fade': _seconds(r)};
  if (pick < 79) return {'do': 'volume', 'voice': voice, 'value': _gain(r)};
  if (pick < 82) return {'do': 'pitch', 'voice': voice, 'value': _pitch(r)};
  if (pick < 86) {
    return {
      'do': 'busVolume',
      'bus': r.pick(['master', 'sfx', 'fx']),
      'value': _gain(r),
    };
  }
  if (pick < 89) {
    return {
      'do': 'mute',
      'bus': r.pick(['master', 'sfx', 'fx']),
      'on': r.chance(2),
    };
  }
  if (pick < 93 && w.emitters.isNotEmpty) {
    return {
      'do': 'moveEmitter',
      'at': r.next(w.emitters.length),
      'to': _spot(r),
      'seconds': r.pick(<double>[0, 0.5, 1]),
    };
  }
  if (pick < 96) {
    return {
      'do': 'moveListener',
      'to': _spot(r, range: 10),
      'seconds': r.pick(<double>[0, 0.5, 1]),
    };
  }
  if (pick < 98) {
    return {'do': 'teleportListener', 'to': _spot(r, range: 10)};
  }
  return {'do': 'finish', 'voice': voice};
}

Map<String, Object?> _musicStepFor(_Rng r) {
  final pick = r.next(100);
  if (pick < 25) {
    return {
      'do': 'go',
      'track': r.chance(20)
          ? 'dance'
          : r.pick(['calm', 'fight', 'drift', 'calm', 'fight']),
    };
  }
  final by = r.pick(<double>[0.05, 0.2, 0.4, 0.5, 1, 1.7, 2, 3.3]);
  if (pick < 50) return {'do': 'musicAdvance', 'by': by};
  if (pick < 75) return {'do': 'audioAdvance', 'by': by};
  if (pick < 80) {
    return {
      'do': 'stop',
      'fade': r.pick(<double>[0, 0.5, 1]),
    };
  }
  if (pick < 90) return {'do': 'stinger'};
  if (pick < 98) return {'do': 'finishStingers'};
  return {'do': 'finishAll'};
}

Map<String, Object?> _cueStepFor(_Rng r) {
  final pick = r.next(100);
  if (pick < 60) {
    return {
      'do': 'update',
      'seeked': r.chance(4),
      'cues': [
        for (var i = r.next(5); i > 0; i--)
          {
            'clip': r.pick(['a.ogg', 'b.ogg', 'bed.ogg']),
            'at': r.pick(<double>[0, 1.5, 3]),
            'gain': r.chance(40) ? -1.0 : r.pick(<double>[0, 0.3, 0.6, 1]),
            'binding': r.pick([null, null, 'a', 'b', 'c']),
          },
      ],
    };
  }
  if (pick < 90) {
    return {
      'do': 'advance',
      'by': r.pick(<double>[0, 0.04, 0.1, 1]),
    };
  }
  if (pick < 95) return {'do': 'stop'};
  return {'do': 'finishAll'};
}

Map<String, Object?> _contactStepFor(_Rng r, _World w) {
  const surfaces = ['wood', 'stone', 'glass', 'metal'];
  final pick = r.next(100);
  if (pick < 60) {
    // Most hits are aimed at a rule, so the rules and their order are tested.
    final rule = r.pick(w.setup['rules']! as List<Map<String, Object?>>);
    final aimed = !r.chance(4);
    final a = aimed ? rule['a']! as String : r.pick(surfaces);
    final b = aimed ? (rule['b'] as String?) ?? r.pick(surfaces) : r.pick(surfaces);
    return {
      'do': 'hit',
      'a': r.chance(2) ? a : b,
      'b': r.chance(2) ? b : a,
      'strength': r.pick(<double>[0, 0.4, 1, 2, 3, 5, 10, 15, _nan]),
      'at': r.chance(2) || w.emitters.isEmpty
          ? null
          : r.next(w.emitters.length),
    };
  }
  if (pick < 90) {
    return {
      'do': 'advance',
      'by': r.pick(<double>[0, 0.1, 1]),
    };
  }
  return {'do': 'finishAll'};
}

Map<String, Object?> _setupFor(_Rng r, String kind) => switch (kind) {
  'music' => {
    'bus': r.pick([null, 'sfx']),
    'duck': r.pick(<double>[0.3, 0.5, 0]),
    'moves': [
      for (var i = r.next(5); i > 0; i--)
        {
          'from': r.pick([null, null, 'calm', 'fight', 'drift']),
          'to': r.pick(['calm', 'fight', 'drift']),
          'when': r.next(3),
          'fade': r.pick(<double>[0, 0.5, 1, 2]),
        },
    ],
  },
  'cues' => {
    'bus': r.pick([null, 'sfx']),
    'beds': r.pick([
      <String>[],
      <String>['bed.ogg'],
    ]),
    'release': r.pick(<double>[0.05, 0, 0.5]),
  },
  'contacts' => {
    'bus': r.pick([null, 'sfx']),
    'quiet': r.pick(<double>[0, 0.5, 1]),
    'loud': r.pick(<double>[2, 10, 20]),
    'limit': r.pick([1, 2, 4, 16]),
    'rules': [
      for (var i = 1 + r.next(4); i > 0; i--)
        {
          'a': r.pick(['wood', 'stone', 'glass', 'metal']),
          'b': r.pick([null, 'wood', 'stone', 'glass', 'metal']),
          'clip': 'c${r.next(100)}.ogg',
        },
    ],
  },
  _ => <String, Object?>{},
};

Map<String, Object?> _script(_Rng r, String kind, int steps) {
  final script = <String, Object?>{
    'op': 'script',
    'kind': kind,
    'listener': _listenerSpec(r),
    'emitters': [for (var i = r.next(4); i > 0; i--) _emitterSpec(r)],
    'setup': _setupFor(r, kind),
  };
  final world = _World(kind, script, script['setup']! as Map<String, Object?>);
  final done = <Map<String, Object?>>[];
  final expect = <Map<String, Object?>>[];
  for (var i = 0; i < steps; i++) {
    final step = switch (kind) {
      'audio' => _audioStepFor(r, world),
      'music' => _musicStepFor(r),
      'cues' => _cueStepFor(r),
      _ => _contactStepFor(r, world),
    };
    done.add(step);
    expect.add(world.run(step));
  }
  return {...script, 'steps': done, 'expect': expect};
}

List<Map<String, Object?>> _scripts() {
  final r = _Rng(9001);
  return [
    for (var i = 0; i < 40; i++) _script(r, 'audio', 8 + r.next(18)),
    for (var i = 0; i < 40; i++) _script(r, 'music', 8 + r.next(18)),
    for (var i = 0; i < 40; i++) _script(r, 'cues', 6 + r.next(12)),
    for (var i = 0; i < 30; i++) _script(r, 'contacts', 6 + r.next(14)),
  ];
}

List<Map<String, Object?>> _heard() {
  final r = _Rng(77);
  final odd = <double>[_nan, _inf, -_inf];
  return [for (var i = 0; i < 500; i++) _heardCase(r, odd)];
}

Map<String, Object?> _heardCase(_Rng r, List<double> odd) {
  final listener = _listenerSpec(r);
  final emitter = _emitterSpec(r);
  final position = emitter['pos']! as List<double>;
  if (r.chance(10)) {
    emitter['pos'] = List<double>.of(listener['pos']! as List<double>);
  } else if (r.chance(30)) {
    position[r.next(3)] = r.pick(odd);
  }
  final level = _heardBy(listener, emitter);
  return {
    'op': 'heardBy',
    'listener': listener,
    'emitter': emitter,
    'expect': _level(level),
  };
}

Level _heardBy(Map<String, Object?> listener, Map<String, Object?> emitter) {
  final heard = Listener();
  _placeListener(heard, listener);
  return _emitterFrom(emitter).heardBy(heard);
}

List<Map<String, Object?>> _curves() {
  final cases = <Map<String, Object?>>[];
  const distances = <double>[
    0,
    0.5,
    1,
    1.5,
    2,
    5,
    10,
    11,
    25,
    49.9,
    50,
    51,
    100,
    150,
    1e9,
    _nan,
    _inf,
    -1,
  ];
  for (final near in const <double>[0.5, 1, 2, 10]) {
    for (final far in const <double>[2, 11, 50, 200]) {
      if (far <= near) continue;
      for (final curve in FalloffCurve.values) {
        final falloff = Falloff(near: near, far: far, curve: curve);
        for (final d in distances) {
          cases.add({
            'op': 'falloff',
            'near': near,
            'far': far,
            'curve': curve.index,
            'distance': d,
            'expect': falloff.gainAt(d),
          });
        }
      }
    }
  }
  for (final cone in const [
    (0.0, 1.0, 0.2),
    (0.4, 1.2, 0.2),
    (1.0, 1.0, 0.0),
    (0.0, 0.0, 1.0),
    (0.3, math.pi, 0.5),
  ]) {
    final shape = Cone(inner: cone.$1, outer: cone.$2, outerGain: cone.$3);
    for (final angle in const <double>[
      0,
      0.2,
      0.4,
      0.8,
      1,
      1.01,
      1.2,
      2,
      3,
      math.pi,
      _nan,
      -1,
    ]) {
      cases.add({
        'op': 'cone',
        'inner': cone.$1,
        'outer': cone.$2,
        'gain': cone.$3,
        'angle': angle,
        'expect': shape.gainAt(angle),
      });
    }
  }
  return cases;
}

bool _throws(void Function() build) {
  try {
    build();
    return false;
  } on ArgumentError {
    return true;
  }
}

/// Which inputs the constructors refuse.
List<Map<String, Object?>> _refusals() {
  const values = <double>[0, 0.5, 1, 2, 50, -1, _nan, _inf, math.pi, 4];
  return [
    for (final near in values)
      for (final far in values)
        {
          'op': 'refuses',
          'what': 'falloff',
          'a': near,
          'b': far,
          'c': 0.0,
          'throws': _throws(() => Falloff(near: near, far: far)),
        },
    for (final inner in values)
      for (final outer in values)
        for (final gain in const <double>[0, 0.5, 1, 2, -1, _nan])
          {
            'op': 'refuses',
            'what': 'cone',
            'a': inner,
            'b': outer,
            'c': gain,
            'throws': _throws(
              () => Cone(inner: inner, outer: outer, outerGain: gain),
            ),
          },
    for (final doppler in values)
      {
        'op': 'refuses',
        'what': 'emitter',
        'a': doppler,
        'b': 0.0,
        'c': 0.0,
        'throws': _throws(
          () => Emitter(position: Vector3.zero(), doppler: doppler),
        ),
      },
    for (final bpm in values)
      for (final beats in const [4, 1, 0, -2])
        {
          'op': 'refuses',
          'what': 'track',
          'a': bpm,
          'b': beats.toDouble(),
          'c': 0.0,
          'throws': _throws(
            () => MusicTrack('a.ogg', bpm: bpm, beatsPerBar: beats),
          ),
        },
    for (final quiet in values)
      for (final loud in values)
        {
          'op': 'refuses',
          'what': 'contacts',
          'a': quiet,
          'b': loud,
          'c': 0.0,
          'throws': _throws(
            () => ContactSounds(
              Audio(RecordedAudio()),
              rules: const [],
              quiet: quiet,
              loud: loud,
            ),
          ),
        },
  ];
}

/// Usage: `dart run bin/audio.dart <repository root>`.
void main(List<String> args) {
  writeFixture(
    root: args.single,
    package: 'orblit_audio',
    name: 'audio',
    cases: [
      for (final c in [
        ..._curves(),
        ..._refusals(),
        ..._heard(),
        ..._scripts(),
      ])
        _encode(c)! as Map<String, Object?>,
    ],
  );
}

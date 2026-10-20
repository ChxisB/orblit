import 'dart:math';

import 'package:orblit_light/orblit_light.dart';
import 'package:orblit_parity_tool/fixture.dart';
import 'package:orblit_weather/orblit_weather.dart';
import 'package:vector_math/vector_math_64.dart';

const _nan = double.nan;
const _inf = double.infinity;

List<Object> _triple(double a, double b, double c) => [
  number(a),
  number(b),
  number(c),
];

List<Object> _vector(Vector3 v) => _triple(v.x, v.y, v.z);

List<Object> _tint(Tint t) => _triple(t.red, t.green, t.blue);

Map<String, Object?> _exposure(CameraExposure e) => {
  'aperture': number(e.aperture),
  'shutterSpeed': number(e.shutterSpeed),
  'sensitivity': number(e.sensitivity),
  'ev100': number(e.ev100),
};

Map<String, Object?> _sky(SkyState s) => {
  'body': s.body.label,
  'direction': _vector(s.direction),
  'altitude': number(s.altitude),
  'lightColour': _tint(s.lightColour),
  'power': number(s.power),
  'skyColour': _tint(s.skyColour),
  'ambient': number(s.ambient),
  'exposure': _exposure(s.exposure),
};

Map<String, Object?> _weather(WeatherState w) => {
  'cloudCover': number(w.cloudCover),
  'fogColour': _tint(w.fogColour),
  'fogDensity': number(w.fogDensity),
  'fogHeight': number(w.fogHeight),
  'fogFalloff': number(w.fogFalloff),
  'mist': number(w.mist),
  'mistSize': number(w.mistSize),
  'windSpeed': number(w.windSpeed),
  'rain': number(w.rain),
  'snow': number(w.snow),
  'lightning': number(w.lightning),
  'cloudHeight': number(w.cloudHeight),
  'isWet': w.isWet,
  'transmitted': number(w.transmitted),
  'spread': number(w.spread),
  'scattered': number(w.scattered),
  'greying': number(w.greying),
};

Map<String, Object?> _strike(Strike s) => {
  'flash': number(s.flash),
  'direction': _vector(s.direction),
  'seed': number(s.seed),
};

/// Every five minutes round the clock, then the hours that wrap, the ones on
/// either side of dawn and dusk, and the ones that are not hours.
List<Map<String, Object?>> _day() {
  final hours = <double>[
    for (var minute = 0; minute < 24 * 60; minute += 5) minute / 60,
    -1,
    -0.0,
    -0.25,
    -30,
    -1e-20,
    24,
    25,
    48.5,
    5.99,
    6,
    6.01,
    6.7,
    6.72,
    17.28,
    17.3,
    17.99,
    18,
    18.01,
    1e6,
    1e15,
    1e20,
    1e300,
    5e-324,
    _nan,
    _inf,
    -_inf,
  ];
  return [
    for (final hour in hours)
      {
        'op': 'day',
        'in': [number(hour)],
        'expect': _sky(DayCycle.at(hour)),
      },
  ];
}

/// A tenth of a decade at a time, then each break in the exposure ladder with a
/// value either side of it.
List<Map<String, Object?>> _exposures() {
  final lux = <double>[
    for (var i = -80; i <= 80; i++) pow(10, i / 8).toDouble(),
    0,
    -0.0,
    -1,
    1e-5,
    _nan,
    _inf,
    -_inf,
    1e300,
    // The film speed ceiling, the slowest shutter, the widest aperture, the
    // fastest aperture and the shortest shutter.
    0.57,
    0.5738,
    0.58,
    146,
    147,
    148,
    612,
    612.5,
    613,
    150000,
    151250,
    151251,
    4.8e6,
    4.84e6,
    4.85e6,
  ];
  return [
    for (final v in lux)
      {
        'op': 'exposure',
        'in': [number(v)],
        'expect': _exposure(CameraExposure.forIlluminance(v)),
      },
    {'op': 'daylight', 'in': [], 'expect': _exposure(CameraExposure.daylight)},
  ];
}

List<Map<String, Object?>> _presets() => [
  for (final condition in WeatherCondition.values)
    {
      'op': 'preset',
      'in': [condition.name],
      'expect': {
        'label': condition.label,
        'cloud': CloudKind.forCondition(condition).label,
        'state': _weather(WeatherState.of(condition)),
      },
    },
];

List<Map<String, Object?>> _labels() => [
  for (final body in CelestialBody.values)
    {
      'op': 'label',
      'in': ['body', body.name],
      'expect': body.label,
    },
  for (final kind in CloudKind.values)
    {
      'op': 'label',
      'in': ['kind', kind.name],
      'expect': kind.label,
    },
];

List<Map<String, Object?>> _lerps() {
  const steps = <double>[-1, 0, 0.25, 0.5, 1, 2, _nan];
  return [
    for (final from in WeatherCondition.values)
      for (final to in WeatherCondition.values)
        for (final t in steps)
          {
            'op': 'lerp',
            'in': [from.name, to.name, number(t)],
            'expect': _weather(
              WeatherState.lerp(WeatherState.of(from), WeatherState.of(to), t),
            ),
          },
  ];
}

List<Map<String, Object?>> _strikes() {
  const clocks = <double>[
    0,
    0.1,
    0.5,
    1,
    1.7,
    3,
    5.5,
    10,
    30,
    100.25,
    1000,
    12345.678,
    1e6,
    1e9,
    1e15,
    1e19,
    1e300,
    -0.0,
    -1,
    _nan,
    _inf,
    -_inf,
  ];
  const frequencies = <double>[
    0.05,
    0.2,
    0.6,
    1,
    2,
    5.2,
    6,
    0,
    -1,
    _nan,
    _inf,
    1e-9,
  ];
  return [
    for (final frequency in frequencies)
      for (final clock in clocks) ...[
        {
          'op': 'strike',
          'in': [number(clock), number(frequency)],
          'expect': attempt(() => _strike(Strike.at(clock, frequency))),
        },
        {
          'op': 'strikeAt',
          'in': [number(clock), number(frequency)],
          'expect': attempt(
            () => _strike(WeatherState.strikeAt(clock, frequency)),
          ),
        },
        {
          'op': 'flashAt',
          'in': [number(clock), number(frequency)],
          'expect': attempt(
            () => number(WeatherState.flashAt(clock, frequency)),
          ),
        },
      ],
    // A scan, because a strike lands in only some of the windows. Six is past
    // the rate where a window is shorter than the strike's own margin.
    for (final frequency in const <double>[0.3, 0.6, 1, 6])
      for (var tenth = 0; tenth < 600; tenth++)
        {
          'op': 'strike',
          'in': [number(tenth / 10), number(frequency)],
          'expect': attempt(() => _strike(Strike.at(tenth / 10, frequency))),
        },
  ];
}

List<Map<String, Object?>> _winds() => [
  for (final degrees in const <double>[
    0,
    45,
    90,
    180,
    270,
    360,
    -90,
    720.5,
    1e6,
    _nan,
    _inf,
    -_inf,
  ])
    {
      'op': 'wind',
      'in': [number(degrees)],
      'expect': {
        'x': number(WeatherState.windFrom(degrees).x),
        'z': number(WeatherState.windFrom(degrees).z),
      },
    },
];

void main(List<String> args) {
  writeFixture(
    root: args.single,
    package: 'orblit_weather',
    name: 'weather',
    cases: [
      ..._day(),
      ..._exposures(),
      ..._presets(),
      ..._labels(),
      ..._lerps(),
      ..._strikes(),
      ..._winds(),
    ],
  );
}

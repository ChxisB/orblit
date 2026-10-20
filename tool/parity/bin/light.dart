import 'package:orblit_light/orblit_light.dart';
import 'package:orblit_parity_tool/fixture.dart';
import 'package:vector_math/vector_math_64.dart';

const _nan = double.nan;
const _inf = double.infinity;

List<Object> _triple(double a, double b, double c) => [
  number(a),
  number(b),
  number(c),
];

List<Object> _vector(Vector3 v) => _triple(v.x, v.y, v.z);

/// Values on both sides of every break in the curves, and the ones that are
/// not colours.
const _channels = <double>[
  0,
  0.0031308,
  0.02,
  0.04045,
  0.5,
  1,
  4,
  -1,
  _nan,
  _inf,
  -_inf,
];

List<Map<String, Object?>> _photometry() {
  const values = <double>[0, 1, 3, 100, 1000, 12566, -5, 1e308, _nan, _inf];
  final single = <String, double Function(double)>{
    'wattsToLumens': Photometry.wattsToLumens,
    'irradianceToLux': Photometry.irradianceToLux,
    'lumensToCandela': Photometry.lumensToCandela,
    'candelaToLumens': Photometry.candelaToLumens,
  };
  return [
    for (final MapEntry(key: name, value: fn) in single.entries)
      for (final v in values)
        {
          'op': 'photometry',
          'fn': name,
          'in': [number(v)],
          'expect': number(fn(v)),
        },
    for (final lumens in values)
      for (final cutoff in const <double>[0.1, 1, 0.01, 0, -1, _nan])
        {
          'op': 'photometry',
          'fn': 'influenceRadius',
          'in': [number(lumens), number(cutoff)],
          'expect': number(
            Photometry.influenceRadius(lumens, cutoffLux: cutoff),
          ),
        },
  ];
}

List<Map<String, Object?>> _tints() {
  final cases = <Map<String, Object?>>[];
  for (final r in _channels) {
    final tint = Tint(r, r / 2, 1 - r);
    cases.add({
      'op': 'tint',
      'in': _triple(tint.red, tint.green, tint.blue),
      'linear': _vector(tint.linear),
      'string': tint.toString(),
    });
    final back = Tint.fromLinear(Vector3(r, r / 2, 1 - r));
    cases.add({
      'op': 'fromLinear',
      'in': _triple(r, r / 2, 1 - r),
      'expect': _triple(back.red, back.green, back.blue),
    });
  }
  for (final value in const [0, 0xFF8A3D, 0x3D8AFF, 0xFFFFFF, 0x010203]) {
    final tint = Tint.hex(value);
    cases.add({
      'op': 'hex',
      'in': value,
      'expect': _triple(tint.red, tint.green, tint.blue),
    });
  }
  const from = Tint(0.1, 0.5, 0.9);
  const to = Tint(0.9, 0.4, 0);
  for (final t in const <double>[0, 0.25, 0.5, 1, -1, 2, _nan]) {
    final mixed = Tint.lerp(from, to, t);
    cases.add({
      'op': 'lerp',
      't': number(t),
      'expect': _triple(mixed.red, mixed.green, mixed.blue),
    });
  }
  return cases;
}

/// What an artist can set, with the awkward values mixed in: a negative or
/// huge cone, a blend outside zero to one, NaN, a size of nothing.
const _powers = <double?>[null, 0, -5, 3, 100, 1000, 5e5];
const _radii = <double>[0.1, 0, 2.5];
const _sunAngles = <double>[0.526, 0, 1, 10];
const _spotSizes = <double>[-10, 0, 45, 60, 180, 400, _nan];
const _blends = <double>[-1, 0, 0.15, 0.25, 1, 2, _nan];
const _sizeXs = <double>[1, 2, 3, 0.5, 0, -1];
const _sizeYs = <double>[1, 0.5, 3, 99];
const _distances = <double?>[null, null, 12, 0];

Map<String, Object?> _render({
  required LightType type,
  required double? power,
  required double radius,
  required double sunAngle,
  required double spotSize,
  required double spotBlend,
  required AreaShape shape,
  required double sizeX,
  required double sizeY,
  required double? customDistance,
  required bool castShadows,
}) {
  final color = Vector3(1, 0.5, 0.2);
  final light = Light(
    type: type,
    color: color,
    power: power,
    radius: radius,
    sunAngle: sunAngle,
    spotSize: spotSize,
    spotBlend: spotBlend,
    shape: shape,
    sizeX: sizeX,
    sizeY: sizeY,
    customDistance: customDistance,
    castShadows: castShadows,
  );
  final r = light.toRenderer();
  return {
    'op': 'render',
    'type': type.index,
    'power': power == null ? null : number(power),
    'radius': number(radius),
    'sunAngle': number(sunAngle),
    'spotSize': number(spotSize),
    'spotBlend': number(spotBlend),
    'shape': shape.index,
    'sizeX': number(sizeX),
    'sizeY': number(sizeY),
    'customDistance': customDistance == null ? null : number(customDistance),
    'castShadows': castShadows,
    'expect': {
      'kind': r.kind.index,
      'color': _vector(r.color),
      'intensity': number(r.intensity),
      'falloffRadius': number(r.falloffRadius),
      'innerConeAngle': number(r.innerConeAngle),
      'outerConeAngle': number(r.outerConeAngle),
      'sunAngularRadius': number(r.sunAngularRadius),
      'sourceRadius': number(r.sourceRadius),
      'castShadows': r.castShadows,
      'width': number(r.width),
      'height': number(r.height),
      'approximated': r.approximated,
      'emittingArea': number(light.emittingArea),
    },
  };
}

List<Map<String, Object?>> _lights() {
  var seed = 4242;
  int next(int below) {
    seed = (seed * 1103515245 + 12345) & 0x7fffffff;
    return (seed >> 8) % below;
  }

  T pick<T>(List<T> from) => from[next(from.length)];

  return [
    // Every type with every shape at the defaults, so no pair is missed.
    for (final type in LightType.values)
      for (final shape in AreaShape.values)
        _render(
          type: type,
          power: null,
          radius: 0.1,
          sunAngle: 0.526,
          spotSize: 45,
          spotBlend: 0.15,
          shape: shape,
          sizeX: 2,
          sizeY: 3,
          customDistance: null,
          castShadows: true,
        ),
    for (var i = 0; i < 250; i++)
      _render(
        type: pick(LightType.values),
        power: pick(_powers),
        radius: pick(_radii),
        sunAngle: pick(_sunAngles),
        spotSize: pick(_spotSizes),
        spotBlend: pick(_blends),
        shape: pick(AreaShape.values),
        sizeX: pick(_sizeXs),
        sizeY: pick(_sizeYs),
        customDistance: pick(_distances),
        castShadows: next(2) == 0,
      ),
  ];
}

/// Usage: `dart run bin/light.dart <repository root>`.
void main(List<String> args) {
  writeFixture(
    root: args.single,
    package: 'orblit_light',
    name: 'light',
    cases: [..._photometry(), ..._tints(), ..._lights()],
  );
}

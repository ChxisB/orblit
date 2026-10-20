import 'package:orblit_noise/orblit_noise.dart';
import 'package:orblit_parity_tool/fixture.dart';

/// The six fields `orblit_noise`'s own tests use, under the same names.
const _gradient = GradientNoise(seed: 7);
const _fields = <String, Noise>{
  'value': ValueNoise(seed: 7),
  'gradient': _gradient,
  'fractal': FractalNoise(_gradient),
  'ridged': RidgedNoise(_gradient),
  'cells': CellNoise(seed: 3),
  'tiling': TilingNoise(_gradient, period: 8),
};

/// Lattice points, a negative that rounds up to the period, and a walk across
/// fractions and signs.
final _points = <List<double>>[
  [0, 0, 0],
  [1, 2, 3],
  [-3, -3, -3],
  [0.5, 0.5, 0.5],
  [-1e-20, 2.5, 1.5],
  for (var i = 0; i < 60; i++) [i * 0.37 - 7, i * 0.29 - 5, i * 0.11 + 1.5],
];

/// Usage: `dart run bin/noise.dart <repository root>`.
void main(List<String> args) {
  writeFixture(
    root: args.single,
    package: 'orblit_noise',
    name: 'noise',
    cases: [
      for (final MapEntry(key: name, value: field) in _fields.entries)
        for (final p in _points)
          {'field': name, 'at': p, 'expect': field.at(p[0], p[1], p[2])},
    ],
  );
}

import 'dart:math' as math;

/// [values] read as [stride] numbers per stop, spread evenly over 0 to 1.
double rampAt(List<double> values, int stride, int channel, double fraction) {
  final last = values.length ~/ stride - 1;
  if (last == 0) return values[channel];
  final at = fraction.clamp(0.0, 1.0) * last;
  final stop = math.min(at.floor(), last - 1);
  final across = at - stop;
  return values[stop * stride + channel] * (1 - across) +
      values[(stop + 1) * stride + channel] * across;
}

import 'dart:math' as math;
import 'dart:typed_data';

/// A 16 bit mono sine as a WAV file, with the peak a full decode should reach,
/// which is the amplitude after the 16 bit rounding.
({Uint8List bytes, double peak}) sineWav({
  double seconds = 0.5,
  int rate = 48000,
  double amplitude = 0.5,
  double hertz = 440,
}) {
  final count = (seconds * rate).round();
  final data = ByteData(44 + count * 2);
  void tag(int at, String text) {
    for (var i = 0; i < 4; i++) {
      data.setUint8(at + i, text.codeUnitAt(i));
    }
  }

  tag(0, 'RIFF');
  data.setUint32(4, 36 + count * 2, Endian.little);
  tag(8, 'WAVE');
  tag(12, 'fmt ');
  data.setUint32(16, 16, Endian.little);
  data.setUint16(20, 1, Endian.little);
  data.setUint16(22, 1, Endian.little);
  data.setUint32(24, rate, Endian.little);
  data.setUint32(28, rate * 2, Endian.little);
  data.setUint16(32, 2, Endian.little);
  data.setUint16(34, 16, Endian.little);
  tag(36, 'data');
  data.setUint32(40, count * 2, Endian.little);
  var peak = 0;
  for (var i = 0; i < count; i++) {
    final sample =
        (amplitude * 32767 * math.sin(2 * math.pi * hertz * i / rate)).round();
    peak = math.max(peak, sample.abs());
    data.setInt16(44 + i * 2, sample, Endian.little);
  }
  return (bytes: data.buffer.asUint8List(), peak: peak / 32768);
}

/// The largest absolute sample on one channel of interleaved stereo.
double peakOf(Float32List samples, {required int channel}) {
  var peak = 0.0;
  for (var i = channel; i < samples.length; i += 2) {
    peak = math.max(peak, samples[i].abs());
  }
  return peak;
}

import 'dart:typed_data';

/// The particles alive at one moment, as flat arrays a renderer can read
/// without unpacking. Slot `i` of every array is the same particle.
final class ParticleBuffer {
  ParticleBuffer(this.capacity)
    : positions = Float32List(capacity * 3),
      sizes = Float32List(capacity),
      colours = Float32List(capacity * 3),
      angles = Float32List(capacity) {
    if (capacity < 1) {
      throw ArgumentError.value(capacity, 'capacity', 'must be 1 or more');
    }
  }

  final int capacity;

  /// x, y, z per particle.
  final Float32List positions;

  /// Edge length per particle.
  final Float32List sizes;

  /// r, g, b per particle.
  final Float32List colours;

  /// Turn about the view axis in radians, per particle.
  final Float32List angles;

  /// How many slots hold a live particle.
  int count = 0;
}

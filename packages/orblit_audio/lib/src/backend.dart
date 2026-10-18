/// How a voice sounds once the mixer has decided. [gain] is linear and
/// already includes the voice, its buses and its distance. [pitch] is a
/// playback rate and includes Doppler. [pan] runs from -1 (left) to 1 (right).
final class Level {
  const Level({this.gain = 1, this.pitch = 1, this.pan = 0});

  final double gain;
  final double pitch;
  final double pan;

  @override
  bool operator ==(Object other) =>
      other is Level &&
      other.gain == gain &&
      other.pitch == pitch &&
      other.pan == pan;

  @override
  int get hashCode => Object.hash(gain, pitch, pan);

  @override
  String toString() => 'Level(gain: $gain, pitch: $pitch, pan: $pan)';
}

/// What [Audio] needs from a device. All the arithmetic stays above this
/// line, so a backend plays the numbers it is given and nothing else.
abstract interface class AudioBackend {
  /// Begins playing [clip] as [voice]. The id is the caller's and is never
  /// reused. A backend that cannot play the clip either throws, and the voice
  /// is dropped, or returns and answers false from [isPlaying], and the voice
  /// ends at the next advance.
  void start(
    int voice,
    String clip, {
    required bool loop,
    required Level level,
  });

  /// Changes how a playing voice sounds. Only called for a voice that is
  /// playing.
  void update(int voice, Level level);

  /// Ends a voice. Only called for a voice that is playing.
  void stop(int voice);

  /// Whether [voice] is still sounding. It is true from the moment [start]
  /// returns, even if the device has not confirmed the start yet, and false
  /// once the clip has played to its end. It is asked of every voice on every
  /// advance, so a backend keeps the answer cheap.
  bool isPlaying(int voice);
}

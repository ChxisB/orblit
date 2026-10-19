/// Thrown when there is no output device to open, or when a clip cannot be
/// started and no handler was given for it.
final class AudioDeviceException implements Exception {
  const AudioDeviceException(this.message);

  final String message;

  @override
  String toString() => 'AudioDeviceException: $message';
}

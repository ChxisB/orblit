/// Plays an `orblit_audio` game on a real output device, natively through
/// miniaudio and in a browser through Web Audio.
library;

export 'src/exception.dart';
export 'src/device_audio_native.dart'
    if (dart.library.js_interop) 'src/device_audio_web.dart';

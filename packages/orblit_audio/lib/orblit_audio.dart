/// Every sound call a game makes, run without a speaker.
///
/// The mixer, the fades, the falloff and the Doppler shift are plain Dart. A
/// backend is given the final numbers for each voice and plays them. That
/// split lets every test of sound run against [RecordedAudio], which is also
/// how a cutscene's sound cues can be checked in order.
///
/// Time here is sampled, like the rest of Orblit. A call starts a voice. The
/// world changes what it sounds like when [Audio.advance] is called, and a
/// frame of no time is a valid advance.
library;

export 'src/audio.dart' show Audio, Voice;
export 'src/backend.dart' show AudioBackend, Level;
export 'src/mixer.dart' show Bus, Mixer;
export 'src/recorded.dart'
    show AudioCall, AudioStarted, AudioStopped, AudioUpdated, RecordedAudio;
export 'src/spatial.dart'
    show Cone, Emitter, Falloff, FalloffCurve, Listener, Motion, speedOfSound;

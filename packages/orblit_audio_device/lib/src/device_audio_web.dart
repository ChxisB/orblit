import 'dart:async';
import 'dart:js_interop';
import 'dart:typed_data';

import 'package:orblit_audio/orblit_audio.dart';
import 'package:web/web.dart' hide Float32List;

import 'exception.dart';

/// One voice, from the call to start until its clip ends or it is stopped.
/// The source is null until the clip has been fetched and decoded.
final class _Playing {
  _Playing({required this.loop, required this.level});

  final bool loop;
  Level level;
  AudioBufferSourceNode? source;
  GainNode? gain;
  StereoPannerNode? pan;
}

/// An [AudioBackend] over Web Audio.
///
/// A clip is a URL, under [directory] when one is given. It is fetched and
/// decoded on first use and shared by every voice playing it. A voice counts
/// as playing from [start], though it is silent until its clip has loaded, so
/// a failed load reaches [onError], or the zone as an uncaught error, and
/// never [start] itself. A browser keeps the context suspended until the page
/// has had a gesture, so start the first sound from a click or a key.
final class DeviceAudio implements AudioBackend {
  DeviceAudio.open({this.directory = '', this.onError})
    : _context = AudioContext();

  /// A context that mixes [frames] of audio with no device when [render] is
  /// called, once. A test hears nothing and reads every sample.
  DeviceAudio.offline({
    required int frames,
    int sampleRate = 48000,
    this.directory = '',
    this.onError,
  }) : _context = OfflineAudioContext(2.toJS, frames, sampleRate);

  final String directory;

  /// Called with the clip and the reason when a load fails, and the voice
  /// then ends.
  final void Function(String clip, String reason)? onError;

  final BaseAudioContext _context;
  final Map<int, _Playing> _voices = {};
  final Map<String, Future<AudioBuffer>> _buffers = {};
  final Set<Future<void>> _loading = {};

  @override
  void start(
    int voice,
    String clip, {
    required bool loop,
    required Level level,
  }) {
    final playing = _Playing(loop: loop, level: level);
    _voices[voice] = playing;
    final context = _context;
    if (context.isA<AudioContext>() && context.state == 'suspended') {
      unawaited((context as AudioContext).resume().toDart);
    }
    late final Future<void> loading;
    loading = _load(
      voice,
      clip,
      playing,
    ).whenComplete(() => _loading.remove(loading));
    _loading.add(loading);
  }

  @override
  void update(int voice, Level level) {
    final playing = _voices[voice];
    if (playing == null) return;
    playing.level = level;
    final now = _context.currentTime;
    // A time constant of about one frame at 60 Hz, so a level that changes
    // each frame glides rather than steps.
    const glide = 0.015;
    playing.gain?.gain.setTargetAtTime(level.gain, now, glide);
    playing.pan?.pan.setTargetAtTime(level.pan, now, glide);
    playing.source?.playbackRate.setTargetAtTime(level.pitch, now, glide);
  }

  @override
  void stop(int voice) {
    final playing = _voices.remove(voice);
    // A clip still loading finds itself gone and never starts.
    playing?.source?.stop();
  }

  @override
  bool isPlaying(int voice) => _voices.containsKey(voice);

  /// Mixes everything started so far into interleaved stereo. An offline
  /// context renders once, so this can be called once.
  Future<Float32List> render() async {
    final context = _context;
    if (!context.isA<OfflineAudioContext>()) {
      throw StateError('only an offline DeviceAudio renders');
    }
    await Future.wait(_loading.toList());
    final buffer = await (context as OfflineAudioContext)
        .startRendering()
        .toDart;
    final left = buffer.getChannelData(0).toDart;
    final right = buffer.getChannelData(1).toDart;
    final out = Float32List(left.length * 2);
    for (var i = 0; i < left.length; i++) {
      out[i * 2] = left[i];
      out[i * 2 + 1] = right[i];
    }
    return out;
  }

  /// Stops every voice and closes the context. Nothing may be called after.
  void dispose() {
    for (final voice in _voices.keys.toList()) {
      stop(voice);
    }
    final context = _context;
    if (context.isA<AudioContext>()) {
      unawaited((context as AudioContext).close().toDart);
    }
  }

  Future<void> _load(int voice, String clip, _Playing playing) async {
    try {
      final buffer = await _buffer(clip);
      // Stopped, or ended, while the clip was loading.
      if (_voices[voice] != playing) return;
      _attach(voice, playing, buffer);
    } on Object catch (e) {
      _voices.remove(voice);
      // A later start tries the fetch again.
      unawaited(_buffers.remove(clip));
      final handler = onError;
      if (handler == null) rethrow;
      handler(clip, '$e');
    }
  }

  Future<AudioBuffer> _buffer(String clip) =>
      _buffers.putIfAbsent(clip, () => _fetch(clip));

  Future<AudioBuffer> _fetch(String clip) async {
    final url = directory.isEmpty ? clip : '$directory/$clip';
    final response = await window.fetch(url.toJS).toDart;
    if (!response.ok) throw AudioDeviceException('$url: ${response.status}');
    final bytes = await response.arrayBuffer().toDart;
    return _context.decodeAudioData(bytes).toDart;
  }

  void _attach(int voice, _Playing playing, AudioBuffer buffer) {
    final source = _context.createBufferSource()
      ..buffer = buffer
      ..loop = playing.loop;
    final gain = _context.createGain();
    final pan = _context.createStereoPanner();
    source.connect(gain);
    gain.connect(pan);
    pan.connect(_context.destination);
    gain.gain.value = playing.level.gain;
    pan.pan.value = playing.level.pan;
    source.playbackRate.value = playing.level.pitch;
    source.onended = ((Event _) {
      if (_voices[voice] == playing) _voices.remove(voice);
      source.disconnect();
    }).toJS;
    playing
      ..source = source
      ..gain = gain
      ..pan = pan;
    source.start();
  }
}

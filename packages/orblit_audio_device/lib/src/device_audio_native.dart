import 'dart:ffi';
import 'dart:io';
import 'dart:typed_data';

import 'package:ffi/ffi.dart';
import 'package:orblit_audio/orblit_audio.dart';

import 'bindings.dart' as native;
import 'exception.dart';

/// An [AudioBackend] that plays through the machine's output device.
///
/// A clip is a path to a WAV, FLAC, MP3 or Ogg Vorbis file, under
/// [directory] when one is given. A file larger than [streamAbove] bytes is
/// read as it plays, which suits music. A smaller one is decoded once and
/// shared by every voice playing it, which suits an effect.
final class DeviceAudio implements AudioBackend {
  /// Opens the default output device. Throws [AudioDeviceException] when
  /// there is none, such as on a server.
  DeviceAudio.open({
    this.directory = '',
    this.streamAbove = defaultStreamAbove,
    this.onError,
  }) : sampleRate = null,
       _engine = _create(offline: false, sampleRate: 0);

  /// An engine with no device, mixed by [render]. A test hears nothing and
  /// reads every sample.
  DeviceAudio.offline({
    int sampleRate = 48000,
    this.directory = '',
    this.streamAbove = defaultStreamAbove,
    this.onError,
  }) : sampleRate = sampleRate,
       _engine = _create(offline: true, sampleRate: sampleRate);

  static const int defaultStreamAbove = 1 << 20;

  final String directory;
  final int streamAbove;

  /// Called with the clip and the reason when a start fails, and the voice
  /// then never plays. Without it the failure is thrown from [start].
  final void Function(String clip, String reason)? onError;

  /// Frames per second of [render]. Only an offline engine has one.
  final int? sampleRate;

  final Pointer<native.OrblitAudioStruct> _engine;
  final Map<String, bool> _streams = {};

  @override
  void start(
    int voice,
    String clip, {
    required bool loop,
    required Level level,
  }) {
    final path = directory.isEmpty ? clip : '$directory/$clip';
    final String? failure = _tryStart(voice, path, loop, level);
    if (failure == null) return;
    final handler = onError;
    if (handler == null) throw AudioDeviceException('$clip: $failure');
    handler(clip, failure);
  }

  @override
  void update(int voice, Level level) =>
      native.audioUpdate(_engine, voice, level.gain, level.pitch, level.pan);

  @override
  void stop(int voice) => native.audioStop(_engine, voice);

  @override
  bool isPlaying(int voice) => native.audioIsPlaying(_engine, voice) != 0;

  /// Mixes the next [frames] of an offline engine as interleaved stereo.
  Float32List render(int frames) {
    final out = calloc<Float>(frames * 2);
    try {
      final written = native.audioRender(_engine, out, frames);
      return Float32List.fromList(out.asTypedList(written * 2));
    } finally {
      calloc.free(out);
    }
  }

  /// Stops every voice and closes the device. Nothing may be called after.
  void dispose() => native.audioDestroy(_engine);

  String? _tryStart(int voice, String path, bool loop, Level level) {
    final bool stream;
    try {
      stream = _streams.putIfAbsent(
        path,
        () => File(path).lengthSync() > streamAbove,
      );
    } on FileSystemException catch (e) {
      return e.osError?.message ?? e.message;
    }
    final name = path.toNativeUtf8();
    try {
      final result = native.audioStart(
        _engine,
        voice,
        name.cast(),
        loop ? 1 : 0,
        stream ? 1 : 0,
        level.gain,
        level.pitch,
        level.pan,
      );
      return result == 0 ? null : _describe(result);
    } finally {
      calloc.free(name);
    }
  }

  static Pointer<native.OrblitAudioStruct> _create({
    required bool offline,
    required int sampleRate,
  }) {
    final result = calloc<Int32>();
    try {
      final engine = native.audioCreate(offline ? 1 : 0, sampleRate, result);
      if (engine == nullptr) {
        throw AudioDeviceException(
          'no output device: ${_describe(result.value)}',
        );
      }
      return engine;
    } finally {
      calloc.free(result);
    }
  }

  static String _describe(int result) =>
      native.audioDescribe(result).cast<Utf8>().toDartString();
}

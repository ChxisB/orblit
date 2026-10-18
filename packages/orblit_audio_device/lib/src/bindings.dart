// Raw FFI declarations for orblit_audio_device.h. Nothing here interprets a
// result or owns a lifetime. See device_audio_native.dart for that.

import 'dart:ffi';

const String kOrblitAudioAsset =
    'package:orblit_audio_device/orblit_audio_device';

final class OrblitAudioStruct extends Opaque {}

@Native<Pointer<OrblitAudioStruct> Function(Int32, Uint32, Pointer<Int32>)>(
  symbol: 'orblit_audio_create',
  assetId: kOrblitAudioAsset,
)
external Pointer<OrblitAudioStruct> audioCreate(
  int offline,
  int sampleRate,
  Pointer<Int32> result,
);

@Native<Void Function(Pointer<OrblitAudioStruct>)>(
  symbol: 'orblit_audio_destroy',
  assetId: kOrblitAudioAsset,
)
external void audioDestroy(Pointer<OrblitAudioStruct> audio);

@Native<
  Int32 Function(
    Pointer<OrblitAudioStruct>,
    Int64,
    Pointer<Char>,
    Int32,
    Int32,
    Float,
    Float,
    Float,
    Float,
  )
>(symbol: 'orblit_audio_start', assetId: kOrblitAudioAsset)
external int audioStart(
  Pointer<OrblitAudioStruct> audio,
  int voice,
  Pointer<Char> path,
  int loop,
  int stream,
  double gain,
  double pitch,
  double pan,
  double offset,
);

@Native<Void Function(Pointer<OrblitAudioStruct>, Int64, Float, Float, Float)>(
  symbol: 'orblit_audio_update',
  assetId: kOrblitAudioAsset,
)
external void audioUpdate(
  Pointer<OrblitAudioStruct> audio,
  int voice,
  double gain,
  double pitch,
  double pan,
);

@Native<Void Function(Pointer<OrblitAudioStruct>, Int64)>(
  symbol: 'orblit_audio_stop',
  assetId: kOrblitAudioAsset,
)
external void audioStop(Pointer<OrblitAudioStruct> audio, int voice);

@Native<Int32 Function(Pointer<OrblitAudioStruct>, Int64)>(
  symbol: 'orblit_audio_is_playing',
  assetId: kOrblitAudioAsset,
)
external int audioIsPlaying(Pointer<OrblitAudioStruct> audio, int voice);

@Native<Uint64 Function(Pointer<OrblitAudioStruct>, Pointer<Float>, Uint64)>(
  symbol: 'orblit_audio_render',
  assetId: kOrblitAudioAsset,
)
external int audioRender(
  Pointer<OrblitAudioStruct> audio,
  Pointer<Float> out,
  int frames,
);

@Native<Pointer<Char> Function(Int32)>(
  symbol: 'orblit_audio_describe',
  assetId: kOrblitAudioAsset,
)
external Pointer<Char> audioDescribe(int result);

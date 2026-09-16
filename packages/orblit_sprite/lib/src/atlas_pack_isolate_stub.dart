import 'atlas_pack.dart';

/// Packs on the caller's own isolate.
///
/// A browser has no [Isolate] to hand this to, so the best this can do is
/// what [packAtlas] already does on any platform: stay synchronous, and let
/// the caller decide when to run it — between frames, or from a Web Worker
/// of its own that this package cannot know about.
Future<AtlasPackResult> packAtlasInBackground(
  List<AtlasSprite> sprites, [
  AtlasPackOptions options = const AtlasPackOptions(),
]) async => packAtlas(sprites, options);

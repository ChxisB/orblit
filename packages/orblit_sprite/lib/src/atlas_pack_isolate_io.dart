import 'dart:isolate';

import 'atlas_pack.dart';

/// Packs on a fresh [Isolate], so a folder of sprites large enough to take
/// real time — the offline cooker's job, but just as much a game importing
/// sprites of its own at run time — never stalls the thread that asked for
/// it.
Future<AtlasPackResult> packAtlasInBackground(
  List<AtlasSprite> sprites, [
  AtlasPackOptions options = const AtlasPackOptions(),
]) => Isolate.run(() => packAtlas(sprites, options));

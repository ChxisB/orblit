import 'package:flutter/services.dart';

/// Bytes handed to the renderer by name, looked for before the disk.
///
/// A scene names its assets as paths — an object's mesh, a material's
/// textures, an environment, a decal, a splat capture — and on a desktop
/// reading them off disk is fine. Almost everywhere else it is not: a browser
/// has no disk, an Android application's assets are inside its archive, and
/// anything fetched over a network or out of a cache is already bytes in
/// memory. [provide] puts bytes under a name, and from then on every renderer
/// in the application looks there first — for the files a `.gltf` names beside
/// itself as well.
///
/// ```dart
/// final data = await rootBundle.load('assets/robot.glb');
/// final name = OrblitResources.nameFor('robot.glb');
/// await OrblitResources.provide(name, data.buffer.asUint8List());
/// // ...and then OrblitObject(mesh: name, ...)
/// ```
///
/// A name stands for bytes that do not change. Providing it again replaces
/// what the next load sees, but a renderer does not load again what it already
/// has, so the way to change an asset is to give the new bytes a new name — a
/// content hash in it is the natural one. A name looked for before it was
/// provided is looked for again once it has been, so a scene may arrive before
/// its bytes; it draws without them until they do, so provide first wherever
/// the order is yours.
abstract final class OrblitResources {
  static const MethodChannel _channel = MethodChannel(
    'orblit_filament/resources',
  );

  /// The prefix [nameFor] puts on a name. Not a path anybody's assets are
  /// likely to sit under, and the renderer's own spelling beside the
  /// `orblit:target/` it already reserves for render targets.
  static const String scheme = 'orblit:resource/';

  /// [path] as a resource name: `robot.glb` is `orblit:resource/robot.glb`.
  static String nameFor(String path) => '$scheme$path';

  /// Keeps [bytes] under [name] for every renderer in the application.
  ///
  /// Copied on the way in, so [bytes] may be reused or dropped as soon as
  /// this completes. On Apple platforms and Android the copy happens off the
  /// platform thread.
  static Future<void> provide(String name, Uint8List bytes) {
    if (name.isEmpty) {
      throw ArgumentError.value(name, 'name', 'a resource needs a name');
    }
    return _channel.invokeMethod<void>('provide', {
      'name': name,
      'bytes': bytes,
    });
  }

  /// Lets go of the bytes under [name], and says whether there were any.
  /// Whatever a renderer already loaded from them it keeps.
  static Future<bool> release(String name) async {
    if (name.isEmpty) {
      throw ArgumentError.value(name, 'name', 'a resource needs a name');
    }
    return await _channel.invokeMethod<bool>('release', {'name': name}) ??
        false;
  }
}

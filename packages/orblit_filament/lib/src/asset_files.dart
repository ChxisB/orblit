import 'package:flutter/foundation.dart';
import 'package:flutter/services.dart';

/// A bundled asset as a place in a file: where to open, where it starts and
/// how long it is.
@immutable
final class OrblitAssetFile {
  const OrblitAssetFile({
    required this.path,
    required this.offset,
    required this.length,
  }) : assert(offset >= 0),
       assert(length >= 0);

  /// A path the process can open and seek in.
  final String path;

  /// Where the asset begins in the file at [path].
  final int offset;

  /// How many bytes the asset is.
  final int length;

  @override
  bool operator ==(Object other) =>
      other is OrblitAssetFile &&
      other.path == path &&
      other.offset == offset &&
      other.length == length;

  @override
  int get hashCode => Object.hash(path, offset, length);

  @override
  String toString() => 'OrblitAssetFile($path, $offset, $length)';
}

/// Where an Android application's assets sit in its package, for readers that
/// seek and so never hold a large asset in memory.
///
/// Every other platform keeps a bundled asset as a file beside the
/// executable, where a plain `dart:io` path finds it. Android keeps assets
/// inside the APK, and `rootBundle` can only hand over all of one as bytes.
/// For a game pack of hundreds of megabytes that is the whole pack in memory
/// before the first asset is drawn. [of] asks the plugin where the asset
/// sits in the APK instead.
///
/// The asset has to be stored without compression, which is the app's
/// `noCompress` list in `android/app/build.gradle.kts`:
///
/// ```kotlin
/// android { androidResources { noCompress += "opack" } }
/// ```
///
/// A compressed asset is copied to the cache folder by the plugin and the
/// range is the whole copy, which works and logs a warning.
///
/// Nothing here depends on how the bytes are read. A caller seeks to
/// [OrblitAssetFile.offset] in [OrblitAssetFile.path] and reads up to
/// [OrblitAssetFile.length].
abstract final class OrblitAssetFiles {
  static const MethodChannel _channel = MethodChannel(
    'orblit_filament/resources',
  );

  /// Where [key] sits, or null where there is nothing to ask: on any
  /// platform but Android, and on Android for a key the application does not
  /// have.
  ///
  /// [key] is the one `rootBundle` takes, so a package's asset is
  /// `packages/<name>/<path>`. The answer holds a descriptor open on the
  /// plugin's side until [release] or until the engine detaches.
  static Future<OrblitAssetFile?> of(String key) async {
    _check(key);
    if (!_isAndroid) return null;
    try {
      final reply = await _channel.invokeMapMethod<String, Object?>(
        'assetFile',
        {'key': key},
      );
      return _parse(reply);
    } on PlatformException catch (e) {
      if (e.code == 'not-found') return null;
      rethrow;
    }
  }

  /// Lets go of what [of] kept open for [key], and says whether it held
  /// anything.
  static Future<bool> release(String key) async {
    _check(key);
    if (!_isAndroid) return false;
    return await _channel.invokeMethod<bool>('releaseAssetFile', {
          'key': key,
        }) ??
        false;
  }

  static bool get _isAndroid =>
      !kIsWeb && defaultTargetPlatform == TargetPlatform.android;

  static void _check(String key) {
    if (key.isEmpty) {
      throw ArgumentError.value(key, 'key', 'an asset needs a key');
    }
  }

  static OrblitAssetFile _parse(Map<String, Object?>? reply) {
    final path = reply?['path'];
    final offset = reply?['offset'];
    final length = reply?['length'];
    if (path is! String || offset is! int || length is! int) {
      throw StateError('The plugin answered an asset file with $reply.');
    }
    return OrblitAssetFile(path: path, offset: offset, length: length);
  }
}

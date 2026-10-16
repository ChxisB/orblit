// The half of the conditional export in bundled_pack.dart for platforms with a
// file system.
import 'dart:io';

import 'pack_file_io.dart';

/// Where Flutter keeps the assets of a built app, as a folder, worked out
/// from the path of the running program. Null when this platform does not
/// keep them in one, or the program is not laid out as a Flutter app.
///
/// [executable] is `Platform.resolvedExecutable` and [platform] is
/// `Platform.operatingSystem`. They are arguments so the layouts can be
/// checked on any machine. The layouts are Flutter's to change. Each one was
/// read from an app built on that platform:
///
/// - macOS: `<App>.app/Contents/MacOS/<exe>`, assets in
///   `Contents/Frameworks/App.framework/Resources/flutter_assets`.
/// - iOS: `<App>.app/<exe>`, assets in
///   `Frameworks/App.framework/flutter_assets`.
/// - Linux and Windows: `<bundle>/<exe>`, assets in
///   `data/flutter_assets`.
///
/// Android keeps assets inside the APK, which has no path. Nothing here
/// answers for it.
String? flutterAssetsDirectory({
  required String executable,
  required String platform,
}) {
  final separator = platform == 'windows' ? r'\' : '/';
  final parts = executable.split(separator);
  if (parts.length < 2) return null;
  final folder = parts.sublist(0, parts.length - 1);
  final List<String> assets;
  switch (platform) {
    case 'macos':
      final inContents = folder.length >= 2 && folder.last == 'MacOS';
      if (!inContents || folder[folder.length - 2] != 'Contents') return null;
      assets = [
        ...folder.sublist(0, folder.length - 1),
        'Frameworks',
        'App.framework',
        'Resources',
        'flutter_assets',
      ];
    case 'ios':
      assets = [...folder, 'Frameworks', 'App.framework', 'flutter_assets'];
    case 'linux' || 'windows':
      assets = [...folder, 'data', 'flutter_assets'];
    default:
      return null;
  }
  return assets.join(separator);
}

/// The pack an app was built with, opened in place, so that none of it is
/// read into memory until an asset is asked for. [assetKey] is the key the
/// pack has in `pubspec.yaml`, such as `assets/cooked/game.macos.opack`.
///
/// Null when this platform's assets are not files, or the file is not where
/// the layout says it is. The caller then reads the pack through
/// `rootBundle` into a `MemoryPackBytes`, which costs the pack's size in
/// memory and works everywhere.
FilePackBytes? bundledPackBytes(String assetKey) {
  final directory = flutterAssetsDirectory(
    executable: Platform.resolvedExecutable,
    platform: Platform.operatingSystem,
  );
  if (directory == null) return null;
  final separator = Platform.pathSeparator;
  final path = '$directory$separator${assetKey.replaceAll('/', separator)}';
  return File(path).existsSync() ? FilePackBytes(path) : null;
}

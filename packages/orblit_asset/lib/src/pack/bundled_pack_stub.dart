// The web half of the conditional export in bundled_pack.dart.
import 'pack_file.dart';

/// Where Flutter keeps an app's assets as files, which a web app does not
/// have.
String? flutterAssetsDirectory({
  required String executable,
  required String platform,
}) => null;

/// The pack an app was built with, opened in place, which a web app cannot
/// do. It fetches the pack and holds it in a `MemoryPackBytes`.
FilePackBytes? bundledPackBytes(String assetKey) => null;

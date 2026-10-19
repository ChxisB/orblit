// The web half of the conditional export in kit_store.dart.
import '../net/transport.dart';

/// Builds a kit store, which needs a file system and is not available on this
/// platform.
Future<({int kits, int releases})> buildKitStore({
  required String entriesPath,
  required String outPath,
  required AssetTransport transport,
}) => throw UnsupportedError(
  'buildKitStore needs a file system, and this platform has none. Build the '
  'store on a build machine with `dart run orblit_asset:kit index`.',
);

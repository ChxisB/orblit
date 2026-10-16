// The web half of the conditional export in pack_bundle.dart.
import '../importer.dart';
import 'pack_summary.dart';

/// Packs a cooked bundle, which needs a file system and is not available on
/// this platform. Pack on a build machine and load the result.
Future<PackSummary> packBundle({
  required String bundlePath,
  required String outPath,
  required CookTarget target,
}) => throw UnsupportedError(
  'packBundle needs a file system, and this platform has none. Pack on a '
  'build machine with `dart run orblit_asset:pack build`.',
);

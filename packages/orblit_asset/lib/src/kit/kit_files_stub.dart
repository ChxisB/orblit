// The web half of the conditional export in kit_files.dart.
import '../asset_id.dart';
import '../pack/pack_summary.dart';
import 'kit.dart';
import 'kit_published.dart';

/// Packs a folder as a kit, which needs a file system and is not available on
/// this platform.
Future<PackSummary> packKit({
  required String folder,
  required String outPath,
}) => throw UnsupportedError(
  'packKit needs a file system, and this platform has none. Pack on a '
  'build machine with `dart run orblit_asset:kit pack`.',
);

/// Packs a kit and writes the entry that lists it, which needs a file system
/// and is not available on this platform.
Future<KitPublished> publishKit({
  required String folder,
  required String url,
  required String outPath,
  required DateTime released,
  String notes = '',
  String? entriesPath,
}) => throw UnsupportedError(
  'publishKit needs a file system, and this platform has none. Publish on a '
  'build machine with `dart run orblit_asset:kit publish`.',
);

/// Names the files an install would overwrite.
final class KitConflicts implements Exception {
  const KitConflicts(this.ids);

  final List<AssetId> ids;
}

/// Writes a kit into a project, which needs a file system and is not
/// available on this platform.
Future<List<AssetId>> extractKit(
  Kit kit,
  String projectPath, {
  bool overwrite = false,
  String into = '',
  Set<AssetId>? only,
}) => throw UnsupportedError('extractKit needs a file system.');

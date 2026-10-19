import '../pack/pack_summary.dart';
import 'kit_entry.dart';

/// What `publishKit` made: the kit file and the entry that lists it.
final class KitPublished {
  const KitPublished({
    required this.summary,
    required this.release,
    required this.entry,
    this.entryPath,
  });

  /// What was written to the kit file. Its hash and size are the release's.
  final PackSummary summary;

  /// The release this run made.
  final KitRelease release;

  /// The whole entry as it should read: the releases it listed already, and
  /// [release].
  final KitEntry entry;

  /// The file [entry] was written to, or null when no folder of entries was
  /// given and nothing but the kit file was written.
  final String? entryPath;
}

import '../content_hash.dart';
import 'asset_pack.dart';

/// A file in a pack folder that could not be opened as a pack, and why.
final class UnreadablePack {
  const UnreadablePack(this.path, this.reason);

  final String path;

  /// One sentence, written to be read by whoever has to fix it.
  final String reason;

  @override
  String toString() => '$path: $reason';
}

/// What a pack folder holds.
final class PackListing {
  const PackListing({
    this.packs = const [],
    this.unreadable = const [],
    this.unfinished = const [],
  });

  /// Every pack that opened, in the order of their hashes.
  final List<AssetPack> packs;

  /// Files that look like packs and are not.
  ///
  /// A damaged file is reported here and not thrown, because one bad file
  /// in the folder must not stop a game that has three good ones.
  final List<UnreadablePack> unreadable;

  /// Downloads that started and have not finished, by the pack's hash.
  ///
  /// Asking for the same offer again resumes from what is there.
  final List<ContentHash> unfinished;
}

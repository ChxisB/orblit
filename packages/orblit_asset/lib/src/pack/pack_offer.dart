import '../content_hash.dart';

/// A pack a game's server says is there to download.
///
/// A URL and the hash the pack must have. The hash is the pack's own, the one
/// `AssetPack.hash` answers and `opack build` prints, which is also what a
/// patch built on it names as its base. The game's server is the one place
/// that can vouch for it, so it comes from there with the URL.
final class PackOffer {
  const PackOffer(this.url, this.hash);

  final Uri url;

  /// The hash of the pack's directory, which stands for every blob in it.
  final ContentHash hash;

  @override
  String toString() => 'PackOffer($url, ${hash.hex.substring(0, 12)})';
}

import 'dart:typed_data';

import '../asset_id.dart';
import '../asset_source.dart';
import 'fetcher.dart';

/// Assets fetched from a server.
///
/// The [AssetSource] face of an [AssetFetcher], so that everything already
/// reading assets works unchanged against a server — including
/// [LayeredAssetSource], which is how a network source is usually wanted:
/// what shipped in the app first, what has been downloaded second, and the
/// server last.
///
/// The bytes are handed over as bytes. Nothing here writes a file, so the same
/// code runs on a phone, on a desktop and in a browser, and an asset never
/// exists in a half-written state something else could pick up.
class NetworkAssetSource implements AssetSource {
  NetworkAssetSource(this.fetcher, {this.urgency = FetchUrgency.soon});

  final AssetFetcher fetcher;

  /// How urgently anything read through this source is wanted.
  ///
  /// One setting for the whole source, because [AssetSource.read] has nowhere
  /// to put a second argument and should not grow one — code that cares about
  /// the order things arrive in should be holding the fetcher, where it can
  /// also cancel and watch progress. Two sources over one fetcher, one urgent
  /// and one not, is the cheap way to have both.
  final FetchUrgency urgency;

  /// The bytes of [id].
  ///
  /// A 404 becomes [AssetNotFound], which is what makes a network source work
  /// as a layer: [LayeredAssetSource] moves to the next source on that and on
  /// nothing else. Every other failure — a refused host, a hash that does not
  /// match the manifest, a connection that never came back — is passed on
  /// untouched, because "the server has not got it" and "the server could not
  /// be reached" must not lead to the same place. One is a missing asset and
  /// the other is a broken connection, and quietly serving something older
  /// for the second hides an outage.
  @override
  Future<Uint8List> read(AssetId id) async {
    try {
      return await fetcher.read(id, urgency: urgency);
    } on FetchFailed catch (failure) {
      if (failure.cause == 404 || failure.cause == 410) {
        throw AssetNotFound(id, reason: 'the server answered ${failure.cause}');
      }
      rethrow;
    }
  }

  /// Whether the manifest says this server has [id].
  ///
  /// **Only as good as the manifest.** Asking a server whether it has
  /// something costs a request, and answering that question by fetching the
  /// asset — which is what a source without a manifest would have to do —
  /// turns a cheap question into the most expensive one in the package. So
  /// with a manifest this is exact, and without one it answers for what has
  /// already been fetched and is therefore certain, and under-reports
  /// everything else.
  ///
  /// Code choosing between sources should read and catch [AssetNotFound]
  /// instead, which costs one fetch rather than two.
  @override
  Future<bool> exists(AssetId id) async {
    final manifest = fetcher.manifest;
    if (manifest != null) return manifest.entries.containsKey(id);
    final url = fetcher.origin.urlOf(id);
    final record = await fetcher.records.get(url);
    return record != null && await fetcher.store.contains(record.hash);
  }
}

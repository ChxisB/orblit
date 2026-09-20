import 'dart:async';
import 'dart:typed_data';

import '../asset_id.dart';
import 'fetcher.dart';

/// One thing to draw while an asset loads, or the asset itself.
class AssetStage {
  const AssetStage({
    required this.bytes,
    required this.id,
    required this.isFinal,
  });

  final Uint8List bytes;

  /// Which asset these bytes are: the stand-in, or the one that was asked
  /// for. Worth knowing, because the two are rarely the same size and a
  /// renderer swapping one for the other has to say so.
  final AssetId id;

  /// Whether anything better is still coming.
  final bool isFinal;
}

/// Loading something in stages, so that there is always something to draw.
extension ProgressiveFetch on AssetFetcher {
  /// Hands over a stand-in while [id] loads, then [id] itself.
  ///
  /// At most two stages, and often one. The stand-in is only handed over if
  /// it has arrived, [standInAfter] has passed, and the real asset has not
  /// turned up in the meantime — which on a warm cache it will have, so a
  /// second launch shows no stand-in at all rather than flashing one for a
  /// frame. That flash is the reason the delay exists: substituting a blurry
  /// texture for a sharp one is worth it while the wait is long, and worse
  /// than nothing when the wait is three frames.
  ///
  /// A stand-in that fails to load is not a failure — it is the thing that
  /// was there to make a failure bearable. The real asset failing ends the
  /// stream with that error.
  ///
  /// Cancelling the subscription cancels both fetches, which is what makes
  /// this safe to start for everything on screen and drop when the scene
  /// changes.
  ///
  /// The other half of progressive loading — handing over low mip levels
  /// before the full-size picture — needs textures cooked with a mip chain.
  /// Every texture this project cooks today holds one level, so there is no
  /// smaller version of it to send first, whatever the transport does.
  Stream<AssetStage> fetchInStages(
    AssetId id, {
    AssetId? standIn,
    FetchUrgency urgency = FetchUrgency.onScreen,
    Duration standInAfter = const Duration(milliseconds: 100),
    void Function(FetchProgress)? onProgress,
  }) {
    final out = StreamController<AssetStage>();
    FetchJob? rough;
    FetchJob? wanted;
    Timer? waiting;
    Uint8List? roughBytes;
    var arrived = false;
    var shown = false;

    void offerRough() {
      if (arrived || shown || out.isClosed) return;
      final bytes = roughBytes;
      // Only once both the bytes and the moment have arrived, whichever of
      // the two is later.
      if (bytes == null || waiting != null) return;
      shown = true;
      out.add(AssetStage(bytes: bytes, id: standIn!, isFinal: false));
    }

    out.onListen = () {
      wanted = fetch(id, urgency: urgency, onProgress: onProgress);

      if (standIn != null) {
        waiting = Timer(standInAfter, () {
          waiting = null;
          offerRough();
        });
        rough = fetch(standIn, urgency: FetchUrgency.onScreen);
        rough!.bytes
            .then((bytes) {
              roughBytes = bytes;
              offerRough();
            })
            .catchError((Object _) {
              // Nothing to draw in the meantime, which is the ordinary state
              // of affairs and not worth ending the load over.
            });
      }

      wanted!.bytes.then(
        (bytes) {
          arrived = true;
          waiting?.cancel();
          rough?.cancel();
          if (out.isClosed) return;
          out.add(AssetStage(bytes: bytes, id: id, isFinal: true));
          out.close();
        },
        onError: (Object error, StackTrace stack) {
          arrived = true;
          waiting?.cancel();
          rough?.cancel();
          if (out.isClosed) return;
          out.addError(error, stack);
          out.close();
        },
      );
    };

    out.onCancel = () {
      waiting?.cancel();
      rough?.cancel();
      wanted?.cancel();
    };

    return out.stream;
  }
}

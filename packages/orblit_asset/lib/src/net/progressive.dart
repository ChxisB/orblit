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
  ///
  /// A stage that is not the last and names the asset that was asked for is
  /// that asset's coarse mip levels — the same picture, smaller.
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
  /// A third stage arrives on its own for a mipped texture: the coarse mip
  /// levels, built out of the front of the file while the rest of it is still
  /// arriving. It costs no extra request and no extra byte — those bytes were
  /// on their way regardless — and it is the same asset rather than a
  /// substitute for it, so it supersedes the stand-in and is never followed
  /// by one. [roughSize] is the longest side worth waiting for; a texture
  /// with no mip chain simply never produces the stage.
  Stream<AssetStage> fetchInStages(
    AssetId id, {
    AssetId? standIn,
    FetchUrgency urgency = FetchUrgency.onScreen,
    Duration standInAfter = const Duration(milliseconds: 100),
    int roughSize = 256,
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
      wanted = fetch(
        id,
        urgency: urgency,
        onProgress: onProgress,
        roughSize: roughSize,
        onRough: (bytes) {
          if (arrived || out.isClosed) return;
          // The asset's own coarse levels beat any stand-in, so the stand-in
          // is called off rather than drawn over the top of them.
          shown = true;
          waiting?.cancel();
          waiting = null;
          rough?.cancel();
          out.add(AssetStage(bytes: bytes, id: id, isFinal: false));
        },
      );

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

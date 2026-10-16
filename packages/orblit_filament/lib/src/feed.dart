import 'package:flutter/foundation.dart' show visibleForTesting;
import 'package:flutter/services.dart';

/// A picture the application draws, which a scene can show on a screen.
///
/// A video is a file the renderer decodes. A feed is the other case: the
/// application makes the pictures itself, from a camera, a decoder of its
/// own, another view, or a game running somewhere else, and hands each one
/// over as it is finished. The picture stays where its producer wrote it and
/// the GPU reads it there, so a feed costs a screen about what a video does.
///
/// Making one is two steps, and the second is the platform's:
///
/// ```dart
/// final source = await OrblitFeedSource.create(width: 1280, height: 720);
/// // Hand source.id to whatever draws. On Apple platforms it calls
/// // OrblitFeeds.submit(pixelBuffer, to: id). On Android it draws into
/// // OrblitFeeds.surface(id).
/// ```
///
/// and a scene shows it by naming it in [OrblitScene.feeds] and pointing a
/// screen at its key with [OrblitMaterial.video].
///
/// Where it is supported: macOS and iOS take pixel buffers, and Android
/// takes a surface from API 26. Linux, Windows and the web say so when one
/// is made.
final class OrblitFeedSource {
  OrblitFeedSource._(this.id, this.width, this.height, this._channel);

  static const MethodChannel _defaultChannel = MethodChannel('orblit_filament');
  static const String _unsupported =
      'Feeds are not supported on this platform yet.';

  /// The number the producer writes to, and the scene shows.
  final int id;

  /// What the producer was first asked to draw at.
  ///
  /// The picture's own size is the producer's, and one that draws at another
  /// size is shown at that one. The renderer does not resize or crop a feed.
  final int width;
  final int height;

  final MethodChannel _channel;
  bool _released = false;

  /// Makes a feed. Throws [UnsupportedError] where the platform has none.
  ///
  /// [width] and [height] are what the producer is first asked to draw at,
  /// where the platform needs to be told: Android allocates its buffers from
  /// them. Apple platforms take each buffer's own size.
  static Future<OrblitFeedSource> create({
    required int width,
    required int height,
    @visibleForTesting MethodChannel? channel,
  }) async {
    if (width < 1 || height < 1) {
      throw ArgumentError(
        'A feed needs a size of at least 1 by 1, not $width by $height.',
      );
    }
    final used = channel ?? _defaultChannel;
    final int? id;
    try {
      id = await used.invokeMethod<int>('createFeed', {
        'width': width,
        'height': height,
      });
    } on MissingPluginException {
      throw UnsupportedError(_unsupported);
    } on PlatformException catch (error) {
      // The web plugin answers a method it has no code for with this code
      // and not with a missing plugin, so both read the same to a caller.
      if (error.code != 'not-implemented') rethrow;
      throw UnsupportedError(_unsupported);
    }
    if (id == null || id < 1) {
      throw StateError('The platform did not make a feed.');
    }
    return OrblitFeedSource._(id, width, height, used);
  }

  /// Whether [release] has been called.
  bool get isReleased => _released;

  /// Lets go of the feed. A screen still showing it keeps its last picture
  /// until the scene stops naming it. A second call does nothing.
  Future<void> release() async {
    if (_released) return;
    _released = true;
    await _channel.invokeMethod<void>('releaseFeed', {'feed': id});
  }
}

/// A feed, shown: its key, where it is drawn, and how to turn it upright.
///
/// Stated in [OrblitScene.feeds] and pointed at by a screen's
/// [OrblitMaterial.video], in the same key space as [OrblitVideo], so a key
/// is one or the other and not both in one scene.
class OrblitFeed {
  const OrblitFeed({
    required this.key,
    required this.source,
    this.rotation = 0,
    this.mirror = false,
  }) : assert(rotation >= 0 && rotation < 4, 'rotation is 0, 1, 2 or 3');

  /// This feed's identity in the scene, stable for as long as it is shown.
  final int key;

  /// What is drawn into, made by [OrblitFeedSource.create].
  final OrblitFeedSource source;

  /// How many quarter turns clockwise the picture needs to be upright.
  ///
  /// A phone's camera sensor is fixed in the phone, so its frames arrive
  /// turned by however the phone is held. The producer knows that and says
  /// so here, and the screen shows the frames the right way up. The picture
  /// is mirrored first if [mirror] is set, then turned.
  final int rotation;

  /// Whether the picture is flipped left to right before it is turned, as a
  /// front camera is.
  final bool mirror;

  /// The name the renderer reads a feed by. Not a path to anything, and
  /// never to be passed as one: a file called this is a feed.
  String get path => 'feed:${source.id}';

  /// The orientation's place in a video's flags: bits 2 and 3 are the turns
  /// and bit 4 is the mirror. Bits 0 and 1 are playing and looping, which a
  /// feed has no use for.
  int get flags => (rotation << 2) | (mirror ? 16 : 0);
}

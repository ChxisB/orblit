import CoreVideo
import Foundation

#if canImport(orblit_filament_native)
  import orblit_filament_native
#endif

/// The producer's end of a feed: where an application hands pictures to a
/// scene that is showing them.
///
/// A feed is made from Dart, with `OrblitFeedSource.create`, which gives the
/// number to pass here. Whatever draws the picture, a camera session, a
/// decoder of the application's own, a Metal view, hands each finished
/// `CVPixelBuffer` to `submit`, from any thread.
public enum OrblitFeeds {
  /// Makes `buffer` the newest picture of `feed`.
  ///
  /// The buffer has to be backed by an IOSurface and be 32BGRA or biplanar
  /// 4:2:0. It is retained and not copied, so the scene draws the memory the
  /// producer wrote. A producer that reuses its buffers should rotate through
  /// several, as a capture session does, and not write into the one it has
  /// just submitted.
  ///
  /// Throws if the feed is not open or the buffer is not one the GPU can
  /// read where it lies.
  public static func submit(_ buffer: CVPixelBuffer, to feed: Int64) throws {
    try OrblitRenderer.submitPixelBuffer(buffer, toFeed: feed)
  }
}

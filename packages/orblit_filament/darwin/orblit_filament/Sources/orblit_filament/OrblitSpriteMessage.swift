import Foundation
import orblit_filament_native

#if canImport(FlutterMacOS)
import FlutterMacOS
#elseif canImport(Flutter)
import Flutter
#endif

/// The sprite layers in a scene message, checked before C++ sees them.
///
/// The same shape as SplatMessage and for the same reason: parallel arrays of
/// layers, plus the packed sprites of whichever layers have changed, every
/// length of which is a pointer the renderer walks. A message that does not
/// add up is refused here, as a channel error, rather than read past the end
/// there.
struct SpriteMessage {
  /// Floats per layer: a column-major transform and a tint. Must match
  /// OrblitSprites.layerStride in Dart and kSpriteLayerParams in C++.
  static let spriteLayerStride = 20

  /// Floats per sprite. Must match OrblitSprites.stride and
  /// kSpriteRecordFloats.
  static let spriteStride = 16

  let keys: [Int32]
  let flags: [Int32]
  let orders: [Int32]
  let revisions: [Int32]
  let params: [Float]
  let paths: [String]
  let changed: [Int32]
  let changedCounts: [Int32]
  let data: [Float]

  /// Nil for a message that is malformed. A message with no sprites at all is
  /// every scene that never uses them, and decodes to an empty list.
  init?(arguments: [String: Any]) {
    keys = SpriteMessage.int32s(arguments["spriteKeys"]) ?? []
    flags = SpriteMessage.int32s(arguments["spriteFlags"]) ?? []
    orders = SpriteMessage.int32s(arguments["spriteOrders"]) ?? []
    revisions = SpriteMessage.int32s(arguments["spriteRevisions"]) ?? []
    params = SpriteMessage.floats(arguments["spriteParams"]) ?? []
    paths = arguments["spritePaths"] as? [String] ?? []
    changed = SpriteMessage.int32s(arguments["spriteChanged"]) ?? []
    changedCounts = SpriteMessage.int32s(arguments["spriteChangedCounts"]) ?? []
    data = SpriteMessage.floats(arguments["spriteData"]) ?? []

    let count = keys.count
    let sprites = changedCounts.reduce(0) { $0 + Int($1) }
    guard flags.count == count, orders.count == count,
          revisions.count == count, paths.count == count,
          params.count == count * SpriteMessage.spriteLayerStride,
          changedCounts.count == changed.count,
          changedCounts.allSatisfy({ $0 >= 0 }),
          changed.allSatisfy({ keys.contains($0) }),
          data.count == sprites * SpriteMessage.spriteStride else { return nil }
  }

  /// Hands the layers to the renderer. Skipped when there are none and the
  /// renderer holds none, so a scene without sprites costs nothing here.
  func apply(to renderer: OrblitRenderer) {
    let count = keys.count
    if count == 0 && !renderer.hasSprites { return }

    // An empty array has no base address, and the renderer's pointers are not
    // nullable; nothing is read through these when the counts are zero.
    let keys = count == 0 ? [Int32(0)] : self.keys
    let flags = count == 0 ? [Int32(0)] : self.flags
    let orders = count == 0 ? [Int32(0)] : self.orders
    let revisions = count == 0 ? [Int32(0)] : self.revisions
    let params = count == 0 ? [Float(0)] : self.params
    let changed = self.changed.isEmpty ? [Int32(0)] : self.changed
    let changedCounts = self.changedCounts.isEmpty ? [Int32(0)] : self.changedCounts
    let records = data.isEmpty ? [Float(0)] : data

    keys.withUnsafeBufferPointer { keyPointer in
      flags.withUnsafeBufferPointer { flagPointer in
        orders.withUnsafeBufferPointer { orderPointer in
          revisions.withUnsafeBufferPointer { revisionPointer in
            params.withUnsafeBufferPointer { paramPointer in
              changed.withUnsafeBufferPointer { changedPointer in
                changedCounts.withUnsafeBufferPointer { countPointer in
                  records.withUnsafeBufferPointer { recordPointer in
                    renderer.applySprites(
                      keyPointer.baseAddress!,
                      flags: flagPointer.baseAddress!,
                      orders: orderPointer.baseAddress!,
                      revisions: revisionPointer.baseAddress!,
                      params: paramPointer.baseAddress!,
                      paths: self.paths,
                      changed: changedPointer.baseAddress!,
                      changedCounts: countPointer.baseAddress!,
                      changedCount: UInt32(self.changed.count),
                      records: recordPointer.baseAddress!,
                      recordFloats: self.data.count,
                      count: UInt32(count))
                  }
                }
              }
            }
          }
        }
      }
    }
  }

  private static func int32s(_ value: Any?) -> [Int32]? {
    guard let typed = value as? FlutterStandardTypedData, typed.type == .int32
    else { return nil }
    return typed.data.withUnsafeBytes { Array($0.bindMemory(to: Int32.self)) }
  }

  private static func floats(_ value: Any?) -> [Float]? {
    guard let typed = value as? FlutterStandardTypedData, typed.type == .float32
    else { return nil }
    return typed.data.withUnsafeBytes { Array($0.bindMemory(to: Float.self)) }
  }
}

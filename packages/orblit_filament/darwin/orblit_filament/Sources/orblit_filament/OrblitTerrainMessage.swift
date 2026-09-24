import Foundation
import orblit_filament_native

#if canImport(FlutterMacOS)
import FlutterMacOS
#elseif canImport(Flutter)
import Flutter
#endif

/// The terrains in a scene message, checked before the renderer takes them.
///
/// Three arrays read in step rather than parallel ones, because a terrain is
/// a variable number of regions and sets and its maps only travel when they
/// change. So the check is the reader itself, OrblitTerrain's parseTerrain,
/// asked here through OrblitRenderer: a message it cannot read whole is
/// refused as a channel error, and nothing is half-applied.
struct TerrainMessage {
  let ints: [Int32]
  let floats: [Float]
  let data: Data

  /// Nil for a message that is malformed. A message with no terrain at all is
  /// every scene that never has one, and decodes to an empty one.
  init?(arguments: [String: Any]) {
    ints = TerrainMessage.int32s(arguments["terrainInts"]) ?? []
    floats = TerrainMessage.floats(arguments["terrainFloats"]) ?? []
    data = (arguments["terrainData"] as? FlutterStandardTypedData)?.data ?? Data()
    guard read({ ints, floats, bytes in
      OrblitRenderer.terrainReads(
        ints, intCount: self.ints.count, floats: floats,
        floatCount: self.floats.count, data: bytes, dataLength: self.data.count)
    }) else { return nil }
  }

  /// Hands the terrains to the renderer. Skipped when there are none and the
  /// renderer holds none, so a scene without terrain costs nothing here.
  func apply(to renderer: OrblitRenderer) {
    if ints.isEmpty && !renderer.hasTerrain { return }
    _ = read { ints, floats, bytes in
      renderer.applyTerrain(
        ints, intCount: self.ints.count, floats: floats,
        floatCount: self.floats.count, data: bytes, dataLength: self.data.count)
    }
  }

  /// The three arrays as pointers. An empty array has no base address, and
  /// the renderer's pointers are not nullable; nothing is read through these
  /// when the counts are zero.
  private func read(
    _ body: (UnsafePointer<Int32>, UnsafePointer<Float>, UnsafePointer<UInt8>) -> Bool
  ) -> Bool {
    let ints = self.ints.isEmpty ? [Int32(0)] : self.ints
    let floats = self.floats.isEmpty ? [Float(0)] : self.floats
    let bytes = data.isEmpty ? Data([0]) : data
    return ints.withUnsafeBufferPointer { intPointer in
      floats.withUnsafeBufferPointer { floatPointer in
        bytes.withUnsafeBytes { (raw: UnsafeRawBufferPointer) in
          body(intPointer.baseAddress!, floatPointer.baseAddress!,
               raw.bindMemory(to: UInt8.self).baseAddress!)
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

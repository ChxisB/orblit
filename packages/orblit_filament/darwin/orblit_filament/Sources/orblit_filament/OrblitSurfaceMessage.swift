import Foundation
import orblit_filament_native

#if canImport(FlutterMacOS)
import FlutterMacOS
#elseif canImport(Flutter)
import Flutter
#endif

/// The author-written surfaces in a scene message, checked before C++ sees
/// them.
///
/// Parallel arrays in the layout of `orblit_renderer_set_custom_materials`:
/// one key, package and entry count per surface, then one name, width and map
/// per entry, then every number end to end. Each length is a pointer the
/// renderer walks, so a message that does not add up is refused here, as a
/// channel error, rather than read past the end there.
struct SurfaceMessage {
  let keys: [Int64]
  let packages: [String]
  let entryCounts: [Int32]
  let names: [String]
  let widths: [Int32]
  let maps: [Int32]
  let values: [Float]
  let texturePaths: [String]
  let textureSrgb: [Int32]

  /// Nil for a message that is malformed. A message with no surfaces at all
  /// is every scene that wears only the built-in materials, and decodes to an
  /// empty list.
  init?(arguments: [String: Any]) {
    keys = SurfaceMessage.int64s(arguments["surfaceKeys"]) ?? []
    packages = arguments["surfacePackages"] as? [String] ?? []
    entryCounts = SurfaceMessage.int32s(arguments["surfaceEntryCounts"]) ?? []
    names = arguments["surfaceNames"] as? [String] ?? []
    widths = SurfaceMessage.int32s(arguments["surfaceWidths"]) ?? []
    maps = SurfaceMessage.int32s(arguments["surfaceMaps"]) ?? []
    values = SurfaceMessage.floats(arguments["surfaceValues"]) ?? []
    texturePaths = arguments["surfaceTexturePaths"] as? [String] ?? []
    textureSrgb = SurfaceMessage.int32s(arguments["surfaceTextureSrgb"]) ?? []

    let entries = names.count
    let slots = widths.reduce(0) { $0 + Int($1) }
    let textures = Int32(texturePaths.count)
    guard packages.count == keys.count, entryCounts.count == keys.count,
          entryCounts.allSatisfy({ $0 >= 0 }),
          entryCounts.reduce(0, { $0 + Int($1) }) == entries,
          widths.count == entries, maps.count == entries,
          widths.allSatisfy({ (0...4).contains($0) }),
          slots == values.count,
          textureSrgb.count == texturePaths.count,
          zip(widths, maps).allSatisfy({ $0 != 0 || (0..<textures).contains($1) })
    else { return nil }
  }

  /// Hands the surfaces to the renderer, to be read by the next
  /// `applyMaterials`. Skipped when there are none, so a scene that wears
  /// only built-in materials costs nothing here.
  func apply(to renderer: OrblitRenderer) {
    if keys.isEmpty { return }

    // An empty array has no base address, and the renderer's pointers are not
    // nullable; nothing is read through these when their counts are zero.
    let entryCounts = self.entryCounts
    let widths = self.widths.isEmpty ? [Int32(0)] : self.widths
    let maps = self.maps.isEmpty ? [Int32(0)] : self.maps
    let values = self.values.isEmpty ? [Float(0)] : self.values
    let srgb = textureSrgb.isEmpty ? [Int32(0)] : textureSrgb

    keys.withUnsafeBufferPointer { keyPointer in
      entryCounts.withUnsafeBufferPointer { countPointer in
        widths.withUnsafeBufferPointer { widthPointer in
          maps.withUnsafeBufferPointer { mapPointer in
            values.withUnsafeBufferPointer { valuePointer in
              srgb.withUnsafeBufferPointer { srgbPointer in
                // A refusal leaves nothing staged, and a key with no staged
                // surface draws the default one, so there is nothing to undo.
                _ = renderer.setCustomMaterials(
                  keyPointer.baseAddress!,
                  packages: self.packages,
                  entryCounts: countPointer.baseAddress!,
                  names: self.names,
                  widths: widthPointer.baseAddress!,
                  maps: mapPointer.baseAddress!,
                  values: valuePointer.baseAddress!,
                  valueFloats: UInt(self.values.count),
                  texturePaths: self.texturePaths,
                  textureSrgb: srgbPointer.baseAddress!)
              }
            }
          }
        }
      }
    }
  }

  private static func int64s(_ value: Any?) -> [Int64]? {
    guard let typed = value as? FlutterStandardTypedData, typed.type == .int64
    else { return nil }
    return typed.data.withUnsafeBytes { Array($0.bindMemory(to: Int64.self)) }
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

import Foundation
import orblit_filament_native

#if canImport(FlutterMacOS)
import FlutterMacOS
#elseif canImport(Flutter)
import Flutter
#endif

/// What models' own files do to them in a scene message — the clip playing,
/// the clip faded from, the material variant and joints set by hand — checked
/// before C++ sees it.
///
/// The same shape as SpriteMessage and for the same reason: parallel arrays of
/// poses, plus joints end to end whose lengths are the sum of per-pose counts,
/// every one of which is a pointer the renderer walks. A message that does not
/// add up is refused here, as a channel error, rather than read past the end
/// there.
struct PoseMessage {
  /// Whole numbers per pose: the clip, the clip faded from, flags and the
  /// variant. Must match OrblitAnimation.intStride in Dart and kPoseInts in
  /// C++.
  static let poseIntStride = 4

  /// Floats per pose: the clip's seconds and speed, the faded-from clip's
  /// seconds and speed, and the fade. Must match OrblitAnimation.stride and
  /// kPoseFloats.
  static let poseStride = 5

  /// Ints per hand-set joint (a skin and a joint of it), and floats per
  /// joint's transform.
  private static let jointInts = 2
  private static let jointTransformFloats = 16

  let keys: [Int64]
  let ints: [Int32]
  let floats: [Float]
  let jointCounts: [Int32]
  let joints: [Int32]
  let jointTransforms: [Float]

  /// Nil for a message that is malformed. A message with no poses at all is
  /// every scene with nothing posed, and decodes to an empty list.
  init?(arguments: [String: Any]) {
    keys = PoseMessage.int64s(arguments["poseKeys"]) ?? []
    ints = PoseMessage.int32s(arguments["poseInts"]) ?? []
    floats = PoseMessage.floats(arguments["poseFloats"]) ?? []
    jointCounts = PoseMessage.int32s(arguments["poseJointCounts"]) ?? []
    joints = PoseMessage.int32s(arguments["poseJoints"]) ?? []
    jointTransforms = PoseMessage.floats(arguments["poseJointTransforms"]) ?? []

    let count = keys.count
    guard jointCounts.allSatisfy({ $0 >= 0 }) else { return nil }
    let set = jointCounts.reduce(0) { $0 + Int($1) }
    guard ints.count == count * PoseMessage.poseIntStride,
          floats.count == count * PoseMessage.poseStride,
          jointCounts.count == count,
          joints.count == set * PoseMessage.jointInts,
          jointTransforms.count == set * PoseMessage.jointTransformFloats
    else { return nil }
  }

  /// Hands the poses to the renderer, at the host's seconds they describe.
  /// After the objects, which they address by key. Skipped when there are
  /// none and the renderer holds none, so a scene without models out of files
  /// costs nothing here; when there are none but the renderer still holds
  /// some, it is called with none so every object goes back to rest.
  func apply(to renderer: OrblitRenderer, at: Double) {
    let count = keys.count
    if count == 0 && !renderer.hasPoses { return }

    // An empty array has no base address, and the renderer's pointers are not
    // nullable; nothing is read through these when the counts are zero.
    let keys = count == 0 ? [Int64(0)] : self.keys
    let ints = count == 0 ? [Int32(0)] : self.ints
    let floats = count == 0 ? [Float(0)] : self.floats
    let jointCounts = count == 0 ? [Int32(0)] : self.jointCounts
    let joints = self.joints.isEmpty ? [Int32(0)] : self.joints
    let jointTransforms =
      self.jointTransforms.isEmpty ? [Float(0)] : self.jointTransforms

    keys.withUnsafeBufferPointer { keyPointer in
      ints.withUnsafeBufferPointer { intPointer in
        floats.withUnsafeBufferPointer { floatPointer in
          jointCounts.withUnsafeBufferPointer { countPointer in
            joints.withUnsafeBufferPointer { jointPointer in
              jointTransforms.withUnsafeBufferPointer { transformPointer in
                renderer.applyPoses(
                  keyPointer.baseAddress!,
                  ints: intPointer.baseAddress!,
                  floats: floatPointer.baseAddress!,
                  jointCounts: countPointer.baseAddress!,
                  joints: jointPointer.baseAddress!,
                  jointTransforms: transformPointer.baseAddress!,
                  at: at,
                  count: UInt32(count))
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

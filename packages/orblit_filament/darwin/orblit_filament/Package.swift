// swift-tools-version: 5.9

import Foundation
import PackageDescription

/// Filament arrives as one binary target rather than twenty archives.
///
/// Swift Package Manager has no way to say "these static libraries and that
/// include directory"; what it takes is an xcframework. `setup.sh` builds one
/// out of the SDK it fetches, because a package plugin runs sandboxed with no
/// network and could never fetch a hundred megabytes of renderer itself.
let filament = "third_party/Filament.xcframework"

// Checked here so a fresh clone is told what to run, rather than being handed
// whatever SwiftPM says about a path that is not there.
let here = URL(fileURLWithPath: #filePath).deletingLastPathComponent()
let generatedSet = ProcessInfo.processInfo.environment["ORBLIT_GENERATED_SET"]
  ?? (ProcessInfo.processInfo.environment["ORBLIT_FILAMENT_SRC"] == nil
    ? "darwin-release"
    : "darwin-source")
guard generatedSet.range(of: "^[A-Za-z0-9_-]+$", options: .regularExpression) != nil else {
  fatalError("orblit_filament: ORBLIT_GENERATED_SET must be a simple directory name")
}
if !FileManager.default.fileExists(atPath: here.appendingPathComponent(filament).path) {
  fatalError(
    """

    orblit_filament: \(filament) is missing.

    Filament is fetched, not vendored. Build it once:

        bash packages/orblit_filament/darwin/setup.sh

    """
  )
}

let package = Package(
  name: "orblit_filament",
  platforms: [
    .macOS("10.15"),
    .iOS("13.0"),
  ],
  products: [
    .library(name: "orblit-filament", targets: ["orblit_filament"])
  ],
  dependencies: [
    .package(name: "FlutterFramework", path: "../FlutterFramework")
  ],
  targets: [
    // The plugin: the method channel, the display link, and the texture
    // registration. Swift, because that is what Flutter's macOS API is.
    .target(
      name: "orblit_filament",
      dependencies: [
        "orblit_filament_native",
        .product(name: "FlutterFramework", package: "FlutterFramework"),
      ]
    ),
    // The renderer. A separate target because a Swift Package Manager target
    // holds one language, and this is Objective-C++ — which is also why the
    // header it publishes is free of C++.
    .target(
      name: "orblit_filament_native",
      dependencies: ["Filament"],
      cSettings: [
        .headerSearchPath("include"),
        .headerSearchPath("generated/\(generatedSet)")
      ],
      linkerSettings: [
        .linkedFramework("Metal"),
        .linkedFramework("MetalKit"),
        .linkedFramework("CoreVideo"),
        // Video playback. CoreMedia comes with it for the time types, and
        // AudioToolbox is here because AVFoundation's auto-link asks for
        // CoreAudioTypes, which is not a framework on this SDK and is not
        // found — naming the real one stops the linker looking.
        .linkedFramework("AVFoundation"),
        .linkedFramework("CoreMedia"),
        .linkedFramework("AudioToolbox"),
        .linkedFramework("QuartzCore"),
        .linkedFramework("IOSurface"),
        // bluegl's fallback backend; Filament links it whether or not the
        // Metal backend is the one in use. macOS only — iOS has no OpenGL
        // framework at all, and the iOS slice of the xcframework does not
        // contain bluegl, so there is nothing there to satisfy. Without the
        // condition the iOS build compiles every file and then fails at the
        // link with "Framework 'OpenGL' not found".
        .linkedFramework("OpenGL", .when(platforms: [.macOS])),
      ]
    ),
    .binaryTarget(name: "Filament", path: filament),
  ],
  cxxLanguageStandard: .cxx17
)

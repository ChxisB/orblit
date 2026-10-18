import 'package:code_assets/code_assets.dart';
import 'package:hooks/hooks.dart';
import 'package:native_toolchain_c/native_toolchain_c.dart';

/// Builds the wrapper and the two vendored decoders into one library the Dart
/// bindings open over FFI.
void main(List<String> args) async {
  await build(args, (input, output) async {
    if (!input.config.buildCodeAssets) return;

    final root = input.packageRoot;
    final os = input.config.code.targetOS;
    final apple = os == OS.macOS || os == OS.iOS;
    final builder = CBuilder.library(
      name: 'orblit_audio_device',
      assetName: 'orblit_audio_device',
      sources: [
        root.resolve('src/orblit_audio_device.c').toFilePath(),
        root.resolve('src/miniaudio_impl.c').toFilePath(),
      ],
      includes: [
        root.resolve('include/').toFilePath(),
        root.resolve('third_party/').toFilePath(),
      ],
      defines: {
        'MA_NO_ENCODING': null,
        'MA_NO_GENERATION': null,
        // Linking the frameworks, not opening them at run time, is what Apple's
        // notarization accepts.
        if (apple) 'MA_NO_RUNTIME_LINKING': null,
      },
      // miniaudio needs the Objective-C compiler on iOS. macOS takes the same
      // flag so Apple has one build.
      language: apple ? Language.objectiveC : Language.c,
      frameworks: [
        if (apple) ...[
          'Foundation',
          'CoreFoundation',
          'CoreAudio',
          'AudioToolbox',
        ],
        if (os == OS.iOS) 'AVFoundation',
      ],
      libraries: [
        if (os == OS.linux) ...['m', 'dl', 'pthread'],
      ],
      optimizationLevel: OptimizationLevel.o2,
    );

    await builder.run(input: input, output: output, logger: null);
  });
}

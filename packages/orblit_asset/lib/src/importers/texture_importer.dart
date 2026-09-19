import 'dart:io';

import '../cook.dart';
import '../import_settings.dart';
import '../importer.dart';
import 'native_tool.dart';

/// Turns a PNG or a JPEG into the compressed KTX2 files a device samples.
///
/// The work is `orblit_texture_cook`'s; what this adds is deciding the flags
/// from the settings and the target instead of from a file-name convention.
/// `tool/cook_textures.sh` had to guess — a name ending `_normal` was a normal
/// map, a name ending `_mask` was single-channel — and a guess is wrong for
/// exactly the assets whose names came from somewhere else. A `.import.json`
/// saying `"normal": true` is not a guess.
///
/// One cook writes one family per target, not all of them: that is the
/// difference between an app that ships ASTC and BC and ETC2 and one that
/// ships what its device reads.
class TextureImporter extends Importer {
  TextureImporter({NativeTool? tool})
    : tool =
          tool ??
          NativeTool(
            'orblit_texture_cook',
            environmentVariable: 'ORBLIT_TEXTURE_COOK',
          );

  final NativeTool tool;

  @override
  String get name => 'texture';

  /// Bump this when the flags chosen below change, or when the encoder does.
  /// The encoder's own version is not visible from here, which is why
  /// `orblit_texture_cook --revisions` exists: a build that wants to be sure
  /// feeds it into the target instead.
  @override
  int get version => 1;

  @override
  Set<String> get extensions => const {'png', 'jpg', 'jpeg', 'ktx2'};

  @override
  Map<String, Object?> resolveSettings(ImportSettings settings) {
    final values = settings.values;
    return {
      'normal': _bool(values, 'normal', false),
      'twoChannelNormals': _bool(values, 'twoChannelNormals', false),
      'singleChannel': _bool(values, 'singleChannel', false),
      'lossless': _bool(values, 'lossless', false),
      'wrap': _bool(values, 'wrap', false),
      'mips': _bool(values, 'mips', true),
      'colourSpace': _colourSpace(values),
      'cutout': _cutout(values),
      'maxSize': _maxSize(values),
      'uastc': _int(values, 'uastc', 2, min: 0, max: 4),
      'zstd': _int(values, 'zstd', 19, min: 1, max: 22),
    };
  }

  @override
  Future<ImportResult> import(ImportRequest request) async {
    final settings = request.settings;
    final families = request.target.textureFamilies;
    final notes = <String>[];

    // An empty list means the caller did not say, and every family is written
    // — right for a project-wide cook whose bundles are split later, wrong for
    // an app build, which is why the cook command always names them.
    final targets = families.isEmpty ? TextureFamily.all : families;
    final unknown = targets.where((f) => !TextureFamily.all.contains(f));
    if (unknown.isNotEmpty) {
      throw ImportFailure(
        request.id,
        'is being cooked for ${unknown.join(', ')}, which is not a texture '
        'family. The families are ${TextureFamily.all.join(', ')}.',
      );
    }

    // `raw` is not a family the cooker writes; it is the absence of one.
    final compressed = [...targets.where((f) => f != TextureFamily.raw)];
    final lossless =
        settings['lossless'] == true ||
        (compressed.isEmpty && targets.contains(TextureFamily.raw));

    final maxSize =
        settings['maxSize'] as int? ?? request.target.maxTextureSize;
    if (settings['maxSize'] == null && maxSize != null) {
      notes.add('held to ${maxSize}px by the target, which samples no larger');
    }

    return withTemporaryDirectory('texture', (work) async {
      final input = File(inside(work, 'in.${request.id.extension}'));
      await input.writeAsBytes(request.bytes, flush: true);

      final out = Directory(inside(work, 'out'));
      await out.create();

      await tool.run([
        input.path,
        beside(out.path, ''),
        if (!lossless) ...['--targets', compressed.join(',')],
        if (lossless) '--lossless',
        if (settings['normal'] == true) '--normal',
        if (settings['twoChannelNormals'] == true) '--two-channel-normals',
        if (settings['singleChannel'] == true) '--single-channel',
        if (settings['colourSpace'] == 'srgb') '--srgb',
        if (settings['colourSpace'] == 'linear') '--linear',
        if (settings['cutout'] != null) ...[
          '--cutout',
          '${settings['cutout']}',
        ],
        if (settings['mips'] != true) '--no-mips',
        if (settings['wrap'] == true) '--wrap',
        if (maxSize != null) ...['--max-size', '$maxSize'],
        '--uastc',
        '${settings['uastc']}',
        '--zstd',
        '${settings['zstd']}',
        '--quiet',
      ], on: request.id);

      final outputs = <String, List<int>>{};
      await for (final written in out.list()) {
        if (written is! File) continue;
        final name = written.path.split(Platform.pathSeparator).last;
        outputs[name] = await written.readAsBytes();
      }
      if (outputs.isEmpty) {
        throw ImportFailure(
          request.id,
          'cooked without error but produced no files, which means the flags '
          'asked for nothing. Targets were ${targets.join(', ')}.',
        );
      }
      return ImportResult(outputs: outputs, notes: notes);
    });
  }

  static bool _bool(Map<String, Object?> values, String key, bool fallback) {
    final value = values[key];
    if (value == null) return fallback;
    if (value is! bool) {
      throw FormatException(
        '"$key" is $value, and it has to be true or false.',
      );
    }
    return value;
  }

  static int _int(
    Map<String, Object?> values,
    String key,
    int fallback, {
    required int min,
    required int max,
  }) {
    final value = values[key];
    if (value == null) return fallback;
    if (value is! int || value < min || value > max) {
      throw FormatException(
        '"$key" is $value, and it has to be a whole number from $min to $max.',
      );
    }
    return value;
  }

  static String? _colourSpace(Map<String, Object?> values) {
    final value = values['colourSpace'] ?? values['colorSpace'];
    if (value == null) return null;
    if (value != 'srgb' && value != 'linear') {
      throw FormatException(
        '"colourSpace" is $value, and it is either "srgb", "linear", or left '
        'out so that the file decides.',
      );
    }
    return value as String;
  }

  static double? _cutout(Map<String, Object?> values) {
    final value = values['cutout'];
    if (value == null) return null;
    final threshold = value is int ? value.toDouble() : value;
    if (threshold is! double || threshold < 0 || threshold > 1) {
      throw FormatException(
        '"cutout" is $value, and an alpha threshold is between 0 and 1.',
      );
    }
    return threshold;
  }

  static int? _maxSize(Map<String, Object?> values) {
    final value = values['maxSize'];
    if (value == null) return null;
    if (value is! int || value <= 0 || (value & (value - 1)) != 0) {
      throw FormatException(
        '"maxSize" is $value, and it has to be a power of two: mip levels are '
        'dropped to reach it, and a level is always half the one above.',
      );
    }
    return value;
  }
}

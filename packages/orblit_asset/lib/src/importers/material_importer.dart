import 'dart:convert';
import 'dart:io';
import 'dart:typed_data';

import '../asset_id.dart';
import '../asset_source.dart';
import '../import_settings.dart';
import '../importer.dart';
import '../surface/surface_diagnostics.dart';
import '../surface/surface_error.dart';
import '../surface/surface_lines.dart';
import '../surface/surface_translator.dart';
import 'native_tool.dart';

/// Cooks an author's `.oshader` into a Filament material package.
///
/// The text is translated to a `.mat` and compiled by `matc` for the
/// backends the target draws with. The outputs are `material.filamat`, which
/// the renderer loads by name, and `params.json`, which lists the uniforms
/// with their hints so an editor can show a colour row or a slider
/// without reading the source again.
///
/// A mistake comes back as `file:line: message` at the author's own line,
/// whether the translator or matc found it.
class MaterialImporter extends Importer {
  MaterialImporter({NativeTool? tool})
    : tool = tool ?? NativeTool('matc', environmentVariable: 'ORBLIT_MATC');

  final NativeTool tool;

  String? _matcVersion;

  @override
  String get name => 'material';

  @override
  int get version => 1;

  @override
  Set<String> get extensions => const {'oshader'};

  /// The matc that cooks must be the matc the runtime Filament was built
  /// with, so which one it is belongs in the key. A package from another
  /// version is refused when it loads, which looks like a missing material.
  @override
  Map<String, Object?> resolveSettings(ImportSettings settings) => {
    'matc': _matcVersion ??= _askMatc(),
  };

  String _askMatc() {
    final program = tool.locate();
    if (program == null) return 'missing';
    final result = Process.runSync(program, ['--version']);
    return result.exitCode == 0 ? (result.stdout as String).trim() : 'unknown';
  }

  /// Every file the source includes, however deep.
  ///
  /// An include that does not exist is left out. The translator reports it at
  /// its line, and once the file is written the key changes.
  @override
  Future<Set<AssetId>> dependenciesIn(
    AssetId id,
    Uint8List bytes,
    Map<String, Object?> settings,
    AssetSource source,
  ) async {
    final files = await _readIncludes(source, id, utf8.decode(bytes));
    return {for (final path in files.keys) AssetId.parse(path)};
  }

  @override
  Future<ImportResult> import(ImportRequest request) async {
    final id = request.id;
    final path = id.toString();
    final text = utf8.decode(request.bytes, allowMalformed: false);
    final files = await _readIncludes(request.source, id, text);
    final SurfaceTranslation translation;
    try {
      translation = translateSurface(
        name: id.name.split('.').first,
        path: path,
        source: text,
        read: (include) => files[include],
      );
    } on SurfaceError catch (error) {
      throw ImportFailure(id, error.toString());
    }

    return withTemporaryDirectory('material', (work) async {
      final input = File(inside(work, 'in.mat'));
      await input.writeAsString(translation.material, flush: true);
      final output = inside(work, 'out.filamat');
      try {
        await tool.run([
          ..._flagsFor(request.target),
          '-o',
          output,
          input.path,
        ], on: id);
      } on ImportFailure catch (failure) {
        final mapped = surfaceDiagnostics('${failure.cause}', translation);
        if (mapped.isEmpty) rethrow;
        throw ImportFailure(id, mapped.join('\n'));
      }
      return ImportResult(
        outputs: {
          'material.filamat': await File(output).readAsBytes(),
          'params.json': utf8.encode(
            jsonEncode([for (final p in translation.params) p.toJson()]),
          ),
        },
      );
    });
  }

  Future<Map<String, String>> _readIncludes(
    AssetSource source,
    AssetId id,
    String text,
  ) async {
    final files = <String, String>{};
    try {
      final queue = includePaths(id.toString(), text);
      while (queue.isNotEmpty) {
        final next = queue.removeLast();
        final include = AssetId.tryParse(next);
        if (files.containsKey(next) || include == null) continue;
        if (!await source.exists(include)) continue;
        final body = utf8.decode(await source.read(include));
        files[next] = body;
        queue.addAll(includePaths(next, body));
      }
    } on SurfaceError catch (error) {
      throw ImportFailure(id, error.toString());
    }
    return files;
  }
}

/// The backends and shader family each target draws with, as the setup
/// script of each platform compiles its own materials.
List<String> _flagsFor(CookTarget target) => switch (target.name) {
  'macos' || 'ios' => ['-a', 'metal', '-p', 'all'],
  'windows' || 'linux' => ['-a', 'vulkan', '-a', 'opengl', '-p', 'desktop'],
  'android' => ['-a', 'opengl', '-a', 'vulkan', '-p', 'mobile'],
  'web' => ['-a', 'webgpu', '-p', 'all'],
  _ => ['-a', 'all', '-p', 'all'],
};

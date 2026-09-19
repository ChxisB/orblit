import 'dart:io';

import '../import_settings.dart';
import '../importer.dart';
import 'native_tool.dart';

/// Turns an FBX or an OBJ into a GLB.
///
/// FBX import is ufbx's and OBJ import is the same program's; both come out as
/// glTF, so that everything downstream — the loader, the atlas packer, the
/// exporter — deals with one format. That was the decision behind dropping FBX
/// *export* while keeping FBX import: the engine reads what artists have, and
/// writes what the ecosystem reads.
///
/// The GLB this writes carries its textures inside it when the source did, and
/// points at files beside it when the source did. That is why
/// [dependenciesOf] defers to the glTF importer's scanner for the result —
/// except it cannot, because the result does not exist until the import has
/// run. An FBX names its textures by path in its own header, and reading that
/// here would mean parsing FBX twice, so instead the converted GLB's own
/// references are reported as notes and the textures are cooked as the assets
/// they already are.
class ModelImporter extends Importer {
  ModelImporter({NativeTool? tool})
    : tool =
          tool ??
          NativeTool('orblit_import', environmentVariable: 'ORBLIT_IMPORT');

  final NativeTool tool;

  @override
  String get name => 'model';

  @override
  int get version => 1;

  @override
  Set<String> get extensions => const {'fbx', 'obj'};

  @override
  Map<String, Object?> resolveSettings(ImportSettings settings) => const {};

  @override
  Future<ImportResult> import(ImportRequest request) async =>
      withTemporaryDirectory('model', (work) async {
        final input = File(inside(work, 'in.${request.id.extension}'));
        await input.writeAsBytes(request.bytes, flush: true);
        final output = File(inside(work, 'out.glb'));

        await tool.run([input.path, output.path], on: request.id);

        if (!output.existsSync()) {
          throw ImportFailure(
            request.id,
            'converted without error but wrote no GLB. The source may hold no '
            'geometry — an FBX of only cameras and lights does this.',
          );
        }
        return ImportResult(outputs: {'glb': await output.readAsBytes()});
      });
}

/// Turns a splat capture into the `.osplat` a launch can read without parsing.
///
/// A `.ply` from a capture pipeline spends its load time on an exponential, a
/// quaternion and a covariance for every splat, and on two passes over its
/// harmonics. None of that happens again once it is cooked, which is the
/// difference between a scene that opens and one that waits.
///
/// The two settings are both about size rather than quality in the abstract: a
/// phone gets fewer bands and fewer splats, and which splats it loses is
/// decided by the same opacity-and-size ranking the renderer would have
/// applied at runtime — so the small file looks like the big one, not like a
/// random half of it.
class SplatImporter extends Importer {
  SplatImporter({NativeTool? tool})
    : tool =
          tool ??
          NativeTool(
            'orblit_splat_cook',
            environmentVariable: 'ORBLIT_SPLAT_COOK',
          );

  final NativeTool tool;

  @override
  String get name => 'splats';

  @override
  int get version => 1;

  @override
  Set<String> get extensions => const {'ply', 'spz', 'splat', 'osplat'};

  @override
  Map<String, Object?> resolveSettings(ImportSettings settings) {
    final harmonics = settings.values['harmonics'];
    if (harmonics != null &&
        (harmonics is! int || harmonics < 0 || harmonics > 3)) {
      throw FormatException(
        '"harmonics" is $harmonics, and it is how many bands to keep: 0 to 3.',
      );
    }
    final limit = settings.values['limit'];
    if (limit != null && (limit is! int || limit <= 0)) {
      throw FormatException(
        '"limit" is $limit, and it is the most splats to keep, so it is a '
        'positive whole number. Leave it out to keep all of them.',
      );
    }
    return {'harmonics': harmonics ?? 3, 'limit': limit};
  }

  @override
  Future<ImportResult> import(ImportRequest request) async =>
      withTemporaryDirectory('splat', (work) async {
        final input = File(inside(work, 'in.${request.id.extension}'));
        await input.writeAsBytes(request.bytes, flush: true);
        final output = File(inside(work, 'out.osplat'));

        final limit = request.settings['limit'];
        await tool.run([
          input.path,
          output.path,
          '--harmonics',
          '${request.settings['harmonics']}',
          if (limit != null) ...['--limit', '$limit'],
        ], on: request.id);

        if (!output.existsSync()) {
          throw ImportFailure(request.id, 'cooked no .osplat');
        }
        return ImportResult(outputs: {'osplat': await output.readAsBytes()});
      });
}

/// Bakes an HDR or EXR panorama into the two KTX files the renderer lights a
/// scene with: the reflections and the backdrop.
///
/// The work is Filament's `cmgen`, at the sizes `tool/bake_environment.sh`
/// settled on. The skybox is four times the reflection map because the
/// backdrop is looked at directly and the reflections are not, and it is held
/// to 1024 because beyond that nobody can tell and everybody pays.
///
/// See the sky-backdrop trap this repository has hit more than once: a scene
/// with one skybox slot and three things that want to own it shows "stamps" in
/// the sky. Cooking the backdrop does not cause that, but it is the file that
/// gets blamed for it.
class EnvironmentImporter extends Importer {
  EnvironmentImporter({NativeTool? tool})
    : tool = tool ?? NativeTool('cmgen', environmentVariable: 'ORBLIT_CMGEN');

  final NativeTool tool;

  @override
  String get name => 'environment';

  @override
  int get version => 1;

  @override
  Set<String> get extensions => const {'hdr', 'exr'};

  @override
  Map<String, Object?> resolveSettings(ImportSettings settings) {
    final size = settings.values['size'];
    if (size != null &&
        (size is! int || size <= 0 || (size & (size - 1)) != 0)) {
      throw FormatException(
        '"size" is $size, and a cubemap face is a power of two.',
      );
    }
    final resolved = (size ?? 256) as int;
    final skybox = settings.values['skyboxSize'];
    if (skybox != null &&
        (skybox is! int || skybox <= 0 || (skybox & (skybox - 1)) != 0)) {
      throw FormatException(
        '"skyboxSize" is $skybox, and a cubemap face is a power of two.',
      );
    }
    return {
      'size': resolved,
      // Four times the reflections, held to 1024: the backdrop is the part
      // anyone looks at directly, and past 1024 it stops paying for itself.
      'skyboxSize': skybox ?? (resolved * 4).clamp(1, 1024),
    };
  }

  @override
  Future<ImportResult> import(ImportRequest request) async =>
      withTemporaryDirectory('environment', (work) async {
        // cmgen names what it writes after the input file, so the input is
        // given the name the outputs should have rather than renamed after.
        const stem = 'environment';
        final input = File(inside(work, '$stem.${request.id.extension}'));
        await input.writeAsBytes(request.bytes, flush: true);

        final reflections = inside(work, 'ibl');
        final sky = inside(work, 'sky');

        await tool.run([
          '--quiet',
          '--format=ktx',
          '--size=${request.settings['size']}',
          '--deploy=${beside(reflections, stem)}',
          input.path,
        ], on: request.id);
        await tool.run([
          '--quiet',
          '--format=ktx',
          '--size=${request.settings['skyboxSize']}',
          '--extract=${beside(sky, stem)}',
          input.path,
        ], on: request.id);

        return ImportResult(
          outputs: {
            'ibl.ktx': await _readOr(
              request,
              beside(beside(reflections, stem), '${stem}_ibl.ktx'),
            ),
            'skybox.ktx': await _readOr(
              request,
              beside(beside(sky, stem), '${stem}_skybox.ktx'),
            ),
            // The harmonics at full precision, which the renderer does not
            // read but a person comparing two bakes does.
            'sh.txt': await _readOr(
              request,
              beside(beside(reflections, stem), 'sh.txt'),
            ),
          },
        );
      });

  static Future<List<int>> _readOr(ImportRequest request, String path) async {
    final file = File(path);
    if (!file.existsSync()) {
      throw ImportFailure(
        request.id,
        'baked without error, but cmgen did not write $path. A cmgen from a '
        'different Filament version names its outputs differently.',
      );
    }
    return file.readAsBytes();
  }
}

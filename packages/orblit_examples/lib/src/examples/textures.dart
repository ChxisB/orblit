import 'dart:typed_data';

import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:orblit_filament/orblit_filament.dart';
import 'package:vector_math/vector_math_64.dart' hide Colors;

import '../example.dart';
import '../platform/fetch.dart';
import '../platform/io.dart';
import 'surface.dart' show linearOf;

/// Textures loaded the way a scene loads them, and a place lighting them.
///
/// A wall of panels, one texture each, lit by an `.hdr` or `.exr` picture
/// filtered while the scene runs. What it is for is the loading rather than
/// the look: every texture is named at once, as a model's are, so the frames
/// while they arrive are the frames worth watching — decoded off the drawing
/// thread, uploaded a few megabytes a frame, smallest level first.
///
/// A cooked texture is named as its set, `x.ktx2`, and the device chooses.
/// On a desktop the renderer looks beside the file for `x.astc.ktx2`,
/// `x.bc.ktx2` and `x.etc2.ktx2` itself. A browser has no files to look
/// beside, so the example asks the device which names are worth fetching
/// (`OrblitDeviceProfile.textureCandidates`), fetches the first that is
/// there, and hands its bytes to the renderer under the name the renderer
/// will look for.
///
/// Fetched rather than shipped, and chosen by switches:
///
///   ORBLIT_TEXTURES        the directory, or on the web the path under the
///                          page: the Bistro's textures by default natively,
///                          `textures` on the web
///   ORBLIT_TEXTURE_FILES   which files in it, comma-separated: PNG, JPEG,
///                          Basis or a cooked set's `x.ktx2`
///   ORBLIT_PICTURE         an equirectangular `.hdr` or `.exr` to light the
///                          wall from, a path natively or a URL on the web
class TexturesExample extends Example {
  TexturesExample();

  @override
  String get name => 'Textures';

  @override
  String get blurb =>
      'A wall of textures named at once — pictures, Basis and cooked sets '
      'chosen by the device — lit by a picture filtered at run time.';

  @override
  ViewPoint get viewpoint =>
      const ViewPoint(distance: 11, pitch: 0.05, yaw: 0.0);

  static final Map<String, String> _environment = Platform.environment;

  /// Where the files are.
  String directory =
      _environment['ORBLIT_TEXTURES'] ??
      (kIsWeb ? 'textures' : '../orblit/assets/bistro/Textures');

  /// Which of them the wall wears, in order.
  List<String> files = [
    for (final file
        in (_environment['ORBLIT_TEXTURE_FILES'] ??
                'Concrete_BaseColor.ktx2,Plaster_BaseColor.ktx2,'
                    'Pavement_Cobblestone_02_BaseColor.ktx2,'
                    'MASTER_Roofing_Shingle_Grey_BaseColor.ktx2')
            .split(','))
      if (file.trim().isNotEmpty) file.trim(),
  ];

  /// The picture the wall is lit from, or null for the sun and sky alone.
  String? picture = _environment['ORBLIT_PICTURE'];

  /// Whether the picture lights the wall, when there is one.
  bool lit = true;

  /// What each file is named as in the scene, once its bytes are where the
  /// renderer will look: the resource name on the web, the path elsewhere.
  final Map<String, String> _named = {};
  String? _pictureName;
  bool _fetching = false;

  /// Starts the fetches, once, when the device is known: which cooked
  /// sibling is worth fetching depends on what the device samples.
  void _fetchAll(OrblitDeviceProfile device) {
    if (_fetching) return;
    _fetching = true;
    if (!kIsWeb) {
      // The renderer reads the disk and chooses a cooked sibling itself.
      for (final file in files) {
        final path = '$directory/$file';
        if (File(path).existsSync()) _named[file] = path;
      }
      final picturePath = picture;
      if (picturePath != null && File(picturePath).existsSync()) {
        _pictureName = picturePath;
      }
      return;
    }

    Future<void> fetchAll() async {
      final named = <String, String>{};
      final missing = <String>[];
      for (final file in files) {
        // Best first; the renderer checks what the file actually holds, so
        // the first that exists is the one to hand over.
        String? found;
        for (final candidate in device.textureCandidates(file)) {
          final bytes = await fetchBytes('$directory/$candidate');
          if (bytes == null) continue;
          await _provide('textures/$candidate', bytes);
          found = candidate;
          break;
        }
        if (found == null) {
          missing.add(file);
        } else {
          named[file] = OrblitResources.nameFor('textures/$file');
        }
      }
      final picturePath = picture;
      if (picturePath != null) {
        final bytes = await fetchBytes(picturePath);
        final pictureFile = picturePath.split('/').last;
        if (bytes == null) {
          missing.add(picturePath);
        } else {
          await _provide('pictures/$pictureFile', bytes);
          _pictureName = OrblitResources.nameFor('pictures/$pictureFile');
        }
      }
      if (missing.isNotEmpty) {
        note = 'Not found under $directory: ${missing.join(', ')}.';
      }
      // Named together, as a model's textures are, so they arrive together.
      _named.addAll(named);
      // Said, so what follows in the console can be timed from this line.
      debugPrint(
        '[textures] named ${named.length} texture(s)'
        '${_pictureName == null ? '' : ' and a picture'}',
      );
    }

    fetchAll();
  }

  Future<void> _provide(String path, Uint8List bytes) =>
      OrblitResources.provide(OrblitResources.nameFor(path), bytes);

  /// Whether a texture holds a colour or a measurement, by the Bistro's
  /// naming. A Basis file says which it is and is refused the other way.
  static bool _isColour(String file) {
    final lower = file.toLowerCase();
    return !(lower.contains('normal') ||
        lower.contains('specular') ||
        lower.contains('roughness'));
  }

  @override
  OrblitScene scene(OrblitCamera camera, double seconds) {
    final profile = device;
    if (profile != null) _fetchAll(profile);

    final materials = <OrblitMaterial>[];
    final objects = <OrblitObject>[];
    const across = 4;
    for (var i = 0; i < files.length; i++) {
      final named = _named[files[i]];
      final column = i % across;
      final row = i ~/ across;
      final rows = (files.length + across - 1) ~/ across;
      final key = 10 + i;
      materials.add(
        OrblitMaterial(
          key: key,
          baseColour: Vector4(1, 1, 1, 1),
          roughness: 0.7,
          baseColourMap: named == null
              ? null
              : OrblitTexture(named, srgb: _isColour(files[i])),
        ),
      );
      objects.add(
        OrblitObject(
          key: key,
          material: key,
          transform: Matrix4.identity()
            ..setTranslation(
              Vector3(
                (column - (across - 1) / 2) * 2.3,
                ((rows - 1) / 2 - row) * 2.3,
                0,
              ),
            )
            ..multiply(Matrix4.diagonal3(Vector3(1, 1, 0.05))),
          colour: Vector3(1, 1, 1),
        ),
      );
    }

    final environmentName = lit ? _pictureName : null;
    return OrblitScene(
      objects: objects,
      materials: materials,
      camera: camera,
      lights: [
        OrblitLight(
          key: 900,
          kind: OrblitLightKind.directional,
          direction: Vector3(-0.3, -0.6, -1),
          intensity: environmentName == null ? 60000 : 20000,
        ),
      ],
      environment: environmentName == null
          ? null
          : OrblitEnvironment.fromImage(environmentName, intensity: 30000),
      sky: OrblitSky(
        colour: linearOf(const Color(0xFF3A4452)),
        ambient: environmentName == null ? 24000 : 0,
        drawn: environmentName == null,
      ),
    );
  }

  @override
  Widget settings(BuildContext context, VoidCallback changed) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Toggle(
          label: 'Picture lights it',
          value: lit,
          enabled: picture != null,
          note: picture ?? 'Set ORBLIT_PICTURE to an .hdr or .exr.',
          onChanged: (value) {
            lit = value;
            changed();
          },
        ),
        Padding(
          padding: const EdgeInsets.symmetric(vertical: 4),
          child: Text(
            '${_named.length} of ${files.length} named, from $directory',
            style: const TextStyle(fontSize: 12, color: Colors.white70),
          ),
        ),
      ],
    );
  }

  @override
  String get code => '''
// On the web, fetch the best file the device samples and provide it.
for (final candidate in device.textureCandidates('wall.ktx2')) {
  final bytes = await fetchBytes('textures/\$candidate');
  if (bytes == null) continue;
  await OrblitResources.provide(
    OrblitResources.nameFor('textures/\$candidate'), bytes);
  break;
}

OrblitMaterial(
  key: 1,
  baseColourMap: OrblitTexture(OrblitResources.nameFor('textures/wall.ktx2')),
);

OrblitScene(
  objects: [...],
  environment: OrblitEnvironment.fromImage(
    OrblitResources.nameFor('pictures/place.hdr')),
  camera: camera,
);
''';
}

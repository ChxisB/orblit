import 'dart:typed_data';

import 'package:flutter/foundation.dart' show kIsWeb;
import 'package:flutter/material.dart';
import 'package:orblit_filament/orblit_filament.dart';
import 'package:vector_math/vector_math_64.dart' hide Colors;

import '../example.dart';
import '../platform/fetch.dart';
import '../platform/io.dart';
import 'surface.dart' show linearOf;

/// One of the files this example can show, and what it is there to show.
class ImportedSample {
  const ImportedSample(
    this.name,
    this.file,
    this.shows, {
    this.companions = const [],
  });

  /// What the list calls it.
  final String name;

  /// Its file, as glTF-Sample-Assets names it.
  final String file;

  /// Why it is in the list.
  final String shows;

  /// The files it names beside itself — an OBJ's material library and its
  /// pictures — handed over under names beside its own, as a host serving
  /// them from one directory would.
  final List<String> companions;
}

/// Somebody else's models, and what their files hold — glTF, FBX and OBJ.
///
/// Meshes shows that a file loads. This shows what is in one beyond the
/// geometry: the clips a character can play, the looks a product comes in,
/// the lights a lamp carries — read back from the renderer as it loaded them,
/// so the controls are the file's own names rather than numbers typed into a
/// box.
///
/// The files are Khronos's samples, fetched rather than committed:
///
///   ./tool/fetch_import_samples.sh
///
/// and read here as bytes and handed to the renderer, the way a browser, an
/// Android package or a download would — so the same example works in all of
/// them. In a browser they are read from `samples/` beside the page.
class ImportedExample extends Example {
  ImportedExample();

  static const samples = [
    ImportedSample('Fox', 'Fox.glb', 'Three clips on one skin'),
    ImportedSample('Cesium Man', 'CesiumMan.glb', 'A walk cycle'),
    ImportedSample(
      'Shoe',
      'MaterialsVariantsShoe.glb',
      'One model, three looks',
    ),
    ImportedSample('Lamp', 'LightsPunctualLamp.glb', 'Lights inside the file'),
    ImportedSample('Clear coat', 'ClearCoatTest.glb', 'A lacquer over paint'),
    ImportedSample('Sheen chair', 'SheenChair.glb', 'Velvet'),
    ImportedSample(
      'Barn lamp',
      'AnisotropyBarnLamp.glb',
      'Something the renderer cannot draw, said out loud',
    ),
    ImportedSample(
      'Dancer',
      'Samba Dancing.fbx',
      'An FBX from Mixamo, converted on the way in',
    ),
    ImportedSample(
      'Man',
      'male02/male02.obj',
      'An OBJ with its material library and pictures',
      companions: [
        'male02/male02.mtl',
        'male02/01_-_Default1noCulling.JPG',
        'male02/male-02-1noCulling.JPG',
        'male02/orig_02_-_Defaul1noCulling.JPG',
      ],
    ),
  ];

  @override
  String get name => 'Imported models';

  @override
  ExampleSection get section => ExampleSection.content;

  @override
  String get blurb =>
      'Clips, material variants and lights out of glTF files, named by the '
      'files themselves.';

  @override
  ViewPoint get viewpoint =>
      const ViewPoint(distance: 4.2, pitch: 0.18, height: 0.8, yaw: 0.6);

  @override
  Downloadable? get needs => const Downloadable(
    what: "Khronos's glTF samples",
    size: 'about 25 MB',
    from: 'KhronosGroup/glTF-Sample-Assets',
    licence: 'CC BY 4.0 and others, per model',
    command: ['tool/fetch_import_samples.sh'],
  );

  /// Where the fetch script puts the files, and an override for a checkout
  /// somewhere else, or a sandbox that can only read its own container.
  ///
  /// Settable rather than read only from `Platform.environment`, which on the
  /// iOS simulator is always empty however the app is launched: the gallery
  /// reads the real environment its own way and says here what it found.
  String directory =
      Platform.environment['ORBLIT_SAMPLES'] ?? '../orblit/assets/samples';

  /// Which sample is showing, by its [ImportedSample.name].
  String get model => _model;
  set model(String wanted) {
    if (wanted == _model) return;
    _model = wanted;
    clip = null;
    variant = null;
    _fadingFrom = null;
  }

  /// The Fox, unless the environment names another — the same override the
  /// sample directory has, and for a neighbouring reason: something driving
  /// the gallery from outside has no settings panel to click, and the first
  /// sample in the list is the smallest file rather than the best picture.
  String _model = samples
      .firstWhere(
        (s) => s.name == Platform.environment['ORBLIT_MODEL'],
        orElse: () => samples.first,
      )
      .name;

  /// Which of the file's clips plays, or null for the first it has.
  int? clip;

  /// Which of the file's material variants it wears, or null for its own.
  int? variant;

  bool playing = true;
  double speed = 1;

  /// Whether the lights the file carries are stated as scene lights.
  bool fileLights = true;

  /// Whether the sun is up. The lamp's own lights are hard to see at noon.
  bool daylight = true;

  /// The clip being faded out of, and when the fade began.
  int? _fadingFrom;
  double _fadeStarted = 0;

  /// How long a change of clip takes to blend, in seconds.
  static const _fade = 0.4;

  ImportedSample get _sample =>
      samples.firstWhere((s) => s.name == _model, orElse: () => samples.first);

  String get _resource => OrblitResources.nameFor('samples/${_sample.file}');

  /// Which files have been handed to the renderer, and which are on their way.
  final Set<String> _provided = {};
  final Set<String> _reading = {};

  /// The moment [scene] last ran, so a change of clip made in the settings
  /// starts its fade at the right time.
  double _now = 0;

  void _provide(ImportedSample sample) {
    final resource = OrblitResources.nameFor('samples/${sample.file}');
    if (_provided.contains(resource) || !_reading.add(resource)) return;
    // The companions first, so the file is never named before what it names.
    Future<bool> handOver() async {
      for (final file in [...sample.companions, sample.file]) {
        final bytes = await _read(file);
        if (bytes == null) {
          note =
              'No $file in $directory. Fetch it with '
              'tool/fetch_import_samples.sh.';
          return false;
        }
        await OrblitResources.provide(
          OrblitResources.nameFor('samples/$file'),
          bytes,
        );
      }
      return true;
    }

    handOver()
        .then((whole) {
          if (whole) _provided.add(resource);
        })
        .whenComplete(() => _reading.remove(resource));
  }

  Future<Uint8List?> _read(String file) async {
    if (kIsWeb) return fetchBytes(Uri(path: 'samples/$file').toString());
    final found = File('$directory/$file');
    if (!found.existsSync()) return null;
    return found.readAsBytes();
  }

  /// A placement that stands the model on the ground, about as tall as a
  /// person, whatever units its file was made in — worked out from the bounds
  /// the renderer reported, so it is the file's own until those arrive.
  Matrix4 _standing(OrblitAssetInfo? info) {
    if (info == null) return Matrix4.identity();
    final low = info.boundsMin;
    final high = info.boundsMax;
    final size = high - low;
    final largest = [size.x, size.y, size.z].reduce((a, b) => a > b ? a : b);
    if (largest <= 0) return Matrix4.identity();
    final scale = 1.6 / largest;
    return Matrix4.identity()
      ..translateByDouble(
        -(low.x + high.x) / 2 * scale,
        -low.y * scale,
        -(low.z + high.z) / 2 * scale,
        1,
      )
      ..scaleByDouble(scale, scale, scale, 1);
  }

  @override
  OrblitScene scene(OrblitCamera camera, double seconds) {
    _now = seconds;
    _provide(_sample);
    final ready = _provided.contains(_resource);
    final info = models[_resource];
    final placement = _standing(info);

    OrblitAnimation? animation;
    final clips = info?.clips.length ?? 0;
    if (ready && clips > 0) {
      final playingClip = (clip ?? 0).clamp(0, clips - 1);
      final time = playing ? seconds * speed : 0.0;
      final rate = playing ? speed : 0.0;
      final from = _fadingFrom;
      final fade = ((seconds - _fadeStarted) / _fade).clamp(0.0, 1.0);
      if (fade >= 1) _fadingFrom = null;
      animation = OrblitAnimation(
        clip: playingClip,
        seconds: time,
        speed: rate,
        from: from != null && fade < 1
            ? OrblitAnimation(clip: from, seconds: time, speed: rate)
            : null,
        fade: fade,
      );
    }

    return OrblitScene(
      objects: [
        if (ready)
          OrblitObject(
            key: 1,
            transform: placement,
            colour: linearOf(const Color(0xFFD9634F)),
            mesh: _resource,
            animation: animation,
            variant: variant,
          ),
        OrblitObject(
          key: 2,
          transform: Matrix4.identity()
            ..setTranslation(Vector3(0, -0.05, 0))
            ..scaleByDouble(6, 0.05, 6, 1),
          colour: linearOf(const Color(0xFF3B424C)),
          castShadows: false,
        ),
      ],
      lights: [
        if (daylight)
          OrblitLight(
            key: 3,
            kind: OrblitLightKind.directional,
            intensity: 76000,
            direction: Vector3(-0.5, -1, -0.4)..normalize(),
            colour: linearOf(const Color(0xFFFFF3E0)),
          ),
        // The file's own lights, as ordinary lights: the renderer takes them
        // out of the model, so they shadow and count like any other.
        if (ready && fileLights && info != null)
          ...info.lightsFor(placement, keyOf: (index) => 100 + index),
      ],
      sky: OrblitSky(
        colour: linearOf(const Color(0xFF1B222C)),
        ambient: daylight ? 14000 : 2,
      ),
      // At night, a camera for night. A file's lights are stated as the file
      // made them, and the lamp's bulb is about twenty lumens — a real bulb's
      // worth, which at a daylight exposure is black.
      camera: daylight
          ? camera
          : OrblitCamera(
              position: camera.position,
              target: camera.target,
              fieldOfView: camera.fieldOfView,
              aperture: 1.4,
              shutterSpeed: 1 / 30,
              sensitivity: 3200,
            ),
    );
  }

  @override
  Widget settings(BuildContext context, VoidCallback changed) {
    final info = models[_resource];
    final clips = info?.clips ?? const <OrblitClipInfo>[];
    final variants = info?.variants ?? const <String>[];
    final clipNames = [
      for (var i = 0; i < clips.length; i++)
        clips[i].name.isEmpty ? 'Clip ${i + 1}' : clips[i].name,
    ];

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Choice(
          label: 'Model',
          options: [for (final sample in samples) sample.name],
          selected: _model,
          onSelect: (value) {
            model = value;
            note = null;
            changed();
          },
        ),
        Padding(
          padding: const EdgeInsets.symmetric(vertical: 4),
          child: Text(
            _sample.shows,
            style: const TextStyle(fontSize: 12, color: Colors.white70),
          ),
        ),
        Choice(
          label: 'Clip',
          options: clipNames.isEmpty ? const ['None'] : clipNames,
          selected: clipNames.isEmpty
              ? 'None'
              : clipNames[(clip ?? 0).clamp(0, clipNames.length - 1)],
          onSelect: clipNames.isEmpty
              ? null
              : (value) {
                  final chosen = clipNames.indexOf(value);
                  if (chosen < 0 || chosen == (clip ?? 0)) return;
                  _fadingFrom = clip ?? 0;
                  _fadeStarted = _now;
                  clip = chosen;
                  changed();
                },
        ),
        Choice(
          label: 'Look',
          options: ['As made', ...variants],
          selected: variant == null || variant! >= variants.length
              ? 'As made'
              : variants[variant!],
          onSelect: variants.isEmpty
              ? null
              : (value) {
                  final chosen = variants.indexOf(value);
                  variant = chosen < 0 ? null : chosen;
                  changed();
                },
        ),
        Toggle(
          label: 'Playing',
          value: playing,
          onChanged: (value) {
            playing = value;
            changed();
          },
        ),
        Setting(
          label: 'Speed',
          value: speed,
          min: 0.1,
          max: 2,
          decimals: 1,
          onChanged: (value) {
            speed = value;
            changed();
          },
        ),
        Toggle(
          label: "The file's lights",
          value: fileLights,
          onChanged: (value) {
            fileLights = value;
            changed();
          },
        ),
        Toggle(
          label: 'Daylight',
          value: daylight,
          onChanged: (value) {
            daylight = value;
            changed();
          },
        ),
        if (info != null && info.unsupported.isNotEmpty)
          Padding(
            padding: const EdgeInsets.only(top: 8),
            child: Text(
              'Not drawn: ${info.unsupported.join(', ')}',
              style: const TextStyle(fontSize: 12, color: Colors.orangeAccent),
            ),
          ),
      ],
    );
  }

  @override
  String get code => '''
// What a file holds comes back from the renderer as it is loaded.
OrblitView(
  scene: scene,
  onAssetInfo: (info) => setState(() => models[info.path] = info),
)

// A clip by the file's own name, fading in from the one before.
final info = models['orblit:resource/fox.glb']!;
OrblitObject(
  key: 1,
  transform: placement,
  colour: grey,
  mesh: 'orblit:resource/fox.glb',
  animation: OrblitAnimation(
    clip: info.clipNamed('Run')!,
    seconds: seconds,
    speed: 1,
    from: OrblitAnimation(clip: info.clipNamed('Walk')!, seconds: seconds),
    fade: 0.5,
  ),
)

// A look the file comes in, by its name.
OrblitObject(..., variant: info.variants.indexOf('beach'))

// The file's lights, as ordinary lights that shadow like any other.
lights: [...info.lightsFor(placement, keyOf: (i) => 100 + i)],
''';
}

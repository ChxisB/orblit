// The `setScene` message, decoded and applied — the web's OrblitScene.kt.
//
// This is the third reader of the same message: OrblitFilamentPlugin.swift's
// `Scene`, OrblitScene.kt, and this. It mirrors the Kotlin one deliberately —
// the same field names in the same order, the same "absent means this part of
// the scene has nothing to say" defaults, and the same apply order — so a
// change made to one is easy to find in the others.
//
// What differs is only the crossing. Kotlin gets a `FloatArray` from the
// standard codec and hands it to JNI; here the arrays arrive as Dart typed
// data and have to be copied into the wasm module's own heap before the C ABI
// can read them (OrblitHeap), because a pointer into Dart's heap means nothing
// to WebAssembly.
//
// One deliberate narrowing, the same one Kotlin made: this checks that the
// arrays a call reads are the *shape* the renderer needs, not that every index
// inside them points somewhere valid. Those numbers come from
// `OrblitScene.toMessage()`, the one place they are built.
library;

import 'dart:js_interop';
import 'dart:typed_data';

import 'package:web/web.dart' as web;

import 'orblit_module.dart';

/// The strides the three sides share, as `native_contract_test.dart` pins
/// them. Only the ones this file needs to turn an array length back into a
/// count — the rest are the renderer's business.
const int _lightStride = 22;
const int _probeStride = 8;
const int _materialStride = 37;
const int _videoStride = 4;
const int _decalStride = 22;
const int _splatStride = 18;
const int _passStride = 13;
const int _targetStride = 6;

/// A scene as it arrives over the channel, applied to one renderer.
///
/// [from] returns null for a message missing what every scene must carry —
/// the object arrays and a camera — which is the `bad-scene` case the Swift
/// and Kotlin plugins answer with. Everything else is optional.
class OrblitSceneWeb {
  OrblitSceneWeb._(this._args);

  final Map<Object?, Object?> _args;

  static OrblitSceneWeb? from(Map<Object?, Object?> args) {
    // The same seven the Kotlin side insists on. `objectKeys` is a plain
    // List<int> rather than an Int64List here: dart2js has no Int64List at
    // all, so scene.dart builds its key arrays through `makeKeyList`, whose
    // web half is an ordinary list. See lib/src/key_list.dart.
    if (args['objectKeys'] is! List ||
        args['transforms'] is! Float32List ||
        args['colours'] is! Float32List ||
        args['meshes'] is! Int32List ||
        args['objectFlags'] is! Int32List ||
        args['cameraPosition'] is! Float32List ||
        args['cameraTarget'] is! Float32List) {
      return null;
    }
    final scene = OrblitSceneWeb._(args);
    final count = scene.count;
    if (scene.objectKeys.length != count ||
        scene.meshes.length != count ||
        scene.objectFlags.length != count ||
        scene.colours.length != count * 3 ||
        scene.cameraPosition.length != 3 ||
        scene.cameraTarget.length != 3) {
      return null;
    }
    return scene;
  }

  // ---- readers, with the same "absent is empty" defaults ----------------

  Float32List _floats(String key) =>
      _args[key] as Float32List? ?? Float32List(0);

  Int32List _ints(String key) => _args[key] as Int32List? ?? Int32List(0);

  Uint8List _bytes(String key) => _args[key] as Uint8List? ?? Uint8List(0);

  /// A key array. Int64 on the wire everywhere else; an ordinary list of ints
  /// here, for want of an Int64List on the web.
  List<int> _keys(String key) => [
    for (final value in (_args[key] as List? ?? const []))
      if (value is int) value else (value as num).toInt(),
  ];

  List<String> _strings(String key) => [
    for (final value in (_args[key] as List? ?? const []))
      if (value is String) value else '',
  ];

  String _string(String key) => _args[key] as String? ?? '';

  double _number(String key, double fallback) {
    final value = _args[key];
    return value is num ? value.toDouble() : fallback;
  }

  bool _flag(String key) => _args[key] as bool? ?? false;

  int get count => _floats('transforms').length ~/ 16;

  List<int> get objectKeys => _keys('objectKeys');
  Float32List get colours => _floats('colours');
  Int32List get meshes => _ints('meshes');
  Int32List get objectFlags => _ints('objectFlags');
  Float32List get cameraPosition => _floats('cameraPosition');
  Float32List get cameraTarget => _floats('cameraTarget');

  /// Applies every part of the scene, in the order `Viewport.write(scene:)`
  /// and `OrblitScene.applyTo` both use. The order is not arbitrary — the
  /// renderer resolves indices between calls, so materials must land before
  /// the objects that name them.
  void applyTo(OrblitModule module, int renderer) {
    final heap = OrblitHeap(module);
    try {
      _apply(module, renderer, heap);
    } finally {
      heap.free();
    }
  }

  void _apply(OrblitModule module, int renderer, OrblitHeap heap) {
    final to = _Renderer(module, renderer, heap);

    _environment(to);
    _graph(to);
    _batching(to);
    _godRays(to);
    _videos(to);
    _materials(to);
    _objects(to);
    _poses(to);
    _populations(to);
    _splats(to);
    _sprites(to);
    _terrain(to);
    _lights(to);
    _decals(to);
    _probes(to);
    _field(to);
    _atmosphere(to);
    _camera(to);
    _outline(to);
  }

  void _environment(_Renderer to) {
    final environmentParams = _floats('environmentParams');
    to.call('orblit_renderer_set_environment', [
      to.renderer,
      to.heap.string(_string('environmentRadiance')),
      to.heap.string(_string('environmentSkybox')),
      to.heap.floats(environmentParams),
      environmentParams.length,
    ]);
  }

  void _graph(_Renderer to) {
    final passes = _floats('graphPasses');
    final targets = _floats('graphTargets');
    final targetNames = _strings('graphTargetNames');
    to.call('orblit_renderer_set_render_graph', [
      to.renderer,
      passes.length ~/ _passStride,
      to.heap.floats(passes),
      passes.length,
      targets.length ~/ _targetStride,
      to.heap.floats(targets),
      targets.length,
      to.heap.strings(targetNames),
      targetNames.length,
    ]);
  }

  void _batching(_Renderer to) {
    to.call('orblit_renderer_set_batching', [
      to.renderer,
      _flag('batching') ? 1 : 0,
    ]);
  }

  void _godRays(_Renderer to) {
    final godRays = _floats('godRayParams');
    final distortions = _floats('distortionParams');
    to.call('orblit_renderer_set_god_rays', [
      to.renderer,
      to.heap.floats(godRays),
      godRays.length,
      to.heap.floats(distortions),
      distortions.length,
    ]);
  }

  void _videos(_Renderer to) {
    final videoKeys = _keys('videoKeys');
    final videoParams = _floats('videoParams');
    to.call('orblit_renderer_apply_videos', [
      to.renderer,
      videoKeys.isNotEmpty
          ? videoKeys.length
          : videoParams.length ~/ _videoStride,
      to.heap.int64s(videoKeys),
      to.heap.ints(_ints('videoFlags')),
      to.heap.floats(videoParams),
      videoParams.length,
      to.heap.strings(_strings('videoPaths')),
      _strings('videoPaths').length,
    ]);
  }

  void _materials(_Renderer to) {
    final materialKeys = _keys('materialKeys');
    final materialParams = _floats('materialParams');
    final materialCount = materialKeys.isNotEmpty
        ? materialKeys.length
        : materialParams.length ~/ _materialStride;
    final materialMaps = _ints('materialMaps');
    final texturePaths = _strings('texturePaths');
    // Absent means "every material has no video", which is -1 each, not an
    // empty array — the same default Kotlin's `intsOr` supplies.
    final materialVideos =
        _args['materialVideos'] as Int32List? ??
        Int32List.fromList(List<int>.filled(materialCount, -1));
    to.call('orblit_renderer_apply_materials', [
      to.renderer,
      materialCount,
      to.heap.int64s(materialKeys),
      to.heap.ints(_ints('materialFlags')),
      to.heap.floats(materialParams),
      materialParams.length,
      to.heap.ints(materialMaps),
      // The total number of map indices, not the number of materials: the ABI
      // spells this the same way it spells `param_floats` just above — how
      // many elements the array holds end to end, which it checks against
      // count * ORBLIT_STRIDE_MATERIAL_MAPS before reading any of them.
      materialMaps.length,
      to.heap.strings(texturePaths),
      to.heap.ints(_ints('textureSrgb')),
      texturePaths.length,
      to.heap.ints(materialVideos),
    ]);
  }

  void _objects(_Renderer to) {
    final transforms = _floats('transforms');
    final objectColours = colours;
    final morphWeights = _floats('objectMorphWeights');
    final meshPaths = _strings('meshPaths');
    final objectMaterials =
        _args['objectMaterials'] as Int32List? ??
        Int32List.fromList(List<int>.filled(count, -1));
    final morphCounts =
        _args['objectMorphCounts'] as Int32List? ?? Int32List(count);
    to.call('orblit_renderer_apply_objects', [
      to.renderer,
      count,
      to.heap.int64s(objectKeys),
      to.heap.floats(transforms),
      transforms.length,
      to.heap.floats(objectColours),
      objectColours.length,
      to.heap.ints(meshes),
      to.heap.ints(objectFlags),
      to.heap.ints(objectMaterials),
      to.heap.ints(morphCounts),
      to.heap.floats(morphWeights),
      morphWeights.length,
      to.heap.strings(meshPaths),
      meshPaths.length,
    ]);
  }

  /// Poses address the objects just applied by key, so they come straight
  /// after them — and every time, even with none, so an object that stops
  /// being posed goes back to rest. Absent from the message when nothing
  /// is posed, which reads as none.
  void _poses(_Renderer to) {
    final poseKeys = _keys('poseKeys');
    final poseInts = _ints('poseInts');
    final poseFloats = _floats('poseFloats');
    final poseJoints = _ints('poseJoints');
    final poseJointTransforms = _floats('poseJointTransforms');
    to.call('orblit_renderer_apply_poses', [
      to.renderer,
      poseKeys.length,
      to.heap.int64s(poseKeys),
      to.heap.ints(poseInts),
      poseInts.length,
      to.heap.floats(poseFloats),
      poseFloats.length,
      to.heap.ints(_ints('poseJointCounts')),
      to.heap.ints(poseJoints),
      poseJoints.length,
      to.heap.floats(poseJointTransforms),
      poseJointTransforms.length,
      _number('at', 0),
    ]);
  }

  /// Absent from the message altogether when a scene has none.
  void _populations(_Renderer to) {
    final populationKeys = _ints('populationKeys');
    if (populationKeys.isNotEmpty) {
      final bounds = _floats('populationBounds');
      final populationPaths = _strings('populationPaths');
      final changed = _ints('populationChanged');
      final populationTransforms = _floats('populationTransforms');
      final populationColours = _floats('populationColours');
      to.call('orblit_renderer_apply_populations', [
        to.renderer,
        populationKeys.length,
        to.heap.ints(populationKeys),
        to.heap.ints(_ints('populationCounts')),
        to.heap.ints(_ints('populationMeshes')),
        to.heap.ints(_ints('populationFlags')),
        to.heap.ints(_ints('populationRevisions')),
        to.heap.floats(_floats('populationRanges')),
        to.heap.floats(bounds),
        bounds.length,
        to.heap.strings(populationPaths),
        populationPaths.length,
        to.heap.ints(changed),
        changed.length,
        to.heap.floats(populationTransforms),
        populationTransforms.length,
        to.heap.floats(populationColours),
        populationColours.length,
      ]);
    }
  }

  void _splats(_Renderer to) {
    final splatKeys = _ints('splatKeys');
    final splatParams = _floats('splatParams');
    final splatPaths = _strings('splatPaths');
    final splatChanged = _ints('splatChanged');
    final splatData = _bytes('splatData');
    to.call('orblit_renderer_apply_splats', [
      to.renderer,
      splatKeys.isNotEmpty
          ? splatKeys.length
          : splatParams.length ~/ _splatStride,
      to.heap.ints(splatKeys),
      to.heap.ints(_ints('splatFlags')),
      to.heap.ints(_ints('splatRevisions')),
      to.heap.floats(splatParams),
      splatParams.length,
      to.heap.strings(splatPaths),
      splatPaths.length,
      to.heap.ints(splatChanged),
      to.heap.ints(_ints('splatChangedCounts')),
      splatChanged.length,
      to.heap.uint8s(splatData),
      splatData.length,
    ]);
  }

  void _sprites(_Renderer to) {
    final spriteKeys = _ints('spriteKeys');
    final spriteParams = _floats('spriteParams');
    final spritePaths = _strings('spritePaths');
    final spriteChanged = _ints('spriteChanged');
    final spriteData = _floats('spriteData');
    to.call('orblit_renderer_apply_sprites', [
      to.renderer,
      spriteKeys.length,
      to.heap.ints(spriteKeys),
      to.heap.ints(_ints('spriteFlags')),
      to.heap.ints(_ints('spriteOrders')),
      to.heap.ints(_ints('spriteRevisions')),
      to.heap.floats(spriteParams),
      spriteParams.length,
      to.heap.strings(spritePaths),
      spritePaths.length,
      to.heap.ints(spriteChanged),
      to.heap.ints(_ints('spriteChangedCounts')),
      spriteChanged.length,
      to.heap.floats(spriteData),
      spriteData.length,
    ]);
  }

  void _terrain(_Renderer to) {
    final terrainInts = _ints('terrainInts');
    final terrainFloats = _floats('terrainFloats');
    final terrainData = _bytes('terrainData');
    to.call('orblit_renderer_apply_terrain', [
      to.renderer,
      to.heap.ints(terrainInts),
      terrainInts.length,
      to.heap.floats(terrainFloats),
      terrainFloats.length,
      to.heap.uint8s(terrainData),
      terrainData.length,
    ]);
  }

  void _lights(_Renderer to) {
    final lightKeys = _keys('lightKeys');
    final lightParams = _floats('lightParams');
    to.call('orblit_renderer_apply_lights', [
      to.renderer,
      lightKeys.isNotEmpty
          ? lightKeys.length
          : lightParams.length ~/ _lightStride,
      to.heap.int64s(lightKeys),
      to.heap.ints(_ints('lightKinds')),
      to.heap.ints(_ints('lightFlags')),
      to.heap.floats(lightParams),
      lightParams.length,
    ]);
  }

  void _decals(_Renderer to) {
    final decalParams = _floats('decalParams');
    final decalPaths = _strings('decalPaths');
    to.call('orblit_renderer_apply_decals', [
      to.renderer,
      decalParams.length ~/ _decalStride,
      to.heap.floats(decalParams),
      decalParams.length,
      to.heap.ints(_ints('decalImages')),
      to.heap.strings(decalPaths),
      decalPaths.length,
    ]);
  }

  void _probes(_Renderer to) {
    final probeKeys = _keys('probeKeys');
    final probeParams = _floats('probeParams');
    to.call('orblit_renderer_apply_probes', [
      to.renderer,
      probeKeys.isNotEmpty
          ? probeKeys.length
          : probeParams.length ~/ _probeStride,
      to.heap.int64s(probeKeys),
      to.heap.floats(probeParams),
      probeParams.length,
    ]);
  }

  /// The irradiance field, only when there is one.
  void _field(_Renderer to) {
    final fieldParams = _floats('fieldParams');
    if (fieldParams.isNotEmpty) {
      to.call('orblit_renderer_apply_field', [
        to.renderer,
        to.heap.floats(fieldParams),
        fieldParams.length,
        to.heap.string(_string('fieldFrom')),
      ]);
    }
  }

  /// The atmosphere, and the frame's composition over it.
  void _atmosphere(_Renderer to) {
    to.call('orblit_renderer_set_sky_colour', [
      to.renderer,
      to.heap.floats(_floats('skyColour')),
      _number('ambient', 0),
      _flag('showBody') ? 1 : 0,
    ]);

    final fogParams = _floats('fogParams');
    to.call('orblit_renderer_set_fog', [
      to.renderer,
      _flag('fogEnabled') ? 1 : 0,
      to.heap.floats(fogParams),
      fogParams.length,
    ]);

    final postParams = _floats('postParams');
    if (postParams.isNotEmpty) {
      to.call('orblit_renderer_set_post_process', [
        to.renderer,
        to.heap.floats(postParams),
        postParams.length,
      ]);
    }

    final pipelineParams = _floats('pipelineParams');
    if (pipelineParams.isNotEmpty) {
      to.call('orblit_renderer_set_pipeline', [
        to.renderer,
        to.heap.floats(pipelineParams),
        pipelineParams.length,
      ]);
    }

    final precipitationParams = _floats('precipitationParams');
    to.call('orblit_renderer_set_precipitation', [
      to.renderer,
      _flag('precipitationEnabled') ? 1 : 0,
      to.heap.floats(precipitationParams),
      precipitationParams.length,
    ]);

    final skyParams = _floats('skyParams');
    to.call('orblit_renderer_set_sky', [
      to.renderer,
      _flag('skyEnabled') ? 1 : 0,
      to.heap.floats(skyParams),
      skyParams.length,
    ]);
  }

  /// Where the camera is, and what the film is.
  void _camera(_Renderer to) {
    to.call('orblit_renderer_set_camera', [
      to.renderer,
      to.heap.floats(cameraPosition),
      to.heap.floats(cameraTarget),
      _number('fieldOfView', 45),
      _flag('orthographic') ? 1 : 0,
      _number('viewHeight', 1),
      _number('at', 0),
    ]);

    to.call('orblit_renderer_set_exposure', [
      to.renderer,
      _number('aperture', 16),
      _number('shutterSpeed', 1.0 / 125.0),
      _number('sensitivity', 100),
    ]);
  }

  void _outline(_Renderer to) {
    final outlineKeys = _keys('outlineKeys');
    final outlineParams = _floats('outlineParams');
    to.call('orblit_renderer_set_outline', [
      to.renderer,
      to.heap.int64s(outlineKeys),
      outlineKeys.length,
      to.heap.floats(outlineParams),
      outlineParams.length,
    ]);
  }
}

/// One renderer, and what it takes to reach it: the module the scene calls
/// live in, and the heap their arrays are written to.
class _Renderer {
  _Renderer(this.module, this.renderer, this.heap);

  final OrblitModule module;
  final int renderer;
  final OrblitHeap heap;

  /// Makes one scene call, and says any orblit_result that is not ORBLIT_OK
  /// out loud.
  ///
  /// The ABI answers a refusal with a code rather than throwing, and a scene
  /// call that applies nothing — a short array, or a null pointer with a
  /// non-zero count — is otherwise indistinguishable from one that worked:
  /// the frame still draws, only without whatever was refused. That is
  /// exactly the shape of a bug that looks like "the lighting is broken".
  void call(String name, List<Object?> args) {
    final result = orblitCall(module, name, args);
    if (result != 0) {
      // console, not debugPrint: this has to be audible in a release build,
      // which is what `flutter build web` produces and what a headless
      // capture runs. debugPrint goes through Flutter's own printing, which
      // a release build is free to say nothing through — and a diagnostic
      // that is silent in the build you actually ship is worse than none,
      // because its silence reads as "nothing was refused".
      web.console.warn('[orblit] $name refused the scene: $result'.toJS);
    }
  }
}

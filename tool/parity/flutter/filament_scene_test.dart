// Writes dotnet/fixtures/orblit_filament/scene.json: the arrays the scene
// packer sends for a handful of scenes, as bytes. It needs the Flutter package
// it reads, so tool/parity/flutter/regen.sh copies it into that package's test
// folder, runs it there and removes the copy.
import 'dart:convert';
import 'dart:io';
import 'dart:math' as math;
import 'dart:typed_data';
import 'dart:ui' show Color;

import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_filament/orblit_filament.dart';
import 'package:vector_math/vector_math_64.dart';

OrblitCamera _camera() =>
    OrblitCamera(position: Vector3(0, 2, 8), target: Vector3.zero());

OrblitObject _object(
  int key, {
  String? mesh,
  Matrix4? transform,
  Vector3? colour,
  bool castShadows = true,
  bool receiveShadows = true,
  bool visible = true,
  int layer = 0,
  List<double>? morphWeights,
  OrblitAnimation? animation,
  int? variant,
  List<OrblitJointPose>? joints,
  int? material,
}) => OrblitObject(
  key: key,
  mesh: mesh,
  transform: transform ?? Matrix4.identity(),
  colour: colour ?? Vector3(1, 1, 1),
  castShadows: castShadows,
  receiveShadows: receiveShadows,
  visible: visible,
  layer: layer,
  morphWeights: morphWeights,
  animation: animation,
  variant: variant,
  joints: joints,
  material: material,
);

Float32List _ramp(int count, double start) =>
    Float32List.fromList([for (var i = 0; i < count; i++) start + i * 0.25]);

OrblitTerrainRegion _region(int x, int z, {int revision = 1}) =>
    OrblitTerrainRegion(
      x: x,
      z: z,
      heights: _ramp(256, x + z * 0.5),
      cover: Uint32List.fromList([for (var i = 0; i < 256; i++) 0xF8000000 + i]),
      colour: Uint8List.fromList([for (var i = 0; i < 1024; i++) i % 251]),
      revision: revision,
    );

Uint8List _picture(int size, int value) =>
    Uint8List(size * size * 4)..fillRange(0, size * size * 4, value);

OrblitEnvironmentVolume _hall() => OrblitEnvironmentVolume.box(
  key: 1,
  centre: Vector3.zero(),
  halfExtents: Vector3.all(5),
  blendDistance: 2,
  overrides: OrblitEnvironmentOverrides(
    fogDensity: 0.1,
    fogColour: Vector3(0.4, 0.3, 0.2),
    ambient: 2000,
    exposureCompensation: -1,
  ),
);

Map<String, ({OrblitScene scene, double? at})> _scenes() => {
  'objects': (
    scene: OrblitScene(
      camera: _camera(),
      objects: [
        _object(1, mesh: '/m/a.glb'),
        _object(
          2,
          mesh: '/m/a.glb',
          transform:
              Matrix4.translationValues(1, 2, 3) * Matrix4.rotationY(0.7),
          colour: Vector3(0.1, 0.25, 0.9),
          castShadows: false,
          receiveShadows: false,
          layer: 3,
        ),
        _object(
          3,
          colour: Vector3(2, -1, 1 / 3),
          visible: false,
          layer: 99,
          morphWeights: [0.25, 0.5],
        ),
        _object(4, mesh: '/m/b.glb', morphWeights: []),
        _object(5, mesh: '/m/c.glb', receiveShadows: false, morphWeights: [1]),
      ],
    ),
    at: null,
  ),
  'poses': (
    scene: OrblitScene(
      camera: _camera(),
      objects: [
        _object(1, mesh: '/m/1.glb'),
        _object(
          7,
          mesh: '/m/7.glb',
          animation: const OrblitAnimation(
            clip: 2,
            seconds: 1.5,
            speed: 0.5,
            fade: 0.25,
          ),
        ),
        _object(
          3,
          mesh: '/m/3.glb',
          animation: const OrblitAnimation(
            clip: 1,
            seconds: 0.25,
            speed: 2,
            loop: false,
            from: OrblitAnimation(clip: 0, seconds: 3, speed: 1, loop: false),
            fade: 0.5,
          ),
        ),
        _object(2, mesh: '/m/2.glb', variant: 3),
        _object(
          5,
          mesh: '/m/5.glb',
          joints: [
            OrblitJointPose(
              skin: 0,
              joint: 6,
              transform: Matrix4.translationValues(1, 2, 3),
            ),
            OrblitJointPose(
              skin: 1,
              joint: 2,
              transform: Matrix4.rotationZ(0.3),
            ),
          ],
        ),
        _object(6, mesh: '/m/6.glb', joints: const []),
      ],
    ),
    at: 12.5,
  ),
  'lights': (
    scene: OrblitScene(
      camera: _camera(),
      objects: const [],
      lights: [
        OrblitLight(
          key: 20,
          kind: OrblitLightKind.directional,
          intensity: 100000,
          colour: Vector3(1, 0.9, 0.8),
          direction: Vector3(0.2, -1, 0.1),
          sunAngularRadius: 0.3,
          haloSize: 12,
          haloFalloff: 70,
        ),
        OrblitLight(
          key: 21,
          kind: OrblitLightKind.point,
          intensity: 800,
          position: Vector3(1, 2, 3),
          falloffRadius: 7,
          sourceRadius: 0.2,
          castShadows: false,
        ),
        OrblitLight(
          key: 22,
          kind: OrblitLightKind.spot,
          intensity: 1200,
          position: Vector3(0, 3, 0),
          innerConeAngle: 0.3,
          outerConeAngle: 0.7,
          falloffRadius: 12,
        ),
        OrblitLight(
          key: 23,
          kind: OrblitLightKind.area,
          intensity: 500,
          position: Vector3(0, 1, -2),
          direction: Vector3(0, 0, 1),
          width: 2,
          height: 0.5,
          tangent: Vector3(0, 0, 1),
        ),
      ],
    ),
    at: null,
  ),
  'air': (
    scene: OrblitScene(
      camera: _camera(),
      objects: const [],
      sky: OrblitSky(
        colour: Vector3(0.2, 0.3, 0.4),
        zenith: Vector3(0.1, 0.2, 0.5),
        horizon: Vector3(0.7, 0.8, 0.9),
        ambient: 15000,
        quality: SkyQuality.fair,
        bodyDirection: Vector3(0, 2, 0),
        bodyColour: Vector3(1, 0.8, 0.6),
        bodySize: 0.01,
        flash: 0.5,
        flashDirection: Vector3(1, 1, 0),
        flashSeed: 7,
        clouds: OrblitClouds(
          colour: Vector3(0.9, 0.9, 0.95),
          cover: 0.5,
          wind: Vector2(3, 1),
          featureSize: 1 / 500,
          altitude: 1200,
          thickness: 700,
          density: 0.8,
          billow: 0.6,
          extinction: 0.02,
        ),
      ),
      fog: OrblitFog(
        colour: Vector3(0.4, 0.5, 0.6),
        density: 0.1,
        distance: 5,
        cutOffDistance: 500,
        maximumOpacity: 0.9,
        height: 3,
        heightFalloff: 0.5,
        structure: 0.4,
        wind: Vector2(0.1, 0.2),
        featureSize: 0.05,
        thickness: 8,
      ),
      precipitation: OrblitPrecipitation(
        colour: Vector3(0.8, 0.8, 0.9),
        amount: 0.6,
        fall: 12,
        wind: Vector2(1, 0.5),
        dropsPerMetre: 8,
        stretch: 30,
        threshold: 0.6,
      ),
    ),
    at: null,
  ),
  'air-fallbacks': (
    scene: OrblitScene(
      camera: _camera(),
      objects: const [],
      sky: OrblitSky(
        drawn: false,
        showBody: false,
        bodyDirection: Vector3.zero(),
        flashDirection: Vector3.zero(),
      ),
      fog: OrblitFog(),
    ),
    at: null,
  ),
  'materials': (
    scene: OrblitScene(
      camera: _camera(),
      objects: [
        _object(1, material: 20),
        _object(2, material: 21),
        _object(3, material: 999),
        _object(4),
        _object(5, material: 22),
      ],
      videos: const [
        OrblitVideo(key: 70, path: '/v/a.mp4'),
        OrblitVideo(key: 71, path: '/v/b.mp4', playing: false, loop: false),
      ],
      materials: [
        OrblitMaterial(
          key: 20,
          shading: OrblitShading.unlit,
          blend: OrblitBlend.masked,
          culling: OrblitCulling.none,
          doubleSided: true,
          baseColour: Vector4(0.2, 0.4, 0.6, 0.5),
          metallic: 0.9,
          roughness: 0.2,
          reflectance: 0.7,
          clearCoat: 0.6,
          clearCoatRoughness: 0.05,
          anisotropy: -0.4,
          sheenColour: Vector3(0.3, 0.2, 0.1),
          sheenRoughness: 0.45,
          wind: const OrblitWind(bearing: 90, speed: 6, strength: 0.75),
          emissive: Vector3(0.5, 0.25, 0.1),
          emissiveIntensity: 3,
          ambientOcclusion: 0.8,
          normalScale: 1.5,
          tiling: Vector2(2, 3),
          offset: Vector2(0.25, 0.5),
          maskThreshold: 0.55,
          depthWrite: false,
          depthBias: 0.01,
          wrap: OrblitWrap.mirror,
          filter: OrblitFilter.sharp,
          baseColourMap: const OrblitTexture('/art/a.png'),
          normalMap: const OrblitTexture('/art/n.png', srgb: false),
          metallicRoughnessMap: const OrblitTexture('/art/n.png', srgb: false),
          occlusionMap: const OrblitTexture('/art/a.png', srgb: false),
          emissiveMap: const OrblitTexture('/art/e.png'),
        ),
        OrblitMaterial(
          key: 21,
          shading: OrblitShading.video,
          video: 71,
          screenMapped: true,
          litScreen: true,
          blendMode: OrblitBlendMode.masked,
          blendAmount: 0.6,
          blendSharpness: 4,
          blendTiling: Vector2(3, 3),
          blendOffset: Vector2(0.1, 0.2),
          blendBaseColourMap: const OrblitTexture('/art/b.png'),
          blendMaskMap: const OrblitTexture('/art/m.png', srgb: false),
          baseColourMap: const OrblitTexture('/art/a.png'),
        ),
        OrblitMaterial(
          key: 22,
          shading: OrblitShading.shadowCatcher,
          video: 999,
          wind: const OrblitWind(bearing: 0, speed: 0, strength: 2),
        ),
        const OrblitMaterial(key: 20, roughness: 0.7),
      ],
    ),
    at: null,
  ),
  'surfaces': (
    scene: OrblitScene(
      camera: _camera(),
      objects: [_object(1, material: 8), _object(2, material: 9)],
      materials: [
        const OrblitMaterial(key: 7),
        OrblitMaterial(
          key: 8,
          surface: OrblitSurface(
            package: '/pkg/water.matc',
            values: {
              'speed': [2.5],
              'tint': [1, 0.5, 0.25, 1],
            },
            textures: {'noise': const OrblitTexture('/art/noise.png')},
          ),
        ),
        OrblitMaterial(
          key: 9,
          surface: OrblitSurface(
            package: '/pkg/water.matc',
            values: {
              'speed': [4],
            },
            textures: {'noise': const OrblitTexture('/art/noise.png')},
          ),
        ),
      ],
    ),
    at: null,
  ),
  'decals': (
    scene: OrblitScene(
      camera: _camera(),
      objects: const [],
      decals: [
        OrblitDecal(
          key: 1,
          position: Vector3(1, 2, 3),
          size: Vector3(2, 0.5, 4),
          rotation: Quaternion.axisAngle(Vector3(1, 0, 0), 1.5707963267948966),
          texture: const OrblitTexture('/art/scorch.png'),
          colour: Vector3(0.9, 0.8, 0.7),
          opacity: 0.75,
          fadeStartAngle: 0.5,
          fadeEndAngle: 1.2,
          roughness: 0.3,
          layers: {0, 2, 9, -1},
          sortOrder: 3,
        ),
        OrblitDecal(
          key: 2,
          position: Vector3.zero(),
          size: Vector3(1, 1, 1),
          metallic: 0.8,
        ),
        OrblitDecal(
          key: 3,
          position: Vector3(0, 1, 0),
          size: Vector3(3, 3, 3),
          texture: const OrblitTexture('/art/scorch.png', srgb: false),
          fadeStartAngle: OrblitDecal.noFade,
          fadeEndAngle: OrblitDecal.noFade,
        ),
      ],
    ),
    at: null,
  ),
  'outline': (
    scene: OrblitScene(
      camera: _camera(),
      objects: [_object(3), _object(7), _object(9)],
      outline: const OrblitOutline(
        keys: {3, 7, 9},
        primary: 7,
        colour: Color(0xFF00FF00),
        primaryColour: Color(0x80FF0000),
        width: 400,
        occluded: OrblitOccluded.faint,
        occludedOpacity: 0.25,
        dash: -5,
      ),
    ),
    at: null,
  ),
  'outline-only-primary': (
    scene: OrblitScene(
      camera: _camera(),
      objects: const [],
      outline: const OrblitOutline(primary: 4, width: -2),
    ),
    at: null,
  ),
  'screen': (
    scene: OrblitScene(
      camera: _camera(),
      objects: const [],
      lights: [
        OrblitLight(
          key: 1,
          kind: OrblitLightKind.point,
          intensity: 100,
          position: Vector3(1, 1, 1),
        ),
        OrblitLight(
          key: 2,
          kind: OrblitLightKind.directional,
          intensity: 1000,
          colour: Vector3(1, 0.8, 0.6),
          direction: Vector3(0, -0.2, -1).normalized(),
        ),
      ],
      sky: OrblitSky(clouds: OrblitClouds.stratus()),
      godRays: const OrblitGodRays(strength: 0.5, samples: 32),
      distortions: [
        OrblitDistortion.shockwave(
          centre: Vector3(1, 2, 3),
          radius: 4,
          thickness: 0.5,
          strength: 0.02,
          chromatic: 0.3,
        ),
        OrblitDistortion.haze(
          centre: Vector3(-1, 0, 2),
          halfSize: Vector3(0.5, 1, 0.5),
          scale: 0.2,
          speed: 2,
          seconds: 3,
        ),
        OrblitDistortion.lens(strength: -0.1),
        OrblitDistortion.expanding(
          centre: Vector3.zero(),
          age: 1.5,
          speed: 4,
          lifetime: 2,
          strength: 0.04,
        ),
        OrblitDistortion.lens(strength: 0),
      ],
    ),
    at: null,
  ),
  'screen-tint': (
    scene: OrblitScene(
      camera: _camera(),
      objects: const [],
      lights: [
        OrblitLight(
          key: 2,
          kind: OrblitLightKind.directional,
          intensity: 1000,
          direction: Vector3(1, 0, 0),
        ),
      ],
      sky: OrblitSky(drawn: false, clouds: OrblitClouds.stratus()),
      godRays: OrblitGodRays(strength: 1, tint: Vector3(0.2, 0.4, 1)),
    ),
    at: null,
  ),
  'camera': (
    scene: OrblitScene(
      camera: OrblitCamera(
        position: Vector3(3, 4, 5),
        target: Vector3(1, 0, -1),
        fieldOfView: 70,
        orthographic: true,
        viewHeight: 12,
        aperture: 2.8,
        shutterSpeed: 1 / 60,
        sensitivity: 400,
      ),
      objects: const [],
    ),
    at: null,
  ),
  'post': (
    scene: OrblitScene(
      camera: _camera(),
      objects: const [],
      post: OrblitPostProcess(
        antiAliasing: AntiAliasing.temporal,
        bloom: OrblitBloom(
          enabled: true,
          strength: 0.3,
          levels: 5,
          threshold: false,
          lensFlare: true,
          flareStrength: 0.07,
        ),
        depthOfField: OrblitDepthOfField(
          enabled: true,
          focusDistance: 4,
          blurScale: 2,
          maxForeground: 3,
          maxBackground: 5,
        ),
        vignette: OrblitVignette(
          enabled: true,
          midPoint: 0.3,
          roundness: 0.8,
          feather: 0.2,
          colour: Vector3(0.1, 0.2, 0.3),
        ),
        occlusion: OrblitOcclusion(
          enabled: true,
          radius: 0.6,
          strength: 0.8,
          bias: 0.002,
          quality: 2,
          bentNormals: true,
        ),
        reflections: OrblitReflections(
          enabled: true,
          thickness: 0.2,
          bias: 0.03,
          maxDistance: 7,
          stride: 3,
        ),
        grading: OrblitGrading(
          enabled: true,
          toneMapping: ToneMapping.aces,
          exposure: 0.5,
          contrast: 1.2,
          saturation: 0.9,
          vibrance: 1.4,
          temperature: -0.2,
          tint: 0.1,
          shadows: Vector3(0.9, 1, 1.1),
          midtones: Vector3(1, 1.05, 1),
          highlights: Vector3(1.2, 1, 0.8),
        ),
        dithering: false,
      ),
    ),
    at: null,
  ),
  'post-off': (
    scene: OrblitScene(
      camera: _camera(),
      objects: const [],
      post: OrblitPostProcess(enabled: false, antiAliasing: AntiAliasing.off),
    ),
    at: null,
  ),
  'pipeline': (
    scene: OrblitScene(
      camera: _camera(),
      objects: const [],
      pipeline: OrblitPipeline(
        shadows: OrblitShadows(
          kind: OrblitShadowKind.area,
          mapSize: 2048,
          cascades: 3,
          distance: 60,
          lambda: 0.7,
          constantBias: 0.002,
          normalBias: 0.5,
          stable: true,
          contact: true,
          softness: 1.5,
          splits: [0.1, 0.3, 0.6, 0.9],
          softnessFalloff: 2,
          contactDistance: 0.5,
          contactSteps: 12,
          variance: OrblitVarianceShadows(
            blur: 2.5,
            anisotropy: 4,
            samples: 2,
            lightBleedReduction: 0.3,
            highPrecision: true,
            mipmapping: true,
            exponential: true,
          ),
        ),
        resolution: OrblitResolution(
          adaptive: true,
          minScale: 0.6,
          maxScale: 0.9,
          sharpness: 0.4,
        ),
        lighting: OrblitLighting(clusterNear: 1, clusterFar: 250),
        samples: 4,
        precise: true,
        culling: false,
        refraction: false,
        textures: OrblitTextureLimits(maxSize: 2048, uploadKilobytes: 512),
      ),
    ),
    at: null,
  ),
  'pipeline-fixed-scale': (
    scene: OrblitScene(
      camera: _camera(),
      objects: const [],
      pipeline: OrblitPipeline(
        shadows: OrblitShadows(
          enabled: false,
          splits: [0.5],
          mapSize: 256,
        ),
        resolution: OrblitResolution(scale: 0.75),
        textures: OrblitTextureLimits(maxSize: 1 << 20, uploadKilobytes: -5),
      ),
    ),
    at: null,
  ),
  'pipeline-handheld': (
    scene: OrblitScene(
      camera: _camera(),
      objects: const [],
      pipeline: OrblitPipeline.forDisplay(
        const OrblitDisplay(
          width: 1280,
          height: 800,
          detail: OrblitDetail.ultra,
        ),
      ),
    ),
    at: null,
  ),
  'pipeline-handheld-low': (
    scene: OrblitScene(
      camera: _camera(),
      objects: const [],
      pipeline: OrblitPipeline.forDisplay(
        const OrblitDisplay(
          width: 1280,
          height: 800,
          detail: OrblitDetail.low,
        ),
      ),
    ),
    at: null,
  ),
  'pipeline-large-display': (
    scene: OrblitScene(
      camera: _camera(),
      objects: const [],
      pipeline: OrblitPipeline.forDisplay(
        const OrblitDisplay(
          width: 3840,
          height: 2160,
          detail: OrblitDetail.high,
        ),
      ),
    ),
    at: null,
  ),
  'environment': (
    scene: OrblitScene(
      camera: _camera(),
      objects: const [],
      environment: const OrblitEnvironment.fromImage(
        '/e/room.hdr',
        intensity: 20000,
        rotation: 1.2,
        showSkybox: false,
        size: 512,
      ),
      probes: [
        OrblitProbe(
          key: 40,
          position: Vector3(1, 2, 3),
          radius: 8,
          resolution: 128,
          version: 2,
          layers: 0x0F,
          intensity: 0.5,
        ),
        OrblitProbe(key: 41, position: Vector3(-4, 1.5, 0)),
      ],
      field: OrblitField(
        enabled: true,
        origin: Vector3(-8, 0, -8),
        spacing: Vector3(1, 1, 1),
        counts: Vector3(4, 2, 4),
        from: 'light',
        intensity: 1.5,
        retention: 0.9,
        bias: 0.2,
      ),
    ),
    at: null,
  ),
  'environment-split': (
    scene: OrblitScene(
      camera: _camera(),
      objects: const [],
      environment: const OrblitEnvironment(
        radiance: '/e/radiance.ktx',
        skybox: '/e/sky.ktx',
      ),
    ),
    at: null,
  ),
  'graph-custom': (
    scene: OrblitScene(
      camera: _camera(),
      objects: const [],
      graph: const OrblitRenderGraph(
        targets: [
          OrblitTarget(name: 'frame', scale: 0.5),
          OrblitTarget(name: 'mirror', width: 640, height: 360, depth: false),
          OrblitTarget(name: 'flat', colour: false),
        ],
        passes: [
          OrblitPass(name: 'mirror', kind: OrblitPassKind.reflection, into: 'mirror', plane: [0, 1, 0, -0.5], clear: false, layers: 0x03),
          OrblitPass(name: 'world', into: 'frame', layers: 0x0F),
          OrblitPass(name: 'off', into: 'flat', enabled: false),
          OrblitPass(
            name: 'sharpen',
            kind: OrblitPassKind.effect,
            effect: OrblitEffect.sharpen,
            reads: ['frame', 'mirror'],
            plane: [0.6],
          ),
        ],
      ),
    ),
    at: null,
  ),
  'graph-problem': (
    scene: OrblitScene(
      camera: _camera(),
      objects: const [],
      graph: const OrblitRenderGraph(
        passes: [OrblitPass(name: 'a'), OrblitPass(name: 'b')],
      ),
    ),
    at: null,
  ),
  'graph-motion-blur': (
    scene: OrblitScene(
      camera: _camera(),
      objects: const [],
      graph: const OrblitMotionBlur(shutter: 0.5, samples: 8).graph(),
    ),
    at: null,
  ),
  'graph-distortion-only': (
    scene: OrblitScene(
      camera: _camera(),
      objects: const [],
      distortions: [OrblitDistortion.lens(strength: -0.1)],
    ),
    at: null,
  ),
  'graph-god-rays-no-sun': (
    scene: OrblitScene(
      camera: _camera(),
      objects: const [],
      godRays: const OrblitGodRays(strength: 0.5),
    ),
    at: null,
  ),
  'populations': (
    scene: OrblitScene(
      camera: _camera(),
      objects: const [],
      populations: [
        OrblitPopulation(
          key: 4,
          transforms: _ramp(3 * 16, 0.5),
          colours: _ramp(3 * 3, 0.1),
          minimum: Vector3(-10, 0, -10),
          maximum: Vector3(10, 6, 10),
          mesh: '/m/tree.glb',
          range: 120,
          fade: OrblitFade.shrink,
          revision: 7,
          castShadows: true,
          layer: 2,
        ),
        OrblitPopulation(
          key: 2,
          transforms: _ramp(2 * 16, 3),
          colours: _ramp(2 * 3, 0.5),
          minimum: Vector3.zero(),
          maximum: Vector3.all(1),
          mesh: '/m/rock.glb',
          fade: OrblitFade.none,
          revision: 1,
          receiveShadows: false,
        ),
        OrblitPopulation(
          key: 9,
          transforms: _ramp(16, 1),
          colours: _ramp(3, 0.9),
          minimum: Vector3(1, 2, 3),
          maximum: Vector3(4, 5, 6),
          mesh: '/m/tree.glb',
        ),
      ],
    ),
    at: null,
  ),
  'terrain': (
    scene: OrblitScene(
      camera: _camera(),
      objects: const [],
      terrain: [
        OrblitTerrain(
          key: 3,
          regions: [_region(0, 0), _region(-1, 2, revision: 4)],
          regionSize: 16,
          spacing: 0.5,
          meshSize: 32,
          levels: 4,
          autoSteep: 2,
          autoFlat: 5,
          sets: [
            OrblitTerrainSet(albedo: _picture(2, 10), normal: _picture(2, 20)),
            const OrblitTerrainSet(tileSize: 8, triplanar: true),
          ],
          picturesRevision: 3,
          castShadows: false,
        ),
        OrblitTerrain(key: 5),
      ],
    ),
    at: null,
  ),
  'volumes': (
    scene: OrblitScene(
      camera: OrblitCamera(position: Vector3(6, 0, 0), target: Vector3.zero()),
      objects: const [],
      sky: OrblitSky(colour: Vector3(0.6, 0.7, 0.9), ambient: 20000),
      fog: OrblitFog(density: 0.002, colour: Vector3(0.7, 0.8, 0.9)),
      environment: const OrblitEnvironment(rotation: 350 * math.pi / 180),
      volumes: [
        _hall(),
        OrblitEnvironmentVolume.sphere(
          key: 2,
          centre: Vector3(3, 0, 0),
          radius: 2,
          blendDistance: 3,
          priority: 1,
          overrides: OrblitEnvironmentOverrides(
            fogColour: Vector3(0.1, 0.2, 0.6),
            ambient: 500,
            environmentRotation: 10 * math.pi / 180,
            bloomStrength: 0.4,
            saturation: 0.5,
            temperature: 0.2,
            midtones: Vector3(1, 0.9, 0.8),
          ),
        ),
      ],
    ),
    at: null,
  ),
  'volumes-outside': (
    scene: OrblitScene(
      camera: OrblitCamera(position: Vector3(0, 0, 20), target: Vector3.zero()),
      objects: const [],
      fog: OrblitFog(density: 0.002),
      volumes: [_hall()],
    ),
    at: null,
  ),
};

String _b64(TypedData data) => base64Encode(
  data.buffer.asUint8List(data.offsetInBytes, data.lengthInBytes),
);

Object _number(double value) => value.isFinite ? value : value.toString();

Map<String, Object?> _dump(Object? value) => switch (value) {
  final Float32List l => {'f32': _b64(l)},
  final Float64List l => {'f64': _b64(l)},
  final Int32List l => {'i32': _b64(l)},
  final Int64List l => {'i64': _b64(l)},
  final Uint8List l => {'u8': _b64(l)},
  final List<String> l => {'strings': l},
  final List<int> l => {'ints': l},
  final bool b => {'bool': b},
  final int i => {'int': i},
  final double d => {'double': _number(d)},
  final String s => {'string': s},
  _ => {'other': value.runtimeType.toString()},
};

String _commit(String root) {
  String git(List<String> args) =>
      (Process.runSync('git', args, workingDirectory: root).stdout as String)
          .trim();
  const path = 'packages/orblit_filament';
  final dirty = git(['status', '--porcelain', '--', path])
      .split('\n')
      .where((line) => line.isNotEmpty && !line.contains('/test/zz_'))
      .isNotEmpty;
  final commit = git(['log', '-1', '--format=%H', '--', path]);
  return dirty ? '$commit+dirty' : commit;
}

void main() {
  test('writes the scene fixture', () {
    final root = Directory('../..').absolute.path;
    final cases = [
      for (final MapEntry(key: name, value: scene) in _scenes().entries)
        {
          'op': 'scene',
          'name': name,
          'at': scene.at,
          'expect': {
            for (final MapEntry(key: key, value: value)
                in scene.scene.toMessage(1, at: scene.at).entries)
              key: _dump(value),
          },
        },
    ];
    final body = cases.map((c) => '    ${jsonEncode(c)}').join(',\n');
    final file = File('$root/dotnet/fixtures/orblit_filament/scene.json');
    file.parent.createSync(recursive: true);
    file.writeAsStringSync(
      '{\n'
      '  "package": "orblit_filament",\n'
      '  "sourceCommit": ${jsonEncode(_commit(root))},\n'
      '  "cases": [\n$body\n  ]\n'
      '}\n',
    );
  });
}

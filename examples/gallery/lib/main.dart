/// Runs one worked example, full window, so a frame of it can be looked at.
///
/// The examples are shown inside the editor, which opens on a project list —
/// so nothing draws there until somebody clicks, and neither a frame smoke nor
/// a person trying to reproduce a rendering fault can get at them. This opens
/// straight into a scene.
///
/// Driven by the environment, so a sweep of camera angles is a shell loop
/// rather than a person dragging:
///
///   ORBLIT_EXAMPLE=Blocks   which one, by name (default: the first)
///   ORBLIT_YAW / ORBLIT_PITCH / ORBLIT_DISTANCE   where to look from
///   ORBLIT_SECONDS          hold the clock still, for a scene that animates
///   ORBLIT_WALK=0           give the camera back, for an example that drives it
///   ORBLIT_RANGE            how far a population is drawn from; 0 draws it all
///   ORBLIT_TREES=0          leave the trees out
///   ORBLIT_MESH             a path to a .glb or .gltf, shown on its own
///   ORBLIT_MORPH            comma-separated shape weights for that model
///   ORBLIT_SHARPEN          run the sharpen effect, 0 to 1, over the frame
///   ORBLIT_EFFECT           an effect by name, shown on its own over the frame
///   ORBLIT_SMAA=1           the whole three-pass SMAA chain
///   ORBLIT_LIGHT            which light the Lights example shows
///   ORBLIT_LUMENS           how bright it is
///   ORBLIT_PANEL_W / _H     the panel's size in metres
///   ORBLIT_CIRCLING=0       stop it going round, so two renders compare
///   ORBLIT_BOUNCE_OFF=1     turn the Bounced light example's effect off
///   ORBLIT_BOUNCE           with ORBLIT_EFFECT=bounce, how much light bounces
///   ORBLIT_BOUNCE_RADIUS    how far it looks, in metres
///   ORBLIT_BOUNCE_THICKNESS how solid the depth buffer's surfaces are
///   ORBLIT_BOUNCE_SLICES    how many directions each pixel fans along
///   ORBLIT_VOLUME_AT        where the Environment volumes walk stands, 0 in
///                          the courtyard to 1 at the back of the hall; stops
///                          the walk, and prints what the volumes resolved to
///   ORBLIT_VOLUMES_OFF=1    the same place with the volumes left out
///   ORBLIT_VOLUME_BLEND     how far outside the hall its look reaches, metres
///   ORBLIT_DECALS_OFF=1     paint none of the Decals example's decals
///   ORBLIT_DECAL_ONLY       paint only that one of them, counting from 0
///   ORBLIT_DECAL_FADE_OFF=1 turn their angle fade off
///   ORBLIT_DECAL_MASK_OFF=1 let the paint splash reach the crate's layer
///   ORBLIT_OUTLINE=0        the Outline example with nothing outlined
///   ORBLIT_OUTLINE_OTHERS=0 outline only the active object
///   ORBLIT_OUTLINE_WIDTH    how wide the outline is, in pixels
///   ORBLIT_OUTLINE_HIDDEN   shown, faint, dashed or hidden: the part a wall hides
///   ORBLIT_AA               off, fxaa or temporal, under the Outline example
///   ORBLIT_SPLAT            a .ply or .splat capture for the Gaussian splats
///                          example to show instead of its generated ring
///   ORBLIT_SPLAT_COUNT      how many splats the generated ring has
///   ORBLIT_SPLAT_SORT=0     draw them unsorted, to measure what the sort does
///   ORBLIT_SPLAT_PILLAR=0   take the solid pillar out of the ring
///   ORBLIT_SPLAT_HARMONICS  how many bands of a capture's view-dependent
///                          colour to read: 0, 1, 2 or 3
///   ORBLIT_BATCHING=0/1     batching off or on, for any example, over the
///                          default (on), so the same frame can be drawn both
///                          ways and compared
///   ORBLIT_PREPASS=0/1      the depth prepass off or on, for any example, so
///                          the same frame can be timed both ways
///   ORBLIT_CRATES           how many crates the Batching example draws
///   ORBLIT_PALETTE          its colours: One, Six or Every one
///   ORBLIT_BATCH_MATERIAL=1 its crates made of one shared material
///   ORBLIT_BATCH_MESH       a .glb for its crates, instead of the cube (which
///                          only batches alongside ORBLIT_BATCH_MATERIAL=1)
///   ORBLIT_MOVING=0         hold its turning crate still
///   ORBLIT_SLABS            how many slabs the Overdraw example crosses
///   ORBLIT_SHADOWS=0        no shadow pass, for any example
///   ORBLIT_POST=0           no post-processing, for any example
///   ORBLIT_CULLING=0        draw everything in the scene whether the renderer
///                          thinks it is on screen or not, for any example.
///                          The one switch that tells a bounding box in the
///                          wrong place from a thing that was never drawn:
///                          what the frustum test wrongly throws away is
///                          exactly what comes back when it is turned off.
///   ORBLIT_WEATHER          which of the Weather example's conditions: Clear,
///                          Fair, Storm, Misty, Rain or Snow
///   ORBLIT_MIST             how much of its air is drawn as banks of cloud,
///                          and ORBLIT_DENSITY how much as even haze. Separate
///                          because every condition raises the two together,
///                          and haze thick enough to go with a real bank of
///                          mist is haze thick enough to hide whether the
///                          bank was drawn at all.
///   ORBLIT_RAIN             how hard it is coming down
///   ORBLIT_PANEL_SHADOW=0   the Panel shadows example's panel casts nothing
///   ORBLIT_PANEL            its panel's edge in metres
///   ORBLIT_PANEL_HEIGHT     how high it hangs
///   ORBLIT_SHADOW_LIGHT     which light the Shadows example casts with: Sun,
///                          Spot or Point
///   ORBLIT_SHADOW_KIND      its edge: Sharp, Soft, Area or Variance
///   ORBLIT_SHADOW_MAP       the map's size in pixels
///   ORBLIT_SHADOW_CASCADES  how many cascades a sun's map is split into
///   ORBLIT_SHADOW_SPLIT     place the splits by hand, the first at this
///                          fraction of the shadow distance
///   ORBLIT_SHADOW_CONTACT=1 screen-space contact shadows
///   ORBLIT_SHADOW_CONTACT_DISTANCE  how far each pixel marches, in metres
///   ORBLIT_SHADOW_SIZE      the light's size in metres, for the Area edge
///   ORBLIT_VSM_BLUR         the Variance edge's blur, in texels
///   ORBLIT_GODRAYS          how strong the god rays are; over any example
///                          but God rays itself, turns them on
///   ORBLIT_SUN_BEARING      the God rays sun's bearing in degrees, 180
///                          behind the camera
///   ORBLIT_SUN_ALTITUDE     and its height above the horizon
///   ORBLIT_COVER            the God rays sky's cloud cover, 0 to 1
///   ORBLIT_GODRAY_SAMPLES / _DECAY / _DENSITY   the rest of its settings
///   ORBLIT_SHOCKWAVE=0      leave the Distortion example's wave out
///   ORBLIT_HAZE=0           and its heat haze
///   ORBLIT_LENS             its lens warp, negative for pincushion
///   ORBLIT_CHROMATIC        its chromatic split
///   ORBLIT_WAVE             its wave's strength
///   ORBLIT_MOTION=0         turn the Motion blur example's blur off
///   ORBLIT_MOTION_OBJECTS=0 blur by the camera's motion only
///   ORBLIT_PAN=1            pan the Motion blur example's camera
///   ORBLIT_SHUTTER          its shutter, in seconds
library;

import 'package:flutter/material.dart';
import 'package:flutter/scheduler.dart';
import 'package:orblit_examples/orblit_examples.dart';
import 'package:orblit_filament/orblit_filament.dart';
import 'package:vector_math/vector_math_64.dart' hide Colors;

import 'orblit_env.dart';

void main() => runApp(const Gallery());

/// Every `ORBLIT_*` switch this gallery was started with.
///
/// Where they come from depends on where this is running — a real process
/// environment on macOS, iOS and Android, the page's query string in a
/// browser — so the reading sits behind a conditional import rather than
/// here. The native half is the code that used to be in this file, moved
/// unchanged, including the dart:ffi reading the iOS simulator needs;
/// orblit_env.dart says why.
final Map<String, String> _orblitEnv = readOrblitEnvironment();

double? _number(String name) => double.tryParse(_orblitEnv[name] ?? '');

class Gallery extends StatelessWidget {
  const Gallery({super.key});

  @override
  Widget build(BuildContext context) => MaterialApp(
    title: 'Orblit Gallery',
    debugShowCheckedModeBanner: false,
    theme: ThemeData.dark(useMaterial3: true),
    home: const _Stage(),
  );
}

class _Stage extends StatefulWidget {
  const _Stage();

  @override
  State<_Stage> createState() => _StageState();
}

class _StageState extends State<_Stage> with SingleTickerProviderStateMixin {
  late final List<Example> _all = engineExamples();
  late final Example _example = _chosen();
  late final GalleryCamera _look = GalleryCamera.from(_example.viewpoint);
  late final Ticker _clock;

  double _seconds = 0;

  /// The moment ORBLIT_SECONDS asked for, or null for the real clock.
  ///
  /// Kept apart from [_seconds] because it is also handed to the view: the
  /// weather animates inside the renderer, on the clock the view sends rather
  /// than on anything in the scene, so a held clock that stops at the widget
  /// leaves the mist and the rain moving and two renders of the same second
  /// are still two different pictures.
  double? _held;

  /// A scene that is one model and nothing else.
  ///
  /// Not an example — a way to point the renderer at a file and see what it
  /// makes of it, which is how a question like "does this decode" gets an
  /// answer rather than an opinion.
  OrblitScene _justTheMesh(String path) {
    // Two passes when a sharpen is asked for, one otherwise. An effect reads
    // what another pass drew, so the world has to land in a texture before
    // anything can be done to it.
    // The whole chain: the world into a texture, its edges into another, the
    // weights into a third, and the blend onto the screen reading the first
    // and the third.
    final smaa = _orblitEnv['ORBLIT_SMAA'];
    if (smaa == 'edgetarget') {
      // Edges into a target, then blitted to the screen by a sharpen set to
      // nothing. Tells apart "the weights shader is wrong" from "a target
      // does not carry what was drawn into it", which look identical from
      // the far end.
      return _sceneWith(
        path,
        OrblitRenderGraph(
          targets: const [
            OrblitTarget(name: 'frame'),
            OrblitTarget(name: 'edges'),
          ],
          passes: const [
            OrblitPass(name: 'world', into: 'frame'),
            OrblitPass(
              name: 'edges',
              kind: OrblitPassKind.effect,
              effect: OrblitEffect.smaaEdges,
              reads: ['frame'],
              into: 'edges',
            ),
            OrblitPass(
              name: 'show',
              kind: OrblitPassKind.effect,
              effect: OrblitEffect.sharpen,
              reads: ['edges'],
            ),
          ],
        ),
      );
    }
    if (smaa == 'weights') {
      // The chain stopped one short, so the weights land on the screen. What
      // pass two decided is otherwise invisible, and a weights pass that
      // quietly outputs nothing looks exactly like one that works.
      return _sceneWith(
        path,
        OrblitRenderGraph(
          targets: const [
            OrblitTarget(name: 'frame'),
            OrblitTarget(name: 'edges'),
          ],
          passes: const [
            OrblitPass(name: 'world', into: 'frame'),
            OrblitPass(
              name: 'edges',
              kind: OrblitPassKind.effect,
              effect: OrblitEffect.smaaEdges,
              reads: ['frame'],
              into: 'edges',
            ),
            OrblitPass(
              name: 'weights',
              kind: OrblitPassKind.effect,
              effect: OrblitEffect.smaaWeights,
              reads: ['edges'],
            ),
          ],
        ),
      );
    }
    if (smaa == '1') {
      return _sceneWith(
        path,
        OrblitRenderGraph(
          targets: const [
            OrblitTarget(name: 'frame'),
            OrblitTarget(name: 'edges'),
            OrblitTarget(name: 'weights'),
          ],
          passes: const [
            OrblitPass(name: 'world', into: 'frame'),
            OrblitPass(
              name: 'edges',
              kind: OrblitPassKind.effect,
              effect: OrblitEffect.smaaEdges,
              reads: ['frame'],
              into: 'edges',
            ),
            OrblitPass(
              name: 'weights',
              kind: OrblitPassKind.effect,
              effect: OrblitEffect.smaaWeights,
              reads: ['edges'],
              into: 'weights',
            ),
            // The picture first, the weights second. The blend reads both and
            // the order is what tells it which is which.
            OrblitPass(
              name: 'blend',
              kind: OrblitPassKind.effect,
              effect: OrblitEffect.smaaBlend,
              reads: ['frame', 'weights'],
            ),
          ],
        ),
      );
    }

    return _sceneWith(path, _effectGraph());
  }

  /// The one-effect graph the environment asks for, or null for none.
  OrblitRenderGraph? _effectGraph() {
    final named = _orblitEnv['ORBLIT_EFFECT'];
    final amount = _number('ORBLIT_SHARPEN');
    final effect = named == null || named.isEmpty
        ? (amount == null ? null : OrblitEffect.sharpen)
        : OrblitEffect.values.firstWhere(
            (one) => one.name.toLowerCase() == named.toLowerCase(),
          );
    return effect == null
        ? null
        : OrblitRenderGraph(
            targets: const [OrblitTarget(name: 'frame')],
            passes: [
              const OrblitPass(name: 'world', into: 'frame'),
              OrblitPass(
                name: 'effect',
                kind: OrblitPassKind.effect,
                effect: effect,
                reads: const ['frame'],
                // The dials ride in the plane's four numbers, which a scene
                // pass uses for its mirror and an effect has no use for.
                // Sharpen reads the first; the bounce reads all four as
                // radius, strength, thickness and how many directions.
                plane: [
                  _number('ORBLIT_BOUNCE_RADIUS') ?? amount ?? 0,
                  _number('ORBLIT_BOUNCE') ?? 0,
                  _number('ORBLIT_BOUNCE_THICKNESS') ?? 0,
                  _number('ORBLIT_BOUNCE_SLICES') ?? 0,
                ],
              ),
            ],
          );
  }

  /// The scene as the example built it, put through whatever effect the
  /// environment asked for.
  OrblitScene _underEffect(OrblitScene scene) {
    // God rays over an example that never asked for any — the Day and night
    // sky, the Weather's cloud. The God rays example takes the same switch
    // as its own strength instead.
    final rays = _number('ORBLIT_GODRAYS');
    final lit = rays != null && _example is! GodRaysExample
        ? scene.copyWith(godRays: OrblitGodRays(strength: rays))
        : scene;
    final graph = _effectGraph();
    final drawn = graph == null ? lit : lit.copyWith(graph: graph);
    // Batching forced one way or the other, over whatever the example chose,
    // so one frame can be drawn both ways and the two compared pixel by pixel.
    // The pipeline and the post-processing are the example's own and are built
    // afresh for every frame, so setting them here cannot leak into the next
    // one.
    if (_orblitEnv['ORBLIT_SHADOWS'] == '0') {
      drawn.pipeline.shadows.enabled = false;
    }
    if (_orblitEnv['ORBLIT_POST'] == '0') drawn.post.enabled = false;
    // Frustum culling off, so a frame shows what the scene holds rather than
    // what the renderer believes is in front of the camera. The difference
    // between the two frames is precisely what the boxes decided, which is
    // the only way to see a box that is in the wrong place: a thing that is
    // wrongly culled looks exactly like a thing that was never there.
    if (_orblitEnv['ORBLIT_CULLING'] == '0') drawn.pipeline.culling = false;
    final batched = switch (_orblitEnv['ORBLIT_BATCHING']) {
      '1' => drawn.copyWith(batching: true),
      '0' => drawn.copyWith(batching: false),
      _ => drawn,
    };
    // The depth prepass forced the same way, and separately, so a frame can be
    // timed with it and without it while everything else about the scene —
    // batching included — stays exactly where the example put it.
    return switch (_orblitEnv['ORBLIT_PREPASS']) {
      '1' => batched.copyWith(depthPrepass: true),
      '0' => batched.copyWith(depthPrepass: false),
      _ => batched,
    };
  }

  /// One model, one light, and whatever graph was asked for.
  OrblitScene _sceneWith(String path, OrblitRenderGraph? graph) {
    return OrblitScene(
      graph: graph,
      camera: _look.toRenderCamera(),
      objects: [
        OrblitObject(
          key: 1,
          mesh: path,
          transform: Matrix4.identity(),
          colour: Vector3(1, 1, 1),
          morphWeights: switch (_orblitEnv['ORBLIT_MORPH']) {
            final set? when set.isNotEmpty =>
              set.split(',').map((one) => double.parse(one.trim())).toList(),
            _ => null,
          },
        ),
      ],
      lights: [
        OrblitLight(
          key: 1,
          kind: OrblitLightKind.directional,
          direction: Vector3(-0.5, -0.7, -0.4)..normalize(),
          colour: Vector3(1, 0.97, 0.92),
          intensity: 90000,
          castShadows: true,
        ),
      ],
      sky: OrblitSky(
        zenith: Vector3(0.30, 0.50, 0.78),
        horizon: Vector3(0.72, 0.84, 0.94),
        ambient: 24000,
      ),
    );
  }

  Example _chosen() {
    final wanted = _orblitEnv['ORBLIT_EXAMPLE'];
    if (wanted == null || wanted.isEmpty) return _all.first;
    return _all.firstWhere(
      (one) => one.name.toLowerCase() == wanted.toLowerCase(),
      orElse: () {
        warn(
          'no example called "$wanted" — there is '
          '${_all.map((e) => e.name).join(', ')}',
        );
        return _all.first;
      },
    );
  }

  @override
  void initState() {
    super.initState();

    // An example that puts somebody inside it drives the camera itself, which
    // is right for playing and useless for looking at a particular corner of
    // it. This hands the camera back.
    final example = _example;
    if (example is VoxelExample) {
      if (_orblitEnv['ORBLIT_WALK'] == '0') example.walking = false;
      final far = _number('ORBLIT_RANGE');
      if (far != null) example.range = far;
      if (_orblitEnv['ORBLIT_TREES'] == '0') example.trees = false;
    }
    if (example is WeatherExample) {
      final condition = _orblitEnv['ORBLIT_WEATHER'];
      if (condition != null && condition.isNotEmpty) {
        if (WeatherExample.conditions.contains(condition)) {
          example.apply(condition);
        } else {
          // debugPrint rather than stderr: this app also builds for the
          // web, where dart:io does not exist.
          debugPrint(
            'no condition called "$condition" — there is '
            '${WeatherExample.conditions.join(', ')}',
          );
        }
      }
      example.structure = _number('ORBLIT_MIST') ?? example.structure;
      example.density = _number('ORBLIT_DENSITY') ?? example.density;
      example.rain = _number('ORBLIT_RAIN') ?? example.rain;
    }
    if (example is ProbesExample) {
      example.intensity = _number('ORBLIT_PROBE') ?? example.intensity;
      example.roughness = _number('ORBLIT_ROUGHNESS') ?? example.roughness;
      if (_orblitEnv['ORBLIT_PROBE_OFF'] == '1') example.on = false;
    }
    if (example is LightsExample) {
      final kind = _orblitEnv['ORBLIT_LIGHT'];
      if (kind != null) example.kind = kind;
      final lumens = _number('ORBLIT_LUMENS');
      if (lumens != null) example.intensity = lumens;
      example.panelWidth = _number('ORBLIT_PANEL_W') ?? example.panelWidth;
      example.panelHeight = _number('ORBLIT_PANEL_H') ?? example.panelHeight;
      // Still, so two renders of the same angle are the same picture.
      if (_orblitEnv['ORBLIT_CIRCLING'] == '0') {
        example.orbiting = false;
      }
    }
    if (example is PanelShadowExample) {
      if (_orblitEnv['ORBLIT_PANEL_SHADOW'] == '0') {
        example.shadows = false;
      }
      example.panel = _number('ORBLIT_PANEL') ?? example.panel;
      example.height = _number('ORBLIT_PANEL_HEIGHT') ?? example.height;
    }
    if (example is ShadowsExample) {
      final light = _orblitEnv['ORBLIT_SHADOW_LIGHT'];
      if (light != null) {
        example.light = ShadowLight.values.firstWhere(
          (one) => one.label == light,
          orElse: () => example.light,
        );
      }
      final shadows = example.shadows;
      final kind = _orblitEnv['ORBLIT_SHADOW_KIND'];
      if (kind != null) {
        shadows.kind = OrblitShadowKind.values.firstWhere(
          (one) => one.label == kind,
          orElse: () => shadows.kind,
        );
      }
      shadows.mapSize = _number('ORBLIT_SHADOW_MAP')?.round() ?? shadows.mapSize;
      shadows.cascades =
          _number('ORBLIT_SHADOW_CASCADES')?.round() ?? shadows.cascades;
      final split = _number('ORBLIT_SHADOW_SPLIT');
      if (split != null) {
        example.handSplits = true;
        example.firstSplit = split;
      }
      if (_orblitEnv['ORBLIT_SHADOW_CONTACT'] == '1') {
        shadows.contact = true;
      }
      shadows.contactDistance =
          _number('ORBLIT_SHADOW_CONTACT_DISTANCE') ?? shadows.contactDistance;
      example.lightSize = _number('ORBLIT_SHADOW_SIZE') ?? example.lightSize;
      shadows.variance.blur = _number('ORBLIT_VSM_BLUR') ?? shadows.variance.blur;
    }
    if (example is FieldExample) {
      example.intensity = _number('ORBLIT_FIELD') ?? example.intensity;
      example.retention = _number('ORBLIT_RETENTION') ?? example.retention;
      if (_orblitEnv['ORBLIT_FIELD_OFF'] == '1') example.on = false;
    }
    if (example is BatchingExample) {
      example.count = _number('ORBLIT_CRATES') ?? example.count;
      example.palette = _orblitEnv['ORBLIT_PALETTE'] ?? example.palette;
      if (_orblitEnv['ORBLIT_BATCH_MATERIAL'] == '1') {
        example.material = true;
      }
      final mesh = _orblitEnv['ORBLIT_BATCH_MESH'];
      if (mesh != null && mesh.isNotEmpty) example.mesh = mesh;
      if (_orblitEnv['ORBLIT_MOVING'] == '0') example.moving = false;
    }
    if (example is OverdrawExample) {
      example.slabs = _number('ORBLIT_SLABS') ?? example.slabs;
    }
    if (example is GodRaysExample) {
      example.strength = _number('ORBLIT_GODRAYS') ?? example.strength;
      example.bearing = _number('ORBLIT_SUN_BEARING') ?? example.bearing;
      example.altitude = _number('ORBLIT_SUN_ALTITUDE') ?? example.altitude;
      example.cover = _number('ORBLIT_COVER') ?? example.cover;
      example.samples = _number('ORBLIT_GODRAY_SAMPLES') ?? example.samples;
      example.decay = _number('ORBLIT_GODRAY_DECAY') ?? example.decay;
      example.density = _number('ORBLIT_GODRAY_DENSITY') ?? example.density;
    }
    if (example is DistortionExample) {
      if (_orblitEnv['ORBLIT_SHOCKWAVE'] == '0') {
        example.shockwave = false;
      }
      if (_orblitEnv['ORBLIT_HAZE'] == '0') example.haze = false;
      example.lens = _number('ORBLIT_LENS') ?? example.lens;
      example.chromatic = _number('ORBLIT_CHROMATIC') ?? example.chromatic;
      example.strength = _number('ORBLIT_WAVE') ?? example.strength;
    }
    if (example is BounceExample) {
      example.strength = _number('ORBLIT_BOUNCE') ?? example.strength;
      example.reach = _number('ORBLIT_BOUNCE_RADIUS') ?? example.reach;
      if (_orblitEnv['ORBLIT_BOUNCE_OFF'] == '1') example.on = false;
    }
    if (example is DecalsExample) {
      final environment = _orblitEnv;
      if (environment['ORBLIT_DECALS_OFF'] == '1') example.on = false;
      if (environment['ORBLIT_DECAL_FADE_OFF'] == '1') example.angleFade = false;
      if (environment['ORBLIT_DECAL_MASK_OFF'] == '1') {
        example.spareTheCrate = false;
      }
      example.only = _number('ORBLIT_DECAL_ONLY')?.round();
    }

    if (example is EnvironmentVolumesExample) {
      final at = _number('ORBLIT_VOLUME_AT');
      if (at != null) {
        example.walking = false;
        example.along = at;
      }
      example.blend = _number('ORBLIT_VOLUME_BLEND') ?? example.blend;
      if (_orblitEnv['ORBLIT_VOLUMES_OFF'] == '1') {
        example.volumes = false;
      }
      // The numbers the frame was drawn with, beside the frame. A blend is
      // checked for a step by reading these along a sweep, not by eye.
      if (at != null) {
        final scene = example.scene(_look.toRenderCamera(), 0);
        final seen = scene.resolved();
        String three(Vector3 v) => [
          v.x,
          v.y,
          v.z,
        ].map((c) => c.toStringAsFixed(4)).join(',');
        warn(
          '[volumes] at=$at z=${scene.camera.position.z.toStringAsFixed(3)} '
          'fogDensity=${seen.fog.density.toStringAsFixed(5)} '
          'fogColour=${three(seen.fog.colour)} '
          'ambient=${seen.sky.ambient.toStringAsFixed(1)} '
          'skyColour=${three(seen.sky.colour)} '
          'shutter=${seen.camera.shutterSpeed.toStringAsFixed(6)}',
        );
      }
    }

    if (example is OutlineExample) {
      if (_orblitEnv['ORBLIT_OUTLINE'] == '0') example.on = false;
      if (_orblitEnv['ORBLIT_OUTLINE_OTHERS'] == '0') {
        example.others = false;
      }
      example.width = _number('ORBLIT_OUTLINE_WIDTH') ?? example.width;
      final hidden = _orblitEnv['ORBLIT_OUTLINE_HIDDEN'];
      if (hidden != null && hidden.isNotEmpty) {
        example.occluded = OrblitOccluded.values.byName(hidden);
      }
      final aa = _orblitEnv['ORBLIT_AA'];
      if (aa != null && aa.isNotEmpty) {
        example.antiAliasing = AntiAliasing.values.byName(aa);
      }
    }

    if (example is SplatsExample) {
      final capture = _orblitEnv['ORBLIT_SPLAT'];
      if (capture != null && capture.isNotEmpty) example.path = capture;
      example.count = _number('ORBLIT_SPLAT_COUNT')?.round() ?? example.count;
      if (_orblitEnv['ORBLIT_SPLAT_SORT'] == '0') {
        example.sorted = false;
      }
      if (_orblitEnv['ORBLIT_SPLAT_PILLAR'] == '0') {
        example.pillar = false;
      }
      example.harmonics =
          _number('ORBLIT_SPLAT_HARMONICS')?.round() ?? example.harmonics;
    }

    if (example is MotionBlurExample) {
      if (_orblitEnv['ORBLIT_MOTION'] == '0') example.on = false;
      if (_orblitEnv['ORBLIT_MOTION_OBJECTS'] == '0') {
        example.objects = false;
      }
      if (_orblitEnv['ORBLIT_PAN'] == '1') example.panning = true;
      example.shutter = _number('ORBLIT_SHUTTER') ?? example.shutter;
    }

    _look.yaw = _number('ORBLIT_YAW') ?? _look.yaw;
    _look.pitch = _number('ORBLIT_PITCH') ?? _look.pitch;
    _look.distance = _number('ORBLIT_DISTANCE') ?? _look.distance;

    // A fixed clock when one is asked for, so two runs of an animating scene
    // are the same picture and can be compared.
    final held = _number('ORBLIT_SECONDS');
    if (held != null) {
      _seconds = held;
      _held = held;
      _clock = Ticker((_) => setState(() {}))..start();
    } else {
      _clock = Ticker((elapsed) {
        setState(() => _seconds = elapsed.inMicroseconds / 1e6);
      })..start();
    }
  }

  @override
  void dispose() {
    _clock.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    body: GestureDetector(
      onPanUpdate: (details) => setState(() => _look.orbit(details.delta)),
      child: OrblitView(
        // The same moment the example was built at, so ORBLIT_SECONDS holds
        // the weather still as well as the scene.
        seconds: _held,
        scene: switch (_orblitEnv['ORBLIT_MESH']) {
          final path? when path.isNotEmpty => _justTheMesh(path),
          // An effect over a real scene rather than only over a lone model.
          // An effect that has only ever been seen against one mesh on a
          // plain background is an effect nobody has actually looked at.
          _ => _underEffect(_example.scene(_look.toRenderCamera(), _seconds)),
        },
      ),
    ),
  );
}

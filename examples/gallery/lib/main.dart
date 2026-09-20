/// Runs one worked example, full window, so a frame of it can be looked
/// at.
///
/// The examples are shown inside the editor too, which opens on a project
/// list — so nothing draws there until somebody clicks, and neither a
/// frame smoke nor a person trying to reproduce a rendering fault can get
/// at them. This opens straight into a scene.
///
/// Which example, where the camera stands, and every setting each example
/// exposes are read from the environment, so a sweep is a shell loop
/// rather than a person dragging. README.md names them all.
library;

import 'package:flutter/material.dart';
import 'package:flutter/scheduler.dart';
import 'package:flutter/services.dart';
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
    home: const _Shell(),
  );
}

/// Which example is on screen, and the way to change it without a shell.
///
/// ORBLIT_EXAMPLE picks the one to open on, which is every switch a desktop
/// sweep has ever needed. A handset has no environment to set it from --
/// `Platform.environment` comes back empty there and the libc path in
/// orblit_env_native.dart is Darwin's -- so on a phone the chooser always fell
/// back to the first example and the other thirty were unreachable. That is
/// what this is for.
///
/// It is chrome over a renderer, so it stays out of the picture. No app bar:
/// one would take a strip off the top of every frame, and the frame is the
/// thing being looked at. A button floats over the scene instead, on narrow
/// viewports only, and ORBLIT_MENU=0 turns it off outright -- so every frame
/// tool/ci_draw_frame*.sh compares is the frame it was before this existed.
class _Shell extends StatefulWidget {
  const _Shell();

  @override
  State<_Shell> createState() => _ShellState();
}

class _ShellState extends State<_Shell> {
  late final List<Example> _all = engineExamples();
  late Example _example = _chosen();

  Example _chosen() {
    final wanted = _orblitEnv['ORBLIT_EXAMPLE'];
    if (wanted == null || wanted.isEmpty) return _all.first;
    return _all.firstWhere(
      (one) => one.name.toLowerCase() == wanted.toLowerCase(),
      orElse: () {
        warn(
          'no example called "$wanted" \u2014 there is '
          '${_all.map((e) => e.name).join(', ')}',
        );
        return _all.first;
      },
    );
  }

  /// Whether to put the chooser on screen at all.
  ///
  /// The shortest side rather than the platform, because what decides it is
  /// whether there is a shell to set ORBLIT_EXAMPLE from, and a phone browser
  /// has no more of one than a phone does. It also means a desktop window is
  /// only ever given the chooser if somebody has made it phone-shaped.
  bool _chooserWanted(BuildContext context) =>
      _orblitEnv['ORBLIT_MENU'] != '0' &&
      MediaQuery.sizeOf(context).shortestSide < 600;

  @override
  Widget build(BuildContext context) {
    final wanted = _chooserWanted(context);
    final portrait = MediaQuery.orientationOf(context) == Orientation.portrait;
    // One shape whether or not the chooser is on screen.
    //
    // The first version of this returned a bare stage when the chooser was
    // not wanted and a Scaffold around one when it was. MediaQuery has no
    // size to give on the first build, so every launch took the first branch
    // and then the second, which moved _Stage to a different slot in the
    // tree -- and a widget that moves is unmounted and built again, taking
    // the renderer with it. The log said so plainly: "engine ready" twice,
    // four seconds apart, and a black window where the first engine's
    // texture used to be. So the Scaffold and the Stack are unconditional
    // now, and `wanted` only decides whether a drawer is attached and a
    // button drawn over the scene. _Stage stays at the same address either
    // way, which is the whole point.
    return Scaffold(
      drawer: wanted ? _chooser(context) : null,
      // StackFit.expand, which is not the default and has to be said.
      // A loose Stack takes its size from its largest child that is not
      // Positioned -- which here was the menu button -- so the stack came out
      // 48 logical pixels square, Positioned.fill filled that, and the scene
      // rendered into a thumbnail in the top-left corner while the rest of
      // the window stayed black. Expanding gives the stage the whole window
      // and leaves the button, which is Positioned, out of the measurement.
      body: Stack(
        fit: StackFit.expand,
        children: [
          // Keyed on the example, so choosing another builds _StageState
          // again from the top rather than mutating it. Every ORBLIT_*
          // switch this gallery reads is applied in initState against
          // `late final` fields; a setter would have to repeat all of that
          // and would drift from it by the second example somebody added.
          // A remount runs the code that is already there -- and here it is
          // asked for, rather than arrived at by accident.
          _Stage(key: ValueKey(_example.name), example: _example),
          // The scene fills the window and the button sits on top of it. An
          // app bar would shorten the viewport instead, and a renderer's
          // gallery is the last place to spend pixels of the picture on
          // chrome.
          if (wanted)
            Positioned(
              left: 0,
              top: 0,
              child: SafeArea(
                child: Padding(
                  padding: const EdgeInsets.all(8),
                  child: Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      // Its own context, so Scaffold.of finds the Scaffold
                      // above rather than looking for one outside this build.
                      Builder(
                        builder: (context) => _chromeButton(
                          icon: Icons.menu,
                          tooltip: 'Examples',
                          onPressed: () => Scaffold.of(context).openDrawer(),
                        ),
                      ),
                      const SizedBox(width: 8),
                      _chromeButton(
                        icon: portrait
                            ? Icons.stay_current_landscape
                            : Icons.stay_current_portrait,
                        tooltip: portrait
                            ? 'Turn it landscape'
                            : 'Turn it portrait',
                        onPressed: () => _turn(portrait),
                      ),
                    ],
                  ),
                ),
              ),
            ),
        ],
      ),
    );
  }

  /// Turns the app the other way up, and keeps it there.
  ///
  /// Asked for rather than left to the accelerometer because a handset with
  /// auto-rotate switched off -- which is most of them, and was this one:
  /// `settings get system accelerometer_rotation` answered 0 -- never turns
  /// whatever the manifest allows. Nothing here locks an orientation, so the
  /// app was already free to rotate and simply never got the chance. A button
  /// does not care what that setting says.
  ///
  /// Both landscapes rather than one, so the phone can still be held either
  /// way round once it is on its side.
  void _turn(bool portraitNow) {
    SystemChrome.setPreferredOrientations(
      portraitNow
          ? const [
              DeviceOrientation.landscapeLeft,
              DeviceOrientation.landscapeRight,
            ]
          : const [DeviceOrientation.portraitUp],
    );
  }

  @override
  void dispose() {
    // Left as it was found. A preferred orientation outlives the widget that
    // asked for one, and a gallery that pinned the next thing on screen to
    // landscape would be a strange thing to have done.
    SystemChrome.setPreferredOrientations(DeviceOrientation.values);
    super.dispose();
  }

  /// One round button over the scene.
  ///
  /// It carries its own contrast rather than trusting whatever happens to be
  /// behind it, because what is behind it is the example's business.
  Widget _chromeButton({
    required IconData icon,
    required String tooltip,
    required VoidCallback onPressed,
  }) => DecoratedBox(
    decoration: BoxDecoration(
      color: Colors.black.withValues(alpha: 0.45),
      shape: BoxShape.circle,
    ),
    child: IconButton(
      icon: Icon(icon),
      color: Colors.white,
      tooltip: tooltip,
      onPressed: onPressed,
    ),
  );

  Widget _chooser(BuildContext context) => Drawer(
    child: SafeArea(
      child: ListView(
        padding: EdgeInsets.zero,
        children: [
          ListTile(
            title: Text(
              'Examples',
              style: Theme.of(context).textTheme.titleLarge,
            ),
            subtitle: Text('${_all.length} of them'),
          ),
          const Divider(height: 1),
          for (final one in _all)
            ListTile(
              selected: identical(one, _example),
              title: Text(one.name),
              subtitle: Text(
                one.blurb,
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
              ),
              onTap: () {
                Navigator.of(context).pop();
                if (!identical(one, _example)) {
                  setState(() => _example = one);
                }
              },
            ),
        ],
      ),
    ),
  );
}

class _Stage extends StatefulWidget {
  const _Stage({super.key, required this.example});

  /// The one to show. Changing it replaces this widget rather than updating
  /// it -- see the key _ShellState builds it with.
  final Example example;

  @override
  State<_Stage> createState() => _StageState();
}

class _StageState extends State<_Stage> with SingleTickerProviderStateMixin {
  Example get _example => widget.example;
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

  @override
  void initState() {
    super.initState();

    // An example that puts somebody inside it drives the camera itself, which
    // is right for playing and useless for looking at a particular corner of
    // it. This hands the camera back.
    final example = _example;
    if (example is VoxelExample) _voxels(example);
    if (example is WeatherExample) _weather(example);
    if (example is ProbesExample) _probes(example);
    if (example is LightsExample) _lights(example);
    if (example is PanelShadowExample) _panelShadow(example);
    if (example is ShadowsExample) _shadows(example);
    if (example is FieldExample) _field(example);
    if (example is BatchingExample) _batching(example);
    if (example is OverdrawExample) _overdraw(example);
    if (example is GodRaysExample) _godRays(example);
    if (example is DistortionExample) _distortion(example);
    if (example is BounceExample) _bounce(example);
    if (example is DecalsExample) _decals(example);
    if (example is EnvironmentVolumesExample) _volumes(example);
    if (example is OutlineExample) _outline(example);
    if (example is ImportedExample) _imported(example);
    if (example is TexturesExample) _textures(example);
    if (example is SplatsExample) _splats(example);
    if (example is MotionBlurExample) _motionBlur(example);

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

  void _voxels(VoxelExample example) {
    if (_orblitEnv['ORBLIT_WALK'] == '0') example.walking = false;
    final far = _number('ORBLIT_RANGE');
    if (far != null) example.range = far;
    if (_orblitEnv['ORBLIT_TREES'] == '0') example.trees = false;
  }

  void _weather(WeatherExample example) {
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

  void _probes(ProbesExample example) {
    example.intensity = _number('ORBLIT_PROBE') ?? example.intensity;
    example.roughness = _number('ORBLIT_ROUGHNESS') ?? example.roughness;
    if (_orblitEnv['ORBLIT_PROBE_OFF'] == '1') example.on = false;
  }

  void _lights(LightsExample example) {
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

  void _panelShadow(PanelShadowExample example) {
    if (_orblitEnv['ORBLIT_PANEL_SHADOW'] == '0') {
      example.shadows = false;
    }
    example.panel = _number('ORBLIT_PANEL') ?? example.panel;
    example.height = _number('ORBLIT_PANEL_HEIGHT') ?? example.height;
  }

  void _shadows(ShadowsExample example) {
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

  void _field(FieldExample example) {
    example.intensity = _number('ORBLIT_FIELD') ?? example.intensity;
    example.retention = _number('ORBLIT_RETENTION') ?? example.retention;
    if (_orblitEnv['ORBLIT_FIELD_OFF'] == '1') example.on = false;
  }

  void _batching(BatchingExample example) {
    example.count = _number('ORBLIT_CRATES') ?? example.count;
    example.palette = _orblitEnv['ORBLIT_PALETTE'] ?? example.palette;
    if (_orblitEnv['ORBLIT_BATCH_MATERIAL'] == '1') {
      example.material = true;
    }
    final mesh = _orblitEnv['ORBLIT_BATCH_MESH'];
    if (mesh != null && mesh.isNotEmpty) example.mesh = mesh;
    if (_orblitEnv['ORBLIT_MOVING'] == '0') example.moving = false;
  }

  void _overdraw(OverdrawExample example) {
    example.slabs = _number('ORBLIT_SLABS') ?? example.slabs;
  }

  void _godRays(GodRaysExample example) {
    example.strength = _number('ORBLIT_GODRAYS') ?? example.strength;
    example.bearing = _number('ORBLIT_SUN_BEARING') ?? example.bearing;
    example.altitude = _number('ORBLIT_SUN_ALTITUDE') ?? example.altitude;
    example.cover = _number('ORBLIT_COVER') ?? example.cover;
    example.samples = _number('ORBLIT_GODRAY_SAMPLES') ?? example.samples;
    example.decay = _number('ORBLIT_GODRAY_DECAY') ?? example.decay;
    example.density = _number('ORBLIT_GODRAY_DENSITY') ?? example.density;
  }

  void _distortion(DistortionExample example) {
    if (_orblitEnv['ORBLIT_SHOCKWAVE'] == '0') {
      example.shockwave = false;
    }
    if (_orblitEnv['ORBLIT_HAZE'] == '0') example.haze = false;
    example.lens = _number('ORBLIT_LENS') ?? example.lens;
    example.chromatic = _number('ORBLIT_CHROMATIC') ?? example.chromatic;
    example.strength = _number('ORBLIT_WAVE') ?? example.strength;
  }

  void _bounce(BounceExample example) {
    example.strength = _number('ORBLIT_BOUNCE') ?? example.strength;
    example.reach = _number('ORBLIT_BOUNCE_RADIUS') ?? example.reach;
    if (_orblitEnv['ORBLIT_BOUNCE_OFF'] == '1') example.on = false;
  }

  void _decals(DecalsExample example) {
    final environment = _orblitEnv;
    if (environment['ORBLIT_DECALS_OFF'] == '1') example.on = false;
    if (environment['ORBLIT_DECAL_FADE_OFF'] == '1') example.angleFade = false;
    if (environment['ORBLIT_DECAL_MASK_OFF'] == '1') {
      example.spareTheCrate = false;
    }
    example.only = _number('ORBLIT_DECAL_ONLY')?.round();
  }

  void _volumes(EnvironmentVolumesExample example) {
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
      String three(Vector3 v) =>
          [v.x, v.y, v.z].map((c) => c.toStringAsFixed(4)).join(',');
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

  void _outline(OutlineExample example) {
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

  void _imported(ImportedExample example) {
    final samples = _orblitEnv['ORBLIT_SAMPLES'];
    if (samples != null && samples.isNotEmpty) example.directory = samples;
    final model = _orblitEnv['ORBLIT_MODEL'];
    if (model != null && model.isNotEmpty) example.model = model;
    example.clip = _number('ORBLIT_CLIP')?.round();
    example.variant = _number('ORBLIT_VARIANT')?.round();
    if (_orblitEnv['ORBLIT_DAYLIGHT'] == '0') example.daylight = false;
  }

  /// Set here rather than left to the example's own reading of
  /// `Platform.environment`, which is empty on the iOS simulator and has
  /// nothing in it on Android.
  void _textures(TexturesExample example) {
    final directory = _orblitEnv['ORBLIT_TEXTURES'];
    if (directory != null && directory.isNotEmpty) {
      example.directory = directory;
    }
    final files = _orblitEnv['ORBLIT_TEXTURE_FILES'];
    if (files != null && files.isNotEmpty) {
      example.files = [
        for (final file in files.split(','))
          if (file.trim().isNotEmpty) file.trim(),
      ];
    }
    final picture = _orblitEnv['ORBLIT_PICTURE'];
    if (picture != null && picture.isNotEmpty) example.picture = picture;
    example.largestTexture =
        _number('ORBLIT_TEXTURE_SIZE')?.round() ?? example.largestTexture;
  }

  void _splats(SplatsExample example) {
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
    final limit = _number('ORBLIT_SPLAT_LIMIT')?.round();
    if (limit != null && limit > 0) example.limit = limit;
    if (_orblitEnv['ORBLIT_SPLAT_COARSE'] == '1') example.coarseOrder = true;
    if (_orblitEnv['ORBLIT_SPLAT_DEVICE'] == '0') {
      example.deviceLimits = false;
    }
  }

  void _motionBlur(MotionBlurExample example) {
    if (_orblitEnv['ORBLIT_MOTION'] == '0') example.on = false;
    if (_orblitEnv['ORBLIT_MOTION_OBJECTS'] == '0') {
      example.objects = false;
    }
    if (_orblitEnv['ORBLIT_PAN'] == '1') example.panning = true;
    example.shutter = _number('ORBLIT_SHUTTER') ?? example.shutter;
  }

  @override
  void dispose() {
    _clock.dispose();
    super.dispose();
  }

  /// Asks the viewport what its device can do, and hands the answer to the
  /// example, which is how one scene scales itself to a desktop, a phone and
  /// a browser.
  ///
  /// Asked again for a few seconds rather than once: on the web the renderer
  /// starts a frame or two after the view is laid out, and answers nothing
  /// until it has.
  Future<void> _learnDevice(int viewport) async {
    for (var attempt = 0; attempt < 50; attempt++) {
      if (!mounted) return;
      final profile = await OrblitView.profileOf(viewport);
      if (profile != null) {
        if (mounted) setState(() => _example.device = profile);
        return;
      }
      await Future<void>.delayed(const Duration(milliseconds: 100));
    }
  }

  @override
  Widget build(BuildContext context) => GestureDetector(
    onPanUpdate: (details) => setState(() => _look.orbit(details.delta)),
    child: OrblitView(
      onViewport: _learnDevice,
      // What each model file holds, for the examples that offer its clips
      // and looks by name.
      onAssetInfo: (info) => setState(() => _example.models[info.path] = info),
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
  );
}

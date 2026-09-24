import 'dart:typed_data';

import 'decal.dart';
import 'material.dart';
import 'outline.dart';
import 'environment.dart';
import 'field.dart';
import 'graph.dart';
import 'key_list.dart';
import 'pipeline.dart';
import 'video.dart';
import 'post.dart';
import 'screen.dart';
import 'volumes.dart';

import 'package:vector_math/vector_math_64.dart';

import 'models.dart';
import 'population.dart';
import 'splats.dart';
import 'sprites.dart';
import 'terrain.dart';

part 'scene_objects.dart';
part 'scene_sky.dart';
part 'scene_message.dart';

class OrblitScene {
  OrblitScene({
    required this.objects,
    required this.camera,
    List<OrblitLight>? lights,
    OrblitSky? sky,
    OrblitFog? fog,
    OrblitPrecipitation? precipitation,
    List<OrblitPopulation>? populations,
    List<OrblitSplats>? splats,
    List<OrblitSprites>? sprites,
    List<OrblitTerrain>? terrain,
    List<OrblitMaterial>? materials,
    List<OrblitVideo>? videos,
    OrblitPostProcess? post,
    OrblitPipeline? pipeline,
    OrblitRenderGraph? graph,
    OrblitEnvironment? environment,
    List<OrblitProbe>? probes,
    OrblitField? field,
    List<OrblitEnvironmentVolume>? volumes,
    List<OrblitDecal>? decals,
    OrblitOutline? outline,
    this.batching = true,
    this.depthPrepass = false,
    OrblitGodRays? godRays,
    List<OrblitDistortion>? distortions,
  }) : lights = lights ?? const [],
       decals = decals ?? const [],
       outline = outline ?? OrblitOutline.none,
       godRays = godRays ?? OrblitGodRays.off,
       distortions = distortions ?? const [],
       probes = probes ?? const [],
       volumes = volumes ?? const [],
       field = field ?? OrblitField.none,
       environment = environment ?? OrblitEnvironment.none,
       pipeline = pipeline ?? OrblitPipeline(),
       graph = graph ?? OrblitRenderGraph.standard(),
       materials = materials ?? const [],
       videos = videos ?? const [],
       post = post ?? OrblitPostProcess(),
       populations = populations ?? const [],
       splats = splats ?? const [],
       sprites = sprites ?? const [],
       terrain = terrain ?? const [],
       sky = sky ?? OrblitSky(),
       fog = fog ?? OrblitFog.none,
       precipitation = precipitation ?? OrblitPrecipitation.none;

  /// The same scene with something changed.
  ///
  /// A scene is stated whole every frame, which makes taking one somebody
  /// else built and altering one thing about it awkward — an editor putting a
  /// gizmo layer over a game's scene, a tool running somebody's scene through
  /// an effect to see what it does to it, a test rendering the same scene
  /// twice with one setting moved. All of those otherwise mean rebuilding a
  /// dozen fields by hand and quietly dropping the one that was added last.
  OrblitScene copyWith({
    List<OrblitSprites>? sprites,
    List<OrblitTerrain>? terrain,
    List<OrblitObject>? objects,
    List<OrblitPopulation>? populations,
    List<OrblitSplats>? splats,
    List<OrblitLight>? lights,
    List<OrblitMaterial>? materials,
    List<OrblitVideo>? videos,
    OrblitCamera? camera,
    OrblitSky? sky,
    OrblitFog? fog,
    OrblitPrecipitation? precipitation,
    OrblitPipeline? pipeline,
    OrblitPostProcess? post,
    OrblitRenderGraph? graph,
    OrblitEnvironment? environment,
    List<OrblitProbe>? probes,
    OrblitField? field,
    List<OrblitEnvironmentVolume>? volumes,
    List<OrblitDecal>? decals,
    OrblitOutline? outline,
    bool? batching,
    bool? depthPrepass,
    OrblitGodRays? godRays,
    List<OrblitDistortion>? distortions,
  }) => OrblitScene(
    // The probes and the field used to be missing here, so any copy quietly
    // dropped them. Resolving the volumes copies every scene that has any,
    // which is how it was noticed.
    probes: probes ?? this.probes,
    godRays: godRays ?? this.godRays,
    distortions: distortions ?? this.distortions,
    field: field ?? this.field,
    volumes: volumes ?? this.volumes,
    batching: batching ?? this.batching,
    depthPrepass: depthPrepass ?? this.depthPrepass,
    objects: objects ?? this.objects,
    populations: populations ?? this.populations,
    splats: splats ?? this.splats,
    sprites: sprites ?? this.sprites,
    terrain: terrain ?? this.terrain,
    lights: lights ?? this.lights,
    materials: materials ?? this.materials,
    videos: videos ?? this.videos,
    camera: camera ?? this.camera,
    sky: sky ?? this.sky,
    fog: fog ?? this.fog,
    precipitation: precipitation ?? this.precipitation,
    pipeline: pipeline ?? this.pipeline,
    post: post ?? this.post,
    graph: graph ?? this.graph,
    environment: environment ?? this.environment,
    decals: decals ?? this.decals,
    outline: outline ?? this.outline,
  );

  final List<OrblitObject> objects;

  /// The parts of the scene that are many copies of one thing.
  ///
  /// Kept apart from [objects] because they are a different question. An
  /// object is tracked one at a time; a population is submitted whole. Mixing
  /// them would mean either paying an object's price for every tree or losing
  /// an object's individuality for every one that needs it.
  final List<OrblitPopulation> populations;

  /// Clouds of 3D Gaussians: captured places, or generated ones.
  ///
  /// Apart from [objects] and [populations] because they are not surfaces.
  /// They are drawn after everything solid, sorted back to front among
  /// themselves, and hidden by anything solid in front of them.
  final List<OrblitSplats> splats;

  /// Layers of flat pictures: sprites, tiles, a scrolling backdrop.
  ///
  /// Drawn after everything solid, in their layers' order and then in the
  /// order each layer gives them, which is what a 2D scene means by "on top".
  /// A scene of nothing else, seen through an orthographic camera, is a 2D
  /// game drawn by the same renderer as a 3D one.
  final List<OrblitSprites> sprites;

  /// Ground: heights over regions of the world, and what covers them.
  ///
  /// Apart from [objects] because it is not a mesh. It is one grid drawn a
  /// few times round the camera and raised by its heights on the GPU, so its
  /// cost is the same however much ground there is, and changing the ground
  /// is sending the regions that changed.
  final List<OrblitTerrain> terrain;

  /// Every light in the scene. A scene with none is lit by its sky alone,
  /// which is dim and even and perfectly legitimate.
  final List<OrblitLight> lights;

  /// Every material any object in the scene is made of.
  ///
  /// Listed here rather than held on the objects because materials are shared
  /// and objects are not: a hundred crates made of the same wood are a
  /// hundred entries in [objects] and one entry here, and the renderer builds
  /// one shader instance for them all. Sending the list whole each frame also
  /// means a material can be edited — a slider dragged — without anything
  /// having to say which objects were affected.
  final List<OrblitMaterial> materials;

  /// Every video the scene is playing.
  ///
  /// Beside the materials rather than inside them, for the same reason: one
  /// film can be on four screens, and playing it four times would be four
  /// decoders doing identical work out of step with each other.
  final List<OrblitVideo> videos;

  final OrblitCamera camera;
  final OrblitSky sky;
  final OrblitFog fog;
  final OrblitPrecipitation precipitation;

  /// How much of the frame's work actually happens.
  ///
  /// On the scene rather than on the view, for the same reason the post
  /// settings are: four views of one world should be drawn to one standard.
  /// It is the tier a machine has been set to, not a property of a window.
  final OrblitPipeline pipeline;

  /// Everything done to the image after the scene is drawn.
  ///
  /// On the scene rather than on the camera, because a look belongs to the
  /// place rather than to where somebody is standing in it: four views of one
  /// world should not each grade it differently.
  final OrblitPostProcess post;

  /// How the frame is put together: which passes there are and what they draw
  /// into.
  ///
  /// [pipeline] says how much of each step happens; this says which steps
  /// there are. The default is one pass into the picture, which is the frame
  /// the renderer drew before graphs existed — so nothing pays for the
  /// generality until it is used.
  final OrblitRenderGraph graph;

  /// The place the scene is standing in, as light and as a backdrop.
  ///
  /// Overrules [sky]'s flat ambient while it is set: a scene lit by a
  /// photograph of a room and *also* by an even grey wash is a scene lit
  /// twice, and the wash is the half that flattens it. The sky's own colour
  /// and its body go on meaning what they meant.
  final OrblitEnvironment environment;

  /// The reflections captured from points inside the scene.
  ///
  /// The camera is inside one of these at a time, and that one lights the
  /// scene in place of [environment]. A scene with none is lit by its
  /// environment as before, which is why adding probes to an existing scene
  /// changes nothing until one of them contains the camera.
  final List<OrblitProbe> probes;

  /// The light kept in the world rather than on the screen.
  ///
  /// Off by default, and free when off: a scene that never mentions one is
  /// lit exactly as it was.
  final OrblitField field;

  /// The regions of the world that look different from the rest of it — a
  /// dim hall off a sunny courtyard, a foggy cave.
  ///
  /// Resolved against [camera] when the scene is sent: the fog, sky,
  /// environment, exposure and grade that go over the channel are this
  /// scene's own, moved towards whatever the volumes around the camera ask
  /// for. The renderer never sees a volume, so none of this is native code.
  /// See [resolved] for the scene that is actually drawn.
  final List<OrblitEnvironmentVolume> volumes;

  /// Shafts of light from the scene's directional light, through whatever
  /// stands against the sky. Off by default, and free when off.
  final OrblitGodRays godRays;

  /// Air that bends the light through it: shockwaves, heat haze, a lens.
  /// None by default, and free when none of them moves anything.
  final List<OrblitDistortion> distortions;

  /// The light god rays come from: the first directional one, which is the
  /// one the renderer draws.
  OrblitLight? get _sun => lights
      .where((light) => light.kind == OrblitLightKind.directional)
      .firstOrNull;

  /// The graph the renderer is actually sent: [graph], with the passes for
  /// [godRays] and [distortions] put in when the graph is the renderer's own
  /// and something asks for them. See [OrblitRenderGraph.withScreenEffects].
  OrblitRenderGraph get drawnGraph => graph.withScreenEffects(
    godRays: godRays.isOn && _sun != null,
    distortion: distortions.any((one) => one.isActive),
  );

  /// This scene as it looks from [at] — the camera's position unless said
  /// otherwise — with every volume applied and none left in it.
  ///
  /// What [toMessage] sends. Public because a host wants to ask the same
  /// question: what the fog is where the player is standing, to decide
  /// whether to play the echoey footsteps.
  OrblitScene resolved([Vector3? at]) {
    if (volumes.isEmpty) return this;
    return OrblitEnvironmentSettings.of(
      this,
    ).resolve(volumes, at ?? camera.position).applyTo(this);
  }

  /// What is painted onto the surfaces: posters, scorches, puddles, road
  /// markings. Each one a box and a picture, projected onto whatever lit
  /// surface is inside the box before it is lit.
  ///
  /// The first [OrblitDecal.budget] are painted; the renderer reports any past
  /// that rather than dropping them without a word.
  final List<OrblitDecal> decals;

  /// Which objects have a line drawn round them, and how.
  ///
  /// On the scene rather than on the objects because it is about the view of
  /// the world rather than the world: a game never sets it, and an editor
  /// changes it on every click without touching a single object. Nothing is
  /// outlined until somebody says otherwise, and nothing is paid for until
  /// then either.
  final OrblitOutline outline;

  /// Whether objects that are the same thing are drawn together.
  ///
  /// A hundred crates with one mesh, one material and the same flags are a
  /// hundred draws per pass without this and a handful with it: the renderer
  /// builds the group as one manually instanced renderable, sixty-four copies
  /// to a renderable. Nothing about the objects themselves changes — each is
  /// still its own entry in [objects], and picking still answers with the one
  /// that was clicked.
  ///
  /// Four or more objects with the same [OrblitObject.mesh],
  /// [OrblitObject.material] and flags form a group; a placeholder cube on
  /// the default surface needs the same [OrblitObject.colour] too, because
  /// there the colour is the material. An object with
  /// [OrblitObject.morphWeights], or a model wearing its own file's
  /// materials, never batches.
  ///
  /// A merged group is culled by one box, so a member can draw or cast a
  /// shadow when only a chunk-mate is in view: wasted drawing at the edge of
  /// a chunk, never a wrong picture. On by default; `batching: false` gets
  /// the unmerged path back. See `native/DRAW_PERFORMANCE.md` for the
  /// measurements behind that default.
  final bool batching;

  /// Whether opaque objects are drawn into depth alone before they are shaded.
  ///
  /// A prepass draws every opaque object twice — once writing depth and no
  /// colour, then once shaded — so a pixel is shaded once however many
  /// surfaces stand over it. It costs a second entity, a second cull and a
  /// second draw for every object it covers.
  ///
  /// It covers visible opaque objects drawn as the placeholder cube. A model
  /// out of a glTF file is not covered, because Filament offers no getter for
  /// a primitive's geometry; a masked or blended surface is not covered,
  /// because depth written for either would be depth in the wrong place.
  /// Objects merged by [batching] get no prepass either, so the two switches
  /// do not compound.
  ///
  /// Off by default: it is worth nothing on a tile-based GPU, which does this
  /// in silicon already, and the only immediate-mode machine measured was a
  /// software rasteriser — too favourable a case to default from. The picture
  /// is bit-identical either way, so the only thing at stake is time. See
  /// `native/DRAW_PERFORMANCE.md` for the numbers.
  final bool depthPrepass;

  /// The highest layer an object may be on.
  ///
  /// Seven of them, because the renderer's own mask is eight bits and the
  /// top one says whether a thing is drawn at all. Seven groups is more
  /// than any scene here has wanted and few enough to stay one byte.
  static const int maxLayer = 6;

  /// The names of the passes that will run, in order.
  ///
  /// What a capture's timings line up against: the renderer sends back two
  /// numbers per pass and this says which pass each pair belongs to, so the
  /// names never have to cross.
  List<String> get passNames => [
    for (final pass in drawnGraph.schedule) pass.name,
  ];

  /// The whole scene, packed into the flat arrays the channel carries.
  ///
  /// [sentRevisions] is what the renderer already holds for each population,
  /// so that buffers it already has are left out. Passing null sends
  /// everything, which is what a fresh renderer needs.
  Map<String, Object> toMessage(
    int textureId, {
    Map<int, int>? sentRevisions,
    Map<int, int>? sentSplatRevisions,
    Map<int, int>? sentSpriteRevisions,
    Map<int, OrblitTerrainHeld>? sentTerrain,
    double? at,
  }) {
    // Volumes are resolved here, where the scene is packed, so that every
    // host gets them without calling anything and the message stays the
    // shape the renderer already reads.
    if (volumes.isNotEmpty) {
      return resolved().toMessage(
        textureId,
        sentRevisions: sentRevisions,
        sentSplatRevisions: sentSplatRevisions,
        sentSpriteRevisions: sentSpriteRevisions,
        sentTerrain: sentTerrain,
        at: at,
      );
    }

    final drawn = drawnGraph;
    final sun = _sun;

    return {
      'textureId': textureId,
      ..._objectMessage(),
      ..._materialMessage(),
      ..._videoMessage(),
      ..._decalMessage(),
      ..._probeMessage(),
      ..._lightMessage(),
      ..._cameraMessage(),
      ..._skyMessage(),
      'batching': batching,
      'depthPrepass': depthPrepass,
      'postParams': post.packed,
      'pipelineParams': pipeline.packed,
      'fieldParams': field.packed,
      'fieldFrom': field.from,
      'environmentRadiance': environment.radiance ?? '',
      'environmentSkybox': environment.skybox ?? '',
      'environmentParams': environment.packed,
      'graphPasses': drawn.packedPasses,
      'graphTargets': drawn.packedTargets,
      'graphTargetNames': [for (final target in drawn.targets) target.name],
      'outlineKeys': outline.packedKeys,
      'outlineParams': outline.packed,
      // The shafts' settings, with the light they come from and the cloud in
      // front of it worked out here, where the scene is whole. Cloud only
      // counts when the sky that carries it is drawn.
      'godRayParams': godRays.pack(
        towardLight: sun == null ? null : -sun.direction,
        lightColour: sun?.colour,
        cloudCover: sky.drawn && sky.clouds.isVisible ? sky.clouds.cover : 0,
      ),
      'distortionParams': OrblitDistortion.packAll(distortions),
      // When the application reckons this is, in its own seconds.
      //
      // The renderer draws far more often than it is told anything, and works
      // out where the camera is in between. Doing that from when the messages
      // *arrived* uses a clock with jitter in it, and dividing by a jittery
      // gap turns a small timing wobble into a large wrong speed. This is the
      // clock the camera was actually solved on.
      'at': at ?? 0.0,
      ...?_populationMessage(sentRevisions),
      ...?_splatMessage(sentSplatRevisions),
      ...?_spriteMessage(sentSpriteRevisions),
      ...?_terrainMessage(sentTerrain),
      // What models' own files do to them: clips, variants, joints. Measured
      // from `at` above, so the renderer can sample a clip at the moment each
      // frame is drawn rather than the moment this arrived.
      ...?packPoses(objects),
    };
  }
}

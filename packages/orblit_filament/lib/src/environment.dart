import 'dart:typed_data';

import 'package:vector_math/vector_math_64.dart';

/// The light a place brings with it, and the backdrop it is seen against.
///
/// A photograph of somewhere real, turned into light. Everything in the scene
/// is then lit by that place — a chrome ball reflects the room it is standing
/// in, a matte surface picks up the colour of the wall beside it, and a
/// shadow is filled by the sky rather than by a number somebody guessed.
///
/// It is the single largest difference between a render that looks computed
/// and one that looks photographed, and once it is built it costs nothing per
/// frame: the lighting is a cubemap, sampled.
///
/// **Two ways to build it, with the same result.**
///
/// *Baked*, ahead of time, by `tool/bake_environment.sh`, which runs
/// Filament's own `cmgen`:
///
/// ```
/// tool/bake_environment.sh kitchen.hdr out
/// ```
///
/// writes `out/kitchen_ibl.ktx` — a prefiltered radiance cubemap with the
/// spherical harmonics for the diffuse part in its metadata — and
/// `out/kitchen_skybox.ktx`, the backdrop. Name them with the default
/// constructor.
///
/// *Filtered at run time*, from the picture itself, with
/// [OrblitEnvironment.fromImage]: an equirectangular `.hdr` or `.exr` is
/// decoded off the drawing thread, its harmonics worked out exactly as
/// `cmgen` works them out, and its reflections filtered on the GPU a frame at
/// a time. The scene is lit a few frames after the picture is named, and
/// while it is being filtered the scene notes say so.
///
/// Bake what ships. The result is fixed before anybody runs it, a launch
/// reads two small files and filters nothing, and every device gets the same
/// light. Name the picture for what is not known in advance — an editor
/// trying environments on, a user's own HDR, anything fetched — and while
/// iterating, where a bake step between every try is the slow part. The two
/// are measured against each other in pixels by `native/headless`'s
/// environment check: matte surfaces agree to within a level, and
/// reflections to a mean of under one level.
class OrblitEnvironment {
  const OrblitEnvironment({
    this.radiance,
    this.skybox,
    this.intensity = 30000,
    this.rotation = 0,
    this.showSkybox = true,
    this.size = 0,
  });

  /// The light and the backdrop of an equirectangular `.hdr` or `.exr`
  /// picture, filtered at run time — no bake.
  ///
  /// [image] is a path, or the name bytes were provided under
  /// (`OrblitResources`), and is named as both [radiance] and [skybox]. What
  /// it costs, measured on an M4 Pro with a 2K picture: a decode and the
  /// harmonics on a worker thread (about 70 ms for an `.hdr`, 150 ms for a
  /// ZIP-compressed `.exr`), then one frame of a few milliseconds to upload
  /// it and one frame of 20 to 55 to filter it. Once per picture: naming the
  /// same picture again, or the same bytes under another name, lights the
  /// scene on the next frame with nothing filtered. In a browser the decode
  /// runs on the page for now.
  ///
  /// A picture that cannot be used — missing, damaged, too large for the
  /// device, not twice as wide as it is tall — is reported in the scene
  /// notes, under `environment` and `skybox`, and the scene is lit by its
  /// flat ambient meanwhile. A device that cannot filter on the GPU filters a
  /// smaller environment on the CPU instead; one that cannot hold a
  /// floating-point cubemap at all says to bake.
  const OrblitEnvironment.fromImage(
    String image, {
    this.intensity = 30000,
    this.rotation = 0,
    this.showSkybox = true,
    this.size = 0,
  }) : radiance = image,
       skybox = image;

  /// Nothing: the scene is lit by its lights and its flat ambient alone.
  static const OrblitEnvironment none = OrblitEnvironment();

  /// Where the light comes from: the prefiltered radiance cubemap `cmgen`
  /// baked (`*_ibl.ktx`), or an `.hdr` or `.exr` picture to filter here.
  ///
  /// Both halves of the lighting come out of it. The mip chain is the
  /// reflection, rough surfaces reading the blurrier levels; the spherical
  /// harmonics are the diffuse — read from a baked cubemap's metadata, or
  /// worked out from a picture. A cubemap with no harmonics in it still
  /// lights reflections and leaves matte surfaces dark, which is what an
  /// unbaked cubemap looks like and is reported rather than guessed at.
  final String? radiance;

  /// The backdrop: a cubemap `cmgen` baked (`*_skybox.ktx`), or an `.hdr` or
  /// `.exr` picture, drawn sharper than the reflections.
  ///
  /// Separate from [radiance] because they are different pictures at
  /// different sizes: the backdrop is what the camera sees and wants to be
  /// sharp, the radiance is what surfaces sample and is deliberately blurred.
  /// One without the other is legitimate — a backdrop that lights nothing, or
  /// light with no visible sky, which is what an interior lit through a window
  /// wants.
  final String? skybox;

  /// How bright the place is, in lux.
  ///
  /// The same unit everything else in the engine states light in, so a
  /// daylight environment and a directional sun can be set from the same
  /// meter reading rather than balanced by eye.
  final double intensity;

  /// How far the environment is turned about the vertical, in radians.
  ///
  /// What lets one bake serve a scene facing any direction: the sun in a
  /// photographed sky is wherever it was when the photograph was taken, and
  /// turning the environment is how it comes to be behind the camera instead.
  final double rotation;

  /// Whether the backdrop is drawn as well as sampled.
  ///
  /// Off is an environment that lights the scene and is never seen — an
  /// interior where the windows look out onto geometry, or a shot composited
  /// over something else later.
  final bool showSkybox;

  /// The side of the reflection cubemap a picture is filtered at, in texels,
  /// or nought for the device's own choice. Ignored for baked cubemaps, which
  /// are the size they were baked at.
  ///
  /// The device's choice, from `OrblitDeviceProfile`: 256 — `cmgen`'s default,
  /// and so a default bake's — at Filament feature level 2 and above; 128 at
  /// feature level 1 (OpenGL ES 3.0, WebGL 2), where filtering costs four
  /// times less; and 64 where the GPU cannot render half floats and the CPU
  /// filters instead. The backdrop is four times this, held to 1024 (512
  /// under 3 GB of memory, 256 on the CPU) and to a quarter of the picture's
  /// width.
  ///
  /// A size given here is rounded down to a power of two and held to 16–1024
  /// (16–128 on the CPU). Give the same size to `tool/bake_environment.sh
  /// --size` and a bake and a run-time environment agree. Changing it filters
  /// the picture again.
  final int size;

  /// Whether there is anything here at all.
  bool get isSet =>
      (radiance != null && radiance!.isNotEmpty) ||
      (skybox != null && skybox!.isNotEmpty);

  OrblitEnvironment copyWith({
    String? radiance,
    String? skybox,
    double? intensity,
    double? rotation,
    bool? showSkybox,
    int? size,
  }) => OrblitEnvironment(
    radiance: radiance ?? this.radiance,
    skybox: skybox ?? this.skybox,
    intensity: intensity ?? this.intensity,
    rotation: rotation ?? this.rotation,
    showSkybox: showSkybox ?? this.showSkybox,
    size: size ?? this.size,
  );

  /// How many floats [packed] holds.
  static const int stride = 4;

  /// Intensity, rotation, whether the backdrop shows, and [size].
  Float32List get packed => Float32List.fromList([
    intensity,
    rotation,
    showSkybox ? 1 : 0,
    size.toDouble(),
  ]);
}

/// A reflection captured from a point in the world.
///
/// An environment is a photograph of somewhere else. Indoors that is the wrong
/// picture: a chrome kettle in a red kitchen reflects the sky, because the sky
/// is the only environment the scene has. A probe is the scene taking its own
/// photograph, from a point inside itself, and lighting from that instead.
///
/// Captured rather than baked, but not captured often. Six renders of the
/// whole scene and a filter over the result is not a per-frame cost, so a
/// probe is taken once and kept until [version] changes. That makes when it
/// happens the host's decision, which is the only place the decision can
/// live: nothing else knows whether the room has been repainted.
/// {@category Lighting and environment}
class OrblitProbe {
  const OrblitProbe({
    required this.key,
    required this.position,
    this.radius = 12,
    this.resolution = 256,
    this.version = 0,
    this.layers = 0xFF,
    this.intensity = 1,
  });

  /// This probe's identity, stable for as long as it exists. Shares the one
  /// key space with objects and lights.
  final int key;

  /// Where the photograph is taken from. The middle of the room, usually, and
  /// at head height rather than on the floor — a probe on the floor sees a
  /// great deal of floor.
  final Vector3 position;

  /// How far its influence reaches, in metres.
  ///
  /// The camera is inside exactly one probe at a time, and that is the probe
  /// the scene is lit by. Where two overlap the nearer middle wins, so a
  /// doorway is two probes with the join wherever their centres say.
  final double radius;

  /// The size of one face of the captured cube.
  ///
  /// Small is not much of a compromise here: what a reflection samples is the
  /// blurred mip chain, and only a mirror reads the sharpest level. Two
  /// hundred and fifty-six is generous for a room.
  final int resolution;

  /// Bump this to take the photograph again.
  ///
  /// A probe is not re-captured because something moved — the renderer has no
  /// way to know that the thing which moved mattered, and re-capturing every
  /// frame would cost six frames a frame. Changing this number is how a host
  /// says the room is different now.
  final int version;

  /// Which layers the capture draws.
  ///
  /// Worth setting, and the reason is not obvious: a probe captured from
  /// inside a shiny object photographs that object, and the object then
  /// reflects a picture of itself. Putting the reflective things on their own
  /// layer and leaving it out here is how a reflection ends up showing the
  /// room rather than a smaller copy of the thing doing the reflecting.
  final int layers;

  /// How much of the captured light counts, as a multiplier.
  ///
  /// One, and not the thirty thousand lux an environment states, because the
  /// two are not the same kind of number. A baked environment is stored
  /// relative to some reference and its intensity is what turns it into lux; a
  /// probe is the scene's own light, rendered with the exposure held at one,
  /// so it arrives already in the units the rest of the frame is in.
  final double intensity;

  /// How many floats one probe occupies.
  static const int stride = 8;

  /// Writes this probe's floats into the scene's probe block.
  void pack(Float32List into, int at) {
    into[at] = position.x;
    into[at + 1] = position.y;
    into[at + 2] = position.z;
    into[at + 3] = radius;
    into[at + 4] = resolution.toDouble();
    into[at + 5] = version.toDouble();
    into[at + 6] = layers.toDouble();
    into[at + 7] = intensity;
  }
}

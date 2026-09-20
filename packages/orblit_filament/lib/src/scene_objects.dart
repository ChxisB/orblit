part of 'scene.dart';

// What a scene is made of, one class each: a thing to draw, the light it
// is drawn by, and the eye that sees it.

/// One thing to draw: where it is, what it is made of, and how it behaves
/// towards light.
///
/// The [key] is what makes a scene cheap to send repeatedly. A scene arrives
/// on every frame of a drag, and a renderer that cannot tell this frame's
/// crate from last frame's has no choice but to destroy everything and build
/// it again. A key that survives an edit lets the renderer move what moved and
/// leave the rest alone.
///
/// Keys are the host's to invent and the host's to keep stable. Any number
/// will do, as long as one object carries the same key for as long as it
/// exists and no new object reuses a key still in the scene.
class OrblitObject {
  /// Creates an object at [transform] in [colour], which is linear RGB — not
  /// sRGB, and not a Flutter Color, because the lighting maths happens in
  /// linear space and a silent conversion is the kind that goes unnoticed.
  const OrblitObject({
    required this.key,
    required this.transform,
    required this.colour,
    this.mesh,
    this.material,
    this.castShadows = true,
    this.receiveShadows = true,
    this.visible = true,
    this.layer = 0,
    this.morphWeights,
    this.animation,
    this.variant,
    this.joints,
  });

  /// Which of the mesh file's own clips plays, and where it is.
  ///
  /// Null plays nothing, and an object that stops naming a clip goes back to
  /// the pose its file rests in. What a file has is in [OrblitAssetInfo],
  /// which [OrblitView] reports as models are loaded. Ignored for the built-in
  /// cube, which has no clips.
  final OrblitAnimation? animation;

  /// Which of the mesh file's material variants the object wears, by its
  /// position in [OrblitAssetInfo.variants] — the same model as a red shoe or
  /// a blue one, sharing one load. Null wears the file's own materials, and
  /// [material] still overrides every variant.
  final int? variant;

  /// Joints of the mesh's skins set by hand, after any clip. See
  /// [OrblitJointPose].
  final List<OrblitJointPose>? joints;

  /// How far each of the mesh's shapes is dialled in, nought to one.
  ///
  /// A morph target is a second set of positions for the same vertices — a
  /// face with its mouth open, a wing folded — and the weight says how far
  /// between the two the mesh currently sits. Several add together, which is
  /// how a face is built from a smile and a blink rather than from every
  /// combination of the two.
  ///
  /// The shapes come out of the glTF; this only says how much of each. Null
  /// leaves whatever the file set, which for most models is all zeroes.
  ///
  /// Not skinning. A skeleton moves a mesh by joints and morphing moves it a
  /// vertex at a time, and they are for different things: a limb bends, a
  /// mouth does not.
  final List<double>? morphWeights;

  /// This object's identity, stable for as long as the object exists.
  final int key;

  final Matrix4 transform;
  final Vector3 colour;

  /// An absolute path to a glTF or glb file, or null for the built-in cube.
  ///
  /// Loaded once and kept, however many scenes mention it. An object naming a
  /// file that cannot be read is drawn as the cube, and the failure comes back
  /// from the publish rather than being logged where nobody sees it.
  final String? mesh;

  /// The key of the material this object is made of, or null to be drawn in
  /// [colour] on the default surface.
  ///
  /// A key rather than the material itself, because a material is shared —
  /// one entry in [OrblitScene.materials] stands behind every object made of
  /// it, and the renderer keeps one instance for the lot. Naming a key the
  /// scene does not list falls back to [colour] rather than failing: a
  /// material that has not finished loading should not take the object off
  /// screen with it.
  ///
  /// On a mesh this *overrides* the materials the file brought with it, on
  /// every primitive. Leave it null to keep them.
  final int? material;

  /// Whether this object appears in other objects' shadows.
  ///
  /// Worth having per object rather than only per scene: a ground plane that
  /// casts is a plane casting a shadow onto itself, and the acne that produces
  /// is the commonest reason a scene looks dirty for no visible cause.
  final bool castShadows;

  /// Whether shadows land on this object.
  final bool receiveShadows;

  /// Whether it is drawn at all.
  ///
  /// Hidden is not deleted: the object keeps its key and its mesh stays
  /// loaded, so showing it again costs a flag rather than a parse.
  final bool visible;

  /// Which group of the scene this belongs to, from 0 to
  /// [OrblitScene.maxLayer].
  ///
  /// What lets one scene serve several passes. A pass draws the layers it
  /// names and no others, so the water can be left out of its own reflection,
  /// the editor's gizmos out of a thumbnail, and a stand-in for an
  /// off-screen object into a shadow pass and nowhere else.
  ///
  /// Zero for everything until somebody says otherwise, and a pass draws every
  /// layer until it says otherwise, so a scene that has never heard of layers
  /// behaves exactly as it did.
  final int layer;

  /// The flag bits this object contributes to the message.
  ///
  /// The layer rides in the high bits rather than in an array of its own: it
  /// is three bits per object, and a parallel array of them would be a fourth
  /// buffer allocated, packed and crossed every frame to carry a byte.
  int get _flags =>
      (castShadows ? 1 : 0) |
      (receiveShadows ? 2 : 0) |
      (visible ? 4 : 0) |
      (layer.clamp(0, OrblitScene.maxLayer) << 8);
}

/// The kinds of light a renderer actually implements.
///
/// Shorter than the list an artist works with, but no longer shorter by one:
/// a rectangle is here because a rectangle is what most real light comes from
/// — a window, a softbox, a strip in a ceiling — and approximating one with a
/// point puts the highlight in the wrong shape, which is the part of the image
/// somebody actually reads the light from.
enum OrblitLightKind {
  /// Parallel rays from infinitely far away, in lux. Filament honours one per
  /// scene, so a second is reported back rather than quietly ignored.
  directional,

  /// Radiates in every direction from a point, in lumens.
  point,

  /// A cone, in lumens.
  spot,

  /// A rectangle that emits from one face, in lumens.
  ///
  /// Not a Filament light: Filament has none, so this one is shaded by the
  /// surface material itself, against a fitted table that gives the rectangle
  /// a closed-form answer. The consequences of being outside Filament's own
  /// lighting are worth stating plainly: an area light casts no shadow, and
  /// it does not count against the punctual budget because it never becomes
  /// a punctual light.
  ///
  /// [OrblitLight.direction] is the face it emits from, [OrblitLight.tangent]
  /// the edge [OrblitLight.width] is measured along, and the height runs along
  /// the two crossed together. The back face emits nothing.
  area,
}

/// A light, in the units a renderer takes.
///
/// Watts, metres and degrees belong upstream in `orblit_light`, where an
/// artist's numbers are converted once. What arrives here is already
/// photometric, because a renderer that converts as well is a second place for
/// the conversion to be wrong.
class OrblitLight {
  OrblitLight({
    required this.key,
    required this.kind,
    required this.intensity,
    Vector3? colour,
    Vector3? position,
    Vector3? direction,
    this.falloffRadius = 10,
    this.innerConeAngle = 0.5,
    this.outerConeAngle = 0.6,
    this.sunAngularRadius = 0.263,
    this.sourceRadius = 0.1,
    this.haloSize = 10,
    this.haloFalloff = 80,
    this.castShadows = true,
    this.width = 1,
    this.height = 1,
    Vector3? tangent,
  }) : colour = colour ?? Vector3(1, 1, 1),
       position = position ?? Vector3.zero(),
       direction = direction ?? Vector3(0, -1, 0),
       tangent = tangent ?? Vector3(1, 0, 0);

  /// This light's identity, stable for as long as it exists. Keys share one
  /// space with [OrblitObject.key]: one number, one thing in the scene.
  final int key;

  final OrblitLightKind kind;

  /// Linear RGB.
  final Vector3 colour;

  /// Lux for a directional light; lumens for a point or a spot.
  final double intensity;

  /// Where the light is. Meaningless for a directional light, which is
  /// everywhere at once.
  final Vector3 position;

  /// The direction light travels, not the direction of the source in the sky.
  final Vector3 direction;

  /// Metres past which the light is ignored. A directional light does not fall
  /// off, so it has no influence radius.
  final double falloffRadius;

  /// Radians. Full brightness within the inner angle, falling to nothing at
  /// the outer one.
  final double innerConeAngle;
  final double outerConeAngle;

  /// Half the sun's angular diameter, in degrees.
  ///
  /// Why an outdoor shadow is crisp at your feet and soft at its far end.
  /// Zero is the quickest way to make a scene look computer-generated.
  final double sunAngularRadius;

  /// The radius of the emitting source in metres, which decides how wide a
  /// penumbra it casts.
  final double sourceRadius;

  /// The glow around the disk a directional light draws in the sky, and how
  /// quickly it fades.
  ///
  /// Only a directional light has a body to draw. Wide and soft reads as a sun
  /// seen through air; tight and small reads as a moon on a clear night, which
  /// is most of what tells the two apart at a glance.
  final double haloSize;
  final double haloFalloff;

  final bool castShadows;

  /// The rectangle's size in metres, for [OrblitLightKind.area]. Width is
  /// measured along [tangent] and height along the direction crossed with it.
  ///
  /// Size is not brightness. [intensity] is the lumens the panel emits, so
  /// making it bigger spreads the same light over more of the scene and
  /// softens its shadow terminator rather than making the room brighter —
  /// which is what somebody moving a softbox expects, and the opposite of
  /// what scaling a point light does.
  final double width;
  final double height;

  /// The edge [width] is measured along, for [OrblitLightKind.area].
  ///
  /// A rectangle needs this and [direction] both: the face alone leaves the
  /// panel free to spin in its own plane, and a strip light spun ninety
  /// degrees is a different light. Squared up against [direction] on the way
  /// through, so it only has to be roughly right.
  final Vector3 tangent;

  /// Writes this light's floats into the scene's light block.
  ///
  /// A fixed stride rather than one array per field: the whole scene is one
  /// channel message, and sixteen floats per light costs less than eleven more
  /// typed arrays to allocate, encode and check.
  void _pack(Float32List into, int at) {
    into[at] = colour.x;
    into[at + 1] = colour.y;
    into[at + 2] = colour.z;
    into[at + 3] = intensity;
    into[at + 4] = position.x;
    into[at + 5] = position.y;
    into[at + 6] = position.z;
    into[at + 7] = direction.x;
    into[at + 8] = direction.y;
    into[at + 9] = direction.z;
    into[at + 10] = falloffRadius;
    into[at + 11] = innerConeAngle;
    into[at + 12] = outerConeAngle;
    into[at + 13] = sunAngularRadius;
    into[at + 14] = sourceRadius;
    into[at + 15] = haloSize;
    into[at + 16] = haloFalloff;
    into[at + 17] = width;
    into[at + 18] = height;
    into[at + 19] = tangent.x;
    into[at + 20] = tangent.y;
    into[at + 21] = tangent.z;
  }

  /// How many floats one light occupies.
  static const int stride = 22;
}

/// Where the viewer is, and how much light reaches it.
class OrblitCamera {
  const OrblitCamera({
    required this.position,
    required this.target,
    this.fieldOfView = 50,
    this.orthographic = false,
    this.viewHeight = 10,
    this.aperture = 16,
    this.shutterSpeed = 1 / 125,
    this.sensitivity = 100,
  });

  final Vector3 position;
  final Vector3 target;

  /// Whether parallel lines stay parallel.
  ///
  /// What a game seen flat on needs, and not the same as a very long lens:
  /// perspective at a narrow angle still converges, so a sprite at the edge of
  /// the frame is still seen slightly from the side — which is exactly what
  /// art drawn face on must not do.
  final bool orthographic;

  /// How much of the world fits in the frame from top to bottom, in metres.
  ///
  /// The flat lens's answer to a field of view, and a separate number because
  /// an angle means nothing without a distance and a flat lens has none.
  /// Ignored when the camera has perspective.
  final double viewHeight;

  /// Vertical field of view in degrees.
  final double fieldOfView;

  /// The three settings that decide how much light gets in: the f-number, the
  /// shutter speed in seconds, and the sensitivity in ISO.
  ///
  /// The defaults are sunny sixteen — what a camera is set to outdoors at
  /// midday. A scene lit by anything dimmer has to say so, because the range
  /// between a night and a noon is about seventeen stops and no single setting
  /// covers both.
  final double aperture;
  final double shutterSpeed;
  final double sensitivity;

  /// The same camera somewhere else, keeping how it is set.
  OrblitCamera copyWith({
    Vector3? position,
    Vector3? target,
    double? fieldOfView,
    bool? orthographic,
    double? viewHeight,
    double? aperture,
    double? shutterSpeed,
    double? sensitivity,
  }) => OrblitCamera(
    position: position ?? this.position,
    target: target ?? this.target,
    fieldOfView: fieldOfView ?? this.fieldOfView,
    orthographic: orthographic ?? this.orthographic,
    viewHeight: viewHeight ?? this.viewHeight,
    aperture: aperture ?? this.aperture,
    shutterSpeed: shutterSpeed ?? this.shutterSpeed,
    sensitivity: sensitivity ?? this.sensitivity,
  );
}

/// The sky, and the light it casts on everything.
///
/// One thing rather than two, because a backdrop that lights nothing reads as
/// a photograph behind the scene rather than the sky the scene stands under.
/// Without it, every shadow and every surface facing away from the sun renders
/// pure black.
/// How much work the sky is allowed to do.
///
/// A phone, a browser and a desktop are not the same machine, and the honest
/// way to span them is to say what the sky may cost rather than to draw a
/// different sky. Every tier draws the same thing; what changes is how finely
/// it is sampled, which shows as softer edges at a distance and nothing else.
///
/// Measured on an M4 Pro at 1656x1400, on a fair-weather sky filling most of
/// the frame — the worst case, since a scene with ground in it pays for the
/// ground's pixels instead.

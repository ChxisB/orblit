#pragma once

// What the renderer is made of, short of the renderer itself.
//
// One record per kind of thing a scene holds — the mesh it is drawn from, the
// light it is drawn by, the video playing on it, the crowd grown from it —
// then the numbers that cross the wire as a render pass, the shapes the
// renderer builds rather than loads, and what a graph target and pass are.
//
// These were at the top of OrblitRenderer.mm and are in that order still, so
// a change made to the old file can be found here. `class Renderer`, which is
// built from them, is in OrblitRendererCore.h, and every file that needs both
// gets them by including that one.

#include <array>
#include <cstdint>
#include <map>
#include <memory>
#include <mutex>
#include <set>
#include <string>
#include <unordered_map>
#include <vector>

#include <filament-iblprefilter/IBLPrefilterContext.h>
#include <filament/Box.h>
#include <filament/Camera.h>
#include <filament/ColorGrading.h>
#include <filament/Engine.h>
#include <filament/IndexBuffer.h>
#include <filament/IndirectLight.h>
#include <filament/InstanceBuffer.h>
#include <filament/Material.h>
#include <filament/MaterialInstance.h>
#include <filament/RenderTarget.h>
#include <filament/Renderer.h>
#include <filament/Scene.h>
#include <filament/Skybox.h>
#include <filament/SwapChain.h>
#include <filament/Texture.h>
#include <filament/TextureSampler.h>
#include <filament/VertexBuffer.h>
#include <filament/View.h>
#include <gltfio/AssetLoader.h>
#include <gltfio/FilamentAsset.h>
#include <gltfio/FilamentInstance.h>
#include <gltfio/MaterialProvider.h>
#include <gltfio/ResourceLoader.h>
#include <gltfio/TextureProvider.h>
#include <math/mat4.h>
#include <math/quat.h>
#include <math/vec2.h>
#include <math/vec3.h>
#include <math/vec4.h>
#include <utils/Entity.h>
#include <utils/NameComponentManager.h>

#include "OrblitBatching.h"
#include "OrblitMaterialPackages.h"
#include "OrblitOutline.h"
#include "OrblitPlatform.h"
#include "OrblitShadows.h"
#include "OrblitSplatSet.h"
#include "OrblitSprites.h"
#include "OrblitSurface.h"
#include "OrblitResources.h"
#include "OrblitTextures.h"
#include "orblit_renderer.h"
// Hook (screen effects): god rays and distortion live in plain C++, beside
// this file, so the port to the renderer's C++ class carries them unchanged.
#include "ScreenEffects.h"
// Motion blur hook: the velocity pass, the tiles and the gather live in plain
// C++ beside this file; the renderer holds one and tells it what moved.
#include "OrblitMotionBlur.h"

namespace orblit {

// Filament's names, which the old file had from `using namespace filament`.
// Named one at a time rather than the whole namespace, because a header that
// pulls a namespace in pulls it into every file that includes it — and
// because filament::Renderer is the one name that must stay out: inside
// orblit, Renderer is this class, and Filament's is always spelled in full.
using filament::Camera;
using filament::ColorGrading;
using filament::Engine;
using filament::IndexBuffer;
using filament::IndirectLight;
using filament::Material;
using filament::MaterialInstance;
using filament::Scene;
using filament::Skybox;
using filament::SwapChain;
using filament::Texture;
using filament::TextureSampler;
using filament::VertexBuffer;
using filament::View;
namespace gltfio = filament::gltfio;
using filament::math::float2;
using filament::math::float3;
using filament::math::float4;
using filament::math::mat4f;
using filament::math::quatf;

/// What a scene asked for that could not be given, and why, keyed by what it
/// is about. An NSDictionary of strings before; a map of them now, and the
/// Objective-C wrapper turns it back into the dictionary Swift reads.
using Notes = std::map<std::string, std::string>;

/// The start of a note's key that carries what a model file holds rather than
/// something wrong with the scene: the rest of the key is the file's path and
/// the note is JSON. Riding on the notes means every host that already hands
/// them back — five plugins and a browser — hands this back too, unchanged;
/// the Dart side takes these out before anybody reads the rest as problems.
constexpr const char *kModelInfoPrefix = "orblit.model:";


/// One loaded glTF file, and the copies made from that one parse.
///
/// Instances rather than one asset per object: a scene with fifty of the same
/// crate parses the file once and shares its geometry and materials.
///
/// `spare` is the pool. An object that stops using a mesh hands its instance
/// back rather than destroying it, so a crate deleted and undone — or a scene
/// closed and reopened — costs a pointer, not another parse.
struct Mesh {
  filament::gltfio::FilamentAsset *asset = nullptr;
  std::vector<filament::gltfio::FilamentInstance *> all;
  std::vector<filament::gltfio::FilamentInstance *> spare;
  /// The resource generation a load of this failed at. Tried again once
  /// bytes have been provided since — see orblit::resourceGeneration.
  uint64_t missingAt = 0;

  /// Whether objects made of it are drawn yet. False from the load until
  /// every texture it names is safe to sample — its own levels for the
  /// uncompressed formats, its placeholder for the ones whose storage reads
  /// as that anyway — so the frame that first draws it neither samples blank
  /// memory nor makes the GPU find all of it at once; see
  /// TextureQueue::unprimed. When the load began, for the line that says how
  /// long that took.
  bool shown = true;
  double loadedAt = 0;

  /// Every node's own transform as the file left it, in the order each copy
  /// lists its entities — which is the same order for every copy, because
  /// each is made by walking the same tree. What an object that stops
  /// animating, or goes back in the pool mid-stride, is put back to.
  std::vector<filament::math::mat4f> rest;

  /// Each entity's box as the file declared it, in the same order, and which
  /// of those entities are skinned. A skinned box is the bind pose's, and a
  /// character that walks out of it is culled while still on screen — so
  /// these are what each frame's box is worked out from. See fitSkinnedBoxes.
  std::vector<filament::Box> boxes;
  std::vector<uint32_t> skinned;

  /// What the file holds — clips, skins, variants, lights, cameras — as the
  /// JSON a host reads it back as. See describeModel.
  std::string info;
};

/// One animation clip playing on an object, as the host last described it.
struct Played {
  /// Which of the file's animations, or -1 for none.
  int32_t clip = -1;
  /// Where it was at the host's moment the pose describes, and how many of
  /// its seconds pass per second of the host's clock.
  float seconds = 0;
  float speed = 0;
  /// Whether it wraps past its end, or holds the last frame.
  bool loops = false;
};

/// What a file's own animation is doing to one object.
struct Posed {
  Played now;
  /// The clip being left, and how far the fade to `now` has gone: one is all
  /// `now`, nought is all `from`.
  Played from;
  float fade = 1;
  /// The host's seconds this was stated at, which each frame's time is
  /// measured from.
  double at = 0;
  /// Joints set by hand, as local transforms, after the clips — resolved to
  /// entities when the pose arrives, so a frame does not look them up.
  std::vector<std::pair<utils::Entity, filament::math::mat4f>> joints;
  /// Whether anything has moved this object's nodes away from the file's
  /// rest, so putting them back is only done when there is something to undo.
  bool moved = false;
};

/// The most copies Filament will draw from one renderable.
///
/// Its own limit, and a hard one: this is the size of the block of
/// per-renderable uniforms it indexes by the copy's own number. Asking for
/// more does not fail, it reads past the end of that block — which draws a
/// screen full of wedges rather than anything recognisable.
///
/// So a population is submitted in sixty-fours. A hundred thousand members is
/// sixteen hundred draws rather than a hundred thousand, and each of those
/// sixteen hundred is culled as one — which for a large world is worth having
/// on its own.
constexpr uint32_t kInstancesPerDraw = 64;

/// How wide the cell is that a member's distance is measured from, matching
/// kCellSide in instanced.mat. Whole cells leave together, so nothing that
/// straddles the boundary is cut in half.
constexpr float kCellSide = 16.0f;

/// How wide the book of transforms is.
///
/// A texture rather than a uniform array. Filament's own InstanceBuffer would
/// carry a transform each, but it fills the per-renderable uniform block and
/// so is capped at sixty-four instances — which is not a number that draws a
/// forest. Two dimensions rather than one so the width stays well inside what
/// every platform allows.
constexpr uint32_t kBookWidth = 2048;

/// Four texels an instance: three rows of an affine, and a colour.
constexpr uint32_t kTexelsPerMember = 4;

/// How far through its range a member starts sinking.
///
/// Three quarters, so the last quarter is the going. Too late and it is a pop
/// with extra steps; too early and half the field is short.
constexpr float kFadeFrom = 0.75f;

/// One population as the renderer holds it between frames.
///
/// The buffers are the expensive part and they are built once. What arrives
/// each frame is a revision number, and when it has not moved there is
/// nothing to do at all — which is the only reason a hundred thousand members
/// costs less than a hundred thousand of anything.
struct Grown {
  std::vector<utils::Entity> entities;
  std::vector<filament::MaterialInstance *> materials;

  /// Which member goes in which slot of the book.
  ///
  /// Sorted so that members near each other in the world are near each other
  /// in the book, and therefore in the same draw. Without it a draw's
  /// sixty-four members are sixty-four places scattered over the whole map,
  /// its bounding box is the whole map, and nothing can ever be culled — the
  /// camera looking at one corner still pays for every draw in the world.
  std::vector<uint32_t> order;

  /// Where each draw's own members actually are, and how far from the middle
  /// of it to the furthest of them.
  std::vector<filament::math::float3> middles;
  std::vector<float> radii;

  /// Which draws are currently in the scene at all.
  std::vector<bool> shown;

  /// One book for the whole population. Sixty-four members a draw would
  /// otherwise mean sixteen hundred textures for a hundred thousand.
  filament::Texture *book = nullptr;

  uint32_t count = 0;
  int32_t revision = INT32_MIN;
  int32_t flags = -1;

  /// How far a member is still drawn from, in metres. Zero is always.
  float range = 0;

  std::string path;
  uint64_t seen = 0;
};

/// One manually-instanced renderable inside a batch group: up to
/// kInstancesPerDraw members sharing a single InstanceBuffer of their own
/// transforms.
///
/// This is Filament's manual instancing — RenderableManager::Builder::
/// instances(count, InstanceBuffer*) — not its automatic kind, and the
/// difference is the whole reason batching exists as a separate feature from
/// setAutomaticInstancingEnabled. See BatchGroup below for why.
///
/// The chunk's own entity is never given a transform: it stays at Filament's
/// default identity, so "each local transform is relative to the transform
/// of the associated renderable" — the InstanceBuffer's own contract —
/// reduces to "each transform is the member's own, absolute, world
/// transform." `transforms` mirrors what the buffer holds, kept here because
/// InstanceBuffer has no getter and a chunk's bounding box has to be
/// rebuilt from somewhere when one member moves.
struct BatchChunk {
  utils::Entity entity;
  filament::InstanceBuffer *buffer = nullptr;
  std::array<filament::math::mat4f, kInstancesPerDraw> transforms{};

  /// How many of the kInstancesPerDraw slots are actually in use. Every
  /// chunk but a group's last is full; a group of one hundred is two chunks,
  /// sixty-four and thirty-six.
  uint32_t count = 0;
};

/// One group the census (OrblitBatching.h) found four or more of: a mesh, a
/// material and a set of shadow/layer flags shared by every member, drawn as
/// a handful of BatchChunks instead of one renderable per member.
///
/// Kept between publishes, keyed by the same BatchKey the census groups
/// objects by, so that a publish where nothing about a group's *membership*
/// changed costs a compare-and-write per member rather than a rebuild — see
/// reconcileBatchGroups. `slotOf` is what "nothing changed" is judged
/// against: which slot each object key held last time, regardless of what
/// order this publish names them in — a set comparison, not a positional
/// one, because the slots themselves are not in publish order (see below)
/// and a host reordering its own object list should not by itself cost a
/// rebuild. Any actual change of membership — one joined, one left, one
/// swapped for another — does rebuild the group's chunks from nothing.
///
/// A rebuild sorts members by where they are in the world before chunking
/// them, the same way OrblitPopulation sorts a population (sortPopulation,
/// mortonOf) — so `slotOf`'s slots are in that spatial order, not the
/// order objects arrived in. Skipping this made a chunk whatever objects
/// happened to be adjacent in the publish: castShadows crates in the
/// Batching example are every fifth object in a grid, so an unsorted chunk
/// of them spans nearly the whole grid, and a chunk's bounding box is the
/// union of its members' — see below — so a box that loose visibly moved
/// where the shadow pass fit its cascades. Sorted, a chunk is a compact
/// patch of the world and its box is close to what the same members would
/// have covered unbatched.
///
/// Everything that is per-renderable in Filament is therefore shared by the
/// whole group rather than decided per member: the material instance, the
/// shadow and layer flags (already guaranteed identical within a group by
/// the census's own key), and culling — every chunk is culled as one box,
/// the union of its members', so one visible member draws every other member
/// of its chunk too. See the doc comment on OrblitScene.batching (Dart side)
/// for why that is an acceptable trade rather than a silent one.
struct BatchGroup {
  std::vector<BatchChunk> chunks;

  /// Which absolute slot (chunk = slot / kInstancesPerDraw, offset = slot %
  /// kInstancesPerDraw) each member key currently holds.
  std::unordered_map<int64_t, uint32_t> slotOf;

  /// Borrowed — from _materialOrder for a named material, from the colour
  /// pool for the placeholder cube — and never destroyed by this group.
  filament::MaterialInstance *material = nullptr;
  uint64_t seen = 0;
};

/// A number that puts nearby places near each other.
///
/// The bits of three coordinates interleaved, so sorting by it walks the world
/// in a way that keeps neighbours together. Sorting by any single axis instead
/// gives draws that are thin slabs across the whole map, which cull almost as
/// badly as no sorting at all.
inline uint64_t mortonOf(uint32_t x, uint32_t y, uint32_t z) {
  auto spread = [](uint32_t v) -> uint64_t {
    uint64_t n = v & 0x1FFFFFull;
    n = (n | (n << 32)) & 0x1F00000000FFFFull;
    n = (n | (n << 16)) & 0x1F0000FF0000FFull;
    n = (n | (n << 8)) & 0x100F00F00F00F00Full;
    n = (n | (n << 4)) & 0x10C30C30C30C30C3ull;
    n = (n | (n << 2)) & 0x1249249249249249ull;
    return n;
  };
  return spread(x) | (spread(y) << 1) | (spread(z) << 2);
}

/// Where the camera was told to be, and when it was told.
///
/// Two of these are kept, because one is a position and two are a motion —
/// and a motion is what lets the picture ask where the camera is *now* rather
/// than where it was when the message arrived.
struct Aimed {
  filament::math::float3 position{0.0f, 0.0f, 0.0f};
  filament::math::float3 target{0.0f, 0.0f, -1.0f};
  float fieldOfView = 50.0f;

  /// Whether parallel lines stay parallel, and how much of the world fits in
  /// the frame from top to bottom when they do.
  bool orthographic = false;
  float viewHeight = 10.0f;

  /// The application's own seconds, which is the clock the camera was solved
  /// on and therefore the only one its speed can honestly be measured against.
  double at = 0.0;

  /// When this arrived here, on the clock the picture is drawn against.
  double arrived = 0.0;

  bool valid = false;
};

/// How far behind the latest word the camera is drawn, as a multiple of the
/// usual gap between words.
///
/// Slightly behind on purpose. The application's own motion is smooth — a
/// tenth of a percent of unevenness, measured — so the best thing that can be
/// done with it is to read it rather than to guess at it. Sampling a little
/// behind means the moment being drawn almost always falls *between* two
/// things the application has said, where the answer is exact, instead of
/// past the last one, where it is a prediction that has to be corrected when
/// the next arrives.
///
/// The cost is about a sixtieth of a second of delay, which is far below
/// noticing. What it buys is the difference between a camera that judders and
/// one that does not.
constexpr double kDrawBehind = 1.15;

/// How far past the last word it will still carry on when one is late, as a
/// multiple of that same gap. Beyond this it holds still rather than
/// inventing a position, because by then it has no idea.
constexpr double kCarryOn = 2.5;

/// How quickly a correction is absorbed, in seconds.
///
/// Predicting where the camera is between words means being a little wrong,
/// and being put right the moment the next word arrives. Snapping to it is a
/// small jump every message, which is most of what is left of a judder once
/// the prediction is doing its job. Carrying the difference and letting it
/// decay spreads each correction over a few frames — and because it is the
/// *difference* being decayed rather than the position, the camera still ends
/// up exactly where it was told, with no trailing behind.
constexpr double kAbsorb = 0.05;

/// One object as the renderer holds it between frames.
///
/// What is kept here is exactly what has to be compared to decide whether a
/// frame's worth of work can be skipped: the shape the object was built as,
/// and the last values written into it.
struct Drawn {
  /// The cube path: an entity this renderer built and owns.
  utils::Entity entity;
  filament::MaterialInstance *material = nullptr;

  /// A second entity over the same geometry that writes depth and no colour,
  /// on the channel below the one everything else draws on, so that it has
  /// filled the depth buffer before any shading happens. Null unless the
  /// depth prepass is on and this object is one it covers; see
  /// Renderer::syncPrepass for which objects those are.
  utils::Entity prepass;

  /// The mesh path: an instance borrowed from a loaded glTF file.
  filament::gltfio::FilamentInstance *instance = nullptr;

  /// Which file it draws, empty for the built-in cube. A change here is a
  /// change of what the object *is*, and the only thing that forces a rebuild.
  std::string path;

  filament::math::mat4f transform;

  /// Whether that transform has ever been written. An instance out of the pool
  /// still stands where its last owner left it, and identity — which is what
  /// this starts as — is a transform a new object might genuinely have. So the
  /// first write is unconditional rather than compared.
  bool placed = false;

  /// Impossible values, so the first publish always writes.
  filament::math::float3 colour = {-1, -1, -1};
  int32_t flags = -1;

  /// Which of this frame's materials the object is made of, or -1 for the
  /// default surface tinted by [colour]. Starts at an index no publish can
  /// name, so the first one always dresses.
  int32_t surface = -2;

  /// The layer bit written into this object's own instance for decals to
  /// test against. One, layer nought, is what a fresh instance is given.
  int32_t decalLayer = 1;

  /// A mesh's own materials, kept from the moment one is overridden so that
  /// clearing the override puts the model back the way the file had it.
  /// Empty while nothing has been overridden, which is the usual case.
  std::vector<filament::MaterialInstance *> ownMaterials;

  /// The loaded file the instance came from, for as long as there is one.
  /// Held here so a frame of animation does not look the path up.
  Mesh *mesh = nullptr;

  /// What the file's animation is doing to it. See Renderer::applyPoses.
  Posed pose;

  /// The shapes the host dialled in, kept only for an animated mesh: a clip
  /// that animates weights would otherwise overwrite them every frame.
  std::vector<float> morphWeights;

  /// Which of the file's material variants it wears, -1 for the file's own
  /// materials, and those own materials — kept the first time a variant is
  /// chosen, because a variant only names the primitives it changes and
  /// everything else has to go back to what it was.
  int32_t variant = -1;
  std::vector<filament::MaterialInstance *> fileMaterials;

  /// The publish that last mentioned this object. Anything not stamped by the
  /// current one has left the scene.
  uint64_t seen = 0;
};

/// How many floats one material's numbers occupy, and how many maps it has
/// room for. Both agree with the Dart side by hand; a mismatch is caught in
/// the plugin, which checks the array lengths before any of this is reached.
constexpr size_t kMaterialParams = 37;
constexpr size_t kMaterialMaps = 7;

/// One pose's row, as OrblitAnimation packs it: whole numbers — the clip,
/// the clip being left, the flags below and the material variant — and then
/// the clip's seconds and speed, the left clip's seconds and speed, and how
/// far the fade has gone.
constexpr size_t kPoseInts = 4;
constexpr size_t kPoseFloats = 5;
constexpr int32_t kPoseLoops = 1 << 0;
constexpr int32_t kPoseFromLoops = 1 << 1;

/// The maps a lit surface has, in the order the Dart side packs them.
///
/// At file scope because two places need them and a second copy is how the
/// blend maps came to be missing from one of them: a material that had never
/// been given them left two samplers unset, which Filament reports on every
/// draw. Hundreds of lines a second, for a surface that was drawing correctly.
/// One of a model's files: what the glTF calls it, where it is, and its bytes
/// once they have been read.
struct Wanted {
  const char *uri;
  std::string path;
  void *bytes;
  size_t size;
  /// Set instead of `bytes` when they were provided rather than read, and
  /// shared with the store rather than copied out of it.
  orblit::SharedBytes shared{};
};

/// Reads one file whole — or takes what was provided under its name — or
/// leaves it null.
///
/// Null is not an error here — it is the answer to "is this file there",
/// which is what the caller is asking. A model that names four hundred
/// textures and finds none of them still loads, and still draws, in black.
///
/// Read, not mapped. Mapping looks like the frugal choice — the pages are
/// backed by the file and the system can evict them — but every page then
/// arrives as a fault when the decoder touches it, and paging four hundred
/// files in sixteen kilobytes at a time measured 2226 ms against 542 ms for
/// reading them. This data is read once, immediately, in full: the access
/// pattern a plain read is for.
inline void readWholeFile(Wanted &one) {
  if (orblit::SharedBytes provided = orblit::findResource(one.path)) {
    // Empty counts as missing, as an empty file does.
    if (!provided->empty()) {
      one.size = provided->size();
      one.shared = std::move(provided);
    }
    return;
  }
  orblit::readWholeFile(one.path, &one.bytes, &one.size);
}

constexpr const char *kMapNames[kMaterialMaps] = {
    "baseColorMap", "normalMap",         "metallicRoughnessMap",
    "occlusionMap", "emissiveMap",       "blendBaseColorMap",
    "blendMaskMap"};
constexpr const char *kMapFlags[kMaterialMaps] = {
    "hasBaseColorMap", "hasNormalMap",      "hasMetallicRoughnessMap",
    "hasOcclusionMap", "hasEmissiveMap",    "hasBlendBaseColorMap",
    "hasBlendMaskMap"};

/// One material as the renderer holds it between frames.
///
/// The instance is the expensive part and the flags decide which compiled
/// material it has to come from, so a change of flags is a rebuild and a
/// change of numbers is a handful of uniform writes. Keeping both here is
/// what lets those be told apart without asking Filament anything.
struct Surfaced {
  filament::MaterialInstance *instance = nullptr;
  int32_t flags = -1;
  float params[kMaterialParams] = {};
  int32_t maps[kMaterialMaps] = {-1, -1, -1, -1, -1, -1, -1};
  bool written = false;
  uint64_t seen = 0;
};

/// How many floats one video contributes to the message.
constexpr size_t kVideoParams = 4;

/// One video as the renderer holds it between frames.
///
/// The frame never becomes an ordinary texture. It stays the buffer the
/// decoder wrote and is handed to the GPU where it lies, which is the whole
/// reason a screen in the scene costs about as much as a flat colour.
struct Movie {
  /// What decodes it, from the platform layer. Null while nothing is open,
  /// and always null where the platform has no decoder yet.
  std::unique_ptr<orblit::VideoDecoder> decoder;
  filament::Texture *texture = nullptr;

  std::string path;
  int32_t flags = -1;
  float rate = 1.0f;
  float volume = 1.0f;
  int32_t seekToken = -1;
  bool looping = false;
  uint64_t seen = 0;
};

/// How many floats one probe takes on the wire. Must match
/// `OrblitProbe.stride` on the Dart side and `probeStride` in the plugin.
constexpr uint32_t kProbeStride = 8;

/// One reflection probe as the renderer holds it between frames.
struct Probe {
  /// What the six faces were drawn into, and what the filter made of it. The
  /// captured cube is kept as well as the filtered one because a re-capture
  /// can reuse it rather than allocating a second time.
  filament::Texture *captured = nullptr;
  /// One render target per face, kept for as long as the cube is.
  ///
  /// Not built and thrown away around each render: Filament records a draw
  /// and performs it later, so a target destroyed on the line after the
  /// render is destroyed before the render happens, and the face comes back
  /// empty. The same trap as a buffer descriptor freed too early.
  filament::RenderTarget *faces[6] = {};
  /// One depth buffer, shared by all six faces: they are drawn one after
  /// another and none of them needs the last one's depth. A colour attachment
  /// on its own is a target with nothing to depth-test against, and what
  /// comes back is the clear colour and nothing else.
  filament::Texture *depth = nullptr;
  filament::Texture *filtered = nullptr;
  filament::IndirectLight *light = nullptr;

  filament::math::float3 position = {0, 0, 0};
  float radius = 0.0f;
  float intensity = 1.0f;
  uint32_t resolution = 0;
  /// The version last captured at. A different one on the wire is the host
  /// saying the room has changed.
  int32_t captured_at = -1;

  /// Set when the version moves, cleared when the photograph is taken.
  ///
  /// A capture cannot happen where it is asked for. The scene arrives a piece
  /// at a time — objects, then lights, then the sky — so a probe captured the
  /// moment it is mentioned photographs a room with no sky in it and comes
  /// back black. It waits for the start of the next frame, by which point the
  /// scene is whole.
  bool wants_capture = false;
  uint8_t capture_layers = 0xFF;
  uint64_t seen = 0;
};
/// How many floats a world-space irradiance field takes on the wire. Must
/// match `OrblitField.stride` on the Dart side and `fieldStride` in the plugin.
constexpr uint32_t kFieldStride = 14;

/// A probe's tile in the atlas, and how many of those fit across it.
///
/// Eight texels: six of directions with a one-texel gutter each side. The
/// gutter carries a mirrored copy of the interior edge so that a bilinear
/// read across the seam of the octahedron lands on the direction actually
/// next to it rather than on the neighbouring probe's tile.
constexpr uint32_t kFieldTile = 8;
constexpr uint32_t kFieldTilesPerRow = 16;

/// How much of the light going round the feedback loop is passed on.
///
/// A field reads the picture the scene drew, and that picture already holds
/// what the field put into it, so the light goes round: field lights room,
/// room is photographed, photograph lights field. Each lap multiplies by the
/// surfaces' albedo and by the strength the host asked for, and an infinite
/// series of that converges only while the product stays below one.
constexpr float kFieldDamping = 0.6f;

/// The largest product of damping and strength that stays convergent.
///
/// Measured in a room with a red wall and a blue one, over six hundred
/// frames: the light that arrives matches what was asked for to within three
/// per cent up to a strength of four, is ten per cent over at five, and
/// **fifty-seven** per cent over at six — and it does not fail by getting
/// brighter, it fails by drifting in hue, because the channel with the
/// highest gain wins the race. One point eight is the last fully linear
/// point with a whole step of margin under the knee.
constexpr float kFieldSafeGain = 1.8f;

/// The most probes a field may hold. A thousand is a large room at two-metre
/// spacing, and the atlas for it is 128 by 512.
constexpr uint32_t kFieldMaxProbes = 1024;

/// How many floats one light takes on the wire.
///
/// Must match `OrblitLight.stride` on the Dart side and `lightStride` in the
/// plugin. Used for both the offset into the message and the size of the copy
/// kept per light, so those two cannot drift apart again — they already did
/// once: the halo fields took a light from sixteen floats to eighteen, the
/// kept copy followed and the offset did not, and every light after the first
/// read a mixture of the one before it and itself.
constexpr uint32_t kLightStride = 22;

/// How many rectangular area lights one view shades.
///
/// They cost differently from Filament's own lights: a rectangle is a polygon
/// integral inside the surface shader, paid by every lit fragment, and there
/// is no culling in front of it. Sixteen is a room with a wall of windows,
/// and the number at which the loop is still cheaper than the alternative.
constexpr uint32_t kAreaLightBudget = 16;

/// How many texels one rectangle occupies: centre, radiance, the two edges
/// with their lengths, whether it casts, and the matrix that says what it
/// could see when it looked.
///
/// The last five are only read for a rectangle that casts, which is why they
/// sit after the four every rectangle needs rather than among them.
constexpr uint32_t kAreaLightTexels = 9;

/// Where the slim surface's decal rows start inside lightData: sixty four
/// rows of two fitted tables, then the sixteen the rectangles occupy.
///
/// Below Filament's third feature level a material has nine samplers to
/// spend, not ten, so decalData has no sampler of its own there — its rows
/// are appended to lightData instead, which is read by texelFetch already
/// and does not mind a third tenant. The standard surface never uses this:
/// it keeps decalData as its own texture, where there is a sampler to spare.
constexpr uint32_t kSlimDecalRow = 64 + kAreaLightBudget;

/// How wide the one shadow map is, in pixels.
///
/// One map, not an atlas, and one casting rectangle rather than sixteen. A
/// scene has one key light and the rest are fill; giving every rectangle a
/// map would cost sixteen scene renders a frame to shadow lights whose whole
/// job is to not be noticed. The second one asked is reported rather than
/// silently ignored.
constexpr uint32_t kAreaShadowSide = 1024;

/// How big every decal's picture is once it is in the array, and how many
/// different pictures one view can hold.
///
/// One size for all of them because a texture array's layers share one: a
/// picture is resampled to this square on the way in. Five hundred and
/// twelve is a poster read from across a room; sixteen of them with their
/// mips is twenty-two megabytes, reserved only once a scene names a picture.
constexpr uint32_t kDecalPictureSide = 512;
constexpr uint32_t kDecalPictureLayers = 16;
constexpr uint32_t kDecalPictureLevels = 10;

/// How many compiled surfaces there are: three shading models in five blend
/// modes, and the shadow catcher on the end.
constexpr int kShadowCatcherSurface = 15;
constexpr int kSurfaceCount = 16;

/// An environment filtered at run time out of an .hdr or .exr picture, ready
/// to light a scene: see OrblitEnvironment.cpp.
///
/// Kept after the scene stops naming it, a few at a time, under a hash of
/// the picture's bytes and the sizes it was filtered at — so naming the same
/// picture again, or the same picture under another name, filters nothing.
struct EnvironmentLighting {
  uint64_t hash = 0;
  /// The sizes and route the key is made of; see EnvironmentPlan.
  uint32_t reflectionSize = 0;
  uint32_t largestSkybox = 0;
  bool onCpu = false;

  /// The blurred chain rough surfaces sample, or null when only the backdrop
  /// was asked for.
  filament::Texture *reflections = nullptr;
  /// The backdrop, sharper than the reflections, or null.
  filament::Texture *sky = nullptr;
  /// Three bands, for a matte surface, as cmgen computes them.
  float3 harmonics[9]{};
  /// When a scene last used it, in publishes, for choosing what to let go.
  uint64_t lastUsed = 0;
};

/// A picture being turned into an EnvironmentLighting: decoded and
/// summarised on a worker, then uploaded and filtered here over a couple of
/// frames. Defined in OrblitEnvironment.cpp.
struct EnvironmentWork;

/// How many pictures every renderer in this process has filtered into an
/// environment, on either route. A picture found in a renderer's cache is not
/// counted, which is how a check tells a cache hit from a second filter.
uint64_t environmentPicturesFiltered();

/// One light as the renderer holds it between frames.
///
/// The whole parameter block is kept rather than the fields that matter,
/// because comparing sixty-four bytes is cheaper than a dozen setter calls
/// that each dirty something downstream.
struct Lit {
  utils::Entity entity;
  int32_t kind = -1;
  int32_t flags = -1;
  float params[kLightStride] = {};
  bool applied = false;
  uint64_t seen = 0;
};

/// The ambient the scene starts with: the skybox's own colour, so an
/// unconfigured scene is lit by the sky it appears to be standing under.
constexpr float3 kDefaultAmbient = {0.10f, 0.12f, 0.16f};
constexpr float kDefaultAmbientIntensity = 28000.0f;


/// Two surfaces, alternated. Filament finishes writing one while Flutter's
/// raster thread samples the other, so a frame is never read while it is being
/// drawn. Each needs its own swap chain because a Filament swap chain is bound
/// to one CVPixelBuffer for its lifetime.
constexpr int kOrblitBufferCount = 2;

/// What an object's flag bits mean. Matches OrblitObject on the Dart side.
constexpr int32_t kCastsShadows = 1;
constexpr int32_t kReceivesShadows = 2;
constexpr int32_t kVisible = 4;

/// Hiding is a layer the view does not draw rather than a removal from the
/// scene: the object keeps its entity, its material and its instance, so
/// showing it again is one byte written instead of a rebuild.
///
/// The low seven bits are the author's own layers, one bit each, and the top
/// one is hidden. That split is why an object that has never heard of layers
/// still lands on bit nought and is still drawn by a pass that asks for
/// everything: what used to be "the visible layer" is now "layer nought", and
/// the two are the same byte.
constexpr uint8_t kVisibleLayer = 0x01;
constexpr uint8_t kHiddenLayer = 0x80;
constexpr uint8_t kAllLayers = 0x7F;

/// Which render channel the depth prepass draws on.
///
/// A channel is the top three bits of Filament's sort key, so everything in
/// a lower channel is drawn before anything in a higher one, whatever else
/// the key says. Everything this renderer builds sits on Filament's default
/// channel of 2 and is left there; the prepass goes on 1, immediately below,
/// so it has filled the depth buffer before the first shaded pixel and
/// nothing else had to move to make room. Not 0, which is no better placed
/// and is one of the two channels Filament forbids to screen-space
/// refraction — leaving it free costs nothing and keeps that door open.
constexpr uint8_t kPrepassChannel = 1;

/// How far behind the real surface the prepass writes its depth.
///
/// Not a fudge factor: a prepass entity is real geometry in the scene, so it
/// is drawn into the structure buffer that contact shadows and ambient
/// occlusion march along as well as into the colour pass's depth. Sitting
/// exactly on the surface it stands in for, it occludes that surface in those
/// passes; sitting behind it, it can never be the nearest thing at a pixel and
/// so occludes nothing anywhere — while still standing far in front of
/// anything genuinely hidden, which is all the colour pass's depth test needs.
/// The slope term goes with the constant one, as it does in applyRasterState,
/// or a surface seen nearly edge-on needs an offset so large that it separates
/// visibly when seen face-on.
constexpr float kPrepassDepthBias = 1.0f;
constexpr int32_t kLayerShift = 8;
constexpr int32_t kLayerMask = 0x07;

/// The layer bit an object's flags ask for.
inline uint8_t layerBitOf(int32_t flags) {
  return static_cast<uint8_t>(1u << ((flags >> kLayerShift) & kLayerMask));
}

/// How many floats a pass and a target take on the wire. Must match
/// OrblitRenderGraph on the Dart side.
constexpr uint32_t kPassStride = 13;
constexpr uint32_t kTargetStride = 6;

/// How many passes a frame may have. Matches OrblitRenderGraph.maxPasses; a
/// graph past it has run away rather than grown.
constexpr uint32_t kMaxPasses = 32;

/// What a pass is for. Matches OrblitPassKind.
constexpr int kPassScene = 0;
constexpr int kPassReflection = 1;
/// A material run over every pixel of what another pass drew. The rails every
/// screen-space effect rides on: read a target, write a target, draw one
/// triangle over the lot.
constexpr int kPassEffect = 2;

/// Which effect, matching OrblitEffect. Minus one is none.
constexpr int kEffectSharpen = 0;
constexpr int kEffectSmaaEdges = 1;
constexpr int kEffectSmaaWeights = 2;
constexpr int kEffectSmaaBlend = 3;
constexpr int kEffectBounce = 4;
constexpr int kEffectCopy = 5;
// Motion blur hook. Appended rather than inserted, so every effect before it
// keeps its number: the effect crosses as its index, and an index that drifts
// runs a different shader rather than failing. motion_blur_test checks it.
constexpr int kEffectMotionBlur = 8;  // god rays are 6 and distortion 7, in ScreenEffects.h

/// Where a material's texture says it comes from a pass rather than a file.
static const char *const kTargetScheme = "orblit:target/";

/// How many punctual lights Filament shades in one view before it starts
/// dropping the ones furthest from the camera. Worth saying out loud: a light
/// that quietly stops working is a long afternoon.
constexpr uint32_t kPunctualLightBudget = 256;

/// The key the placeholder cube is filed under while no host owns the scene.
/// Far out of the way of anything a host would count from.
constexpr int64_t kPlaceholderKey = INT64_MIN;

struct Vertex {
  float3 position;
  quatf tangents;

  /// Where the corner sits on its face. The standard surface asks for texture
  /// coordinates whether the material has a map or not — a shader either
  /// declares an attribute or it does not — so even the placeholder cube
  /// carries them, and a texture put on one lands square on each face.
  float2 uv;
};

/// A corner of one sheet of mist: where it is, and where it sits across the
/// sheet so the edges can be faded out.
struct MistVertex {
  float3 position;
  float2 uv;
};

/// The sheet, lying flat and two metres across, which the transform then makes
/// as wide as the weather needs to be.
constexpr MistVertex kMistCorners[4] = {
    {{-1, 0, -1}, {0, 0}},
    {{1, 0, -1}, {1, 0}},
    {{1, 0, 1}, {1, 1}},
    {{-1, 0, 1}, {0, 1}},
};

constexpr uint16_t kMistIndices[6] = {0, 1, 2, 2, 3, 0};

/// How many sheets a bank is drawn with.
///
/// Ten is enough that a bank reads as depth from a shallow angle and few
/// enough that the screen is only covered ten times over. Every one of them is
/// a full-screen pass of four-octave noise, which is the whole cost of this.
constexpr int kMistSheets = 10;

/// How finely the sky dome is divided, and how big it is.
///
/// The dome is only somewhere to put pixels: the cloud is worked out per
/// pixel from where that pixel's view ray crosses a flat layer, so the mesh
/// needs enough triangles to interpolate a direction smoothly and no more.
constexpr int kSkyRings = 10;
constexpr int kSkySegments = 32;
constexpr float kSkyRadius = 900.0f;

/// How many panes a curtain of rain is drawn with, and how far in front of
/// the camera each one hangs.
///
/// Three, at three depths, because one pane is a flat pattern and the eye
/// reads depth from things moving past each other at different rates.
constexpr int kRainCurtains = 3;
constexpr float kRainDistances[kRainCurtains] = {2.5f, 7.0f, 18.0f};

/// How far a bank reaches, in metres. Centred on the camera, so it is always
/// around whoever is looking rather than somewhere in the world they might
/// walk out of.
constexpr float kMistReach = 260.0f;

// A unit cube with four vertices per face, so every face keeps a flat normal
// and the lighting reads as six distinct planes rather than a smooth blob.
constexpr float3 kPositions[24] = {
    {-1, -1, 1},  {1, -1, 1},   {1, 1, 1},    {-1, 1, 1},    // +Z
    {1, -1, -1},  {-1, -1, -1}, {-1, 1, -1},  {1, 1, -1},    // -Z
    {1, -1, 1},   {1, -1, -1},  {1, 1, -1},   {1, 1, 1},     // +X
    {-1, -1, -1}, {-1, -1, 1},  {-1, 1, 1},   {-1, 1, -1},   // -X
    {-1, 1, 1},   {1, 1, 1},    {1, 1, -1},   {-1, 1, -1},   // +Y
    {-1, -1, -1}, {1, -1, -1},  {1, -1, 1},   {-1, -1, 1},   // -Y
};

constexpr float3 kNormals[24] = {
    {0, 0, 1},  {0, 0, 1},  {0, 0, 1},  {0, 0, 1},
    {0, 0, -1}, {0, 0, -1}, {0, 0, -1}, {0, 0, -1},
    {1, 0, 0},  {1, 0, 0},  {1, 0, 0},  {1, 0, 0},
    {-1, 0, 0}, {-1, 0, 0}, {-1, 0, 0}, {-1, 0, 0},
    {0, 1, 0},  {0, 1, 0},  {0, 1, 0},  {0, 1, 0},
    {0, -1, 0}, {0, -1, 0}, {0, -1, 0}, {0, -1, 0},
};

constexpr uint16_t kIndices[36] = {
    0,  1,  2,  2,  3,  0,  4,  5,  6,  6,  7,  4,
    8,  9,  10, 10, 11, 8,  12, 13, 14, 14, 15, 12,
    16, 17, 18, 18, 19, 16, 20, 21, 22, 22, 23, 20,
};

/// One image a pass draws into, and everything needed to keep it.
///
/// Rebuilt when its size changes and not otherwise: a render target is a
/// texture, a depth buffer and a piece of driver state, and reallocating all
/// three on a frame where nothing moved is the sort of cost that only shows
/// up as a stutter while somebody drags a window.
struct GraphTarget {
  std::string name;
  uint32_t width = 0;   // zero follows the view
  uint32_t height = 0;
  float scale = 1.0f;
  bool keepsDepth = true;
  bool keepsColour = true;

  filament::Texture *colour = nullptr;
  filament::Texture *depth = nullptr;
  filament::RenderTarget *target = nullptr;
  uint32_t builtWidth = 0;
  uint32_t builtHeight = 0;
};

/// One material sampler that reads what a pass drew.
///
/// Kept so the renderer can put it right by itself. A target that follows the
/// view is a new texture every time the window changes, and a host is under
/// no obligation to publish again afterwards — a static scene never does. The
/// binding has to be renewed by whoever rebuilt the texture.
struct TargetBinding {
  filament::MaterialInstance *instance = nullptr;
  std::string parameter;
  std::string target;
};

/// A target texture that is no longer wanted but may still be bound.
struct RetiredTexture {
  filament::Texture *texture = nullptr;
  uint64_t afterGeneration = 0;
};

/// One step of a frame, as the renderer holds it.
struct GraphPass {
  int kind = kPassScene;
  int into = -1;  // an index into the targets, or -1 for the frame
  uint8_t layers = kAllLayers;
  bool clears = true;
  float plane[4] = {0.0f, 1.0f, 0.0f, 0.0f};

  /// Which screen-space effect, for an effect pass. -1 for every other kind.
  int effect = -1;

  /// The targets this pass samples, as indices, or -1. An effect reads the
  /// first of them that was actually built.
  int reads[4] = {-1, -1, -1, -1};

  /// The one triangle an effect pass draws, and what it is dressed in. Built
  /// on first use and kept, because a pass runs every frame.
  ///
  /// Every piece of it belongs to the pass and is given back with the pass, in
  /// releaseGraph. The compiled Material behind the instance is the exception:
  /// that one is shared between passes and outlives any graph.
  filament::Scene *effectScene = nullptr;
  utils::Entity effectEntity;
  filament::MaterialInstance *effectMaterial = nullptr;
  filament::VertexBuffer *effectVertices = nullptr;
  filament::IndexBuffer *effectIndices = nullptr;

  /// The view this pass renders through, for a pass that draws into a target.
  /// The frame pass uses the renderer's own view.
  filament::View *view = nullptr;
  filament::Camera *camera = nullptr;
  utils::Entity cameraEntity;

  /// What it cost last frame, in milliseconds, and how much it drew.
  double milliseconds = 0;
  int drawn = 0;
};

/// The matrix that mirrors the world in a plane.
///
/// `n` is the plane's normal and `d` its distance from the origin, so that a
/// point on it satisfies dot(n, p) + d = 0. Reflecting about it is what turns
/// a camera into the camera behind the glass, which is the whole of how a
/// planar reflection is drawn: the same scene, the same lights, one matrix
/// different.
inline filament::math::mat4f reflectionAbout(const float plane[4]) {
  using filament::math::float3;
  using filament::math::float4;
  using filament::math::mat4f;

  float3 n{plane[0], plane[1], plane[2]};
  const float length = std::sqrt(n.x * n.x + n.y * n.y + n.z * n.z);
  // A plane with no normal is not a plane. Standing the camera still is a
  // reflection of nothing, which is visibly wrong and does not divide by zero.
  if (length < 1e-6f) return mat4f();
  n = n / length;
  const float d = plane[3] / length;

  mat4f mirror;
  mirror[0] = float4{1 - 2 * n.x * n.x, -2 * n.x * n.y, -2 * n.x * n.z, 0};
  mirror[1] = float4{-2 * n.x * n.y, 1 - 2 * n.y * n.y, -2 * n.y * n.z, 0};
  mirror[2] = float4{-2 * n.x * n.z, -2 * n.y * n.z, 1 - 2 * n.z * n.z, 0};
  mirror[3] = float4{-2 * n.x * d, -2 * n.y * d, -2 * n.z * d, 1};
  return mirror;
}

/// How many post-processing numbers the renderer will read.
///
/// Larger than the description needs, so a host built against a newer version
/// sending more of them is ignored from here on rather than reading past the
/// end of the array.
constexpr size_t kMaxPostParams = 128;

/// What one pass of the last frame cost, in milliseconds, and how many
/// renderables it submitted.
struct PassTiming {
  double milliseconds = 0;
  int drawn = 0;
};

}  // namespace orblit

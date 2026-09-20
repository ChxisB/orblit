#include "OrblitRendererInternal.h"

// Drawing many identical objects as one, and drawing the opaque ones into
// depth before they are shaded.
//
// One of the files orblit::Renderer is defined across; see PORTING.md.

using namespace filament;
using namespace filament::math;

namespace orblit {

/// Grows a running world-space min/max to include the exact box the
/// placeholder cube — [-1, 1] on every local axis — covers once `transform`
/// is applied.
///
/// The exact bound for an affine-transformed box, not an approximation: on
/// each world axis, the half-extent is the sum of the absolute values of
/// that axis's row across the rotation and scale, because whichever of the
/// eight local corners has the matching sign on every term is the farthest
/// one out along that axis. Cheap enough to do for every member of a chunk
/// whenever one of them moves, and worth doing exactly — a chunk is already
/// culled coarser than an unbatched object, one box for up to sixty-four
/// members rather than one each (see BatchGroup's own comment), and a loose
/// bound on top of that would be a second, needless way for batched and
/// unbatched to cull differently.
static inline void expandBoxByTransform(const mat4f &m, float3 &least,
                                        float3 &most) {
  const float3 centre{m[3].x, m[3].y, m[3].z};
  const float3 half{std::fabs(m[0].x) + std::fabs(m[1].x) + std::fabs(m[2].x),
                    std::fabs(m[0].y) + std::fabs(m[1].y) + std::fabs(m[2].y),
                    std::fabs(m[0].z) + std::fabs(m[1].z) + std::fabs(m[2].z)};
  least = min(least, centre - half);
  most = max(most, centre + half);
}

/// The world box Filament works out for one member on its own, from the same
/// object-space box an unbatched crate declares.
///
/// This is here to hold the two paths against each other. expandBoxByTransform
/// above takes the member's box to be the unit cube centred on the origin: it
/// reads the centre straight off the translation column. That is only right
/// while the declared box says the same thing, and for a long time it did not
/// — the placeholder cube declared `{{-1,-1,-1},{1,1,1}}`, which in Filament's
/// {centre, half-extent} Box is a cube centred on (-1,-1,-1) reaching the
/// origin, about two thirds of a metre from where a chunk put it for a crate
/// at 0.4 scale. With the declaration corrected to `{{0,0,0},{1,1,1}}` (see
/// Renderer::build) this computes what expandBoxByTransform computes, bit for
/// bit: c is zero, so the centre is the translation column, and h is one, so
/// the half-extent is the same sum of absolute values in the same order.
/// Diagnostic: ORBLIT_BATCH_BOX=object builds a chunk's box this way instead,
/// so a frame that moves when it is set means the two have drifted apart
/// again.
static inline void expandBoxByObjectBox(const mat4f &m, const float3 &c,
                                        const float3 &h, float3 &least,
                                        float3 &most) {
  const float3 centre{m[0].x * c.x + m[1].x * c.y + m[2].x * c.z + m[3].x,
                      m[0].y * c.x + m[1].y * c.y + m[2].y * c.z + m[3].y,
                      m[0].z * c.x + m[1].z * c.y + m[2].z * c.z + m[3].z};
  const float3 half{
      std::fabs(m[0].x) * h.x + std::fabs(m[1].x) * h.y + std::fabs(m[2].x) * h.z,
      std::fabs(m[0].y) * h.x + std::fabs(m[1].y) * h.y + std::fabs(m[2].y) * h.z,
      std::fabs(m[0].z) * h.x + std::fabs(m[1].z) * h.y + std::fabs(m[2].z) * h.z};
  least = min(least, centre - half);
  most = max(most, centre + half);
}

/// Orders `indices` so that members near each other in the world are near
/// each other in the list, and therefore end up sharing a chunk — the same
/// technique sortPopulation uses, for the same reason (see mortonOf).
///
/// Skipping this made a chunk whatever objects happened to be adjacent in
/// the publish, which is not "nearby": the Batching example's shadow casters
/// are every fifth object in a grid, so sixty-four consecutive *casters*
/// span nearly the whole grid rather than a corner of it. A chunk's bounding
/// box is the union of its members', so a box that loose is not just an
/// overdraw risk — it visibly moved where the shadow pass fit its cascades,
/// which is what turned up as a scene-wide, if subtle, pixel difference
/// between batched and unbatched before this was added.
void Renderer::sortBatchIndices(std::vector<uint32_t> &indices,
                                const float *transforms) {
  if (indices.size() < 2) return;

  float3 least{std::numeric_limits<float>::max()};
  float3 most{std::numeric_limits<float>::lowest()};
  for (uint32_t index : indices) {
    const float *m = transforms + size_t(index) * 16;
    least = min(least, float3{m[12], m[13], m[14]});
    most = max(most, float3{m[12], m[13], m[14]});
  }
  const float3 span = max(most - least, float3{1e-4f});

  std::vector<std::pair<uint64_t, uint32_t>> keyed(indices.size());
  for (size_t i = 0; i < indices.size(); i++) {
    const float *m = transforms + size_t(indices[i]) * 16;
    const float3 at = (float3{m[12], m[13], m[14]} - least) / span;
    keyed[i] = {mortonOf(uint32_t(std::clamp(at.x, 0.0f, 1.0f) * 1023.0f),
                        uint32_t(std::clamp(at.y, 0.0f, 1.0f) * 1023.0f),
                        uint32_t(std::clamp(at.z, 0.0f, 1.0f) * 1023.0f)),
                indices[i]};
  }
  std::sort(keyed.begin(), keyed.end());
  for (size_t i = 0; i < indices.size(); i++) indices[i] = keyed[i].second;
}

/// Makes every batch group's Filament state agree with what this publish's
/// census found, and writes this publish's batching stats.
///
/// `groups` is this publish's membership, one list of object indices per key
/// that reached the threshold, in the order the objects arrived — an order
/// this function does not rely on; see sortBatchIndices and BatchGroup's own
/// comment. Reconciling a group against it is one of three things, cheapest
/// first:
///
///  * Nothing to do with its chunks at all, if the exact same set of object
///    keys is what it was already built from — checked against the group's
///    own slotOf, which is what "nothing changed" is judged against. The
///    colour pool still has to be asked for the group's material, though,
///    whether or not anything else moved: an instance not claimed this
///    publish is swept below as if nobody wanted it.
///  * A write into chunks that already exist, if the membership matches but
///    a transform or the material has: updateBatchGroup.
///  * A rebuild, if the membership does not match: one joined, one left, or
///    one swapped for another. rebuildBatchGroup sorts, sizes new chunks to
///    fit and writes every slot once.
///
/// A group nobody asks for this time — every member left the scene, moved
/// out of eligibility, or changed key — is torn down once every live group
/// has had its turn, the same order the colour pool is swept in and for the
/// same reason: nothing still standing on a chunk should have it destroyed
/// from under it.
void Renderer::reconcileBatchGroups(
    const std::unordered_map<orblit::BatchKey, std::vector<uint32_t>,
                              orblit::BatchKeyHash> &groups,
    const int64_t *keys, const float *transforms, const float *colours,
    uint64_t generation) {
  if (groups.empty() && _groups.empty()) {
    _batchedObjects = 0;
    _batchGroups = 0;
    return;
  }

  uint32_t batchedObjects = 0;
  uint32_t chunkCount = 0;

  for (const auto &entry : groups) {
    const orblit::BatchKey &key = entry.first;
    const std::vector<uint32_t> &indices = entry.second;
    BatchGroup &group = _groups[key];
    group.seen = generation;
    batchedObjects += uint32_t(indices.size());

    // The material every chunk in this group wears, asked for on every
    // publish the group is alive whether or not anything else about it
    // moved: a colour pool instance is swept the moment a publish does not
    // ask for it (see the end of applyObjects), and a group that only wrote
    // a transform this time still needs its colour kept.
    const bool named = key.surface >= 0 &&
                       key.surface < static_cast<int32_t>(_materialOrder.size());
    MaterialInstance *material =
        named ? _materialOrder[key.surface]
              : _colourPool.take(colours + indices[0] * 3, generation,
                                 [this](const float *colour) {
        MaterialInstance *made = surfaceAt(0)->createInstance();
        setDefaultsOn(made);
        made->setParameter("baseColor",
                           float4{colour[0], colour[1], colour[2], 1.0f});
        return made;
      });

    // Same set of keys as last time, regardless of what order this publish
    // named them in — a host reordering its own object list is not a
    // membership change.
    bool sameMembers = !group.chunks.empty() && indices.size() == group.slotOf.size();
    for (size_t i = 0; sameMembers && i < indices.size(); i++) {
      if (group.slotOf.find(keys[indices[i]]) == group.slotOf.end()) {
        sameMembers = false;
      }
    }

    if (sameMembers) {
      updateBatchGroup(group, indices, keys, transforms, material);
    } else {
      rebuildBatchGroup(group, key, indices, keys, transforms, material);
    }
    chunkCount += uint32_t(group.chunks.size());
  }

  for (auto it = _groups.begin(); it != _groups.end();) {
    if (it->second.seen == generation) {
      ++it;
      continue;
    }
    destroyBatchGroup(it->second);
    it = _groups.erase(it);
  }

  _batchedObjects = batchedObjects;
  _batchGroups = chunkCount;
}

/// Rebuilds one group's chunks from nothing: `indices` sorted into spatial
/// order (sortBatchIndices), new InstanceBuffers sized to fit, and every
/// slot written once. Called whenever the group did not already hold this
/// publish's exact set of members — a new group, or an old one that gained,
/// lost, or swapped one member for another.
void Renderer::rebuildBatchGroup(BatchGroup &group, const orblit::BatchKey &key,
                                 std::vector<uint32_t> indices,
                                 const int64_t *keys, const float *transforms,
                                 MaterialInstance *material) {
  destroyBatchGroup(group);
  group.material = material;
  sortBatchIndices(indices, transforms);

  // Matches applyFlags exactly — every setting a chunk's members would have
  // been given individually, decided once here because the census already
  // guarantees every member of a group carries the same flags. Missing any
  // of these is not a drawing error, only a quieter one: the object is still
  // there and still shaded, just not quite the way it would have been drawn
  // alone. screenSpaceContactShadows in particular is off by default in
  // Filament and on by default in applyFlags, so a chunk that skipped it
  // would still look right at a glance and only show up as a faint,
  // widespread shift in the fine contact shadows between close objects —
  // which is exactly what turned up, and why this list is deliberately
  // exhaustive rather than "whatever seemed to matter".
  const bool castShadows = (key.flags & kCastsShadows) != 0;
  const bool receiveShadows = (key.flags & kReceivesShadows) != 0;
  const uint8_t layer = layerBitOf(key.flags);

  for (size_t at = 0; at < indices.size(); at += _chunkSize) {
    const uint32_t members =
        uint32_t(std::min(size_t(_chunkSize), indices.size() - at));

    BatchChunk chunk;
    chunk.count = members;

    float3 least{std::numeric_limits<float>::max()};
    float3 most{std::numeric_limits<float>::lowest()};
    for (uint32_t slot = 0; slot < members; slot++) {
      const uint32_t index = indices[at + slot];
      group.slotOf[keys[index]] = uint32_t(at) + slot;
      std::memcpy(&chunk.transforms[slot], transforms + size_t(index) * 16,
                 sizeof(mat4f));
      if (_objectChunkBox) {
        expandBoxByObjectBox(chunk.transforms[slot], float3{0, 0, 0},
                             float3{1, 1, 1}, least, most);
      } else {
        expandBoxByTransform(chunk.transforms[slot], least, most);
      }
    }

    // Diagnostic (ORBLIT_BATCH_ROOT=transform, one-member chunks only): put the
    // member's placement on the chunk renderable's own transform and leave the
    // instance buffer at identity — which is exactly where an unbatched
    // object's placement lives. In principle it draws the same crate in the
    // same place; in arithmetic it takes a different route to the model
    // matrix. A renderable's transform is narrowed to float once, from the
    // double product of the world origin and the object's own transform
    // (FScene::prepare). An instance's model matrix is that already-narrowed
    // root multiplied again, in float, by the instance's float transform
    // (FInstanceBuffer::prepare). The two agree by algebra and can disagree in
    // the last place — and a shadow map is a threshold test, so a last-place
    // disagreement in a caster's depth is a flipped comparison at the edges.
    const bool rootPlacement = _rootTransformChunks && members == 1;
    const mat4f identity;
    chunk.buffer = filament::InstanceBuffer::Builder(members)
                       .localTransforms(rootPlacement ? &identity
                                                      : chunk.transforms.data())
                       .build(*_engine);

    // What a chunk hands Filament is a *world-space* box on a renderable that
    // never gets a transform, where an unbatched object hands over its
    // object-space box and lets Filament transform it (Box::transform, which
    // is centre = m*c + t and half-extent = abs(m) * h). For one member the
    // two are the same box by algebra and not by arithmetic: least and most
    // are centre-half and centre+half, each rounded, so recovering the
    // half-extent as (most-least)*0.5f lands a unit in the last place away
    // from abs(m)*h. Every crate in the Batching example misses by that much.
    // It matters because a caster's world box is what the shadow camera is
    // fitted from — near plane from the casters' near, far plane and the x-y
    // focus from their volume (Filament's ShadowMap::computeLightFrustumBounds)
    // — so a last-place difference in the box moves the whole map slightly.
    // ORBLIT_BATCH_BOX=exact takes that difference out, for measuring against.
    Box box{(least + most) * 0.5f, (most - least) * 0.5f};
    if (_exactChunkBox && members == 1) {
      const mat4f &m = chunk.transforms[0];
      box = Box{float3{m[3].x, m[3].y, m[3].z},
                float3{std::fabs(m[0].x) + std::fabs(m[1].x) + std::fabs(m[2].x),
                       std::fabs(m[0].y) + std::fabs(m[1].y) + std::fabs(m[2].y),
                       std::fabs(m[0].z) + std::fabs(m[1].z) + std::fabs(m[2].z)}};
    }
    // With the placement on the renderable's own transform, the box has to be
    // the object-space one an unbatched crate declares: Filament transforms a
    // renderable's box by that renderable's transform, so handing it a
    // world-space box here would place the box twice.
    if (rootPlacement) box = Box{{0, 0, 0}, {1, 1, 1}};

    chunk.entity = utils::EntityManager::get().create();
    RenderableManager::Builder(1)
        .boundingBox(box)
        .material(0, material)
        .geometry(0, RenderableManager::PrimitiveType::TRIANGLES, _vertexBuffer,
                  _indexBuffer, 0, 36)
        .instances(members, chunk.buffer)
        .receiveShadows(receiveShadows)
        .castShadows(castShadows)
        .screenSpaceContactShadows(receiveShadows)
        .layerMask(0xFF, layer)
        .build(*_engine, chunk.entity);
    if (rootPlacement) {
      auto &tcm = _engine->getTransformManager();
      tcm.create(chunk.entity);
      tcm.setTransform(tcm.getInstance(chunk.entity), chunk.transforms[0]);
    }
    _scene->addEntity(chunk.entity);

    group.chunks.push_back(chunk);
  }
}

/// Writes into a group's existing chunks without touching their layout:
/// every member named in `indices` already holds the slot group.slotOf says
/// it does, so only what actually changed — a transform, or the material
/// every chunk wears — is written.
void Renderer::updateBatchGroup(BatchGroup &group,
                                const std::vector<uint32_t> &indices,
                                const int64_t *keys, const float *transforms,
                                MaterialInstance *material) {
  auto &renderables = _engine->getRenderableManager();

  if (material != group.material) {
    group.material = material;
    for (auto &chunk : group.chunks) {
      auto instance = renderables.getInstance(chunk.entity);
      if (instance) renderables.setMaterialInstanceAt(instance, 0, material);
    }
  }

  // Which chunks had a member move, so a box is only rebuilt for one that
  // did — the usual case is one crate turning inside a group of thousands,
  // and the other chunks' boxes have no reason to be touched.
  std::vector<bool> dirty(group.chunks.size(), false);

  for (uint32_t index : indices) {
    const uint32_t slot = group.slotOf[keys[index]];
    const uint32_t chunkAt = slot / _chunkSize;
    const uint32_t offset = slot % _chunkSize;
    BatchChunk &chunk = group.chunks[chunkAt];

    mat4f placement;
    std::memcpy(&placement, transforms + size_t(index) * 16, sizeof(mat4f));
    if (std::memcmp(&placement, &chunk.transforms[offset], sizeof(mat4f)) == 0) {
      continue;
    }
    chunk.transforms[offset] = placement;
    chunk.buffer->setLocalTransforms(&placement, 1, offset);
    dirty[chunkAt] = true;
  }

  for (size_t c = 0; c < group.chunks.size(); c++) {
    if (!dirty[c]) continue;
    BatchChunk &chunk = group.chunks[c];

    float3 least{std::numeric_limits<float>::max()};
    float3 most{std::numeric_limits<float>::lowest()};
    for (uint32_t slot = 0; slot < chunk.count; slot++) {
      expandBoxByTransform(chunk.transforms[slot], least, most);
    }
    auto instance = renderables.getInstance(chunk.entity);
    if (instance) {
      renderables.setAxisAlignedBoundingBox(
          instance, Box{(least + most) * 0.5f, (most - least) * 0.5f});
    }
  }
}

/// Takes a group's chunks apart. Order matters, as it does for a
/// population's book: a renderable holds its InstanceBuffer, so the
/// renderable goes first — destroying the buffer first trips a Filament
/// precondition, which is fatal rather than an error code. The material is
/// never destroyed here: it is borrowed, from _materialOrder or from the
/// colour pool, and each already owns its own lifetime.
void Renderer::destroyBatchGroup(BatchGroup &group) {
  for (auto &chunk : group.chunks) {
    if (chunk.entity) {
      _scene->remove(chunk.entity);
      _engine->destroy(chunk.entity);
      utils::EntityManager::get().destroy(chunk.entity);
    }
    if (chunk.buffer != nullptr) _engine->destroy(chunk.buffer);
  }
  group.chunks.clear();
  group.slotOf.clear();
  group.material = nullptr;
}

/// Whether identical objects are drawn as manually-instanced groups instead
/// of one renderable each. See OrblitBatching.h for how a scene is divided
/// into groups and reconcileBatchGroups for how a group becomes renderables.
///
/// Not Filament's own automatic instancing, and deliberately so.
/// setAutomaticInstancingEnabled turns on RenderPass::instanceify(), which
/// merges draw commands after the fact by comparing them — and on stock
/// Filament 1.76 it compares a custom command's stale leftover state as
/// though it were a draw, and can fold the colour-grading subpass into a
/// neighbouring instanced run so it never executes: the whole frame comes
/// back black, on some scenes and not others depending on what was left in
/// the command arena. A fix exists on Orblit's own Filament fork, in no
/// release. Manual instancing — one renderable built with
/// RenderableManager::Builder::instances(count, InstanceBuffer*), exactly
/// the feature OrblitPopulation already draws forests with — never reaches
/// instanceify() at all, so the bug is sidestepped rather than depended on
/// being fixed. See ORBLIT_FORCE_INSTANCING in startWithWidth for reproducing
/// it on demand, now that nothing here asks for it on its own.
///
/// Purely a flag: the work of building or tearing down groups happens in
/// applyObjects, on the next publish, because that is where the census
/// already runs and where a scene's objects are known.
void Renderer::setBatching(bool enabled) {
  if (_disposed || _engine == nullptr) return;
  _batching = enabled;
}

uint32_t Renderer::batchedObjects() {
  return _batchedObjects;
}

uint32_t Renderer::batchGroups() {
  return _batchGroups;
}

/// Whether opaque objects are drawn into depth alone before they are shaded.
///
/// Filament has no depth-prepass API to ask for. It had one: the View setting
/// was deprecated in 1.4.5 and the APIs removed in 1.5.0, and the paragraph
/// still in Renderer.h describing a depth pre-pass stage is stale. What it
/// has instead covers other ground — the structure pass is half-resolution by
/// default, allocates its own mipmapped buffer and reaches the colour pass
/// only as a sampler, while the colour pass always allocates and clears its
/// own full-resolution depth; and TransparencyMode::TWO_PASSES_ONE_SIDE is a
/// real per-object prepass but is gated to materials that are not opaque, so
/// it cannot cover the geometry this is for.
///
/// So this is built out of render channels, which are public and documented:
/// a second entity per covered object over the same vertex and index buffers,
/// wearing the depth-only surface, on the channel below the one everything
/// else draws on. A channel is the top three bits of Filament's sort key, so
/// it beats pass, priority, Z-bucket and material — every prepass draw is
/// issued before every shaded one. Both channels draw inside one RenderPass
/// against one full-resolution depth attachment, so the prepass is visible to
/// the colour draws with no extra FrameGraph pass and no second target.
///
/// What is deliberately *not* done: the colour draws keep the depth state
/// they already had. Filament renders reversed-Z and its opaque draws already
/// test GE, so a fragment the prepass has covered from in front fails GE and
/// is rejected before it is shaded — which is the entire saving. Switching
/// those draws to E would reject exactly the same fragments and gain nothing,
/// while risking an object disappearing outright if the depth-only surface's
/// vertex shader and the real surface's disagree about a position by one unit
/// in the last place. Leaving the test alone also means a material instance
/// shared between objects — some covered by the prepass, some not — cannot be
/// made to hide the ones it does not cover.
///
/// Purely a flag, exactly like setBatching: the entities are built and torn
/// down in applyObjects, on the next publish, because that is where the
/// objects are known.
void Renderer::setDepthPrepass(bool enabled) {
  if (_disposed || _engine == nullptr) return;
  _depthPrepass = enabled;
}

uint32_t Renderer::prepassObjects() {
  return _prepassObjects;
}

/// The one surface every prepass entity wears.
///
/// Shared by all of them rather than one each: nothing is written through it
/// that could differ between objects, because nothing it computes is kept.
/// Colour write off is what makes this a depth pass rather than a wasted
/// colour one; depth write and depth test on are what make it write anything.
/// All three are rasteriser state and so belong on the instance — there is no
/// .mat keyword for a depth function, and colour write set here rather than in
/// the material is what lets the same compiled surface stay a normal one.
MaterialInstance *Renderer::depthOnlyInstance() {
  if (_depthOnly != nullptr) return _depthOnly;
  if (_depthMaterial == nullptr) {
    _depthMaterial = materialFrom(Package::depth);
  }
  _depthOnly = _depthMaterial->createInstance();
  _depthOnly->setColorWrite(false);
  _depthOnly->setDepthWrite(true);
  _depthOnly->setDepthCulling(true);
  // Pushed a hair further from the camera than the surface it stands in for.
  //
  // This is the one part of a duplicate-entity prepass that is not obvious,
  // and leaving it out is visibly wrong. The prepass entity is a second piece
  // of geometry in the same scene at the same place, so it is drawn into every
  // depth-derived pass Filament runs — the structure buffer that screen-space
  // contact shadows and ambient occlusion march along, which every object here
  // receives. Coincident depth there is not harmless: the two entities are
  // drawn by two different shaders, so their depths agree to within a unit in
  // the last place rather than exactly, and wherever the prepass lands the
  // nearer of the two every receiving surface finds an occluder immediately in
  // front of itself and shades itself dark. Measured, before this offset: one
  // per cent of the frame differed, in clusters, by up to 229 of 255 — surfaces
  // self-shadowing, not rounding.
  //
  // Pushing it behind removes that whole class of interaction: it can never be
  // the nearest thing at a pixel, so it occludes nothing in any pass, while
  // still sitting far in front of anything genuinely hidden behind the surface
  // — which is what the colour pass's existing reversed-Z test then rejects.
  // Same call and the same sign convention as applyRasterState uses to settle
  // which of two things sharing a plane is behind.
  _depthOnly->setPolygonOffset(kPrepassDepthBias, kPrepassDepthBias * 1000.0f);
  return _depthOnly;
}

/// Whether this object is one the prepass draws.
///
/// Three things have to hold, and each excluded case is excluded for its own
/// reason rather than out of caution:
///
///  * It is drawn as the placeholder cube. A model out of a glTF file is not
///    covered, because Filament's public RenderableManager offers no way to
///    ask a primitive which vertex and index buffers it draws — setGeometryAt
///    exists, the matching getter does not — so a second renderable over the
///    same geometry cannot be built through the public API at all. Such an
///    object draws exactly as it always did; it simply gets no help.
///  * It is visible. A hidden object writes no colour, and filling depth for
///    one would hide whatever stands behind it.
///  * Its surface is opaque. A masked surface punches its own pixels out by
///    alpha and a blended one never owns its pixels, so depth written for
///    either would be depth in the wrong place. Asked of Filament rather than
///    tracked here, so a material that changes its blend mode cannot leave a
///    stale answer behind.
bool Renderer::prepassCovers(int32_t flags, int32_t material,
                             const Drawn &drawn) {
  if (!drawn.entity || drawn.instance != nullptr) return false;
  if ((flags & kVisible) == 0) return false;
  const MaterialInstance *worn =
      (material >= 0 && material < static_cast<int32_t>(_materialOrder.size()))
          ? _materialOrder[material]
          : drawn.material;
  if (worn == nullptr) return false;
  return worn->getMaterial()->getBlendingMode() == BlendingMode::OPAQUE;
}

/// Builds, keeps or drops this object's prepass entity.
///
/// Called for every object on every publish, so that turning the prepass on
/// or off, hiding an object, or changing what it is made of all take effect
/// on the next scene rather than each needing a message of its own.
void Renderer::syncPrepass(Drawn &drawn, int32_t flags, int32_t material) {
  if (!_depthPrepass || !prepassCovers(flags, material, drawn)) {
    dropPrepass(drawn);
    return;
  }

  if (!drawn.prepass) {
    drawn.prepass = utils::EntityManager::get().create();
    RenderableManager::Builder(1)
        .boundingBox({{-1, -1, -1}, {1, 1, 1}})
        .material(0, depthOnlyInstance())
        .geometry(0, RenderableManager::PrimitiveType::TRIANGLES, _vertexBuffer,
                  _indexBuffer, 0, 36)
        // Neither casts nor receives: this entity exists for one depth buffer
        // in one pass. A second caster over the same geometry would double the
        // shadow pass's work to produce an identical map.
        .castShadows(false)
        .receiveShadows(false)
        .channel(kPrepassChannel)
        .build(*_engine, drawn.prepass);
    _scene->addEntity(drawn.prepass);

    // Straight onto the object's own placement, which the transform block
    // above has already written for this publish. Waiting for the next move
    // would leave the prepass standing at the origin — writing depth across
    // the middle of the scene — until the object happened to shift.
    auto &transforms = _engine->getTransformManager();
    transforms.setTransform(transforms.getInstance(drawn.prepass),
                            drawn.transform);
  }

  // The same layer as the object, so a pass that narrows the view to some
  // layers gets a prepass for exactly the objects it is going to draw, and
  // none for the objects it is not.
  auto &renderables = _engine->getRenderableManager();
  auto renderable = renderables.getInstance(drawn.prepass);
  if (renderable) {
    renderables.setLayerMask(renderable, 0xFF, layerBitOf(flags));
  }
  _prepassObjects++;
}

/// Takes an object's prepass entity out of the scene.
///
/// Safe on an object that has none, which is every object while the prepass
/// is off. The material is not destroyed here: it is the one shared instance,
/// which belongs to the renderer and outlives every object wearing it.
void Renderer::dropPrepass(Drawn &drawn) {
  if (!drawn.prepass) return;
  _scene->remove(drawn.prepass);
  _engine->destroy(drawn.prepass);
  utils::EntityManager::get().destroy(drawn.prepass);
  drawn.prepass = utils::Entity();
}

/// Drops the geometry of any file no object names any more.
///
/// A mesh is read once per path and kept, which is right while something is
/// drawn from it and a leak the moment nothing is. It never showed up on a
/// scene of authored assets, where the set of paths is fixed for the life of
/// the app. It shows up the first time geometry is *generated*: a mesh built
/// at runtime has to arrive under a name the renderer has not seen to be read
/// at all, so a host that rebuilds one chunk of a block world every time
/// somebody digs otherwise leaves every version it ever built on the GPU, and
/// the memory climbs for as long as the game is played.
///
/// Swept after the objects rather than inside `recycle`, because a path
/// leaving one object and arriving at another within the same publish is a
/// rename and not a deletion — destroying it in between would throw away
/// geometry that is about to be drawn again.
void Renderer::sweepUnnamedMeshes() {
  if (_meshes.empty()) return;

  std::set<std::string> named;
  for (const auto &pair : _drawn) {
    if (!pair.second.path.empty()) named.insert(pair.second.path);
  }

  for (auto it = _meshes.begin(); it != _meshes.end();) {
    if (named.count(it->first) != 0) {
      ++it;
      continue;
    }
    // Destroying the asset takes its instances with it, the pooled spares
    // included — which is why nothing may still be holding one, and why this
    // runs only after the object sweep has recycled them all.
    if (it->second.asset != nullptr) {
      // Its textures may still be on their way, and nothing may upload into
      // one after the asset has destroyed it. And the loader marks textures
      // ready in the asset it began last, so if that is this one it is told
      // to stop first.
      _textureQueue->forget(it->second.asset);
      if (_loadingAsset == it->second.asset) {
        _resourceLoader->asyncCancelLoad();
        _loadingAsset = nullptr;
        _loadingResources = false;
      }
      _assetLoader->destroyAsset(it->second.asset);
    }
    // The note about why it would not load goes with it. Keeping it would
    // answer for a file nothing is asking about, and the next object to name
    // this path reads the disk again and finds out for itself.
    _assetNotes.erase(it->first);
    it = _meshes.erase(it);
  }
}
}  // namespace orblit

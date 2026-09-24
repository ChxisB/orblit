#pragma once

// The renderer, in plain C++.
//
// Everything Orblit draws is decided here: the scene and its reconciliation,
// the materials, lights, sky and weather, the render graph and its passes,
// probes, the irradiance field, post-processing, decals, splats, the outline
// and batching. No Objective-C and no Apple header, so the same class runs
// behind the Swift plugin on macOS and iOS, behind the C ABI in
// include/orblit_renderer.h anywhere else, and behind a host with no Flutter
// at all.
//
// It was the Objective-C class in OrblitRenderer.mm, and it is laid out as
// that class was, so that a change made to the old file can be found in this
// one: every method is a member function named for the first part of its
// selector, in the OrblitRenderer*.cpp file for its topic — in the same
// order within that file, with the same comments — and every ivar is a member
// of the same name. The structs and constants it is built from are in
// OrblitRendererTypes.h, still in the old file's order. What the operating
// system provides — logging, the clock, files, pictures, video — is asked of
// OrblitPlatform.h, and where a frame is presented is OrblitSurface's
// business, as it was before.

#include "OrblitRendererTypes.h"

namespace orblit {

class Renderer {
 public:
  /// Takes ownership of `surface`, which is where frames are presented, and
  /// remembers which backend to ask for. Nothing starts until initWithWidth.
  Renderer(OrblitSurface *surface, OrblitBackend backend);
  ~Renderer();
  Renderer(const Renderer &) = delete;
  Renderer &operator=(const Renderer &) = delete;

  /// Starts Filament and allocates buffers. False if the backend would not
  /// start or Filament refused, with the reason logged.
  bool initWithWidth(uint32_t width, uint32_t height);

  // OrblitRenderer.h's interface in C++ types, in the order it declares them.
  // The documentation there is the documentation of these.
  void renderAtTime(double time);
  void applyObjects(const int64_t *keys, const float *transforms,
                    const float *colours, const int32_t *meshes,
                    const int32_t *flags, const int32_t *materials,
                    const int32_t *morphCounts, const float *morphWeights,
                    const std::vector<std::string> &paths, uint32_t count);
  bool hasPoses();
  void applyPoses(const int64_t *keys, const int32_t *ints,
                  const float *floats, const int32_t *jointCounts,
                  const int32_t *joints, const float *jointTransforms,
                  double at, uint32_t count);
  void setBatching(bool enabled);
  uint32_t batchedObjects();
  uint32_t batchGroups();
  void setDepthPrepass(bool enabled);
  uint32_t prepassObjects();
  void applyMaterials(const int64_t *keys, const int32_t *flags,
                      const float *params, const int32_t *maps,
                      const std::vector<std::string> &texturePaths,
                      const int32_t *textureSrgb, const int32_t *videos,
                      uint32_t count);
  void setPipeline(const float *params, size_t count);
  void applyVideos(const int64_t *keys, const int32_t *flags,
                   const float *params, const std::vector<std::string> &paths,
                   uint32_t count);
  void applyLights(const int64_t *keys, const int32_t *kinds,
                   const int32_t *flags, const float *params, uint32_t count);
  void applyDecals(const float *params, const int32_t *images,
                   const std::vector<std::string> &paths, uint32_t count);
  void setFogEnabled(bool enabled, const float *params);
  void setPostProcess(const float *params, size_t count);
  void applyProbes(const int64_t *keys, const float *params, uint32_t count);
  void applyField(const float *params, const std::string &from);
  void setEnvironmentRadiance(const std::string &radiance,
                              const std::string &skybox, const float *params);
  void setRenderGraph(const float *passes, uint32_t count,
                      const float *targets, uint32_t targetCount,
                      const std::vector<std::string> &names);
  // Hook (screen effects): the host's god-ray and distortion settings.
  void setGodRays(const float *godRays, size_t count,
                  const float *distortions, size_t distortionCount);
  std::vector<PassTiming> passTimings();
  double gpuMilliseconds();
  double cpuMilliseconds();
  bool hasPopulations();
  void applyPopulations(const int32_t *keys, const int32_t *counts,
                        const int32_t *meshes, const int32_t *flags,
                        const int32_t *revisions, const float *ranges,
                        const float *bounds,
                        const std::vector<std::string> &paths,
                        const int32_t *changed, uint32_t changedCount,
                        const float *transforms, const float *colours,
                        uint32_t count);
  bool hasSplats();
  void applySplats(const int32_t *keys, const int32_t *flags,
                   const int32_t *revisions, const float *params,
                   const std::vector<std::string> &paths,
                   const int32_t *changed, const int32_t *changedCounts,
                   uint32_t changedCount, const uint8_t *data,
                   size_t dataLength, uint32_t count);
  bool hasSprites();
  void applySprites(const int32_t *keys, const int32_t *flags,
                    const int32_t *orders, const int32_t *revisions,
                    const float *params, const std::vector<std::string> &paths,
                    const int32_t *changed, const int32_t *changedCounts,
                    uint32_t changedCount, const float *records,
                    size_t recordFloats, uint32_t count);
  bool hasTerrain();
  void applyTerrain(const std::vector<orblit::TerrainRequest> &requests);
  void setSkyEnabled(bool enabled, const float *params);
  void setPrecipitationEnabled(bool enabled, const float *params);
  Notes notes();
  void setSkyColour(const float *colour, float ambient, bool showBody);
  void setCameraPosition(const float *position, const float *target,
                         float fieldOfView, bool orthographic,
                         float viewHeight, double at);
  void setExposure(float aperture, float shutter, float sensitivity);
  void setOutlineKeys(const int64_t *keys, uint32_t count,
                      const float *params);
  void resizeToWidth(uint32_t width, uint32_t height);

  /// Replaces the presentation surface after construction: detaches whatever
  /// is attached (see detachSurface) and allocates fresh buffers onto the new
  /// one, taking ownership of it exactly as the constructor does with the
  /// first one. Android is why this exists — a Surface there can be
  /// destroyed and handed back any number of times across backgrounding
  /// while the engine, scene and every GPU resource in it survive untouched,
  /// which resizeToWidth's pending-dimensions dance was never built for.
  /// False if Filament could not build the new swap chain, which leaves the
  /// renderer presenting nowhere — not back on the old surface, which by the
  /// time a host has a new one to offer is usually already gone.
  bool attachSurface(OrblitSurface *surface, uint32_t width, uint32_t height);

  /// Destroys the swap chain(s) and gives the surface back, leaving the
  /// renderer presenting nowhere until the next attachSurface. Safe to call
  /// with nothing attached. drawAtTime already returns before touching a
  /// null swap chain, so a host may keep calling render while detached —
  /// nothing is drawn until the surface returns.
  void detachSurface();

  /// The most recently presented frame with a reference the caller owns, or
  /// null before the first one. Opaque: on Apple it is a CVPixelBufferRef.
  void *copyPresentedBuffer();

  /// Tears down Filament. Idempotent; the renderer is inert afterwards.
  void dispose();

  /// Which backend the engine was built with, once it has been.
  OrblitBackend backend() const { return _backend; }

  /// One answer about the device, asked once when the engine started — see
  /// orblit_capability. -1 before that, after dispose, and for a question
  /// this build does not know.
  int32_t capability(orblit_capability which) const;

  /// Frames that reached endFrame; read on the thread that drives this renderer.
  uint64_t renderedFrames() const { return _frameCount; }

  /// Asks for the next frame drawn to be read back into memory as well as
  /// presented. For a host with nowhere to present to — a test, a server, a
  /// console tool — this is the picture.
  void requestCapture();

  /// The last frame read back, as RGBA8 with the top row first. False until
  /// one has arrived, which is a frame or two after it was asked for.
  bool capturedFrame(std::vector<uint8_t> &rgba, uint32_t &width,
                     uint32_t &height);

 private:
  void startWithWidth(uint32_t width, uint32_t height);
  void buildGeometry();
  void buildQuad();
  void buildMist();
  void updateMistAtTime(double time);
  void buildClouds();
  void updateCloudsAtTime(double time);
  void buildRain();
  void updateRainAtTime(double time);
  void setAmbientColour(float3 colour, float intensity);
  void startAssetLoader();
  Mesh *meshAtPath(const std::string &path);
  gltfio::FilamentInstance *takeInstanceOf(Mesh *mesh);
  void recycle(Drawn &drawn);
  void removeEverything();
  void applyFlags(int32_t flags, utils::Entity entity);
  void morph(const Drawn &drawn, const float *weights, size_t count);
  void applyFlags(int32_t flags, const Drawn &drawn);
  void build(Drawn &drawn, const std::string &path);

  // Models out of files: what they hold, and what their own animation does.
  // In OrblitModels.cpp.
  SharedBytes convertedModel(const std::string &path,
                             const SharedBytes &source, std::string &carried);
  void readModel(Mesh &mesh, gltfio::FilamentInstance *first);
  void describeModel(Mesh &mesh, gltfio::FilamentInstance *first,
                     const std::string &path, const uint8_t *bytes,
                     size_t size);
  void dropFileLights(gltfio::FilamentInstance *instance);
  void animate();
  void restPose(Drawn &drawn);
  void fitSkinnedBoxes(Drawn &drawn);
  void wearVariant(Drawn &drawn, int32_t variant);
  bool prepassCovers(int32_t flags, int32_t material, const Drawn &drawn);
  void syncPrepass(Drawn &drawn, int32_t flags, int32_t material);
  void dropPrepass(Drawn &drawn);
  filament::MaterialInstance *depthOnlyInstance();
  void clearPopulation(Grown &grown);
  filament::InstanceBuffer *identityInstances();
  void growPopulation(Grown &grown, uint32_t count, const float *bounds, int32_t flags);
  void sortPopulation(Grown &grown, const float *transforms);
  void fillPopulation(Grown &grown, const float *transforms, const float *colours);
  void rangePopulations();
  int surfaceIndexFor(int32_t flags);
  Material *surfaceAt(int index);
  /// Compiles one of the single-purpose materials. The packages themselves
  /// live in OrblitMaterialPackages.cpp; this is the two lines every caller
  /// of one would otherwise write out.
  Material *materialFrom(Package which);
  void warmUp(Material *material);
  Texture *blankTexture();
  void setDefaultsOn(MaterialInstance *instance);
  float fieldStrength();
  void bindFieldEverywhere();
  void bindFieldTo(MaterialInstance *instance);
  Texture *textureAtPath(const std::string &path, bool srgb);
  void pollTextures();
  void pumpTextures();
  void applyTextureLimits();
  TextureSampler samplerFor(int32_t flags);
  void write(Surfaced &surface, const float *params, const int32_t *maps, const std::vector<std::string> &texturePaths, const int32_t *textureSrgb, int32_t video);
  void applyRasterState(Surfaced &surface, float threshold, float bias);
  void open(Movie &movie, const std::string &path);
  void close(Movie &movie);
  Texture *cubemapAtPath(const std::string &path, float3 *harmonics, bool *hasThose, const std::string &note);
  void rebuildEnvironmentLight();
  void releaseEnvironment();
  // Environments from .hdr and .exr pictures (OrblitEnvironment.cpp).
  void requestEnvironmentImages(const std::string &radiance,
                                const std::string &skybox,
                                float requestedSize);
  void pollEnvironment();
  void installEnvironment(const EnvironmentLighting &lighting, bool light,
                          bool sky);
  void showEnvironment();
  void releaseEnvironmentCache();
  void prepareTargets();
  void rebindTargets();
  void buildSmaaTables();
  void packRectangle(const float *p, float *out, bool casting);
  void uploadRectangles(const float *rectangles, uint32_t count);
  void renderAreaShadow();
  void buildAreaShadow();
  bool aimAreaShadowAt(const float *p);
  void buildLtcTables();
  filament::Material *materialForEffect(int effect);
  bool buildEffect(GraphPass &pass);
  void applyPostTo(View *view);
  void runEffect(GraphPass &pass, GraphTarget *into);
  View *viewForPass(GraphPass &pass);
  void aimPass(GraphPass &pass, uint32_t wide, uint32_t tall);
  void releaseTarget(GraphTarget &target);
  void sweepRetiredTextures();
  void releaseGraph();
  Texture *targetTextureNamed(const std::string &name);
  void shadowOptionsFor(utils::Entity entity);
  void refreshShadowOptions();
  void pumpVideos();
  void dress(Drawn &drawn, int32_t index);
  void reconcileBatchGroups(
      const std::unordered_map<orblit::BatchKey, std::vector<uint32_t>,
                                orblit::BatchKeyHash> &groups,
      const int64_t *keys, const float *transforms, const float *colours,
      uint64_t generation);
  void rebuildBatchGroup(BatchGroup &group, const orblit::BatchKey &key,
                         std::vector<uint32_t> indices, const int64_t *keys,
                         const float *transforms, MaterialInstance *material);
  void updateBatchGroup(BatchGroup &group, const std::vector<uint32_t> &indices,
                        const int64_t *keys, const float *transforms,
                        MaterialInstance *material);
  void destroyBatchGroup(BatchGroup &group);
  void sortBatchIndices(std::vector<uint32_t> &indices, const float *transforms);
  void sweepUnnamedMeshes();
  void writeLight(const Lit &lit);
  void buildDecalData();
  void bindDecalsTo(MaterialInstance *instance);
  void bindDecalsEverywhere();
  int32_t decalLayerFor(const std::string &path, Notes &notes, bool *uploaded);
  void capture(Probe &probe, uint8_t layers);
  void releaseProbe(Probe &probe);
  void captureOwedProbes();
  void chooseProbe();
  bool buildField();
  void runField();
  void releaseField();
  void applyGrading(bool enabled, int toneMapper, float exposure, float contrast, float saturation, float vibrance, float temperature, float tint, const float *shadows, const float *midtones, const float *highlights);
  void placeCamera();
  void projectWith(float fieldOfView, bool orthographic, float tall);
  void allocateBuffers();
  void releaseBuffers();
  void applyViewportSize();
  void drawOutline();
  void renderPasses();
  void drawAtTime(double time);

  /// Reads the frame being drawn back into memory, if one was asked for.
  /// Between the passes and endFrame, which is the only place Filament will
  /// read a swap chain.
  void readBackIfAsked();

  /// Which backend a host asked for, and which one the engine was built with.
  OrblitBackend _backendAsked = ORBLIT_BACKEND_DEFAULT;
  OrblitBackend _backend = ORBLIT_BACKEND_DEFAULT;

  /// Videos this platform could not open. Replaced on every publish.
  Notes _videoNotes;

  /// A frame read back for requestCapture, and whether one is wanted, in
  /// flight, or ready. Written by Filament's callback, read by the host.
  std::mutex _captureLock;
  bool _captureWanted = false;
  bool _captureInFlight = false;
  bool _captureReady = false;
  std::vector<uint8_t> _captured;
  uint32_t _capturedWidth = 0;
  uint32_t _capturedHeight = 0;

  // ---- What was the ivar block of @implementation OrblitRenderer ----
  Engine *_engine{};

  /// The colour grading currently on the view.
  ///
  /// A resource with a baked lookup table rather than a struct, so it is kept
  /// and rebuilt only when the numbers move.
  ColorGrading *_colorGrading{};

  /// The post-processing numbers as last applied, so a frame that changes
  /// nothing costs a memcmp rather than a dozen option rebuilds.
  float _postParams[kMaxPostParams]{};
  size_t _postCount{};

  /// Just the grading numbers, compared separately: the rest of the options
  /// are cheap to set and this one bakes a lookup table.
  float _gradingParams[17]{};
  filament::Renderer *_renderer{};
  Scene *_scene{};
  View *_view{};
  Camera *_camera{};
  utils::Entity _cameraEntity{};

  /// Everything in the scene, by the key its host gave it. This map is the
  /// whole reason a drag is cheap: it is what lets a publish be read as "these
  /// three moved" rather than "here is a new scene".
  std::unordered_map<int64_t, Drawn> _drawn{};
  std::unordered_map<int64_t, Lit> _lit{};

  /// Stamps for the mark-and-sweep. One counter each, because objects and
  /// lights arrive in separate calls.
  uint64_t _objectGeneration{};
  uint64_t _lightGeneration{};

  /// Whether identical objects are drawn as manually-instanced groups. See
  /// OrblitBatching.h for how a scene is divided into groups and
  /// reconcileBatchGroups for how a group becomes renderables.
  ///
  /// On unless a host turns it off, which is what every host that says
  /// nothing gets: this is the initial state the C ABI documents, so a
  /// console host that never calls orblit_renderer_set_batching batches, and
  /// one that calls it with 0 does not. What it costs against what it saves
  /// is set out on OrblitScene.batching (Dart side); the short of it is fifty
  /// one renderables where three thousand would do, against a bounded
  /// difference at the edges of shadows.
  bool _batching{true};
  orblit::BatchCensus _census{};

  /// Whether opaque objects are drawn twice: once into depth alone, then
  /// once shaded. Off by default — see OrblitScene.depthPrepass (Dart side)
  /// for the measurements that decided that.
  bool _depthPrepass{};

  /// The one instance every prepass entity wears: unlit, opaque, colour
  /// write off. Shared rather than one each, because nothing is written
  /// through it that could differ between objects. Built on first use and
  /// destroyed with the engine.
  filament::MaterialInstance *_depthOnly{};

  /// How many objects the last publish gave a prepass entity to. Nought
  /// while the prepass is off, and always at most the object count.
  uint32_t _prepassObjects{};
  /// Diagnostic only (ORBLIT_BATCH_CHUNK, ORBLIT_BATCH_BOX): how many members
  /// share one instanced draw, and whether a one-member chunk's bounding box
  /// is built the way Filament builds an unbatched object's. The defaults are
  /// what a release build does — kInstancesPerDraw members and the union box
  /// — so neither switch changes anything unless it is set. Both exist to
  /// measure what a batched shadow caster does differently from an unbatched
  /// one; see rebuildBatchGroup.
  uint32_t _chunkSize{kInstancesPerDraw};
  bool _exactChunkBox{};

  /// ORBLIT_LOAD_TRACE: say what a slow frame spent its time on, and what a
  /// model's load spent the publish on. Diagnostic only, off unless set;
  /// native/headless/orblit_load_bench reads these lines.
  bool _loadTrace{};
  /// How long the last frame waited for the backend to run what it asked.
  double _lastFlushSeconds{};
  bool _objectChunkBox{};
  bool _rootTransformChunks{};

  /// Shared material instances for the placeholder cube, one per colour,
  /// claimed by a BatchGroup rather than by an individual object now that
  /// batching builds one renderable per group instead of dressing objects
  /// one at a time. Still exactly the pool an unbatched, named-material
  /// object never touches.
  orblit::ColourPool<filament::MaterialInstance> _colourPool{};

  /// Every group large enough to batch, from the last publish that had any,
  /// keyed the same way the census keys objects. Kept rather than rebuilt
  /// from nothing each publish, so moving one member of a group of three
  /// thousand costs one instance write rather than three thousand.
  std::unordered_map<orblit::BatchKey, BatchGroup, orblit::BatchKeyHash> _groups{};

  /// What the last publish batched: objects in a group large enough to
  /// merge, and how many chunks — manually-instanced renderables, each of up
  /// to kInstancesPerDraw members — those groups came to once built. Exact,
  /// not a ceiling: a chunk is one renderable whether or not every member in
  /// it is visible, so unlike Filament's automatic instancing there is no
  /// "if it manages to merge" left to measure. Nought while batching is off.
  uint32_t _batchedObjects{};
  uint32_t _batchGroups{};

  gltfio::AssetLoader *_assetLoader{};
  gltfio::ResourceLoader *_resourceLoader{};
  gltfio::MaterialProvider *_materialProvider{};

  /// Every texture on its way to the GPU — a material's, a sprite layer's,
  /// a model's — decoded off this thread and uploaded under one budget a
  /// frame. See OrblitTextures.h.
  std::unique_ptr<orblit::TextureQueue> _textureQueue{};

  /// The glTF loader's view of that queue, for PNG, JPEG and KTX 2.
  ///
  /// Materials push into the queue directly rather than through a second
  /// provider: a provider is popped, popping takes whatever comes out, and
  /// the resource loader pops everything its providers hold. Each side pops
  /// only its own, by client.
  std::unique_ptr<orblit::QueuedTextureProvider> _modelTextures{};

  /// The asset whose resources the loader began last, which is the one it
  /// would mark textures ready in — so destroying it has to stop that first.
  gltfio::FilamentAsset *_loadingAsset{};

  /// Whether any mesh is hidden while its textures' memory is made.
  bool _meshesWaiting{};
  void showPrimedMeshes();

  /// Problems with textures, by the texture's path, and what named each: a
  /// model's path, or empty for a material's or a sprite layer's. Reported
  /// only while whatever named it is still in the scene.
  Notes _textureNotes{};
  std::unordered_map<std::string, std::string> _textureNotedFor{};

  /// The texture paths the last publish of materials and of sprites named.
  std::set<std::string> _materialTexturePaths{};
  std::set<std::string> _spriteTexturePaths{};

  /// The compiled surfaces, indexed by shading and blend mode. Built on
  /// first use: a scene of opaque lit objects should not compile the four
  /// blending variants it never draws.
  filament::Material *_surfaces[kSurfaceCount]{};

  /// Which of them have already been handed to the shader compiler ahead of
  /// the frame that draws them, one bit each — kSurfaceCount is sixteen, so
  /// the whole record is one word. Asked once per surface because a warm-up
  /// is free the second time only in the sense that it does nothing, and a
  /// scene is published every time anything in it changes.
  uint32_t _surfacesWarmed{};

  /// The same, for the screen effects a render graph names. Kept as the set
  /// of effect numbers rather than bits because effects are sparse and the
  /// numbering is a public contract that will keep growing.
  std::set<int> _effectsWarmed{};

  /// Whether this engine cannot manage the standard lit surface's feature
  /// level, decided once in startWithWidth from what the device answered and
  /// never revisited — a GPU does not grow samplers mid-session. surfaceAt
  /// reads it to build the slim five packages in place of the standard
  /// five, and everywhere a sampler the slim surface does not declare would
  /// otherwise be bound reads it too.
  bool _slimSurface{};

  /// What capability() answers, measured once by measureCapabilities while
  /// the engine starts and never again: none of it changes mid-session.
  std::array<int32_t, ORBLIT_CAPABILITY_COUNT> _capabilities{};
  bool _capabilitiesMeasured{};
  void measureCapabilities(filament::Engine::FeatureLevel supported);

  /// Whether a mesh that could not be loaded may load now, because bytes
  /// have been provided since it was looked for.
  bool meshMayHaveArrived(const std::string &path) const;

  /// Whether a shadow map can be sampled with a depth comparison on this
  /// device, decided once in startWithWidth for the same reason as the line
  /// above and never revisited. False means Filament has rewritten the
  /// shadow sampler's comparison to "never" and the three comparison-based
  /// shadow kinds all return nought, so applyViewShadows substitutes a
  /// variance shadow, which compares in the shader instead. See
  /// orblit::shadowComparisonAvailable in OrblitPlatform.h.
  bool _shadowComparison{true};

  /// Every material the host has named, by its key.
  std::unordered_map<int64_t, Surfaced> _materials{};

  /// This frame's materials in the order they arrived, which is what an
  /// object's index points into. Rebuilt each publish; never outlives one.
  std::vector<filament::MaterialInstance *> _materialOrder{};

  /// Which of them were built afresh this publish, so the objects wearing
  /// them are re-dressed rather than left pointing at what was destroyed.
  std::vector<bool> _materialRebuilt{};

  /// Instances no material needs any more.
  ///
  /// Destroyed at the start of the *next* publish rather than this one. An
  /// object still wearing one is not put right until objects are applied,
  /// which happens after materials — so destroying them here would leave a
  /// renderable pointing at freed memory for the rest of the call.
  std::vector<filament::MaterialInstance *> _materialsSpent{};

  /// Images loaded for materials, by path and colour space — the same file
  /// read as sRGB and as linear is two textures, and asking for one when the
  /// other is loaded would be a silent wrong answer.
  std::unordered_map<std::string, filament::Texture *> _ownTextures{};
  /// For each texture that could not be loaded, the resource generation it
  /// failed at — see Mesh::missingAt.
  std::unordered_map<std::string, uint64_t> _texturesMissingAt{};

  /// One white pixel, standing in for every map a material does not set.
  filament::Texture *_blankTexture{};

  /// An external image that never gets one, for a screen with no video on it
  /// yet. Filament wants every sampler bound whether the shader reads it or
  /// not, and a screen showing nothing is a legitimate state to be in.
  filament::Texture *_blankExternal{};

  /// How the frame is put together, already in the order it runs.
  ///
  /// Empty until a host says otherwise, which is read as the ordinary frame:
  /// one pass, every layer, straight into the picture. Empty rather than a
  /// default row, so that "nobody has said" and "somebody asked for exactly
  /// this" are not the same state.
  std::vector<GraphPass> _passes{};
  std::vector<GraphTarget> _targets{};

  /// Target textures given up but not yet destroyed, with the material
  /// generation they were given up in.
  std::vector<RetiredTexture> _retiredTextures{};

  /// Every material sampler currently reading a pass, rebuilt on each
  /// publish and replayed whenever a target is rebuilt.
  std::vector<TargetBinding> _targetBindings{};

  /// The place the scene is standing in, when a host has named one.
  ///
  /// Held apart from the flat ambient rather than replacing it, so that
  /// clearing an environment puts back the sky the day cycle had been
  /// writing rather than leaving the scene unlit.
  filament::IndirectLight *_environmentLight{};

  /// The reflections the scene has taken of itself, by key.
  std::unordered_map<int64_t, Probe> _probes{};

  /// The filter that turns a captured cube into the blurred chain a rough
  /// surface samples. Built once — it compiles its own materials and holds a
  /// kernel texture, so one per renderer rather than one per capture.
  /// The view and camera every capture is taken through, kept for the same
  /// reason the targets are: destroying them beside the render destroys them
  /// before it.
  filament::View *_captureView{};
  filament::Camera *_captureCamera{};

  IBLPrefilterContext *_prefilter{};
  IBLPrefilterContext::SpecularFilter *_specularFilter{};

  /// Which probe is lighting the scene, or zero for none.
  int64_t _activeProbe{};

  filament::Texture *_environmentRadiance{};
  filament::Texture *_environmentSkyTexture{};
  filament::Skybox *_environmentSkybox{};

  /// Whether the environment's backdrop is the one in the scene, so the
  /// procedural sky knows to stay out of the way.
  bool _showingEnvironmentSkybox{};
  std::string _environmentRadiancePath{};
  std::string _environmentSkyboxPath{};
  float _environmentParams[4]{};

  /// The diffuse harmonics read off the radiance cubemap, kept because
  /// Filament does not hand them back and the bundle they came from is freed
  /// as soon as the driver has taken the pixels. Turning an environment or
  /// dimming it rebuilds the light, and a rebuild without these is an
  /// environment that lights reflections and nothing matte.
  float3 _environmentHarmonics[9]{};
  bool _environmentHasHarmonics{};

  /// Whether the radiance and backdrop textures above belong to
  /// _environmentCache rather than to the environment, so releasing the
  /// environment leaves them for the next scene that names the same picture.
  bool _environmentRadianceCached{};
  bool _environmentSkyCached{};

  /// Pictures filtered at run time, most recently used last. See
  /// EnvironmentLighting.
  std::vector<EnvironmentLighting> _environmentCache{};

  /// Pictures being decoded or filtered for the environment the scene names.
  std::vector<std::shared_ptr<EnvironmentWork>> _environmentWork{};

  /// What each picture name was found to be, keyed by name and sizes: its
  /// hash, or why it could not be used. Good until bytes are provided under
  /// any name, which is when a name's bytes can have changed.
  struct EnvironmentName {
    uint64_t hash = 0;
    std::string note;
    uint64_t generation = 0;
  };
  std::map<std::string, EnvironmentName> _environmentNames{};

  /// Counts publishes that set an environment, for EnvironmentLighting's
  /// lastUsed.
  uint64_t _environmentPublishes{};

  /// The filters an environment picture goes through: an equirectangular
  /// picture into a cube, and a cube into the blurred chain. Built on first
  /// use, beside the probes' own filter and sharing its context.
  IBLPrefilterContext::EquirectangularToCubemap *_equirectangularFilter{};
  IBLPrefilterContext::SpecularFilter *_environmentFilter{};
  uint8_t _environmentFilterLevels{};

  /// The last flat ambient asked for, kept so it can be put back when an
  /// environment is cleared. The day cycle writes this on every frame and
  /// would otherwise have to be waited for.
  float3 _ambientColour{0.0f, 0.0f, 0.0f};
  float _ambientIntensity{};

  /// The graph as last received, so a scene republished sixty times a second
  /// only rebuilds views and render targets when something in it moved.
  std::vector<float> _graphPassParams{};
  std::vector<float> _graphTargetParams{};
  std::vector<std::string> _graphTargetNames{};

  /// The last pipeline settings applied, so a scene republished sixty times
  /// a second only reconfigures the view when something actually moved.
  ///
  /// Larger than the block a current host sends, deliberately: a host that
  /// sends more is a newer one, and its extra floats are dropped rather than
  /// written past the end of this. The assertion is what keeps the two facts
  /// in step, because the truncation is silent and reads as a dial that has
  /// stopped working.
  float _pipelineParams[32]{};
  size_t _pipelineCount{};

  /// Every video the host has named, by its key.
  std::unordered_map<int64_t, Movie> _movies{};

  /// This frame's videos in the order they arrived, which is what a material
  /// points into.
  std::vector<Movie *> _movieOrder{};

  uint64_t _videoGeneration{};

  uint64_t _materialGeneration{};

  /// Whether any asset is still decoding its textures. An ivar block takes
  /// no initialiser, so this is zeroed by the runtime like the rest.
  bool _loadingResources{};
  /// What the load in flight is, and when it started, for the timing report.
  std::string _loadingName{};
  size_t _loadingResourceCount{};
  double _loadingFrom{};

  /// Loaded glTF files, by path. Kept for the life of the renderer: a scene
  /// arrives on every drag, and the parse is the expensive part.
  std::map<std::string, Mesh> _meshes{};

  /// Sixty-four identity transforms, lent to every population draw. Built
  /// once, because every draw wants the same nothing.
  filament::InstanceBuffer *_identityInstances{};

  /// One material per effect, built on first use and shared by every pass
  /// that runs it. Indexed by the effect's own number.
  std::map<int, filament::Material *> _effectMaterials{};

  /// Hook (screen effects): what the host said about god rays and
  /// distortion, turned into material parameters when their pass runs.
  orblit::ScreenEffects _screenEffects{};
  /// Motion blur hook: what motion blur remembers between frames and the
  /// passes it runs. Made the first time a graph asks for the effect, so a
  /// renderer that never blurs allocates none of it.
  std::unique_ptr<orblit::MotionBlur> _motionBlur{};

  /// SMAA's two precomputed tables, uploaded once.
  /// The world-space irradiance field: two atlases, written in turn.
  ///
  /// Two because a probe's new value is a blend of what it just learned with
  /// what it already held, and a shader cannot read the texture it is writing.
  /// One is the answer being read this frame while the other is being built.
  filament::Texture *_fieldAtlas[2]{};
  filament::RenderTarget *_fieldTargets[2]{};
  int _fieldFront{};
  bool _fieldHasHistory{};
  uint32_t _fieldProbes{};

  /// The triangle the field is drawn with, and what draws it.
  filament::View *_fieldView{};
  filament::Camera *_fieldCamera{};
  filament::Scene *_fieldScene{};
  utils::Entity _fieldEntity{};
  filament::MaterialInstance *_fieldInstance{};
  filament::Material *_fieldMaterial{};
  filament::VertexBuffer *_fieldVertices{};
  filament::IndexBuffer *_fieldIndices{};

  float _fieldParams[kFieldStride]{};
  std::string _fieldFrom{};

  filament::Texture *_smaaArea{};
  filament::Texture *_smaaSearch{};

  /// The fitted tables every rectangular area light is shaded against, and
  /// this frame's rectangles. Both are built once and live as long as the
  /// renderer: the tables never change, and the lights are rewritten in
  /// place so that a material instance can bind the texture once and not
  /// care that its contents moved.
  /// The fitted tables and this frame's rectangles, in one texture.
  ///
  /// One rather than two because a material at Filament's first feature level
  /// may have nine samplers, and alongside the irradiance field's atlas these
  /// would have been the tenth. They share without interfering: the tables are
  /// read with filtering and the lights with texelFetch, which ignores it.
  /// The tables occupy the first 64 rows and the rectangles the ones below.
  filament::Texture *_lightData{};

  /// The one rectangle's depth map, and everything needed to draw it.
  ///
  /// A view and a camera of its own rather than the scene's, because what a
  /// light can see is a different picture from what the camera can: the same
  /// objects, a different frustum, and no shading worth doing — only how far
  /// away the nearest thing is in each direction.
  filament::Texture *_areaShadow{};
  filament::RenderTarget *_areaShadowTarget{};
  filament::View *_areaShadowView{};
  filament::Camera *_areaShadowCamera{};
  utils::Entity _areaShadowCameraEntity{};

  /// Where the casting rectangle stood when it last looked, and whether one
  /// is casting at all. Kept between frames because the surfaces read it out
  /// of the light data, which is written once per frame rather than per draw.
  filament::math::mat4f _areaShadowMatrix{};
  // Where the casting rectangle stood when its map was drawn: the near plane
  // and field of view the surface needs to turn map depth back into metres.
  orblit::AreaShadowFrame _areaShadowFrame{};
  bool _areaShadowCasting{};

  /// The rectangles as the GPU currently holds them, so a frame that changed
  /// none of them uploads nothing. Almost every scene has no area lights at
  /// all, and that scene should not pay a texture upload a frame to keep
  /// saying so.
  std::vector<float> _areaLightsOnGpu{};

  /// Decals: one row of numbers each, and one layer of picture each.
  ///
  /// The numbers are rewritten in place like the rectangles', so a surface
  /// binds the texture once. The pictures are built the first time a scene
  /// names one; until then surfaces are pointed at a one-texel stand-in,
  /// because Filament refuses to draw a material with a sampler nobody bound.
  filament::Texture *_decalData{};
  filament::Texture *_decalPictures{};
  filament::Texture *_decalBlankPictures{};
  std::vector<float> _decalsOnGpu{};

  /// Which layer each picture went into, by path. Negative for a picture
  /// that could not be read (-1) or found no room (-2): kept, so a missing
  /// file is looked for once rather than every frame.
  std::unordered_map<std::string, int32_t> _decalPictureLayer{};
  uint32_t _decalPictureCount{};
  Notes _decalNotes{};


  /// Assets that could not be loaded. Sticky, because a file is read once and
  /// a failure that reported itself only on the frame of the attempt would
  /// never be seen again.
  Notes _assetNotes{};

  /// What the current objects and lights add up to that the renderer cannot
  /// honour. Replaced on every publish, so fixing the scene clears it.
  Notes _objectNotes{};
  Notes _lightNotes{};
  Notes _poseNotes{};

  /// What each model built by the last publish holds, under kModelInfoPrefix
  /// and its path. Filled as objects are built rather than on every publish:
  /// a host learns what a file contains when something new is made of it,
  /// and a scene that is only moving costs nothing here.
  Notes _modelInfo{};

  /// The objects the last applyPoses gave a pose, which are the only ones a
  /// frame has to animate.
  std::vector<int64_t> _posed{};

  /// The objects the last applyPoses dressed in a material variant, so the
  /// next can take it off any it no longer names without walking the scene.
  std::vector<int64_t> _varied{};

  /// Names for the entities files make, so a joint, a light and a camera can
  /// be reported by what the file calls them. gltfio fills this only when it
  /// is given one.
  utils::NameComponentManager *_names{};

  /// What the slim surface cost, said once rather than left for a host to
  /// notice by its absence. "surface" is set once, in startWithWidth, and
  /// stays for the renderer's life; "field" comes and goes with whether the
  /// current scene actually asks for a field, the same way areaShadows in
  /// _lightNotes comes and goes with whether a light asks to cast.
  Notes _surfaceNotes{};

  bool _sceneIsOwnedByHost{};
  Skybox *_skybox{};
  IndirectLight *_ambient{};

  /// The sky as it currently stands. A day cycle changes it on every frame,
  /// and a skybox rebuilt sixty times a second is sixty allocations to say
  /// what one setter says.
  float3 _skyColour{0.0f, 0.0f, 0.0f};
  float _skyAmbient{};
  bool _skyShowsBody{};
  bool _skyBuilt{};

  /// Drawn many times over from one submission. Built the first time a scene
  /// has a population in it, because most have none.
  Material *_instancedMaterial{};

  /// The depth-only surface the prepass draws with, built the first time a
  /// scene asks for a prepass and never rebuilt. Its one instance is
  /// _depthOnly.
  Material *_depthMaterial{};
  std::unordered_map<int32_t, Grown> _populations{};
  uint64_t _populationGeneration{};

  /// Gaussian splat clouds. Everything about them is in OrblitSplatSet, in
  /// plain C++; this only holds it, feeds it the scene and the camera, and
  /// passes on what it could not load.
  std::unique_ptr<orblit::SplatScene> _splats{};
  Notes _splatNotes{};
  std::unique_ptr<orblit::SpriteScene> _sprites{};
  Notes _spriteNotes{};
  /// Ground, drawn from heights on the GPU. OrblitTerrain holds all of it;
  /// this places its grids round the camera each frame.
  std::unique_ptr<orblit::TerrainScene> _terrain{};
  Notes _terrainNotes{};
  VertexBuffer *_vertexBuffer{};
  IndexBuffer *_indexBuffer{};

  /// The sheets a bank of mist is drawn with, and what they are made of.
  /// Built the first time a scene asks for weather and kept after that.
  Material *_mistMaterial{};
  VertexBuffer *_quadVertices{};
  IndexBuffer *_quadIndices{};
  std::vector<utils::Entity> _mistEntities{};
  std::vector<MaterialInstance *> _mistInstances{};

  /// The dome the sky's cloud is drawn on, and what it is made of.
  Material *_cloudMaterial{};
  VertexBuffer *_skyVertices{};
  IndexBuffer *_skyIndices{};
  utils::Entity _cloudEntity{};
  MaterialInstance *_cloudInstance{};
  bool _cloudsShowing{};

  /// The panes a curtain of rain or snow is drawn on.
  Material *_rainMaterial{};
  std::vector<utils::Entity> _rainEntities{};
  std::vector<MaterialInstance *> _rainInstances{};
  bool _rainShowing{};

  /// What the current weather is, so the sheets are only rewritten when it
  /// changes rather than on every frame.
  bool _mistShowing{};
  float _mistHeight{};
  float _mistThickness{};
  float3 _mistCentre{0.0f, 0.0f, 0.0f};

  OrblitSurface *_surface{};
  SwapChain *_swapChains[kOrblitBufferCount]{};
  int _backIndex{};
  int _presentedIndex{};

  uint32_t _width{};
  uint32_t _height{};
  uint32_t _pendingWidth{};
  uint32_t _pendingHeight{};
  float _fieldOfView{};
  bool _orthographic{};
  float _viewHeight{};

  /// The last two things the camera was told, and the lock between the thread
  /// that says them and the thread that draws.
  Aimed _aimedNow{};
  Aimed _aimedWas{};
  std::mutex _aimLock;
  double _clockOffset{};
  bool _clocksAligned{};

  /// How fast the camera is going, followed rather than measured fresh.
  float3 _aimVelocity{0.0f, 0.0f, 0.0f};
  float3 _lookVelocity{0.0f, 0.0f, 0.0f};
  float _lensVelocity{};
  bool _movingKnown{};

  /// The word this is currently predicting from, and how wrong the last
  /// prediction turned out to be — carried, and decaying.
  double _spokeAt{};

  /// The host's moment this frame is drawn at, as placeCamera works it out,
  /// and whether it has. Animation is sampled at the same moment the camera
  /// is, so a character and the camera following it never disagree about
  /// when it is — and on a held clock this is exactly the moment the host
  /// stated, so a frame is the same every time.
  double _drawnHostSeconds{};
  bool _drawnHostSecondsKnown{};
  float3 _spokePosition{0.0f, 0.0f, 0.0f};
  float3 _spokeTarget{0.0f, 0.0f, 0.0f};
  float3 _carriedPosition{0.0f, 0.0f, 0.0f};
  float3 _carriedTarget{0.0f, 0.0f, 0.0f};
  double _placedAt{};
  double _placedWas{};

  /// The usual gap between words, followed. A single gap is far too noisy to
  /// decide anything with.
  double _spanUsual{};
  float3 _toldFrom{0.0f, 0.0f, 0.0f};
  double _toldAt{};
  float _toldSpeedWas{};
  float _toldSpeedTotal{};
  float _toldJerkTotal{};
  int _toldCount{};
  double _reachedTotal{};
  int _reachedCount{};
  int _saturated{};
  bool _pacing{};
  double _gpuTotal{};
  int _gpuCount{};

  std::mutex _presentLock;
  bool _disposed{};
  uint64_t _frameCount{};
  double _startedAt{};
  float _skyFlash{};
  int _cameraUpdates{};
  double _pacedAt{};
  float3 _pacedFrom{0.0f, 0.0f, 0.0f};
  double _pacedFrameAt{};
  float _stepWas{};
  float _stepTotal{};
  float _jerkTotal{};
  int _stepCount{};
  bool _dumped{};

  /// The selection outline, made the first time something is highlighted
  /// and not before: a renderer nobody asks for an outline holds nothing
  /// for one.
  std::unique_ptr<orblit::Outline> _outline{};

  /// Which objects are highlighted, by key, the active ones first. Resolved
  /// to entities at the top of each frame rather than when they arrive,
  /// because an object can be rebuilt as a different mesh between the two.
  std::vector<int64_t> _outlineKeys{};
  uint32_t _outlinePrimaryCount{};
  orblit::OutlineStyle _outlineStyle{};
};

}  // namespace orblit

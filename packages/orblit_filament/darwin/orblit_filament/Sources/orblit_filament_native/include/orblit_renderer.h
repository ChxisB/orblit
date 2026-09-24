#ifndef ORBLIT_RENDERER_H
#define ORBLIT_RENDERER_H

/* The renderer, as C.
 *
 * Everything a host needs to drive Orblit's renderer without Objective-C,
 * without C++ and without Flutter: create one with a backend and a surface,
 * resize it, publish a scene, draw at a time, and read back what it drew, what
 * it cost and what it could not do. A Kotlin plugin calls this through JNI, a
 * Linux or Windows plugin calls it from C++, and a console host with no
 * Flutter at all calls it from main().
 *
 * C rather than C++ because a C ABI is the one every language and every
 * compiler agrees on: a C++ class compiled by one toolchain cannot be called
 * from code compiled by another, and a JNI or FFI binding can only name C.
 *
 * The rules, which every function below keeps:
 *
 *  - Handles are opaque, and every function takes NULL as a handle and
 *    returns ORBLIT_ERROR_NULL rather than crashing.
 *  - Arrays arrive as a pointer and a length in elements. The length is
 *    checked against what the count and the layout need before anything is
 *    read, and a short array is ORBLIT_ERROR_LENGTH with nothing applied — a
 *    host that gets a stride wrong is told by a return value, not by a crash.
 *    orblit_renderer_stride says how wide each row is, so a host need not
 *    copy the numbers.
 *  - Scene calls describe the whole of their part of the scene every time,
 *    as the Flutter plugin's do: anything not named has gone. The renderer
 *    works out what changed.
 *  - Nothing throws across this boundary. If Filament refuses something the
 *    renderer stops, the call returns ORBLIT_ERROR_FAILED, and so does every
 *    call after it; orblit_renderer_destroy is still safe.
 *  - One thread drives a renderer, apart from orblit_renderer_set_camera,
 *    orblit_renderer_resize and orblit_renderer_copy_presented, which are safe
 *    from any thread.
 *
 * The row layouts are the Dart side's — OrblitLight, OrblitMaterial and the
 * rest in package:orblit_filament — and the documentation of each
 * orblit_renderer_* call is the documentation of the matching method in
 * OrblitRenderer.h, which this mirrors in the same order.
 */

#include <stddef.h>
#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

/* Which graphics API the renderer draws with.
 *
 * DEFAULT is the platform's own choice: Metal on Apple platforms, Vulkan on
 * Android, Linux (the Steam Deck included) and Windows with OpenGL as the
 * fallback where Vulkan will not start, and OpenGL — WebGL 2 — on the web.
 * WebGPU is reserved for the web and chosen only when asked for by name,
 * until the materials are compiled for it. The environment variable
 * ORBLIT_BACKEND (metal, vulkan, opengl, webgpu) overrides DEFAULT, for
 * testing a backend on a machine whose default is another. */
typedef enum OrblitBackend {
  ORBLIT_BACKEND_DEFAULT = 0,
  ORBLIT_BACKEND_METAL = 1,
  ORBLIT_BACKEND_VULKAN = 2,
  ORBLIT_BACKEND_OPENGL = 3,
  ORBLIT_BACKEND_WEBGPU = 4
} OrblitBackend;

/* What a call came to. Nought is success; everything else is negative. */
typedef enum orblit_result {
  ORBLIT_OK = 0,
  /* A handle, or an array with a non-zero count, was NULL. */
  ORBLIT_ERROR_NULL = -1,
  /* An array is shorter than its count and its row say it must be. */
  ORBLIT_ERROR_LENGTH = -2,
  /* The renderer has stopped: Filament refused something, or it was
   * disposed. The reason was logged. */
  ORBLIT_ERROR_FAILED = -3,
  /* An index names something past the end of its array: a render graph pass
   * reading a target the graph does not have, or a note that is not there. */
  ORBLIT_ERROR_RANGE = -4
} orblit_result;

/* Where frames go. */
typedef enum orblit_surface_kind {
  /* Nowhere anybody sees: an offscreen swap chain, read back with
   * orblit_renderer_request_capture. Tests, servers, a console host that has
   * not opened a window. */
  ORBLIT_SURFACE_HEADLESS = 0,
  /* A native window, in `window`: an ANativeWindow* on Android, an HWND on
   * Windows, an X11 Window or a wl_surface on Linux, a CAMetalLayer* on
   * Apple — whatever Filament's createSwapChain takes on that platform. */
  ORBLIT_SURFACE_WINDOW = 1,
  /* The platform's texture-sharing surface, where it has one: on Apple, the
   * IOSurface-backed CVPixelBuffers the Flutter plugin hands to Flutter,
   * reached with orblit_renderer_copy_presented. Refused elsewhere. */
  ORBLIT_SURFACE_PLATFORM = 2
} orblit_surface_kind;

typedef struct orblit_surface_desc {
  orblit_surface_kind kind;
  void *window;
} orblit_surface_desc;

/* The rows the scene calls take, so a host can size its arrays from the
 * renderer rather than from a copy of the numbers. */
typedef enum orblit_stride {
  ORBLIT_STRIDE_TRANSFORM = 0,
  ORBLIT_STRIDE_COLOUR,
  ORBLIT_STRIDE_MATERIAL,
  ORBLIT_STRIDE_MATERIAL_MAPS,
  ORBLIT_STRIDE_VIDEO,
  ORBLIT_STRIDE_LIGHT,
  ORBLIT_STRIDE_DECAL,
  ORBLIT_STRIDE_FOG,
  ORBLIT_STRIDE_PROBE,
  ORBLIT_STRIDE_FIELD,
  ORBLIT_STRIDE_ENVIRONMENT,
  ORBLIT_STRIDE_PASS,
  ORBLIT_STRIDE_TARGET,
  ORBLIT_STRIDE_GOD_RAYS,
  ORBLIT_STRIDE_DISTORTION,
  ORBLIT_STRIDE_POPULATION_BOUNDS,
  ORBLIT_STRIDE_SPLAT,
  ORBLIT_STRIDE_SPLAT_RECORD_BYTES,
  ORBLIT_STRIDE_SKY,
  ORBLIT_STRIDE_PRECIPITATION,
  ORBLIT_STRIDE_OUTLINE,
  ORBLIT_STRIDE_PIPELINE,
  ORBLIT_STRIDE_SPRITE_LAYER,
  ORBLIT_STRIDE_SPRITE,
  /* A pose's whole numbers and its floats: see orblit_renderer_apply_poses. */
  ORBLIT_STRIDE_POSE_INTS,
  ORBLIT_STRIDE_POSE,
  /* A terrain's whole numbers, a region's, a terrain's floats and a set's:
   * see orblit_renderer_apply_terrain. */
  ORBLIT_STRIDE_TERRAIN_INTS,
  ORBLIT_STRIDE_TERRAIN_REGION_INTS,
  ORBLIT_STRIDE_TERRAIN,
  ORBLIT_STRIDE_TERRAIN_SET
} orblit_stride;

/* How wide one row of `which` is, in floats (bytes for a splat record), or
 * nought for a value this build does not know. */
uint32_t orblit_renderer_stride(orblit_stride which);

/* ---- Bytes by name ---- */

/* Keeps a copy of `length` bytes under `name` for every renderer in the
 * process, including renderers made later. Wherever a scene names a file — a
 * mesh, a texture, an environment, a decal's picture, a splat capture, and
 * the files a .gltf names beside itself — the renderer looks for bytes
 * provided under that name before it asks the disk. That is how a host with
 * no file system (a browser), one whose assets are inside an archive (an
 * Android APK) or one that fetched them over a network hands them over.
 *
 * A name stands for bytes that do not change. Providing it again replaces
 * what the next load sees, but nothing already loaded is loaded again, so the
 * way to change an asset is a new name — a content hash in it is the natural
 * one. A name looked for before it was provided is tried again once it has
 * been. `.` and `..` segments are worked out, so a .gltf provided as
 * "models/robot.gltf" finds "models/../textures/wood.png" provided as
 * "textures/wood.png".
 *
 * Any thread. ORBLIT_ERROR_NULL for a NULL name, or NULL bytes with a
 * non-zero length; ORBLIT_ERROR_FAILED if the copy could not be allocated. */
int orblit_renderer_provide_resource(const char *name, const uint8_t *bytes,
                                    size_t length);

/* Lets go of what was provided under `name`. Anything already loaded from it
 * stays loaded. ORBLIT_ERROR_RANGE when nothing was provided under that name.
 * Any thread. */
int orblit_renderer_release_resource(const char *name);

typedef struct orblit_renderer orblit_renderer;

/* ---- Lifetime ---- */

/* Starts Filament with `backend` onto `surface` (NULL is headless) at the
 * given size. NULL if the backend would not start or Filament refused, with
 * the reason logged. */
orblit_renderer *orblit_renderer_create(OrblitBackend backend,
                                      const orblit_surface_desc *surface,
                                      uint32_t width, uint32_t height);

/* Tears it down. NULL is fine. */
void orblit_renderer_destroy(orblit_renderer *renderer);

/* The backend the engine was built with. */
OrblitBackend orblit_renderer_backend(const orblit_renderer *renderer);

/* New dimensions, applied at the top of the next frame. Any thread. */
int orblit_renderer_resize(orblit_renderer *renderer, uint32_t width,
                          uint32_t height);

/* Replaces the presentation surface after construction: detaches whatever is
 * attached (see orblit_renderer_detach_surface) and allocates fresh buffers
 * onto the new one. `surface` may be HEADLESS or WINDOW; PLATFORM is refused,
 * as it is at create — there is nowhere off Apple to get one from, and
 * Apple's own texture-sharing surface lives for the renderer's whole life
 * and never needs replacing this way.
 *
 * This is for Android, where a Surface can be destroyed and handed back any
 * number of times across backgrounding while the engine, scene and every GPU
 * resource in it survive untouched: detach on the way out, attach on the way
 * back in, same renderer throughout. ORBLIT_ERROR_FAILED if Filament could not
 * build the new swap chain, which leaves the renderer presenting nowhere —
 * not back on the old surface, which by the time a host has a new one to
 * offer is usually already gone. Unlike other calls, a failure here does not
 * stop the renderer: the surface lifecycle this exists for is expected to be
 * retried, not treated as fatal the way a scene call's failure is. */
int orblit_renderer_attach_surface(orblit_renderer *renderer,
                                  const orblit_surface_desc *surface,
                                  uint32_t width, uint32_t height);

/* Destroys the swap chain(s) and gives the surface back, leaving the
 * renderer presenting nowhere until the next orblit_renderer_attach_surface.
 * Drawing while detached is safe and draws nothing. NULL is fine. */
int orblit_renderer_detach_surface(orblit_renderer *renderer);

/* Draws one frame at `seconds` and presents it. */
int orblit_renderer_draw(orblit_renderer *renderer, double seconds);

/* Frames that reached endFrame, excluding skipped draws and draws while
 * detached. This counts rendering, not display presentations. Read on the
 * thread that calls draw. Zero for NULL. Kept separate from orblit_stats so
 * existing callers' struct layouts do not change. */
uint64_t orblit_renderer_rendered_frames(const orblit_renderer *renderer);

/* ---- The scene ---- */

/* `count` objects: transforms are 16 floats each, column-major; colours 3;
 * meshes index `paths` — a .gltf, .glb, .fbx or .obj, the last two converted
 * to glTF off the drawing thread before they draw — or are -1 for the
 * built-in cube; flags as
 * OrblitObject packs them; materials index the last apply_materials or are
 * -1; morph_counts say how many of `morph_weights` each takes, end to end. */
int orblit_renderer_apply_objects(orblit_renderer *renderer, uint32_t count,
                                 const int64_t *keys,
                                 const float *transforms,
                                 size_t transform_floats,
                                 const float *colours, size_t colour_floats,
                                 const int32_t *meshes, const int32_t *flags,
                                 const int32_t *materials,
                                 const int32_t *morph_counts,
                                 const float *morph_weights,
                                 size_t morph_weight_floats,
                                 const char *const *paths,
                                 uint32_t path_count);

/* What each model's own file does to it, for `count` objects of the last
 * apply_objects, by key. Call it after apply_objects, and whole every time:
 * an object it does not name goes back to the file's rest pose and its own
 * materials.
 *
 * Per pose, ORBLIT_STRIDE_POSE_INTS whole numbers — the clip playing (-1 for
 * none), the clip being faded from (-1), flags (1: the clip loops, 2: the
 * faded-from clip loops) and the material variant (-1 for the file's own) —
 * and ORBLIT_STRIDE_POSE floats: the clip's seconds and its speed, the
 * faded-from clip's seconds and speed, and how far the fade has gone, one
 * being all the clip. `joint_counts` says how many joints each pose sets, end
 * to end in `joints` (a skin and a joint of it, two each) and
 * `joint_transforms` (a column-major local transform, sixteen each), which
 * replace what the clips gave those joints.
 *
 * `at` is the host's seconds these describe, on the clock set_camera's `at`
 * is on. Each frame samples the clips at the moment that frame is drawn, so a
 * clip moves smoothly between publishes and stands exactly still on a held
 * clock. A clip, variant or joint the file does not have is a note. */
int orblit_renderer_apply_poses(orblit_renderer *renderer, uint32_t count,
                               const int64_t *keys, const int32_t *ints,
                               size_t int_count, const float *floats,
                               size_t float_count,
                               const int32_t *joint_counts,
                               const int32_t *joints, size_t joint_ints,
                               const float *joint_transforms,
                               size_t joint_transform_floats, double at);

/* Whether objects that are the same thing are drawn together: four or more
 * sharing a mesh, a material and their flags become a handful of manually
 * instanced renderables rather than one each.
 *
 * On for a renderer that has just been created, so a host that never calls
 * this batches. Pass 0 to turn it off — worth doing for a scene that wants
 * every renderable culled on its own, because a merged group is culled by
 * one box for up to sixty-four members and its shadows are fitted from that
 * same looser box. See OrblitScene.batching in package:orblit_filament for
 * what that is measured to cost. */
int orblit_renderer_set_batching(orblit_renderer *renderer, int enabled);

/* Whether opaque objects are drawn into depth alone before being shaded.
 * Off unless asked for; see OrblitScene.depthPrepass on the Dart side for
 * what it costs and where it pays. Set before apply_objects, which is where
 * the second entity per object is built. */
int orblit_renderer_set_depth_prepass(orblit_renderer *renderer, int enabled);

/* `count` materials of ORBLIT_STRIDE_MATERIAL floats and
 * ORBLIT_STRIDE_MATERIAL_MAPS map indices each. Maps index `texture_paths`,
 * and `texture_srgb` has one entry per path. `videos` has one per material. */
int orblit_renderer_apply_materials(orblit_renderer *renderer, uint32_t count,
                                   const int64_t *keys, const int32_t *flags,
                                   const float *params, size_t param_floats,
                                   const int32_t *maps, size_t map_count,
                                   const char *const *texture_paths,
                                   const int32_t *texture_srgb,
                                   uint32_t texture_count,
                                   const int32_t *videos);

int orblit_renderer_set_pipeline(orblit_renderer *renderer, const float *params,
                                size_t count);

/* `count` videos of ORBLIT_STRIDE_VIDEO floats; `paths` has one each. Where
 * the platform has no decoder yet, the notes say so and screens draw blank. */
int orblit_renderer_apply_videos(orblit_renderer *renderer, uint32_t count,
                                const int64_t *keys, const int32_t *flags,
                                const float *params, size_t param_floats,
                                const char *const *paths, uint32_t path_count);

/* `count` lights of ORBLIT_STRIDE_LIGHT floats. */
int orblit_renderer_apply_lights(orblit_renderer *renderer, uint32_t count,
                                const int64_t *keys, const int32_t *kinds,
                                const int32_t *flags, const float *params,
                                size_t param_floats);

/* `count` decals of ORBLIT_STRIDE_DECAL floats; `images` index `paths`. */
int orblit_renderer_apply_decals(orblit_renderer *renderer, uint32_t count,
                                const float *params, size_t param_floats,
                                const int32_t *images,
                                const char *const *paths, uint32_t path_count);

int orblit_renderer_set_fog(orblit_renderer *renderer, int enabled,
                           const float *params, size_t count);

int orblit_renderer_set_post_process(orblit_renderer *renderer,
                                    const float *params, size_t count);

/* `count` probes of ORBLIT_STRIDE_PROBE floats. */
int orblit_renderer_apply_probes(orblit_renderer *renderer, uint32_t count,
                                const int64_t *keys, const float *params,
                                size_t param_floats);

/* One field of ORBLIT_STRIDE_FIELD floats, filled from the target `from`. */
int orblit_renderer_apply_field(orblit_renderer *renderer, const float *params,
                               size_t count, const char *from);

/* The scene's environment. `radiance` and `skybox` each name a KTX cubemap
 * cmgen baked (tool/bake_environment.sh), or an equirectangular .hdr or .exr
 * picture the renderer decodes and filters itself over the next few frames;
 * either may be NULL or empty. ORBLIT_STRIDE_ENVIRONMENT floats: intensity in
 * lux, rotation in radians, 1 to draw the backdrop, and the reflection size a
 * picture is filtered at (0 for the device's own choice). A picture that
 * cannot be used, or is still being filtered, is a note under "environment"
 * or "skybox". */
int orblit_renderer_set_environment(orblit_renderer *renderer,
                                   const char *radiance, const char *skybox,
                                   const float *params, size_t count);

/* Passes of ORBLIT_STRIDE_PASS floats, targets of ORBLIT_STRIDE_TARGET, and a
 * name per target. */
int orblit_renderer_set_render_graph(orblit_renderer *renderer,
                                    uint32_t pass_count, const float *passes,
                                    size_t pass_floats, uint32_t target_count,
                                    const float *targets, size_t target_floats,
                                    const char *const *names,
                                    uint32_t name_count);

/* One row of god-ray settings, or none, and distortions end to end. */
int orblit_renderer_set_god_rays(orblit_renderer *renderer,
                                const float *god_rays, size_t god_ray_floats,
                                const float *distortions,
                                size_t distortion_floats);

/* `count` populations. bounds are ORBLIT_STRIDE_POPULATION_BOUNDS floats
 * each; `changed` names the populations whose transforms (16 floats a
 * member) and colours (3) are packed end to end in that order. */
int orblit_renderer_apply_populations(
    orblit_renderer *renderer, uint32_t count, const int32_t *keys,
    const int32_t *counts, const int32_t *meshes, const int32_t *flags,
    const int32_t *revisions, const float *ranges, const float *bounds,
    size_t bound_floats, const char *const *paths, uint32_t path_count,
    const int32_t *changed, uint32_t changed_count, const float *transforms,
    size_t transform_floats, const float *colours, size_t colour_floats);

/* `count` splat clouds of ORBLIT_STRIDE_SPLAT floats and a path each; the
 * `changed` in-memory clouds' records are in `data`, end to end. */
int orblit_renderer_apply_splats(orblit_renderer *renderer, uint32_t count,
                                const int32_t *keys, const int32_t *flags,
                                const int32_t *revisions, const float *params,
                                size_t param_floats, const char *const *paths,
                                uint32_t path_count, const int32_t *changed,
                                const int32_t *changed_counts,
                                uint32_t changed_count, const uint8_t *data,
                                size_t data_length);

/* `count` sprite layers of ORBLIT_STRIDE_SPRITE_LAYER floats each — a
 * column-major transform and an RGBA tint — with an image path ("" for plain
 * white), flags and a draw order each. The `changed` layers' sprites,
 * ORBLIT_STRIDE_SPRITE floats a sprite, are in `records` end to end, in the
 * order `changed` names them; a layer named but not changed keeps the sprites
 * it had, and a layer not named has gone.
 *
 * A sprite is x, y, depth, rotation in radians, width, height, the pivot as a
 * fraction of the size, the rectangle of the image as u0 v0 u1 v1 with v0 at
 * the top, and a linear RGBA colour. Flags: 1 sharp (nearest) sampling, 2
 * vertices snapped to whole pixels, 4 an image of measurements rather than
 * colour, 8 additive. Sprites are unlit, so exposure does not reach them. Layers draw lowest
 * order first, and within a layer in the order given. Refused whole, with
 * ORBLIT_ERROR_LENGTH, when `records` is shorter than the changed counts add
 * up to. */
int orblit_renderer_apply_sprites(orblit_renderer *renderer, uint32_t count,
                                 const int32_t *keys, const int32_t *flags,
                                 const int32_t *orders,
                                 const int32_t *revisions, const float *params,
                                 size_t param_floats, const char *const *paths,
                                 uint32_t path_count, const int32_t *changed,
                                 const int32_t *changed_counts,
                                 uint32_t changed_count, const float *records,
                                 size_t record_floats);

/* Every terrain in the scene, in three arrays read in step. A terrain not
 * named has gone. Empty arrays are no terrain at all.
 *
 * `ints` starts with the terrain count. Each terrain is then
 * ORBLIT_STRIDE_TERRAIN_INTS whole numbers — key, flags (1 casts shadows, 2
 * receives them), region size (a power of two, 16 to 2048), mesh size (even,
 * 16 to 256), levels (1 to 12), the automatic cover's steep and flat sets,
 * the set count (up to 32), the pictures' size, 1 if the pictures are in this
 * message, the triplanar sets as a bit a set, and the region count (up to
 * 256) — followed by ORBLIT_STRIDE_TERRAIN_REGION_INTS a region: x and z in
 * regions, and 1 if its maps are in this message.
 *
 * `floats`, a terrain at a time: ORBLIT_STRIDE_TERRAIN floats — spacing,
 * blend sharpness, and the automatic cover's slope and height falloff — then
 * ORBLIT_STRIDE_TERRAIN_SET a set, the size one copy of its picture covers.
 *
 * `data`, a terrain at a time: its pictures when they came, set-count layers
 * of sRGB albedo with a height in alpha then as many of a normal with a
 * roughness in alpha, RGBA bytes, the pictures' size squared each; then the
 * maps of each region that came, in the order named — region size squared
 * float heights, as many 32-bit cover words, then as many RGBA colours, all
 * little-endian. A region kept from an earlier message is named with 0 and
 * its maps left out.
 *
 * Refused whole: ORBLIT_ERROR_LENGTH when the three arrays are not used up
 * exactly, ORBLIT_ERROR_RANGE when a number is outside its limits or a key or
 * region is named twice. What was accepted but could not be drawn — a region
 * too far from the rest, or one named but never sent — is in the notes. */
int orblit_renderer_apply_terrain(orblit_renderer *renderer,
                                  const int32_t *ints, size_t int_count,
                                  const float *floats, size_t float_count,
                                  const uint8_t *data, size_t data_length);

int orblit_renderer_set_sky(orblit_renderer *renderer, int enabled,
                           const float *params, size_t count);

int orblit_renderer_set_precipitation(orblit_renderer *renderer, int enabled,
                                     const float *params, size_t count);

int orblit_renderer_set_sky_colour(orblit_renderer *renderer,
                                  const float colour[3], float ambient,
                                  int show_body);

/* Where the camera is, and when the host reckons that was, in its own
 * seconds. Any thread. */
int orblit_renderer_set_camera(orblit_renderer *renderer,
                              const float position[3], const float target[3],
                              float field_of_view, int orthographic,
                              float view_height, double at);

int orblit_renderer_set_exposure(orblit_renderer *renderer, float aperture,
                                float shutter, float sensitivity);

/* Objects to outline by key, and ORBLIT_STRIDE_OUTLINE floats of style. */
int orblit_renderer_set_outline(orblit_renderer *renderer, const int64_t *keys,
                               uint32_t count, const float *params,
                               size_t param_floats);

/* ---- What the device can do ---- */

/* The questions orblit_renderer_capability answers. Each is asked once, when
 * the renderer starts, because none of the answers changes mid-session. */
typedef enum orblit_capability {
  /* The OrblitBackend actually drawing, which DEFAULT has been resolved to. */
  ORBLIT_CAPABILITY_BACKEND = 0,
  /* Filament's feature level the device supports, 0 to 3. Below 3 the
   * renderer draws the slim lit surface. */
  ORBLIT_CAPABILITY_FEATURE_LEVEL,
  /* Texels on a side of the largest 2D texture. */
  ORBLIT_CAPABILITY_MAX_TEXTURE_SIZE,
  /* Layers in the largest array texture. */
  ORBLIT_CAPABILITY_MAX_ARRAY_TEXTURE_LAYERS,
  /* ORBLIT_FORMAT_* bits: which block-compressed families can be sampled. */
  ORBLIT_CAPABILITY_COMPRESSED_FORMATS,
  /* 1 if an RGBA16F texture can be sampled and mipmapped, which rendering an
   * environment's levels at run time needs. */
  ORBLIT_CAPABILITY_HALF_FLOAT_TEXTURES,
  /* Threads work can spread across: the cores, or 1 without threading. */
  ORBLIT_CAPABILITY_WORKER_THREADS,
  /* Physical memory in megabytes, or 0 where the platform will not say. */
  ORBLIT_CAPABILITY_SYSTEM_MEMORY_MEGABYTES,
  /* How many questions there are. Not a question. */
  ORBLIT_CAPABILITY_COUNT
} orblit_capability;

/* Block-compressed texture families, as ORBLIT_CAPABILITY_COMPRESSED_FORMATS
 * spells them. A family counts only when its linear and sRGB forms are both
 * there. */
typedef enum orblit_format_family {
  ORBLIT_FORMAT_ETC2 = 1 << 0,  /* ETC2 RGBA8, sRGB and linear */
  ORBLIT_FORMAT_ASTC = 1 << 1,  /* ASTC 4x4 LDR, sRGB and linear */
  ORBLIT_FORMAT_BC1_3 = 1 << 2, /* S3TC / DXT */
  ORBLIT_FORMAT_BC4_5 = 1 << 3, /* RGTC: one and two channels */
  ORBLIT_FORMAT_BC6H = 1 << 4,  /* BPTC half float, unsigned */
  ORBLIT_FORMAT_BC7 = 1 << 5    /* BPTC, sRGB and linear */
} orblit_format_family;

/* One answer about the device `renderer` runs on. -1 for a NULL or stopped
 * renderer, and for a question this build does not know. Any thread. */
int32_t orblit_renderer_capability(const orblit_renderer *renderer,
                                  orblit_capability which);

/* ---- What it drew, and what it cost ---- */

/* Asks for the next frame drawn to be read back into memory. */
int orblit_renderer_request_capture(orblit_renderer *renderer);

/* The last frame read back: RGBA8, top row first. Returns the bytes it
 * takes, and copies them into `rgba` when `capacity` is at least that. Nought
 * until a frame has arrived, which is a frame or two after it was asked for.
 * `width` and `height` may be NULL. */
size_t orblit_renderer_read_capture(orblit_renderer *renderer, uint8_t *rgba,
                                   size_t capacity, uint32_t *width,
                                   uint32_t *height);

typedef struct orblit_stats {
  /* Medians of recent frames, in milliseconds; nought until the backend
   * has reported. */
  double gpu_milliseconds;
  double cpu_milliseconds;
  /* What the last apply_objects batched. */
  uint32_t batched_objects;
  uint32_t batch_groups;
  /* How many passes the last frame ran; see orblit_renderer_pass_timings. */
  uint32_t pass_count;
} orblit_stats;

int orblit_renderer_stats(orblit_renderer *renderer, orblit_stats *stats);

/* What each pass of the last frame cost and drew, up to `capacity` of them.
 * Returns how many there were. Either array may be NULL. */
uint32_t orblit_renderer_pass_timings(orblit_renderer *renderer,
                                     double *milliseconds, int32_t *drawn,
                                     uint32_t capacity);

/* The most recently presented frame, retained, for a PLATFORM surface: a
 * CVPixelBufferRef on Apple. NULL for any other surface. Any thread. */
void *orblit_renderer_copy_presented(orblit_renderer *renderer);

/* ---- What it could not do ---- */

/* Takes a snapshot of what the scene asked for that could not be given, and
 * returns how many notes it holds.
 *
 * Also in it, and not problems: a description of each model file the last
 * apply_objects built something new out of — its clips, skins, material
 * variants, lights and cameras — as JSON, under "orblit.model:" and the path
 * the scene named it by. A host that only wants problems passes those over. */
uint32_t orblit_renderer_notes(orblit_renderer *renderer);

/* One note of the last snapshot: what it is about, and what it says. The
 * strings belong to the renderer and last until the next
 * orblit_renderer_notes or orblit_renderer_destroy. */
int orblit_renderer_note(orblit_renderer *renderer, uint32_t index,
                        const char **about, const char **saying);

#ifdef __cplusplus
}
#endif

#endif /* ORBLIT_RENDERER_H */

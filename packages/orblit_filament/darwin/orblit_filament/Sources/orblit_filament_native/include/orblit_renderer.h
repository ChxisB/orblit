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
  ORBLIT_STRIDE_PIPELINE
} orblit_stride;

/* How wide one row of `which` is, in floats (bytes for a splat record), or
 * nought for a value this build does not know. */
uint32_t orblit_renderer_stride(orblit_stride which);

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
 * meshes index `paths` or are -1 for the built-in cube; flags as
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

/* Cubemap paths as cmgen writes them, either may be NULL or empty, and
 * ORBLIT_STRIDE_ENVIRONMENT floats. */
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
 * returns how many notes it holds. */
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

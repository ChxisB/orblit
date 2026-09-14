/* A console front end, running on a desktop.
 *
 * Everything in this file is the same code on every platform, console
 * included: it owns a frame loop, a scene, a camera and somebody walking
 * about in it, and it reaches the machine it is on only through the eight
 * calls in orblit_host_platform.h and the renderer only through the C ABI in
 * orblit_renderer.h. No Flutter, no Dart, no Objective-C, no SDL header.
 *
 * That is the shape of the claim being made. A port to a console is
 * orblit_host_sdl.c written again against an SDK that cannot be discussed in
 * public, plus a graphics back end in Filament's fork; this file, orblit_pad.c
 * and the whole renderer go across untouched.
 *
 *   orblit_host [metal|vulkan|opengl|webgpu]
 *
 * Or ORBLIT_BACKEND, which the renderer reads itself.
 */

#include "orblit_host_platform.h"
#include "orblit_renderer.h"

#include <math.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#ifndef M_PI
#define M_PI 3.14159265358979323846
#endif

static OrblitBackend backend_named(const char *name) {
  if (name == NULL) return ORBLIT_BACKEND_DEFAULT;
  if (strcmp(name, "metal") == 0) return ORBLIT_BACKEND_METAL;
  if (strcmp(name, "vulkan") == 0) return ORBLIT_BACKEND_VULKAN;
  if (strcmp(name, "opengl") == 0) return ORBLIT_BACKEND_OPENGL;
  if (strcmp(name, "webgpu") == 0) return ORBLIT_BACKEND_WEBGPU;
  return ORBLIT_BACKEND_DEFAULT;
}

static const char *backend_name(OrblitBackend backend) {
  switch (backend) {
    case ORBLIT_BACKEND_METAL: return "Metal";
    case ORBLIT_BACKEND_VULKAN: return "Vulkan";
    case ORBLIT_BACKEND_OPENGL: return "OpenGL";
    case ORBLIT_BACKEND_WEBGPU: return "WebGPU";
    default: return "default";
  }
}

/* A column-major transform: a box of this size, turned about its own upright
 * axis, then moved. The order matters — turning after moving would swing the
 * box round the world's middle instead of its own. */
static void place(float *m, float x, float y, float z, float sx, float sy,
                  float sz, float yaw) {
  const float c = cosf(yaw);
  const float s = sinf(yaw);
  memset(m, 0, 16 * sizeof(float));
  m[0] = c * sx;
  m[2] = -s * sx;
  m[5] = sy;
  m[8] = s * sz;
  m[10] = c * sz;
  m[12] = x;
  m[13] = y;
  m[14] = z;
  m[15] = 1.0f;
}

/* ---- The scene ----
 *
 * A ground, a ring of pillars that rise and fall on their own so the window
 * is never still, and one block that is the player. Every object is the
 * built-in cube: this host exists to prove the loop, not to load a model,
 * and a mesh path would only be one more thing that could be missing.
 */
enum { kPillars = 9, kObjects = kPillars + 2 };

/* Where the ring sits and how big it is. */
static const float kRingRadius = 5.5f;
static const float kGroundY = -0.55f;

int main(int argc, char **argv) {
  const OrblitBackend asked = backend_named(argc > 1 ? argv[1] : NULL);

  if (!orblit_host_open("Orblit", 1280, 720)) return 1;

  int width = 0;
  int height = 0;
  orblit_host_size(&width, &height);

  orblit_surface_desc surface = {ORBLIT_SURFACE_WINDOW,
                                orblit_host_native_window()};
  orblit_renderer *renderer = orblit_renderer_create(
      asked, &surface, (uint32_t)width, (uint32_t)height);
  if (renderer == NULL) {
    fprintf(stderr, "the renderer would not start; the log above says why\n");
    orblit_host_close();
    return 1;
  }
  printf("drawing with %s at %dx%d\n",
         backend_name(orblit_renderer_backend(renderer)), width, height);

  const float sky[3] = {0.30f, 0.45f, 0.70f};
  orblit_renderer_set_sky_colour(renderer, sky, 24000.0f, 1);

  const int64_t sun_key = 1;
  const int32_t sun_kind = 0;
  const int32_t sun_flags = 1;
  const float sun[22] = {1.0f,  0.95f, 0.88f, 100000.0f, 0, 0,     0,
                         -0.45f, -0.8f, -0.55f, 0,       0, 0,     0.53f,
                         0.1f,  10.0f, 80.0f, 0,         0, 0,     0,
                         0};
  orblit_renderer_apply_lights(renderer, 1, &sun_key, &sun_kind, &sun_flags,
                              sun, 22);

  orblit_renderer_set_exposure(renderer, 16.0f, 1.0f / 125.0f, 100.0f);

  int64_t keys[kObjects];
  float transforms[kObjects * 16];
  float colours[kObjects * 3];
  int32_t meshes[kObjects];
  int32_t flags[kObjects];
  int32_t materials[kObjects];
  int32_t shapes[kObjects];
  for (int i = 0; i < kObjects; i++) {
    keys[i] = 100 + i;
    meshes[i] = -1;       /* the built-in cube */
    flags[i] = 1 | 2 | 4; /* casts, receives, drawn */
    materials[i] = -1;
    shapes[i] = 0;
  }
  /* The ground. */
  colours[0] = 0.52f;
  colours[1] = 0.54f;
  colours[2] = 0.50f;
  /* The pillars, round the colour wheel so it is obvious which is which when
   * the camera swings past them. */
  for (int i = 0; i < kPillars; i++) {
    const float t = (float)i / (float)kPillars;
    colours[(i + 1) * 3 + 0] = 0.5f + 0.45f * cosf((float)(2.0 * M_PI) * t);
    colours[(i + 1) * 3 + 1] =
        0.5f + 0.45f * cosf((float)(2.0 * M_PI) * (t + 0.33f));
    colours[(i + 1) * 3 + 2] =
        0.5f + 0.45f * cosf((float)(2.0 * M_PI) * (t + 0.66f));
  }
  /* The player, pale enough to read against any of them. */
  colours[(kObjects - 1) * 3 + 0] = 0.95f;
  colours[(kObjects - 1) * 3 + 1] = 0.93f;
  colours[(kObjects - 1) * 3 + 2] = 0.88f;

  /* Where the player is, which way they are facing, and where the camera is
   * looking from. The camera's own yaw is separate from the player's: the
   * right stick turns the camera, the left stick walks, and walking is in
   * the camera's frame, which is what every third-person game does and what
   * makes a pad feel right. */
  float at_x = 0.0f;
  float at_z = 0.0f;
  float facing = 0.0f;
  float camera_yaw = 0.0f;
  float camera_pitch = 0.42f;
  float camera_distance = 13.0f;
  float height_above = 0.0f; /* A hop, for the south button. */
  float rise = 0.0f;

  double last = orblit_host_seconds();
  double reported = last;
  int frames = 0;
  int running = 1;
  int failed = 0;

  while (running && !failed) {
    int resized = 0;
    running = orblit_host_pump(&resized);
    if (resized) {
      orblit_host_size(&width, &height);
      orblit_renderer_resize(renderer, (uint32_t)width, (uint32_t)height);
    }

    const double now = orblit_host_seconds();
    /* Clamped, because a window dragged between displays or a machine that
     * went to sleep hands back a step long enough to teleport the player
     * through the ring. */
    float dt = (float)(now - last);
    if (dt < 0.0f) dt = 0.0f;
    if (dt > 0.1f) dt = 0.1f;
    last = now;

    const orblit_pad_state *pad = orblit_pad_first(orblit_host_pads());

    /* The right stick turns and tilts the camera; the triggers pull it in
     * and push it out. All four are rates rather than positions, so how long
     * somebody holds the stick is what matters and not how often this loop
     * happens to run. */
    camera_yaw -= pad->stick_x[ORBLIT_PAD_RIGHT] * 2.4f * dt;
    camera_pitch += pad->stick_y[ORBLIT_PAD_RIGHT] * 1.4f * dt;
    if (camera_pitch < -0.2f) camera_pitch = -0.2f;
    if (camera_pitch > 1.3f) camera_pitch = 1.3f;
    camera_distance += (pad->trigger[ORBLIT_PAD_RIGHT] -
                        pad->trigger[ORBLIT_PAD_LEFT]) * 8.0f * dt;
    if (camera_distance < 3.0f) camera_distance = 3.0f;
    if (camera_distance > 24.0f) camera_distance = 24.0f;

    /* The left stick walks, and the d-pad stands in for it, so a pad whose
     * sticks have given up still gets about. orblit_pad_dpad is there for
     * exactly this. */
    float walk_x = pad->stick_x[ORBLIT_PAD_LEFT];
    float walk_y = pad->stick_y[ORBLIT_PAD_LEFT];
    if (walk_x == 0.0f && walk_y == 0.0f) {
      orblit_pad_dpad(pad, &walk_x, &walk_y);
      const float length = sqrtf(walk_x * walk_x + walk_y * walk_y);
      if (length > 1.0f) {
        walk_x /= length;
        walk_y /= length;
      }
    }
    if (walk_x != 0.0f || walk_y != 0.0f) {
      /* In the camera's frame: stick-up is away from the camera. */
      const float c = cosf(camera_yaw);
      const float s = sinf(camera_yaw);
      const float dx = walk_x * c - walk_y * s;
      const float dz = -walk_x * s - walk_y * c;
      const float speed = 6.0f;
      at_x += dx * speed * dt;
      at_z += dz * speed * dt;
      facing = atan2f(dx, dz);
      /* Kept inside the ring, so somebody cannot walk out of the scene and
       * be left looking at nothing and wondering whether it crashed. */
      const float from_middle = sqrtf(at_x * at_x + at_z * at_z);
      const float limit = kRingRadius + 2.5f;
      if (from_middle > limit) {
        at_x *= limit / from_middle;
        at_z *= limit / from_middle;
      }
    }

    /* A hop on the bottom face button, which is A on an Xbox pad, B on a
     * Nintendo one and a cross on a PlayStation one — the whole reason
     * orblit_pad_button names positions. */
    if (orblit_pad_was_pressed(pad, ORBLIT_PAD_SOUTH) && height_above <= 0.0f) {
      rise = 7.0f;
    }
    height_above += rise * dt;
    rise -= 22.0f * dt;
    if (height_above < 0.0f) {
      height_above = 0.0f;
      rise = 0.0f;
    }

    /* The scene, described whole every frame, as every other host of this
     * renderer describes it. */
    const float seconds = (float)now;
    place(transforms, 0.0f, kGroundY, 0.0f, 18.0f, 0.1f, 18.0f, 0.0f);
    for (int i = 0; i < kPillars; i++) {
      const float angle = (float)(2.0 * M_PI) * (float)i / (float)kPillars;
      const float bob = 0.5f + 0.5f * sinf(seconds * 1.3f + angle * 2.0f);
      const float tall = 0.7f + 2.3f * bob;
      place(transforms + (i + 1) * 16, cosf(angle) * kRingRadius,
            kGroundY + tall * 0.5f, sinf(angle) * kRingRadius, 0.8f, tall,
            0.8f, angle + seconds * 0.4f);
    }
    place(transforms + (kObjects - 1) * 16, at_x,
          kGroundY + 0.45f + height_above, at_z, 0.55f, 0.9f, 0.55f, facing);

    if (orblit_renderer_apply_objects(
            renderer, kObjects, keys, transforms,
            sizeof transforms / sizeof(float), colours,
            sizeof colours / sizeof(float), meshes, flags, materials, shapes,
            NULL, 0, NULL, 0) != ORBLIT_OK) {
      fprintf(stderr, "the scene was refused\n");
      failed = 1;
      break;
    }

    const float eye[3] = {
        at_x - sinf(camera_yaw) * cosf(camera_pitch) * camera_distance,
        kGroundY + 1.0f + sinf(camera_pitch) * camera_distance,
        at_z - cosf(camera_yaw) * cosf(camera_pitch) * camera_distance};
    const float look[3] = {at_x, kGroundY + 1.2f + height_above * 0.5f, at_z};
    orblit_renderer_set_camera(renderer, eye, look, 50.0f, 0, 10.0f, now);

    if (orblit_renderer_draw(renderer, now) != ORBLIT_OK) {
      fprintf(stderr, "the frame was refused\n");
      failed = 1;
      break;
    }

    /* Start or menu stops it, so a machine with no keyboard can still be
     * quit from the pad it does have. */
    if (orblit_pad_was_pressed(pad, ORBLIT_PAD_START)) running = 0;

    frames++;
    if (now - reported >= 2.0) {
      orblit_stats stats;
      const double per = (double)frames / (now - reported);
      if (orblit_renderer_stats(renderer, &stats) == ORBLIT_OK) {
        printf("%5.1f fps, cpu %.2f ms, gpu %.2f ms; %d pad(s), %s; "
               "at %.1f, %.1f\n",
               per, stats.cpu_milliseconds, stats.gpu_milliseconds,
               orblit_pad_count(orblit_host_pads()),
               pad->connected ? pad->name : "none", (double)at_x, (double)at_z);
        fflush(stdout);
      }
      frames = 0;
      reported = now;
    }
  }

  /* What the scene asked for and could not have — a missing decal, a video
   * nobody can decode, a surface the machine is too old for. Said once, on
   * the way out, because a host that never looks is a host that never finds
   * out why the picture is wrong. */
  const uint32_t notes = orblit_renderer_notes(renderer);
  for (uint32_t i = 0; i < notes; i++) {
    const char *about = NULL;
    const char *saying = NULL;
    if (orblit_renderer_note(renderer, i, &about, &saying) == ORBLIT_OK) {
      printf("note: %s: %s\n", about, saying);
    }
  }

  orblit_renderer_destroy(renderer);
  orblit_host_close();
  return failed ? 1 : 0;
}

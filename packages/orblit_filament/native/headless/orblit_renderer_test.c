/* The C ABI's own test.
 *
 * C99, and it includes one header: orblit_renderer.h. That is the first half of
 * what it checks — that a host written in plain C can be built against the
 * renderer at all, with nothing of C++, Objective-C or Filament leaking
 * through the header. The second half runs once it is linked: that the ABI
 * refuses what it should, and that a renderer made through it draws. */

#include "orblit_renderer.h"

#include <math.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

static int failures = 0;

static void expect(int holds, const char *what) {
  if (!holds) {
    fprintf(stderr, "FAIL: %s\n", what);
    failures++;
  }
}

/* ---- A capture whose colour is not the same from both sides ----
 *
 * Five splats and first-band spherical harmonics, chosen so that the answer
 * is obvious rather than subtle. Every f_dc is nought, so the flat colour is
 * the mid grey 0.5 + Y00 * 0 from everywhere. Red's third coefficient is -1
 * and blue's is +1, and the third basis function is -sqrt(3/4pi) x — so a
 * splat seen from -x, where the direction towards it is +x, is red, and the
 * same splat seen from +x is blue.
 *
 * Written as the binary little-endian .ply the reference trainer writes,
 * which is the only PLY the renderer reads; the floats go down as this host
 * stores them, and every host this builds on stores them little-endian. */
enum { kTestProperties = 23, kTestSplats = 5 };

static int write_capture(const char *path) {
  static const char *const kNames[kTestProperties] = {
      "x",        "y",        "z",
      "f_dc_0",   "f_dc_1",   "f_dc_2",
      "f_rest_0", "f_rest_1", "f_rest_2",  /* red, all three coefficients */
      "f_rest_3", "f_rest_4", "f_rest_5",  /* then green's */
      "f_rest_6", "f_rest_7", "f_rest_8",  /* then blue's */
      "opacity",  "scale_0",  "scale_1",   "scale_2",
      "rot_0",    "rot_1",    "rot_2",     "rot_3"};
  /* One in the middle and four around it, all in the plane the x axis runs
   * through, so the two views this is checked from are mirror images and the
   * middle pixel is the same in both. */
  static const float kPlaces[kTestSplats][3] = {
      {0.0f, 0.0f, 0.0f},  {0.0f, 0.35f, 0.0f}, {0.0f, -0.35f, 0.0f},
      {0.0f, 0.0f, 0.35f}, {0.0f, 0.0f, -0.35f}};

  FILE *file = fopen(path, "wb");
  if (file == NULL) return 0;
  fprintf(file, "ply\nformat binary_little_endian 1.0\nelement vertex %d\n",
          kTestSplats);
  for (int i = 0; i < kTestProperties; i++) {
    fprintf(file, "property float %s\n", kNames[i]);
  }
  fprintf(file, "end_header\n");

  for (int s = 0; s < kTestSplats; s++) {
    float row[kTestProperties];
    memset(row, 0, sizeof row);
    row[0] = kPlaces[s][0];
    row[1] = kPlaces[s][1];
    row[2] = kPlaces[s][2];
    row[8] = -1.0f;   /* f_rest_2: red's third coefficient */
    row[14] = 1.0f;   /* f_rest_8: blue's */
    row[15] = 6.0f;   /* opacity as a logit, so all but opaque */
    row[16] = row[17] = row[18] = -1.9f;  /* log scales, about 15 cm */
    row[19] = 1.0f;   /* the identity rotation, (w, x, y, z) */
    if (fwrite(row, sizeof(float), kTestProperties, file) != kTestProperties) {
      fclose(file);
      return 0;
    }
  }
  return fclose(file) == 0;
}

/* The brightest channel in the frame last measured, for the diagnostic. */
static uint8_t brightest = 0;

/* A column-major transform: a box scaled about its middle and moved. */
static void place(float *m, float x, float y, float z, float sx, float sy,
                  float sz) {
  memset(m, 0, 16 * sizeof(float));
  m[0] = sx;
  m[5] = sy;
  m[10] = sz;
  m[12] = x;
  m[13] = y;
  m[14] = z;
  m[15] = 1.0f;
}

/* The middle of the frame, over a few pixels rather than one, so a single
 * stray pixel cannot decide the answer. Nought when no frame arrived.
 *
 * Frames to settle, then the capture asked for, then more frames, and the
 * read once at the end. Two traps, and both of them look like a renderer that
 * draws nothing rather than like a mistake here.
 *
 * The settling comes first because a cloud's textures are uploaded through the
 * driver and do not arrive in the frame that asked for them. Capture the next
 * frame after handing over a cloud and the splat texture is still empty: every
 * splat reads a centre of nought and a covariance of nought, which the
 * shader's own low-pass widening turns into a dot about a pixel across at the
 * middle of the screen. The frame that comes back is then black but for one
 * faint pixel, which is exactly what this measured before the settling frames
 * were put in.
 *
 * The read comes last because a read hands back the last frame that arrived
 * and goes on handing it back. A loop that stops at the first non-empty answer
 * stops on the *previous* capture, so every view after the first measures the
 * one before it — two cameras agreeing perfectly about a picture only one of
 * them was in. */
static int middle_colour(orblit_renderer *renderer, double seconds,
                         float rgb[3]) {
  uint32_t width = 0;
  uint32_t height = 0;
  for (int frame = 0; frame < 4; frame++) {
    orblit_renderer_draw(renderer, seconds + frame / 60.0);
  }
  orblit_renderer_request_capture(renderer);
  for (int frame = 4; frame < 10; frame++) {
    orblit_renderer_draw(renderer, seconds + frame / 60.0);
  }
  const size_t bytes =
      orblit_renderer_read_capture(renderer, NULL, 0, &width, &height);
  if (bytes == 0 || width < 8 || height < 8) return 0;

  uint8_t *pixels = malloc(bytes);
  if (pixels == NULL) return 0;
  orblit_renderer_read_capture(renderer, pixels, bytes, &width, &height);

  /* The brightest pixel anywhere in the frame, kept for the diagnostic
   * below. A middle that came out black is two quite different faults — the
   * cloud drawn in the wrong colour, or nothing drawn at all — and this is
   * the one number that tells them apart. */
  brightest = 0;
  for (size_t i = 0; i + 3 < bytes; i += 4) {
    for (int c = 0; c < 3; c++) {
      if (pixels[i + c] > brightest) brightest = pixels[i + c];
    }
  }

  double sum[3] = {0.0, 0.0, 0.0};
  int taken = 0;
  for (uint32_t y = height / 2 - 2; y <= height / 2 + 2; y++) {
    for (uint32_t x = width / 2 - 2; x <= width / 2 + 2; x++) {
      const uint8_t *pixel = pixels + ((size_t)y * width + x) * 4;
      sum[0] += pixel[0];
      sum[1] += pixel[1];
      sum[2] += pixel[2];
      taken++;
    }
  }
  free(pixels);
  for (int c = 0; c < 3; c++) rgb[c] = (float)(sum[c] / taken);
  return 1;
}

/* Draws the capture from the two sides and reports what the middle came out.
 * `degree` is how many spherical-harmonic bands to read, in the flag bits
 * above the one that asks for the sort. */
static int draw_both_ways(orblit_renderer *renderer, const char *path,
                          int32_t degree, float left[3], float right[3]) {
  const int32_t key = 1;
  const int32_t revision = 0;
  const int32_t flags = 1 | (degree << 1);
  const char *paths[1];
  int32_t changed = 0;
  int32_t changed_counts = 0;
  uint8_t nothing = 0;
  float params[18];
  memset(params, 0, sizeof params);
  params[0] = params[5] = params[10] = params[15] = 1.0f;  /* the identity */
  params[16] = 1.0f;  /* opacity */
  params[17] = 1.0f;  /* brightness */
  paths[0] = path;

  if (orblit_renderer_apply_splats(renderer, 1, &key, &flags, &revision, params,
                                  18, paths, 1, &changed, &changed_counts, 0,
                                  &nothing, 0) != ORBLIT_OK) {
    return 0;
  }

  const float look[3] = {0.0f, 0.0f, 0.0f};
  const float from_left[3] = {-1.5f, 0.0f, 0.0f};
  const float from_right[3] = {1.5f, 0.0f, 0.0f};
  orblit_renderer_set_camera(renderer, from_left, look, 42.0f, 0, 10.0f, 0.0);
  if (!middle_colour(renderer, 1.0, left)) return 0;
  orblit_renderer_set_camera(renderer, from_right, look, 42.0f, 0, 10.0f, 0.0);
  return middle_colour(renderer, 2.0, right);
}


/* How many times the graph below is replaced. Any number above one would
 * catch the fault; several make it plain that what leaked scaled with the
 * changes rather than being a single stray object. */
enum { kGraphChanges = 8 };

/* Sets a graph, changes it, and destroys the renderer.
 *
 * An effect pass builds the triangle it draws — a material instance, an
 * entity and a scene of its own — the first time it runs, and keeps them,
 * because the pass runs on every frame. They belong to the pass, so a graph
 * that replaces the pass has to give them back. While it did not, each change
 * orphaned one instance of the effect's material, and nothing said so until
 * the renderer went down: Filament will not destroy a material with an
 * instance of it still alive, and ends the process rather than the frame.
 *
 * So this is a test that has to *reach the end*, not one that reads a value
 * back. It is also the reason to write it here rather than beside it: while
 * teardown aborted, no runtime test could set a graph at all — whatever it
 * was really checking, it died on the way out. */
static void graph_changes_and_goes_down(void) {
  orblit_surface_desc headless = {ORBLIT_SURFACE_HEADLESS, NULL};
  orblit_renderer *renderer =
      orblit_renderer_create(ORBLIT_BACKEND_DEFAULT, &headless, 64, 48);
  /* Said once already by the caller; a host with no device is not a failure. */
  if (renderer == NULL) return;

  /* One target that follows the view — which is what a width and height of
   * nought mean — keeping colour, so that an effect can sample it. */
  const float target[6] = {0.0f, 0.0f, 1.0f, 0.0f, 1.0f, 0.0f};
  const char *const names[1] = {"scene"};

  for (int change = 0; change < kGraphChanges; change++) {
    /* Two passes: the scene into that target, then a sharpen reading it onto
     * the frame. */
    float passes[2 * 13] = {0};
    passes[0] = 0;    /* a scene pass */
    passes[1] = 0;    /* into target nought */
    passes[2] = 127;  /* every layer */
    passes[3] = 1;    /* clearing */
    passes[4] = passes[5] = passes[6] = passes[7] = -1; /* reading nothing */
    passes[12] = -1;  /* no effect */

    passes[13 + 0] = 2;    /* an effect pass */
    passes[13 + 1] = -1;   /* onto the frame */
    passes[13 + 2] = 127;
    passes[13 + 3] = 1;
    passes[13 + 4] = 0;    /* reading target nought */
    passes[13 + 5] = passes[13 + 6] = passes[13 + 7] = -1;
    /* The effect's one dial, moved every time round. A graph identical to the
     * one already set is ignored — deliberately, since a host sends one every
     * frame — so a loop that did not move something would set one graph and
     * test nothing. */
    passes[13 + 8] = 0.2f + 0.05f * (float)change;
    passes[13 + 12] = 0;   /* sharpen */

    expect(orblit_renderer_set_render_graph(renderer, 2, passes, 2 * 13, 1,
                                           target, 6, names, 1) == ORBLIT_OK,
           "a scene pass and an effect are taken as a graph");
    /* Drawn, and not only set: a pass that never runs never builds the
     * triangle whose ownership this is about. */
    expect(orblit_renderer_draw(renderer, change / 60.0) == ORBLIT_OK,
           "a frame of that graph draws");
  }

  /* The whole of the test. Filament ends the process inside here if anything
   * the graph built outlived the graph. */
  orblit_renderer_destroy(renderer);
  printf("a graph changed %d times and the renderer went down cleanly\n",
         kGraphChanges);
}

int main(void) {
  /* Nothing is a handle, and nothing crashes on being given nothing. */
  expect(orblit_renderer_draw(NULL, 0.0) == ORBLIT_ERROR_NULL,
         "a null renderer is refused, not dereferenced");
  orblit_renderer_destroy(NULL);
  expect(orblit_renderer_notes(NULL) == 0, "a null renderer has no notes");
  expect(orblit_renderer_rendered_frames(NULL) == 0, "a null renderer has no frames");

  /* The rows are the renderer's to say, so a host need not copy them. */
  expect(orblit_renderer_stride(ORBLIT_STRIDE_LIGHT) == 22,
         "a light is twenty-two floats");
  expect(orblit_renderer_stride(ORBLIT_STRIDE_MATERIAL) == 37,
         "a material is thirty-seven floats");
  expect(orblit_renderer_stride(ORBLIT_STRIDE_PASS) == 13,
         "a graph pass is thirteen floats");

  /* Bytes by name need no renderer, and refuse what they should. */
  {
    const uint8_t three[3] = {1, 2, 3};
    expect(orblit_renderer_provide_resource(NULL, three, 3) == ORBLIT_ERROR_NULL,
           "a resource needs a name");
    expect(orblit_renderer_provide_resource("orblit:resource/x", NULL, 3) ==
               ORBLIT_ERROR_NULL,
           "and bytes, when it says it has some");
    expect(orblit_renderer_provide_resource("orblit:resource/x", three, 3) ==
               ORBLIT_OK,
           "a resource can be provided");
    expect(orblit_renderer_release_resource("orblit:resource/./x") == ORBLIT_OK,
           "and released under the same name spelled another way");
    expect(orblit_renderer_release_resource("orblit:resource/x") ==
               ORBLIT_ERROR_RANGE,
           "and releasing it twice says there was nothing the second time");
    expect(orblit_renderer_capability(NULL, ORBLIT_CAPABILITY_FEATURE_LEVEL) == -1,
           "a null renderer can do nothing");
  }

  orblit_surface_desc headless = {ORBLIT_SURFACE_HEADLESS, NULL};
  orblit_renderer *renderer =
      orblit_renderer_create(ORBLIT_BACKEND_DEFAULT, &headless, 64, 48);
  if (renderer == NULL) {
    fprintf(stderr, "FAIL: no renderer could start; build-only is not a runtime pass\n");
    return 1;
  }

  /* What the device can do is known as soon as the renderer is. */
  {
    const int32_t level =
        orblit_renderer_capability(renderer, ORBLIT_CAPABILITY_FEATURE_LEVEL);
    const int32_t side =
        orblit_renderer_capability(renderer, ORBLIT_CAPABILITY_MAX_TEXTURE_SIZE);
    const int32_t threads =
        orblit_renderer_capability(renderer, ORBLIT_CAPABILITY_WORKER_THREADS);
    printf("capabilities: backend %d, feature level %d, textures to %d, "
           "formats 0x%x, half float %d, %d threads, %d MB\n",
           orblit_renderer_capability(renderer, ORBLIT_CAPABILITY_BACKEND),
           level, side,
           orblit_renderer_capability(renderer,
                                     ORBLIT_CAPABILITY_COMPRESSED_FORMATS),
           orblit_renderer_capability(renderer,
                                     ORBLIT_CAPABILITY_HALF_FLOAT_TEXTURES),
           threads,
           orblit_renderer_capability(renderer,
                                     ORBLIT_CAPABILITY_SYSTEM_MEMORY_MEGABYTES));
    expect(level >= 0 && level <= 3, "the feature level is Filament's own");
    expect(side >= 2048, "every device this runs on has 2048 textures");
    expect(threads >= 1, "there is at least the thread asking");
    expect(orblit_renderer_capability(renderer, ORBLIT_CAPABILITY_COUNT) == -1,
           "the count is not a question");
  }

  /* A short array is refused, and nothing is applied. */
  {
    const int64_t key = 1;
    const int32_t kind = 0;
    const int32_t flags = 1;
    const float sun[22] = {1.0f, 0.95f, 0.9f, 100000.0f, 0, 0, 0,
                           -0.5f, -1.0f, -0.3f, 0, 0, 0, 0.53f, 0.1f,
                           10.0f, 80.0f, 0, 0, 0, 0, 0};
    expect(orblit_renderer_apply_lights(renderer, 1, &key, &kind, &flags, sun,
                                       21) == ORBLIT_ERROR_LENGTH,
           "a light one float short is refused");
    expect(orblit_renderer_apply_lights(renderer, 1, &key, &kind, &flags, sun,
                                       22) == ORBLIT_OK,
           "a whole light is taken");
    expect(orblit_renderer_apply_lights(renderer, 1, NULL, &kind, &flags, sun,
                                       22) == ORBLIT_ERROR_NULL,
           "a missing array is refused");
  }

  /* A graph that reads a target it does not have is refused by index. */
  {
    float pass[13] = {0};
    pass[1] = -1;           /* into the frame */
    pass[2] = 127;          /* every layer */
    pass[4] = 3;            /* reads target three, of none */
    pass[5] = pass[6] = pass[7] = -1;
    pass[12] = -1;
    expect(orblit_renderer_set_render_graph(renderer, 1, pass, 13, 0, NULL, 0,
                                           NULL, 0) == ORBLIT_ERROR_RANGE,
           "a pass reading a target that is not there is refused");
  }

  /* It draws, and the frame comes back. */
  expect(orblit_renderer_rendered_frames(renderer) == 0,
         "a new renderer has not rendered a frame");
  orblit_renderer_request_capture(renderer);
  for (int frame = 0; frame < 6; frame++) {
    expect(orblit_renderer_draw(renderer, frame / 60.0) == ORBLIT_OK,
           "a frame draws");
  }
  {
    uint32_t width = 0;
    uint32_t height = 0;
    const size_t bytes =
        orblit_renderer_read_capture(renderer, NULL, 0, &width, &height);
    expect(bytes == 64u * 48u * 4u && width == 64 && height == 48,
           "a 64 by 48 frame comes back");
  }
  {
    orblit_stats stats;
    expect(orblit_renderer_stats(renderer, &stats) == ORBLIT_OK,
           "stats are there to read");
  }

  {
    const uint64_t frames = orblit_renderer_rendered_frames(renderer);
    expect(frames > 0 && frames <= 6, "only completed draws count as frames");
    expect(orblit_renderer_detach_surface(renderer) == ORBLIT_OK, "surface detaches");
    expect(orblit_renderer_draw(renderer, 1.0) == ORBLIT_OK, "detached draw is safe");
    expect(orblit_renderer_rendered_frames(renderer) == frames,
           "a successful detached draw is not counted as a rendered frame");
    expect(orblit_renderer_attach_surface(renderer, &headless, 64, 48) == ORBLIT_OK,
           "surface reattaches");
    for (int frame = 0; frame < 6; frame++) {
      expect(orblit_renderer_draw(renderer, 2.0 + frame / 60.0) == ORBLIT_OK,
             "reattached renderer draws");
    }
    expect(orblit_renderer_rendered_frames(renderer) > frames,
           "rendered frame count survives surface replacement");
  }

  /* Batching is on for a renderer nobody has told anything.
   *
   * This is the C ABI's half of the default, and the half no Flutter host
   * exercises: the Dart side always puts `batching` on the wire, so the
   * plugins never fall back and the initial state below is only reached by a
   * host that calls orblit_renderer_set_batching never or late — a console
   * host, a Linux or Windows plugin. Six placeholder cubes alike in mesh,
   * material, colour and flags are a group by the census's rule of four, so
   * the stats say plainly which way the renderer started. */
  {
    const uint32_t count = 6;
    int64_t keys[6];
    float transforms[6 * 16];
    float colours[6 * 3];
    int32_t meshes[6];
    int32_t flags[6];
    int32_t materials[6];
    int32_t morphs[6];
    for (uint32_t i = 0; i < count; i++) {
      keys[i] = (int64_t)(100 + i);
      for (int f = 0; f < 16; f++) transforms[i * 16 + f] = 0.0f;
      transforms[i * 16 + 0] = 1.0f;
      transforms[i * 16 + 5] = 1.0f;
      transforms[i * 16 + 10] = 1.0f;
      transforms[i * 16 + 12] = (float)i * 3.0f; /* spread along x */
      transforms[i * 16 + 15] = 1.0f;
      colours[i * 3 + 0] = 0.5f;
      colours[i * 3 + 1] = 0.4f;
      colours[i * 3 + 2] = 0.3f;
      meshes[i] = -1;    /* the built-in cube */
      materials[i] = -1; /* the default surface, where colour is the material */
      flags[i] = 1 | 2 | 4; /* casts, receives, visible, layer nought */
      morphs[i] = 0;
    }

    orblit_stats stats;
    expect(orblit_renderer_apply_objects(renderer, count, keys, transforms,
                                        count * 16, colours, count * 3, meshes,
                                        flags, materials, morphs, NULL, 0, NULL,
                                        0) == ORBLIT_OK,
           "six identical cubes are taken");
    expect(orblit_renderer_stats(renderer, &stats) == ORBLIT_OK,
           "stats are there to read after publishing objects");
    expect(stats.batched_objects == count && stats.batch_groups == 1,
           "a renderer nobody configured batches: six objects, one group");

    /* And off is reachable, which is the half a flipped default makes easy to
     * lose. Nought batched, and the objects are still all there — batching
     * decides how they are drawn, never whether they exist. */
    expect(orblit_renderer_set_batching(renderer, 0) == ORBLIT_OK,
           "batching can be turned off");
    expect(orblit_renderer_apply_objects(renderer, count, keys, transforms,
                                        count * 16, colours, count * 3, meshes,
                                        flags, materials, morphs, NULL, 0, NULL,
                                        0) == ORBLIT_OK,
           "the same six cubes are taken again");
    expect(orblit_renderer_stats(renderer, &stats) == ORBLIT_OK,
           "stats are there to read with batching off");
    expect(stats.batched_objects == 0 && stats.batch_groups == 0,
           "nothing is batched once batching is turned off");

    /* Back on by asking, not only by never having asked. */
    expect(orblit_renderer_set_batching(renderer, 1) == ORBLIT_OK,
           "batching can be turned on again");
    expect(orblit_renderer_apply_objects(renderer, count, keys, transforms,
                                        count * 16, colours, count * 3, meshes,
                                        flags, materials, morphs, NULL, 0, NULL,
                                        0) == ORBLIT_OK,
           "the same six cubes are taken a third time");
    expect(orblit_renderer_stats(renderer, &stats) == ORBLIT_OK,
           "stats are there to read with batching on again");
    expect(stats.batched_objects == count && stats.batch_groups == 1,
           "the group comes back when batching is turned back on");

    /* Capture rows must be top-first on every backend. A red cube below
     * the camera's aim leaves blue sky above it: an asymmetric fixture
     * catches an extra OpenGL row flip that a centred cube cannot. */
    transforms[13] = -1.0f;
    colours[0] = 1.0f;
    colours[1] = 0.02f;
    colours[2] = 0.01f;
    expect(orblit_renderer_apply_objects(renderer, 1, keys, transforms, 16,
                                        colours, 3, meshes, flags, materials,
                                        morphs, NULL, 0, NULL, 0) == ORBLIT_OK,
           "an asymmetric capture fixture is taken");
    const float sky[3] = {0.1f, 0.2f, 0.8f};
    const float eye[3] = {0, 0, 6};
    const float look[3] = {0, 0, 0};
    orblit_renderer_set_sky_colour(renderer, sky, 20000.0f, 1);
    orblit_renderer_set_camera(renderer, eye, look, 42.0f, 0, 10.0f, 0.0);
    orblit_renderer_set_exposure(renderer, 16.0f, 1.0f / 125.0f, 100.0f);
    for (int frame = 0; frame < 6; frame++) {
      orblit_renderer_request_capture(renderer);
      expect(orblit_renderer_draw(renderer, 3.0 + frame / 60.0) == ORBLIT_OK,
             "the asymmetric fixture draws");
    }
    uint8_t pixels[64 * 48 * 4];
    uint32_t width = 0, height = 0;
    const size_t bytes = orblit_renderer_read_capture(
        renderer, pixels, sizeof pixels, &width, &height);
    expect(bytes == sizeof pixels && width == 64 && height == 48,
           "the asymmetric fixture is captured");
    if (bytes == sizeof pixels && width == 64 && height == 48) {
      const uint8_t *top = pixels + (12 * 64 + 32) * 4;
      const uint8_t *bottom = pixels + (36 * 64 + 32) * 4;
      expect(top[2] > top[0] + 10, "blue sky is above the cube in capture");
      expect(bottom[0] > bottom[2] + 10, "red cube is below the sky in capture");
    }
  }

  /* A capture's spherical harmonics, in the pixels.
   *
   * The one check in this file that measures what was drawn rather than what
   * the ABI refused. It belongs here all the same: view-dependent colour is a
   * claim about what reaches the screen, and the only place such a claim can
   * be settled is a frame. The same five splats are drawn from two sides, and
   * then drawn again with their bands left out — so one run says both that
   * the colour moves with the view and that nothing moves without them. */
  {
    const char *directory = getenv("TMPDIR");
    const int slashed = directory != NULL && directory[0] != '\0' &&
                        directory[strlen(directory) - 1] == '/';
    char path[1024];
    snprintf(path, sizeof path, "%s%sorblit_splat_harmonics_test.ply",
             directory != NULL ? directory : "/tmp", slashed ? "" : "/");

    if (!write_capture(path)) {
      expect(0, "the test's own capture could be written");
    } else {
      /* Nothing behind them, so the middle of the frame is the splats rather
       * than a sky washing through what they did not quite cover. */
      /* What comes back before any of this touches the scene.
       *
       * A renderer drawing nothing at all and a renderer drawing the cloud
       * wrongly are the same black frame, and this is the number that tells
       * them apart: an empty scene still has the placeholder the renderer
       * puts up, lit by the sun applied further up, so this should not be
       * black. */
      float baseline[3] = {0.0f, 0.0f, 0.0f};
      middle_colour(renderer, 0.5, baseline);
      const unsigned before = brightest;

      /* Bigger than the 64 by 48 the checks above are content with: a three
       * sigma ellipse a few pixels across is mostly the shader's own low-pass
       * widening, and measuring that is not measuring colour. */
      orblit_renderer_resize(renderer, 512, 384);

      /* A wall behind each of the two viewpoints, so that a frame with no
       * cloud in it is not the same picture as a frame with a black cloud in
       * it. The middle of the frame is then the cloud over a wall, and the
       * wall is what the measurement falls back to if the cloud is not drawn
       * at all — which is the difference between a colour that came out wrong
       * and a colour that never arrived. */
      {
        const int64_t walls[2] = {200, 201};
        float transforms[32];
        const float greens[6] = {0.10f, 0.35f, 0.12f, 0.10f, 0.35f, 0.12f};
        const int32_t meshes[2] = {-1, -1};
        const int32_t drawn[2] = {1 | 2 | 4, 1 | 2 | 4};
        const int32_t materials[2] = {-1, -1};
        const int32_t shapes[2] = {0, 0};
        place(transforms, 3.0f, 0.0f, 0.0f, 0.1f, 2.0f, 2.0f);
        place(transforms + 16, -3.0f, 0.0f, 0.0f, 0.1f, 2.0f, 2.0f);
        expect(orblit_renderer_apply_objects(renderer, 2, walls, transforms, 32,
                                            greens, 6, meshes, drawn, materials,
                                            shapes, NULL, 0, NULL,
                                            0) == ORBLIT_OK,
               "the walls behind the cloud are taken");
      }

      /* The walls alone, from the first of the two viewpoints, with the sky
       * and the exposure still as the renderer had them. Another number for
       * the diagnostic: if this is black then the camera or the light is
       * wrong and nothing below means anything, and if it is not then what
       * follows is about the cloud. */
      {
        const float look[3] = {0.0f, 0.0f, 0.0f};
        const float from_left[3] = {-1.5f, 0.0f, 0.0f};
        orblit_renderer_set_camera(renderer, from_left, look, 42.0f, 0, 10.0f,
                                  0.0);
      }
      float walls[3] = {0.0f, 0.0f, 0.0f};
      middle_colour(renderer, 0.7, walls);
      const unsigned with_walls = brightest;

      /* Now the light goes out, and the sky with it.
       *
       * The walls stay, for two reasons. They are what makes the scene the
       * host's rather than the renderer's, and the placeholder the renderer
       * puts up for a scene nobody has claimed is a solid cube spanning
       * [-1, 1] — exactly where this cloud stands, so it hid the cloud
       * completely and this measured a black frame. And they are the backstop
       * above: a frame with nothing in it and a frame with a black cloud in
       * it are the same picture without them.
       *
       * But they cannot be lit while the cloud is being measured. A splat is
       * 99% opaque at its centre and no more, and 1% of a sunlit surface —
       * tens of thousands of times brighter than an unlit cloud — is enough
       * to wash the middle of the frame white, which is what the view from
       * +x came back as. So the same sun is applied again with nothing in
       * it, and the walls go black. */
      {
        const int64_t sun = 1;
        const int32_t directional = 0;
        const int32_t lit = 1;
        const float dark[22] = {1.0f, 1.0f,  1.0f,  0.0f, 0,     0, 0, -0.5f,
                                -1.0f, -0.3f, 0,     0,    0,     0.53f, 0.1f,
                                10.0f, 80.0f, 0,     0,    0,     0,     0};
        expect(orblit_renderer_apply_lights(renderer, 1, &sun, &directional,
                                           &lit, dark, 22) == ORBLIT_OK,
               "the sun can be turned down for the measurement");
      }

      const float black[3] = {0.0f, 0.0f, 0.0f};
      orblit_renderer_set_sky_colour(renderer, black, 0.0f, 0);
      /* An exposure of about one. Splats are unlit — a capture's colours are
       * the photographs' own — but the camera's exposure still multiplies
       * them, and the daylight exposure a scene with a sun in it wants
       * (f/16 at a 125th) takes a mid grey down to a single level of 255.
       * That is not a wrong colour, it is no colour at all, and it is what
       * this measured before the exposure was set here. */
      orblit_renderer_set_exposure(renderer, 1.0f, 1.0f, 100.0f);

      float left[3] = {0.0f, 0.0f, 0.0f};
      float right[3] = {0.0f, 0.0f, 0.0f};
      float flat_left[3] = {0.0f, 0.0f, 0.0f};
      float flat_right[3] = {0.0f, 0.0f, 0.0f};
      const int drew = draw_both_ways(renderer, path, 1, left, right);
      const int drew_flat =
          draw_both_ways(renderer, path, 0, flat_left, flat_right);
      expect(drew, "a capture with harmonics draws from both sides");
      expect(drew_flat, "and again with its bands left out");

      /* What it measured, printed whether or not it passed: a colour that
       * came out wrong is a number somebody has to see to work out why. */
      printf("splat harmonics: from -x %.0f/%.0f/%.0f, from +x %.0f/%.0f/%.0f;"
             " with no bands %.0f/%.0f/%.0f and %.0f/%.0f/%.0f"
             " (brightest pixel before any of this %u, with the walls up %u"
             " and %.0f/%.0f/%.0f in the middle of them, in the last frame"
             " %u)\n",
             (double)left[0], (double)left[1], (double)left[2],
             (double)right[0], (double)right[1], (double)right[2],
             (double)flat_left[0], (double)flat_left[1], (double)flat_left[2],
             (double)flat_right[0], (double)flat_right[1],
             (double)flat_right[2], before, with_walls, (double)walls[0],
             (double)walls[1], (double)walls[2], (unsigned)brightest);

      if (drew && drew_flat) {
        /* Seen from -x the direction towards the splats is +x, where red's
         * coefficient adds and blue's takes away; from +x it is the other way
         * about. The margin is far beyond what the quantisation, the dither
         * or the tone curve could account for. */
        expect(left[0] > left[2] + 24.0f, "from one side the cloud is red");
        expect(right[2] > right[0] + 24.0f,
               "from the other side the same cloud is blue");

        /* With no bands read it is one colour from everywhere: the picture
         * this drew before it could read them at all. Two levels rather than
         * none because the two views are mirror images, not one image. */
        expect(fabsf(flat_left[0] - flat_right[0]) <= 2.0f &&
                   fabsf(flat_left[1] - flat_right[1]) <= 2.0f &&
                   fabsf(flat_left[2] - flat_right[2]) <= 2.0f,
               "with no bands read the colour is the same from both sides");

        /* And the bands moved it off that colour in both directions, which
         * is the same splats drawn twice: nothing else could have. */
        expect(left[0] > flat_left[0] + 24.0f &&
                   right[0] + 24.0f < flat_right[0],
               "the bands move the colour away from the flat one, each way");
      }
      /* The same capture again, handed over as bytes under a name nothing on
       * disk has — as a browser, an APK or a download hands it over — and
       * looked for by a spelling of that name with a detour in it. It is the
       * same splats, so it is the same picture. */
      {
        FILE *file = fopen(path, "rb");
        long length = -1;
        uint8_t *bytes = NULL;
        if (file != NULL && fseek(file, 0, SEEK_END) == 0) {
          length = ftell(file);
          rewind(file);
        }
        if (length > 0) bytes = (uint8_t *)malloc((size_t)length);
        const int read_back = bytes != NULL &&
                              fread(bytes, 1, (size_t)length, file) ==
                                  (size_t)length;
        if (file != NULL) fclose(file);
        expect(read_back, "the capture reads back off disk");
        if (read_back) {
          expect(orblit_renderer_provide_resource(
                     "orblit:resource/captures/five.ply", bytes,
                     (size_t)length) == ORBLIT_OK,
                 "a capture can be provided as bytes");
          free(bytes);
          float named_left[3] = {0.0f, 0.0f, 0.0f};
          float named_right[3] = {0.0f, 0.0f, 0.0f};
          const int drew_named = draw_both_ways(
              renderer, "orblit:resource/captures/../captures/five.ply", 1,
              named_left, named_right);
          expect(drew_named, "a capture provided as bytes draws");
          if (drew && drew_named) {
            expect(fabsf(named_left[0] - left[0]) <= 2.0f &&
                       fabsf(named_left[2] - left[2]) <= 2.0f &&
                       fabsf(named_right[0] - right[0]) <= 2.0f &&
                       fabsf(named_right[2] - right[2]) <= 2.0f,
                   "and draws what the file on disk drew");
          }
          expect(orblit_renderer_release_resource(
                     "orblit:resource/captures/five.ply") == ORBLIT_OK,
                 "and can be let go of afterwards");
        } else {
          free(bytes);
        }
      }
      remove(path);
    }
  }

  orblit_renderer_destroy(renderer);

  /* Its own renderer, because what it checks is the teardown. */
  graph_changes_and_goes_down();

  if (failures == 0) printf("the C ABI refuses what it should and draws\n");
  return failures == 0 ? 0 : 1;
}

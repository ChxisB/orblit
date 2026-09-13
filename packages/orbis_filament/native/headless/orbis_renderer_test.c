/* The C ABI's own test.
 *
 * C99, and it includes one header: orbis_renderer.h. That is the first half of
 * what it checks — that a host written in plain C can be built against the
 * renderer at all, with nothing of C++, Objective-C or Filament leaking
 * through the header. The second half runs once it is linked: that the ABI
 * refuses what it should, and that a renderer made through it draws. */

#include "orbis_renderer.h"

#include <stdio.h>

static int failures = 0;

static void expect(int holds, const char *what) {
  if (!holds) {
    fprintf(stderr, "FAIL: %s\n", what);
    failures++;
  }
}

int main(void) {
  /* Nothing is a handle, and nothing crashes on being given nothing. */
  expect(orbis_renderer_draw(NULL, 0.0) == ORBIS_ERROR_NULL,
         "a null renderer is refused, not dereferenced");
  orbis_renderer_destroy(NULL);
  expect(orbis_renderer_notes(NULL) == 0, "a null renderer has no notes");

  /* The rows are the renderer's to say, so a host need not copy them. */
  expect(orbis_renderer_stride(ORBIS_STRIDE_LIGHT) == 22,
         "a light is twenty-two floats");
  expect(orbis_renderer_stride(ORBIS_STRIDE_MATERIAL) == 37,
         "a material is thirty-seven floats");
  expect(orbis_renderer_stride(ORBIS_STRIDE_PASS) == 13,
         "a graph pass is thirteen floats");

  orbis_surface_desc headless = {ORBIS_SURFACE_HEADLESS, NULL};
  orbis_renderer *renderer =
      orbis_renderer_create(ORBIS_BACKEND_DEFAULT, &headless, 64, 48);
  if (renderer == NULL) {
    printf("no renderer could start here; the ABI compiled and linked\n");
    return failures == 0 ? 0 : 1;
  }

  /* A short array is refused, and nothing is applied. */
  {
    const int64_t key = 1;
    const int32_t kind = 0;
    const int32_t flags = 1;
    const float sun[22] = {1.0f, 0.95f, 0.9f, 100000.0f, 0, 0, 0,
                           -0.5f, -1.0f, -0.3f, 0, 0, 0, 0.53f, 0.1f,
                           10.0f, 80.0f, 0, 0, 0, 0, 0};
    expect(orbis_renderer_apply_lights(renderer, 1, &key, &kind, &flags, sun,
                                       21) == ORBIS_ERROR_LENGTH,
           "a light one float short is refused");
    expect(orbis_renderer_apply_lights(renderer, 1, &key, &kind, &flags, sun,
                                       22) == ORBIS_OK,
           "a whole light is taken");
    expect(orbis_renderer_apply_lights(renderer, 1, NULL, &kind, &flags, sun,
                                       22) == ORBIS_ERROR_NULL,
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
    expect(orbis_renderer_set_render_graph(renderer, 1, pass, 13, 0, NULL, 0,
                                           NULL, 0) == ORBIS_ERROR_RANGE,
           "a pass reading a target that is not there is refused");
  }

  /* It draws, and the frame comes back. */
  orbis_renderer_request_capture(renderer);
  for (int frame = 0; frame < 6; frame++) {
    expect(orbis_renderer_draw(renderer, frame / 60.0) == ORBIS_OK,
           "a frame draws");
  }
  {
    uint32_t width = 0;
    uint32_t height = 0;
    const size_t bytes =
        orbis_renderer_read_capture(renderer, NULL, 0, &width, &height);
    expect(bytes == 64u * 48u * 4u && width == 64 && height == 48,
           "a 64 by 48 frame comes back");
  }
  {
    orbis_stats stats;
    expect(orbis_renderer_stats(renderer, &stats) == ORBIS_OK,
           "stats are there to read");
  }

  /* Batching is on for a renderer nobody has told anything.
   *
   * This is the C ABI's half of the default, and the half no Flutter host
   * exercises: the Dart side always puts `batching` on the wire, so the
   * plugins never fall back and the initial state below is only reached by a
   * host that calls orbis_renderer_set_batching never or late — a console
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

    orbis_stats stats;
    expect(orbis_renderer_apply_objects(renderer, count, keys, transforms,
                                        count * 16, colours, count * 3, meshes,
                                        flags, materials, morphs, NULL, 0, NULL,
                                        0) == ORBIS_OK,
           "six identical cubes are taken");
    expect(orbis_renderer_stats(renderer, &stats) == ORBIS_OK,
           "stats are there to read after publishing objects");
    expect(stats.batched_objects == count && stats.batch_groups == 1,
           "a renderer nobody configured batches: six objects, one group");

    /* And off is reachable, which is the half a flipped default makes easy to
     * lose. Nought batched, and the objects are still all there — batching
     * decides how they are drawn, never whether they exist. */
    expect(orbis_renderer_set_batching(renderer, 0) == ORBIS_OK,
           "batching can be turned off");
    expect(orbis_renderer_apply_objects(renderer, count, keys, transforms,
                                        count * 16, colours, count * 3, meshes,
                                        flags, materials, morphs, NULL, 0, NULL,
                                        0) == ORBIS_OK,
           "the same six cubes are taken again");
    expect(orbis_renderer_stats(renderer, &stats) == ORBIS_OK,
           "stats are there to read with batching off");
    expect(stats.batched_objects == 0 && stats.batch_groups == 0,
           "nothing is batched once batching is turned off");

    /* Back on by asking, not only by never having asked. */
    expect(orbis_renderer_set_batching(renderer, 1) == ORBIS_OK,
           "batching can be turned on again");
    expect(orbis_renderer_apply_objects(renderer, count, keys, transforms,
                                        count * 16, colours, count * 3, meshes,
                                        flags, materials, morphs, NULL, 0, NULL,
                                        0) == ORBIS_OK,
           "the same six cubes are taken a third time");
    expect(orbis_renderer_stats(renderer, &stats) == ORBIS_OK,
           "stats are there to read with batching on again");
    expect(stats.batched_objects == count && stats.batch_groups == 1,
           "the group comes back when batching is turned back on");
  }

  orbis_renderer_destroy(renderer);
  if (failures == 0) printf("the C ABI refuses what it should and draws\n");
  return failures == 0 ? 0 : 1;
}

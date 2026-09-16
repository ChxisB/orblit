/* Models out of files, checked through the C ABI against Khronos's samples.
 *
 * What a file holds is reported, a file's own clips play and hold still on a
 * held clock, a pose that is taken away leaves nothing behind, a material
 * variant is worn and taken off again, and a joint set by hand moves the
 * mesh. Each is measured in pixels or in the renderer's own description of the
 * file, never assumed from a call having returned.
 *
 * The samples are not in the repository — they are Khronos's to publish, and
 * several megabytes — so this reads them from ORBLIT_SAMPLES and says which
 * it skipped:
 *
 *   ORBLIT_SAMPLES=/path/to/dir build/orblit_models_check
 *
 * where the directory holds Fox.glb, CesiumMan.glb, RiggedSimple.glb,
 * MaterialsVariantsShoe.glb, LightsPunctualLamp.glb, AnisotropyBarnLamp.glb,
 * ClearCoatTest.glb and SheenChair.glb, as glTF-Sample-Assets names them. */

/* For nanosleep: textures decode on Filament's own threads, in wall-clock
 * time, however many frames are drawn meanwhile. */
#define _POSIX_C_SOURCE 200809L

#include "orblit_renderer.h"

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <time.h>

enum { kWidth = 192, kHeight = 144 };

static int failures = 0;
static int skipped = 0;

static void expect(int holds, const char *what) {
  if (!holds) {
    fprintf(stderr, "FAIL: %s\n", what);
    failures++;
  }
}

/* Reads a sample and provides it under a name of its own, written into
 * `name`. Null when the sample is not there, which is a skip rather than a
 * failure. */
static const char *provide_into(const char *file, char *name, size_t room) {
  const char *dir = getenv("ORBLIT_SAMPLES");
  if (dir == NULL) return NULL;
  char path[1024];
  snprintf(path, sizeof path, "%s/%s", dir, file);
  FILE *in = fopen(path, "rb");
  if (in == NULL) {
    printf("skipped %s: not in ORBLIT_SAMPLES\n", file);
    skipped++;
    return NULL;
  }
  fseek(in, 0, SEEK_END);
  const long size = ftell(in);
  fseek(in, 0, SEEK_SET);
  uint8_t *bytes = malloc((size_t)size);
  const int read =
      bytes != NULL && fread(bytes, 1, (size_t)size, in) == (size_t)size;
  fclose(in);
  snprintf(name, room, "orblit:resource/samples/%s", file);
  const int provided =
      read && orblit_renderer_provide_resource(name, bytes, (size_t)size) ==
                  ORBLIT_OK;
  free(bytes);
  return provided ? name : NULL;
}

static const char *provide(const char *file) {
  static char name[256];
  return provide_into(file, name, sizeof name);
}

/* One object naming `path`, at `transform`. */
static int one_object(orblit_renderer *renderer, const char *path,
                      const float transform[16]) {
  const int64_t key = 1;
  const float grey[3] = {0.8f, 0.8f, 0.8f};
  const int32_t mesh = 0;
  const int32_t flags = 7;
  const int32_t material = -1;
  const int32_t morphs = 0;
  const float weight = 0.0f;
  const char *paths[1] = {path};
  return orblit_renderer_apply_objects(renderer, 1, &key, transform, 16, grey,
                                      3, &mesh, &flags, &material, &morphs,
                                      &weight, 0, paths, 1) == ORBLIT_OK;
}

/* A pose for that object: a clip at a time, a variant, and joints. */
static int pose(orblit_renderer *renderer, int32_t clip, float seconds,
                int32_t variant, int32_t joints, const int32_t *which,
                const float *transforms, double at) {
  const int64_t key = 1;
  const int32_t ints[4] = {clip, -1, 1, variant};
  const float floats[5] = {seconds, 1.0f, 0.0f, 0.0f, 1.0f};
  const int32_t zero[2] = {0, 0};
  const float none[16] = {0};
  return orblit_renderer_apply_poses(
             renderer, 1, &key, ints, 4, floats, 5, &joints,
             joints > 0 ? which : zero, (size_t)joints * 2,
             joints > 0 ? transforms : none, (size_t)joints * 16, at) ==
         ORBLIT_OK;
}

/* A clip fading in from another, and whether each loops. */
static int fading(orblit_renderer *renderer, int32_t clip, float seconds,
                  int32_t from, float from_seconds, float fade, int32_t flags,
                  double at) {
  const int64_t key = 1;
  const int32_t ints[4] = {clip, from, flags, -1};
  const float floats[5] = {seconds, 1.0f, from_seconds, 1.0f, fade};
  const int32_t none = 0;
  const int32_t zero[2] = {0, 0};
  const float identity[16] = {0};
  return orblit_renderer_apply_poses(renderer, 1, &key, ints, 4, floats, 5,
                                    &none, zero, 0, identity, 0, at) ==
         ORBLIT_OK;
}

/* No poses at all: everything back as its file has it. */
static int unpose(orblit_renderer *renderer, double at) {
  return orblit_renderer_apply_poses(renderer, 0, NULL, NULL, 0, NULL, 0, NULL,
                                    NULL, 0, NULL, 0, at) == ORBLIT_OK;
}

/* A whole frame from `eye` towards `look`, after enough frames for shadows to
 * settle, with the clock held at `at`. Freed by the caller. */
static uint8_t *frame_from(orblit_renderer *renderer, const float eye[3],
                           const float look[3], double at) {
  orblit_renderer_set_camera(renderer, eye, look, 45.0f, 0, 10.0f, at);
  for (int i = 0; i < 12; i++) orblit_renderer_draw(renderer, 1.0);
  orblit_renderer_request_capture(renderer);
  for (int i = 0; i < 6; i++) orblit_renderer_draw(renderer, 1.0);
  uint32_t width = 0;
  uint32_t height = 0;
  const size_t bytes =
      orblit_renderer_read_capture(renderer, NULL, 0, &width, &height);
  if (bytes == 0) return NULL;
  uint8_t *pixels = malloc(bytes);
  if (pixels != NULL) {
    orblit_renderer_read_capture(renderer, pixels, bytes, &width, &height);
  }
  return pixels;
}

static uint8_t *frame(orblit_renderer *renderer, double at) {
  const float eye[3] = {0.0f, 0.9f, 3.2f};
  const float look[3] = {0.0f, 0.7f, 0.0f};
  return frame_from(renderer, eye, look, at);
}

/* Writes a frame as a PPM into ORBLIT_CHECK_DUMP, when it is set, so what was
 * measured can be looked at. */
static void dump(const uint8_t *pixels, const char *name) {
  const char *dir = getenv("ORBLIT_CHECK_DUMP");
  if (dir == NULL || pixels == NULL) return;
  char path[1024];
  snprintf(path, sizeof path, "%s/%s.ppm", dir, name);
  FILE *out = fopen(path, "wb");
  if (out == NULL) return;
  fprintf(out, "P6\n%d %d\n255\n", kWidth, kHeight);
  for (size_t i = 0; i < (size_t)kWidth * kHeight; i++) {
    fwrite(pixels + i * 4, 1, 3, out);
  }
  fclose(out);
}

/* The largest difference in any channel of any pixel of two frames. */
static int largest(const uint8_t *a, const uint8_t *b) {
  if (a == NULL || b == NULL) return 255;
  int most = 0;
  for (size_t i = 0; i < (size_t)kWidth * kHeight * 4; i++) {
    if (i % 4 == 3) continue;
    const int d = abs((int)a[i] - (int)b[i]);
    if (d > most) most = d;
  }
  return most;
}

/* How many pixels of two frames differ by more than `by` in any channel, or
 * -1 when either frame is missing. */
static int differing(const uint8_t *a, const uint8_t *b, int by) {
  if (a == NULL || b == NULL) return -1;
  int count = 0;
  for (size_t i = 0; i < (size_t)kWidth * kHeight; i++) {
    for (int c = 0; c < 3; c++) {
      const int d = (int)a[i * 4 + c] - (int)b[i * 4 + c];
      if (d > by || d < -by) {
        count++;
        break;
      }
    }
  }
  return count;
}

/* The description the renderer gave of the model at `path`, or null. The
 * string belongs to the renderer until its next snapshot. */
static const char *described(orblit_renderer *renderer, const char *path) {
  char key[512];
  snprintf(key, sizeof key, "orblit.model:%s", path);
  const uint32_t count = orblit_renderer_notes(renderer);
  for (uint32_t i = 0; i < count; i++) {
    const char *about = NULL;
    const char *saying = NULL;
    if (orblit_renderer_note(renderer, i, &about, &saying) == ORBLIT_OK &&
        about != NULL && strcmp(about, key) == 0) {
      return saying;
    }
  }
  return NULL;
}

static int noted(orblit_renderer *renderer, const char *about_part,
                 const char *saying_part) {
  const uint32_t count = orblit_renderer_notes(renderer);
  for (uint32_t i = 0; i < count; i++) {
    const char *about = NULL;
    const char *saying = NULL;
    if (orblit_renderer_note(renderer, i, &about, &saying) == ORBLIT_OK &&
        about != NULL && saying != NULL && strstr(about, about_part) != NULL &&
        strstr(saying, saying_part) != NULL) {
      return 1;
    }
  }
  return 0;
}

/* A renderer whose frames can be compared exactly.
 *
 * Dithering is temporal — a different pattern of noise every frame — and so
 * is temporal anti-aliasing, so with either on no two frames are the same
 * however still the scene. Both off, and a frame depends on the scene alone. */
static orblit_renderer *start(void) {
  orblit_surface_desc headless = {ORBLIT_SURFACE_HEADLESS, NULL};
  orblit_renderer *renderer = orblit_renderer_create(
      ORBLIT_BACKEND_DEFAULT, &headless, kWidth, kHeight);
  if (renderer != NULL) {
    float post[49];
    memset(post, 0, sizeof post);
    post[0] = 1.0f; /* post-processing on, for tone mapping; nothing else */
    orblit_renderer_set_post_process(renderer, post, 49);
  }
  return renderer;
}

/* Draws for a second and a half of wall-clock time, so a model's textures
 * have arrived before anything is measured against them. */
static void settle(orblit_renderer *renderer) {
  const struct timespec pause = {0, 25 * 1000 * 1000};
  for (int i = 0; i < 60; i++) {
    orblit_renderer_draw(renderer, 1.0);
    nanosleep(&pause, NULL);
  }
}

static const float kIdentity[16] = {1, 0, 0, 0, 0, 1, 0, 0,
                                    0, 0, 1, 0, 0, 0, 0, 1};

/* The fox is modelled in centimetres, about a hundred and fifty of them nose
 * to tail; scaled to a hundredth or so it stands in the frame. */
static const float kFoxScale[16] = {0.012f, 0, 0, 0, 0, 0.012f, 0, 0,
                                    0, 0, 0.012f, 0, 0, 0, 0, 1};

static void clips_are_described_and_play(void) {
  const char *fox = provide("Fox.glb");
  if (fox == NULL) return;
  orblit_renderer *renderer = start();
  expect(renderer != NULL, "a renderer starts for the fox");
  if (renderer == NULL) return;

  expect(one_object(renderer, fox, kFoxScale), "the fox is named");
  const char *info = described(renderer, fox);
  expect(info != NULL, "building the fox describes it");
  if (info != NULL) {
    printf("Fox: %.160s...\n", info);
    expect(strstr(info, "\"name\":\"Survey\"") != NULL &&
               strstr(info, "\"name\":\"Walk\"") != NULL &&
               strstr(info, "\"name\":\"Run\"") != NULL,
           "the fox's three clips are named");
    expect(strstr(info, "\"joints\":[") != NULL, "and its skin is listed");
  }
  /* Described when it was built, and not on every publish after. */
  expect(one_object(renderer, fox, kFoxScale), "the fox is named again");
  expect(described(renderer, fox) == NULL,
         "a publish that builds nothing new describes nothing");

  settle(renderer);
  uint8_t *rest = frame(renderer, 1.0);
  expect(pose(renderer, 2, 0.25f, -1, 0, NULL, NULL, 1.0), "the fox runs");
  uint8_t *running = frame(renderer, 1.0);
  uint8_t *again = frame(renderer, 1.0);
  expect(pose(renderer, 2, 0.50f, -1, 0, NULL, NULL, 1.0),
         "further into the run");
  uint8_t *later = frame(renderer, 1.0);
  expect(unpose(renderer, 1.0), "the pose taken away");
  uint8_t *after = frame(renderer, 1.0);

  const int moved = differing(rest, running, 2);
  const int held = differing(running, again, 0);
  const int advanced = differing(running, later, 2);
  const int restored = differing(rest, after, 0);
  printf("Fox: run differs from rest in %d px, held %d, advanced %d, "
         "restored %d; largest held %d, restored %d\n",
         moved, held, advanced, restored, largest(running, again),
         largest(rest, after));
  dump(rest, "fox_rest");
  dump(running, "fox_run");
  dump(again, "fox_run_again");
  dump(later, "fox_later");
  dump(after, "fox_after");
  expect(moved > 50, "a clip moves the fox");
  expect(held == 0, "on a held clock the same pose draws the same frame");
  expect(advanced > 20, "a later moment in the clip is a different frame");
  expect(restored == 0, "taking the pose away puts the fox back exactly");

  /* Halfway from walking to running is neither. */
  expect(fading(renderer, 1, 0.25f, 2, 0.25f, 1.0f, 3, 1.0), "walking");
  uint8_t *walking = frame(renderer, 1.0);
  expect(fading(renderer, 1, 0.25f, 2, 0.25f, 0.5f, 3, 1.0),
         "half faded from running to walking");
  uint8_t *between = frame(renderer, 1.0);
  expect(fading(renderer, 1, 0.25f, 2, 0.25f, 0.0f, 3, 1.0),
         "not faded at all");
  uint8_t *unfaded = frame(renderer, 1.0);
  printf("Fox: halfway differs from walking in %d px and from running in %d; "
         "a fade of nought is running to within %d px, by at most %d\n",
         differing(between, walking, 2), differing(between, running, 2),
         differing(unfaded, running, 0), largest(unfaded, running));
  expect(differing(between, walking, 2) > 20 &&
             differing(between, running, 2) > 20,
         "a fade halfway between two clips is neither of them");
  /* Not to the bit: gltfio blends by taking every node's matrix apart into a
   * translation, rotation and scale and putting it back together, and that
   * round trip moves the last bits of a float. */
  expect(largest(unfaded, running) <= 2,
         "a fade that has not begun is the clip being left");

  /* A clip that does not loop holds its last frame; one that does wraps. */
  expect(fading(renderer, 2, 1.1583333f, -1, 0.0f, 1.0f, 0, 1.0),
         "the run's last moment");
  uint8_t *last = frame(renderer, 1.0);
  expect(fading(renderer, 2, 100.0f, -1, 0.0f, 1.0f, 0, 1.0),
         "long past the end, not looping");
  uint8_t *held_end = frame(renderer, 1.0);
  expect(fading(renderer, 2, 100.0f, -1, 0.0f, 1.0f, 1, 1.0),
         "long past the end, looping");
  uint8_t *wrapped = frame(renderer, 1.0);
  printf("Fox: past the end %d px from the last frame; looping %d px\n",
         differing(held_end, last, 0), differing(wrapped, last, 2));
  expect(differing(held_end, last, 0) == 0,
         "a clip that does not loop stops on its last frame");
  expect(differing(wrapped, last, 2) > 20, "one that loops goes round");
  free(walking);
  free(between);
  free(unfaded);
  free(last);
  free(held_end);
  free(wrapped);

  expect(pose(renderer, 7, 0.0f, -1, 0, NULL, NULL, 1.0),
         "a clip the fox does not have is not a failure");
  expect(noted(renderer, "pose 1", "Clip 7"), "but it is a note");

  free(rest);
  free(running);
  free(again);
  free(later);
  free(after);
  orblit_renderer_destroy(renderer);
  orblit_renderer_release_resource(fox);
}

static void a_joint_set_by_hand_moves_the_mesh(void) {
  const char *rig = provide("RiggedSimple.glb");
  if (rig == NULL) return;
  orblit_renderer *renderer = start();
  if (renderer == NULL) return;

  /* RiggedSimple is a column lying along z, about four metres of it; a
   * quarter of it stood up fills the frame. */
  const float stand[16] = {0.25f, 0, 0, 0, 0, 0, -0.25f, 0,
                           0, 0.25f, 0, 0, 0, 0.2f, 0, 1};
  expect(one_object(renderer, rig, stand), "the column is named");
  const char *info = described(renderer, rig);
  expect(info != NULL && strstr(info, "\"skins\":[{") != NULL,
         "the column has a skin");
  if (info != NULL) printf("RiggedSimple: %.200s\n", info);

  settle(renderer);
  uint8_t *rest = frame(renderer, 1.0);
  /* The second joint turned a quarter turn, where it stands. */
  const int32_t which[2] = {0, 1};
  const float turned[16] = {1, 0, 0, 0, 0, 0, 1, 0,
                            0, -1, 0, 0, 0, 0, 4.18f, 1};
  expect(pose(renderer, -1, 0.0f, -1, 1, which, turned, 1.0),
         "a joint is set");
  uint8_t *bent = frame(renderer, 1.0);
  expect(unpose(renderer, 1.0), "and let go");
  uint8_t *after = frame(renderer, 1.0);
  const int moved = differing(rest, bent, 2);
  const int restored = differing(rest, after, 0);
  printf("RiggedSimple: bent differs in %d px, restored %d (largest %d)\n",
         moved, restored, largest(rest, after));
  dump(rest, "rig_rest");
  dump(bent, "rig_bent");
  expect(moved > 50, "a joint set by hand bends the column");
  expect(restored == 0, "and letting go straightens it exactly");

  const int32_t nowhere[2] = {0, 99};
  expect(pose(renderer, -1, 0.0f, -1, 1, nowhere, turned, 1.0),
         "a joint the skin does not have is not a failure");
  expect(noted(renderer, "pose 1", "Joint 99"), "but it is a note");

  free(rest);
  free(bent);
  free(after);
  orblit_renderer_destroy(renderer);
  orblit_renderer_release_resource(rig);
}

/* A skinned mesh is culled by the box it was built with, which is the bind
 * pose's. Moved ten metres by its root joint — along its armature's own x,
 * which this file's armature turns to the world's z — and looked at from
 * between the two places, facing away from where it was bound, it has to
 * still be drawn: which it is only if its box went with it. */
static void a_posed_skin_is_not_culled_where_it_was_bound(void) {
  const char *rig = provide("RiggedSimple.glb");
  if (rig == NULL) return;
  orblit_renderer *renderer = start();
  if (renderer == NULL) return;

  expect(one_object(renderer, rig, kIdentity), "the column is named");
  settle(renderer);
  const float eye[3] = {0.0f, 1.0f, 3.0f};
  const float look[3] = {0.0f, 0.0f, 10.0f};
  uint8_t *empty = frame_from(renderer, eye, look, 1.0);

  const int32_t root[2] = {0, 0};
  const float away[16] = {1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 10, 0, 0, 1};
  expect(pose(renderer, -1, 0.0f, -1, 1, root, away, 1.0),
         "the root joint is moved away");
  uint8_t *moved = frame_from(renderer, eye, look, 1.0);
  const int drawn = differing(empty, moved, 2);
  printf("RiggedSimple ten metres away: %d px drawn\n", drawn);
  dump(empty, "cull_empty");
  dump(moved, "cull_moved");
  expect(drawn > 100,
         "a skin posed out of its bind box is drawn where it is, not culled "
         "where it was");

  free(empty);
  free(moved);
  orblit_renderer_destroy(renderer);
  orblit_renderer_release_resource(rig);
}

/* Publishes the object again and again, drawing between, until the renderer
 * has described the model — which for a file being converted off the drawing
 * thread is some frames after it was first named. Gives up after ten
 * seconds. */
static const char *built(orblit_renderer *renderer, const char *path,
                         const float transform[16]) {
  const struct timespec pause = {0, 50 * 1000 * 1000};
  for (int attempt = 0; attempt < 200; attempt++) {
    if (!one_object(renderer, path, transform)) return NULL;
    const char *info = described(renderer, path);
    if (info != NULL) return info;
    orblit_renderer_draw(renderer, 1.0);
    nanosleep(&pause, NULL);
  }
  return NULL;
}

/* An FBX and an OBJ, named by a scene like any glTF: converted on the way
 * in, off the drawing thread, and then the same as a glTF in every way —
 * described, posed and drawn. */
static void converted_models_load_and_play(void) {
  const char *samba = provide("Samba Dancing.fbx");
  if (samba != NULL) {
    orblit_renderer *renderer = start();
    if (renderer != NULL) {
      /* Mixamo's are in centimetres, and the conversion makes them metres:
       * identity is a person-sized dancer. */
      expect(one_object(renderer, samba, kIdentity), "the dancer is named");
      expect(noted(renderer, "Samba Dancing.fbx", "converted"),
             "while it converts, the scene says so");
      const char *info = built(renderer, samba, kIdentity);
      expect(info != NULL, "the FBX is converted and described");
      if (info != NULL) {
        printf("Samba: %.160s...\n", info);
        expect(strstr(info, "\"name\":\"mixamo.com\"") != NULL,
               "with the dance named as Mixamo names it");
        expect(strstr(info, "\"joints\":[\"") != NULL, "and its skin");
      }
      settle(renderer);
      const float eye[3] = {0.0f, 1.0f, 3.4f};
      const float look[3] = {0.0f, 0.9f, 0.0f};
      uint8_t *rest = frame_from(renderer, eye, look, 1.0);
      expect(pose(renderer, 0, 6.0f, -1, 0, NULL, NULL, 1.0), "it dances");
      uint8_t *dancing = frame_from(renderer, eye, look, 1.0);
      const int moved = differing(rest, dancing, 2);
      printf("Samba: dancing differs from rest in %d px\n", moved);
      dump(rest, "samba_rest");
      dump(dancing, "samba_dancing");
      expect(moved > 100, "a converted clip moves a converted skin");
      free(rest);
      free(dancing);
      orblit_renderer_destroy(renderer);
    }
    orblit_renderer_release_resource(samba);
  }

  /* An OBJ names its material library, and the library its pictures, by
   * paths beside it — so all five are provided under names beside each
   * other, as a host serving them from one directory would. */
  static const char *const kMale[] = {
      "male02/male02.obj", "male02/male02.mtl",
      "male02/01_-_Default1noCulling.JPG", "male02/male-02-1noCulling.JPG",
      "male02/orig_02_-_Defaul1noCulling.JPG"};
  char names[5][256];
  int all = 1;
  for (int i = 0; i < 5; i++) {
    if (provide_into(kMale[i], names[i], sizeof names[i]) == NULL) all = 0;
  }
  if (all) {
    orblit_renderer *renderer = start();
    if (renderer != NULL) {
      /* In centimetres too, and OBJ has no way to say so: a hundredth. */
      const float hundredth[16] = {0.01f, 0, 0, 0, 0, 0.01f, 0, 0,
                                   0, 0, 0.01f, 0, 0, 0, 0, 1};
      const char *info = built(renderer, names[0], hundredth);
      expect(info != NULL, "the OBJ is converted and described");
      if (info != NULL) printf("male02: %.200s\n", strstr(info, "\"materials\""));
      settle(renderer);
      expect(!noted(renderer, "male02.obj", "missing"),
             "its material library and its pictures are all found");
      const float eye[3] = {0.0f, 1.0f, 3.4f};
      const float look[3] = {0.0f, 0.9f, 0.0f};
      uint8_t *shot = frame_from(renderer, eye, look, 1.0);
      dump(shot, "male02");
      free(shot);
      orblit_renderer_destroy(renderer);
    }
  }
  for (int i = 0; i < 5; i++) orblit_renderer_release_resource(names[i]);
}

static void variants_are_worn_and_taken_off(void) {
  const char *shoe = provide("MaterialsVariantsShoe.glb");
  if (shoe == NULL) return;
  orblit_renderer *renderer = start();
  if (renderer == NULL) return;

  const float big[16] = {5, 0, 0, 0, 0, 5, 0, 0, 0, 0, 5, 0, 0, 0.3f, 0, 1};
  expect(one_object(renderer, shoe, big), "the shoe is named");
  const char *info = described(renderer, shoe);
  expect(info != NULL && strstr(info, "\"variants\":[\"") != NULL,
         "the shoe's variants are listed");
  if (info != NULL) {
    printf("Shoe: %.120s\n", strstr(info, "\"variants\""));
  }

  settle(renderer);
  uint8_t *own = frame(renderer, 1.0);
  expect(pose(renderer, -1, 0.0f, 1, 0, NULL, NULL, 1.0), "a variant worn");
  uint8_t *second = frame(renderer, 1.0);
  expect(pose(renderer, -1, 0.0f, 2, 0, NULL, NULL, 1.0), "another");
  uint8_t *third = frame(renderer, 1.0);
  expect(unpose(renderer, 1.0), "and taken off");
  uint8_t *after = frame(renderer, 1.0);
  printf("Shoe: variant 1 differs by %d px, variant 2 by %d, restored %d "
         "(largest %d)\n",
         differing(own, second, 2), differing(own, third, 2),
         differing(own, after, 0), largest(own, after));
  dump(own, "shoe_own");
  dump(second, "shoe_1");
  dump(third, "shoe_2");
  expect(differing(own, second, 2) > 50, "a variant changes the shoe");
  expect(differing(second, third, 2) > 50, "and another changes it again");
  expect(differing(own, after, 0) == 0,
         "taking it off puts the file's own materials back exactly");

  free(own);
  free(second);
  free(third);
  free(after);
  orblit_renderer_destroy(renderer);
  orblit_renderer_release_resource(shoe);
}

static void lights_and_extensions_are_described(void) {
  const char *lamp = provide("LightsPunctualLamp.glb");
  if (lamp != NULL) {
    orblit_renderer *renderer = start();
    if (renderer != NULL) {
      expect(one_object(renderer, lamp, kIdentity), "the lamp is named");
      const char *info = described(renderer, lamp);
      expect(info != NULL && strstr(info, "\"lights\":[{") != NULL,
             "the lamp's lights are described");
      if (info != NULL) {
        printf("Lamp: %.240s\n", strstr(info, "\"lights\""));
      }
      orblit_renderer_destroy(renderer);
    }
    orblit_renderer_release_resource(lamp);
  }

  const char *barn = provide("AnisotropyBarnLamp.glb");
  if (barn != NULL) {
    orblit_renderer *renderer = start();
    if (renderer != NULL) {
      expect(one_object(renderer, barn, kIdentity), "the barn lamp is named");
      const char *info = described(renderer, barn);
      expect(info != NULL && strstr(info, "KHR_materials_anisotropy") != NULL,
             "anisotropy is reported as not drawn");
      expect(noted(renderer, "AnisotropyBarnLamp", "KHR_materials_anisotropy"),
             "and said as a problem too");
      orblit_renderer_destroy(renderer);
    }
    orblit_renderer_release_resource(barn);
  }

  const char *const kDrawnWhole[] = {"ClearCoatTest.glb", "SheenChair.glb",
                                     "CesiumMan.glb"};
  for (size_t i = 0; i < sizeof kDrawnWhole / sizeof kDrawnWhole[0]; i++) {
    const char *model = provide(kDrawnWhole[i]);
    if (model == NULL) continue;
    orblit_renderer *renderer = start();
    if (renderer != NULL) {
      expect(one_object(renderer, model, kIdentity), "a sample is named");
      const char *info = described(renderer, model);
      char what[256];
      snprintf(what, sizeof what, "%s uses nothing the renderer cannot draw",
               kDrawnWhole[i]);
      expect(info != NULL && strstr(info, "\"unsupported\":[]") != NULL, what);
      orblit_renderer_destroy(renderer);
    }
    orblit_renderer_release_resource(model);
  }
}

int main(void) {
  expect(orblit_renderer_stride(ORBLIT_STRIDE_POSE_INTS) == 4,
         "a pose is four whole numbers");
  expect(orblit_renderer_stride(ORBLIT_STRIDE_POSE) == 5, "and five floats");
  expect(orblit_renderer_apply_poses(NULL, 0, NULL, NULL, 0, NULL, 0, NULL,
                                    NULL, 0, NULL, 0, 0.0) == ORBLIT_ERROR_NULL,
         "a null renderer is refused");

  if (getenv("ORBLIT_SAMPLES") == NULL) {
    printf("orblit_models_check: ORBLIT_SAMPLES is not set, so only the ABI "
           "was checked\n");
  } else {
    clips_are_described_and_play();
    a_joint_set_by_hand_moves_the_mesh();
    a_posed_skin_is_not_culled_where_it_was_bound();
    variants_are_worn_and_taken_off();
    converted_models_load_and_play();
    lights_and_extensions_are_described();
  }

  if (failures > 0) {
    fprintf(stderr, "orblit_models_check: %d failed\n", failures);
    return 1;
  }
  printf("orblit_models_check: passed%s\n",
         skipped > 0 ? ", with samples skipped" : "");
  return 0;
}

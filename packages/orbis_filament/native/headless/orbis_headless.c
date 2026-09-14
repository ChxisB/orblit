/* A host for the renderer with no Flutter, no Objective-C and no window.
 *
 * It drives the C ABI and nothing else: makes a renderer on the backend it is
 * told to use, publishes a small scene — a ground, five blocks and a sun —
 * draws it offscreen, reads the frame back and writes it out as a PNG. What
 * it proves is that the renderer's core runs with nobody but a C program in
 * front of it, which is what a console host is. It is also where one starts.
 *
 *   orbis_headless [out.png] [metal|vulkan|opengl|webgpu]
 *   orbis_headless out.png backend --benchmark frames warmup [width height]
 */

#if !defined(_WIN32) && !defined(_POSIX_C_SOURCE)
#define _POSIX_C_SOURCE 200809L
#endif

#include "orbis_renderer.h"

#include <errno.h>
#include <math.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#if defined(_WIN32)
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#else
#include <time.h>
#endif

static double milliseconds(void) {
#if defined(_WIN32)
  LARGE_INTEGER ticks, frequency;
  if (!QueryPerformanceFrequency(&frequency) || !QueryPerformanceCounter(&ticks)) {
    fprintf(stderr, "performance clock unavailable\n");
    exit(1);
  }
  return 1000.0 * (double)ticks.QuadPart / (double)frequency.QuadPart;
#else
  struct timespec ticks;
  if (clock_gettime(CLOCK_MONOTONIC, &ticks) != 0) {
    perror("clock_gettime");
    exit(1);
  }
  return 1000.0 * (double)ticks.tv_sec + (double)ticks.tv_nsec / 1.0e6;
#endif
}

static int positive_integer(const char *text, uint32_t limit, uint32_t *value) {
  if (*text < '0' || *text > '9') return 0;
  char *end = NULL;
  errno = 0;
  const unsigned long number = strtoul(text, &end, 10);
  if (errno || *end || number == 0 || number > limit) return 0;
  *value = (uint32_t)number;
  return 1;
}

static int compare_costs(const void *a, const void *b) {
  const double left = *(const double *)a;
  const double right = *(const double *)b;
  return (left > right) - (left < right);
}

/* Nearest-rank percentiles, after sorting at least one observation. */
static double percentile(const double *costs, uint32_t count, uint32_t percent) {
  return costs[((uint64_t)count * percent + 99) / 100 - 1];
}

typedef struct benchmark_result {
  double first_frame_ms;
  double elapsed_ms;
  double p50_ms, p95_ms, p99_ms;
  uint32_t skipped;
  orbis_stats stats;
} benchmark_result;

/* A draw call can succeed without drawing (Filament's frame skipper), so
 * count endFrame completions. Time includes the core's current GPU wait;
 * capture/readback, PNG encoding and percentile calculation are outside it.
 * This is offscreen throughput, not display FPS or input-to-photon latency. */
static int measure(orbis_renderer *renderer, uint32_t frames, uint32_t warmup,
                   benchmark_result *result) {
  double *costs = malloc((size_t)frames * sizeof(double));
  if (!costs) return 0;
  uint64_t previous = orbis_renderer_rendered_frames(renderer);
  const uint64_t target = (uint64_t)frames + warmup;
  uint64_t completed = 0;
  double measured_from = 0;
  const double first_from = milliseconds();
  for (uint64_t attempt = 0; attempt < target * 100 + 1000; attempt++) {
    const double before = milliseconds();
    if (orbis_renderer_draw(renderer, (double)completed / 60.0) != ORBIS_OK) {
      free(costs);
      return 0;
    }
    const double after = milliseconds();
    const uint64_t rendered = orbis_renderer_rendered_frames(renderer);
    if (rendered == previous) {
      if (completed >= warmup) result->skipped++;
      continue;
    }
    previous = rendered;
    if (completed == 0) result->first_frame_ms = after - first_from;
    if (completed >= warmup) costs[completed - warmup] = after - before;
    completed++;
    if (completed == warmup) measured_from = after;
    if (completed == target) {
      result->elapsed_ms = after - measured_from;
      if (orbis_renderer_stats(renderer, &result->stats) != ORBIS_OK) {
        free(costs);
        return 0;
      }
      qsort(costs, frames, sizeof(double), compare_costs);
      result->p50_ms = percentile(costs, frames, 50);
      result->p95_ms = percentile(costs, frames, 95);
      result->p99_ms = percentile(costs, frames, 99);
      free(costs);
      return result->elapsed_ms > 0;
    }
  }
  fprintf(stderr, "benchmark stopped: too many draws produced no frame\n");
  free(costs);
  return 0;
}

/* ---- A PNG, written without a library ----
 *
 * Deflate's stored blocks need no compressor, only a checksum each side, so
 * a PNG is sixty lines of C rather than a dependency. The file is as large as
 * the pixels, which for a proof is the right trade. */

static uint32_t crc_table[256];

static void crc_start(void) {
  for (uint32_t n = 0; n < 256; n++) {
    uint32_t c = n;
    for (int k = 0; k < 8; k++) c = (c & 1) ? 0xEDB88320u ^ (c >> 1) : c >> 1;
    crc_table[n] = c;
  }
}

static uint32_t crc_of(const uint8_t *bytes, size_t length, uint32_t crc) {
  crc ^= 0xFFFFFFFFu;
  for (size_t i = 0; i < length; i++) {
    crc = crc_table[(crc ^ bytes[i]) & 0xFF] ^ (crc >> 8);
  }
  return crc ^ 0xFFFFFFFFu;
}

static void put32(uint8_t *to, uint32_t value) {
  to[0] = (uint8_t)(value >> 24);
  to[1] = (uint8_t)(value >> 16);
  to[2] = (uint8_t)(value >> 8);
  to[3] = (uint8_t)value;
}

static int write_chunk(FILE *file, const char *type, const uint8_t *data,
                       size_t length) {
  uint8_t head[8];
  put32(head, (uint32_t)length);
  memcpy(head + 4, type, 4);
  uint32_t crc = crc_of(head + 4, 4, 0);
  crc = crc_of(data, length, crc);
  uint8_t tail[4];
  put32(tail, crc);
  return fwrite(head, 1, 8, file) == 8 &&
         (length == 0 || fwrite(data, 1, length, file) == length) &&
         fwrite(tail, 1, 4, file) == 4;
}

static int write_png(const char *path, const uint8_t *rgba, uint32_t width,
                     uint32_t height) {
  const size_t row = (size_t)width * 4 + 1;
  const size_t raw = row * height;
  const size_t blocks = (raw + 65534) / 65535;
  uint8_t *zlib = malloc(2 + raw + blocks * 5 + 4);
  if (zlib == NULL) return 0;

  /* The rows, each behind a filter byte of nought, then stored in blocks
   * of at most 65535 bytes with a running Adler-32 over the lot. */
  size_t at = 0;
  zlib[at++] = 0x78;
  zlib[at++] = 0x01;
  uint32_t a = 1;
  uint32_t b = 0;
  size_t done = 0;
  while (done < raw) {
    const size_t take = raw - done < 65535 ? raw - done : 65535;
    zlib[at++] = done + take == raw ? 1 : 0;
    zlib[at++] = (uint8_t)(take & 0xFF);
    zlib[at++] = (uint8_t)(take >> 8);
    zlib[at++] = (uint8_t)(~take & 0xFF);
    zlib[at++] = (uint8_t)((~take >> 8) & 0xFF);
    for (size_t i = 0; i < take; i++) {
      const size_t offset = done + i;
      const size_t column = offset % row;
      const uint8_t byte =
          column == 0 ? 0 : rgba[(offset / row) * width * 4 + column - 1];
      zlib[at++] = byte;
      a = (a + byte) % 65521;
      b = (b + a) % 65521;
    }
    done += take;
  }
  put32(zlib + at, (b << 16) | a);
  at += 4;

  uint8_t header[13];
  put32(header, width);
  put32(header + 4, height);
  header[8] = 8; /* bits per channel */
  header[9] = 6; /* RGBA */
  header[10] = header[11] = header[12] = 0;

  FILE *file = fopen(path, "wb");
  int ok = file != NULL;
  if (ok) {
    static const uint8_t signature[8] = {137, 'P', 'N', 'G', 13, 10, 26, 10};
    ok = fwrite(signature, 1, 8, file) == 8 &&
         write_chunk(file, "IHDR", header, sizeof header) &&
         write_chunk(file, "IDAT", zlib, at) &&
         write_chunk(file, "IEND", NULL, 0);
    ok = fclose(file) == 0 && ok;
  }
  free(zlib);
  return ok;
}

/* ---- The scene ---- */

static OrbisBackend backend_named(const char *name) {
  if (name == NULL) return ORBIS_BACKEND_DEFAULT;
  if (strcmp(name, "metal") == 0) return ORBIS_BACKEND_METAL;
  if (strcmp(name, "vulkan") == 0) return ORBIS_BACKEND_VULKAN;
  if (strcmp(name, "opengl") == 0) return ORBIS_BACKEND_OPENGL;
  if (strcmp(name, "webgpu") == 0) return ORBIS_BACKEND_WEBGPU;
  return ORBIS_BACKEND_DEFAULT;
}

static const char *backend_name(OrbisBackend backend) {
  switch (backend) {
    case ORBIS_BACKEND_METAL: return "Metal";
    case ORBIS_BACKEND_VULKAN: return "Vulkan";
    case ORBIS_BACKEND_OPENGL: return "OpenGL";
    case ORBIS_BACKEND_WEBGPU: return "WebGPU";
    default: return "default";
  }
}

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

/* A slab through the middle, turned about Y and then tipped about X.
 *
 * The Overdraw example's scene, in C. Every slab crosses every other at the
 * centre, so each one is in front of the others somewhere and behind them
 * somewhere else and no order of objects is the right order for every pixel.
 * That is the case a depth prepass exists for, and the case this host is here
 * to time on a GPU that is not tile-based. */
static void slab(float *m, int index, int count) {
  const double turn = 3.14159265358979 * (double)index / (double)count;
  const double tip = sin((double)index * 1.7) * 0.35;
  const float cy = (float)cos(turn), sy = (float)sin(turn);
  const float cx = (float)cos(tip), sx = (float)sin(tip);
  /* Scale, then tip about X, then turn about Y: column-major, so the columns
   * below are the axes of the result. */
  const float scale[3] = {5.0f, 3.0f, 0.05f};
  memset(m, 0, 16 * sizeof(float));
  m[0] = cy * scale[0];
  m[1] = 0.0f;
  m[2] = -sy * scale[0];
  m[4] = sy * sx * scale[1];
  m[5] = cx * scale[1];
  m[6] = cy * sx * scale[1];
  m[8] = sy * cx * scale[2];
  m[9] = -sx * scale[2];
  m[10] = cy * cx * scale[2];
  m[15] = 1.0f;
}

/* An environment variable as a number, or `fallback` when it is unset. */
static int number_from(const char *name, int fallback) {
  const char *value = getenv(name);
  if (value == NULL || *value == '\0') return fallback;
  return atoi(value);
}

int main(int argc, char **argv) {
  if (argc == 2 && strcmp(argv[1], "--help") == 0) {
    printf("usage: %s [out.png] [metal|vulkan|opengl|webgpu]\n"
           "       %s out.png backend --benchmark frames warmup [width height]\n",
           argv[0], argv[0]);
    return 0;
  }
  const char *out = argc > 1 ? argv[1] : "orbis_headless.png";
  const OrbisBackend asked = backend_named(argc > 2 ? argv[2] : NULL);
  uint32_t width = 960, height = 540, frames = 0, warmup = 0;
  if ((argc > 2 && asked == ORBIS_BACKEND_DEFAULT) ||
      (argc > 3 &&
       ((argc != 6 && argc != 8) || strcmp(argv[3], "--benchmark") != 0 ||
        !positive_integer(argv[4], 100000, &frames) ||
        !positive_integer(argv[5], 100000, &warmup) ||
        (argc == 8 && (!positive_integer(argv[6], 8192, &width) ||
                       !positive_integer(argv[7], 8192, &height)))))) {
    fprintf(stderr, "invalid arguments; use --help (counts must be 1..100000, dimensions 1..8192)\n");
    return 2;
  }

  orbis_surface_desc surface = {ORBIS_SURFACE_HEADLESS, NULL};
  const double created_from = milliseconds();
  orbis_renderer *renderer =
      orbis_renderer_create(asked, &surface, width, height);
  if (renderer == NULL) {
    /* Not named here: ORBIS_BACKEND may have chosen for DEFAULT, and the
     * renderer's own log says which backend it tried and why it would not. */
    fprintf(stderr, "the renderer would not start; the log above says why\n");
    return 1;
  }
  const double startup_ms = milliseconds() - created_from;
  benchmark_result benchmark = {0};
  printf("drawing with %s\n", backend_name(orbis_renderer_backend(renderer)));

  /* Whether opaque objects are drawn into depth alone before being shaded.
   * Set before the objects, which is where the second entity per object is
   * built. Off unless asked for, exactly as it is everywhere else. */
  const int prepass = number_from("ORBIS_PREPASS", 0);
  orbis_renderer_set_depth_prepass(renderer, prepass);

  /* How many slabs cross at the middle, or none for the five blocks.
   *
   * The blocks barely overlap, which makes them a fine picture and a useless
   * measurement: there is almost no hidden surface for a prepass to save. The
   * slabs are the Overdraw example's scene, built to be the opposite. */
  const int slabs = number_from("ORBIS_SLABS", 0);

  /* The sky it stands under, which is also what lights the shadows. */
  const float sky[3] = {0.30f, 0.45f, 0.70f};
  orbis_renderer_set_sky_colour(renderer, sky, slabs > 0 ? 9000.0f : 24000.0f,
                                slabs > 0 ? 0 : 1);

  /* A low sun, so the blocks throw shadows worth looking at, and — under the
   * slabs — six point lights besides, so that shading a pixel actually costs
   * something and drawing it twice costs twice. A light is 22 floats: colour,
   * intensity, position, direction, then falloff radius and the rest. */
  enum { kLightFloats = 22, kMaxLights = 7 };
  int64_t light_keys[kMaxLights];
  int32_t light_kinds[kMaxLights];
  int32_t light_flags[kMaxLights];
  float lights[kMaxLights * kLightFloats];
  memset(lights, 0, sizeof lights);
  light_keys[0] = 1;
  light_kinds[0] = 0; /* directional */
  light_flags[0] = 1; /* casts */
  {
    static const float sun[kLightFloats] = {
        1.0f, 0.95f, 0.88f, 100000.0f, 0, 0, 0, -0.45f, -0.8f, -0.55f, 0,
        0,    0,     0.53f, 0.1f,      10.0f, 80.0f, 0, 0, 0, 0, 0};
    memcpy(lights, sun, sizeof sun);
    if (slabs > 0) lights[3] = 70000.0f;
  }
  uint32_t light_count = 1;
  if (slabs > 0) {
    for (int i = 0; i < 6; i++) {
      float *row = lights + (size_t)light_count * kLightFloats;
      light_keys[light_count] = 10 + i;
      light_kinds[light_count] = 1; /* point */
      light_flags[light_count] = 0; /* casts no shadow */
      row[0] = 0.37f + 0.35f * (float)i / 5.0f;
      row[1] = 0.66f - 0.25f * (float)i / 5.0f;
      row[2] = 0.83f - 0.50f * (float)i / 5.0f;
      row[3] = 30000.0f;
      row[4] = (float)cos((double)i * 1.05) * 4.0f;
      row[5] = 0.5f;
      row[6] = (float)sin((double)i * 1.05) * 4.0f;
      row[10] = 9.0f; /* falloff radius */
      light_count++;
    }
  }
  orbis_renderer_apply_lights(renderer, light_count, light_keys, light_kinds,
                              light_flags, lights, light_count * kLightFloats);

  /* A ground and five blocks, each its own colour — or a ground and a fan of
   * slabs all crossing at the middle. Allocated rather than declared, because
   * how many there are is only known here. */
  const int count = slabs > 0 ? slabs + 1 : 6;
  int64_t *keys = malloc((size_t)count * sizeof *keys);
  float *transforms = malloc((size_t)count * 16 * sizeof *transforms);
  float *colours = malloc((size_t)count * 3 * sizeof *colours);
  int32_t *meshes = malloc((size_t)count * sizeof *meshes);
  int32_t *flags = malloc((size_t)count * sizeof *flags);
  int32_t *materials = malloc((size_t)count * sizeof *materials);
  int32_t *shapes = malloc((size_t)count * sizeof *shapes);
  if (keys == NULL || transforms == NULL || colours == NULL ||
      meshes == NULL || flags == NULL || materials == NULL || shapes == NULL) {
    fprintf(stderr, "out of memory for %d objects\n", count);
    orbis_renderer_destroy(renderer);
    return 1;
  }
  static const float blocks[5][4] = {
      {-1.8f, 0.0f, 0.2f, 0.5f}, {-0.6f, 0.25f, -1.0f, 0.75f},
      {0.7f, -0.1f, 0.6f, 0.4f}, {1.8f, 0.4f, -0.5f, 0.9f},
      {0.0f, -0.25f, 1.9f, 0.25f}};
  static const float tints[6][3] = {{0.55f, 0.55f, 0.5f}, {0.85f, 0.28f, 0.18f},
                                    {0.20f, 0.55f, 0.85f}, {0.95f, 0.75f, 0.2f},
                                    {0.35f, 0.75f, 0.35f}, {0.75f, 0.35f, 0.8f}};
  for (int i = 0; i < count; i++) {
    keys[i] = 100 + i;
    meshes[i] = -1;   /* the built-in cube */
    /* The slabs cast nothing: a shadow pass over ninety-six interpenetrating
     * sheets measures the shadow pass, not the overdraw this is here for. */
    flags[i] = slabs > 0 ? (2 | 4) : (1 | 2 | 4);
    materials[i] = -1;
    shapes[i] = 0;
    memcpy(colours + i * 3, tints[i % 6], sizeof tints[0]);
  }
  if (slabs > 0) {
    place(transforms, 0.0f, -3.2f, 0.0f, 14.0f, 0.05f, 14.0f);
    for (int i = 0; i < slabs; i++) {
      slab(transforms + (size_t)(i + 1) * 16, i, slabs);
      /* One colour for all of them, so that what differs between two runs is
       * the prepass and nothing else. */
      colours[(i + 1) * 3] = 0.62f;
      colours[(i + 1) * 3 + 1] = 0.42f;
      colours[(i + 1) * 3 + 2] = 0.30f;
    }
  } else {
    place(transforms, 0.0f, -0.55f, 0.0f, 6.0f, 0.05f, 6.0f);
    for (int i = 0; i < 5; i++) {
      const float *b = blocks[i];
      place(transforms + (i + 1) * 16, b[0], b[1] - 0.5f + b[3], b[2], b[3],
            b[3], b[3]);
    }
  }
  if (orbis_renderer_apply_objects(renderer, (uint32_t)count, keys, transforms,
                                   (size_t)count * 16, colours,
                                   (size_t)count * 3, meshes, flags, materials,
                                   shapes, NULL, 0, NULL, 0) != ORBIS_OK) {
    fprintf(stderr, "the scene was refused\n");
    orbis_renderer_destroy(renderer);
    return 1;
  }

  /* The Overdraw example's own viewpoint under the slabs, so the two hosts
   * are looking at the same thing: yaw 0.3, pitch 0.25, sixteen metres out. */
  const float blocks_eye[3] = {5.5f, 3.6f, 6.5f};
  const float slabs_eye[3] = {4.58f, 3.96f, 14.81f};
  const float look[3] = {0.0f, 0.0f, 0.0f};
  orbis_renderer_set_camera(renderer, slabs > 0 ? slabs_eye : blocks_eye, look,
                            42.0f, 0, 10.0f, 0.0);
  orbis_renderer_set_exposure(renderer, 16.0f, 1.0f / 125.0f, 100.0f);

  if (frames && !measure(renderer, frames, warmup, &benchmark)) {
    fprintf(stderr, "benchmark failed\n");
    orbis_renderer_destroy(renderer);
    return 1;
  }

  /* A few frames to settle, then one read back. */
  for (int frame = 0; frame < 4; frame++) {
    orbis_renderer_draw(renderer, frame / 60.0);
  }
  orbis_renderer_request_capture(renderer);
  size_t bytes = 0;
  uint32_t got_width = 0;
  uint32_t got_height = 0;
  for (int frame = frames; frame < frames + 8 && bytes == 0; frame++) {
    orbis_renderer_draw(renderer, frame / 60.0);
    bytes = orbis_renderer_read_capture(renderer, NULL, 0, &got_width,
                                        &got_height);
  }
  if (bytes == 0) {
    fprintf(stderr, "no frame came back\n");
    orbis_renderer_destroy(renderer);
    return 1;
  }
  uint8_t *pixels = malloc(bytes);
  if (!pixels) {
    orbis_renderer_destroy(renderer);
    return 1;
  }
  orbis_renderer_read_capture(renderer, pixels, bytes, &got_width,
                              &got_height);

  crc_start();
  const int wrote = write_png(out, pixels, got_width, got_height);
  free(pixels);

  orbis_stats stats;
  if (orbis_renderer_stats(renderer, &stats) == ORBIS_OK) {
    printf("%ux%u -> %s; cpu %.2f ms, gpu %.2f ms\n", got_width, got_height,
           wrote ? out : "(not written)", stats.cpu_milliseconds,
           stats.gpu_milliseconds);
  }
  const uint32_t notes = orbis_renderer_notes(renderer);
  for (uint32_t i = 0; i < notes; i++) {
    const char *about = NULL;
    const char *saying = NULL;
    if (orbis_renderer_note(renderer, i, &about, &saying) == ORBIS_OK) {
      printf("note: %s: %s\n", about, saying);
    }
  }
  if (frames && wrote) {
    char gpu[64] = "null";
    if (benchmark.stats.gpu_milliseconds > 0) {
      snprintf(gpu, sizeof gpu, "%.4f", benchmark.stats.gpu_milliseconds);
    }
    printf("{\"backend\":\"%s\",\"width\":%u,\"height\":%u,"
           "\"frames\":%u,\"warmup\":%u,\"skipped_draws\":%u,"
           "\"startup_ms\":%.4f,\"first_frame_ms\":%.4f,"
           "\"elapsed_ms\":%.4f,\"offscreen_fps\":%.2f,"
           "\"draw_p50_ms\":%.4f,\"draw_p95_ms\":%.4f,\"draw_p99_ms\":%.4f,"
           "\"reported_cpu_ms\":%.4f,\"reported_gpu_ms\":%s}\n",
           backend_name(orbis_renderer_backend(renderer)), width, height,
           frames, warmup, benchmark.skipped, startup_ms, benchmark.first_frame_ms,
           benchmark.elapsed_ms, 1000.0 * frames / benchmark.elapsed_ms,
           benchmark.p50_ms, benchmark.p95_ms, benchmark.p99_ms,
           benchmark.stats.cpu_milliseconds, gpu);
  }
  orbis_renderer_destroy(renderer);
  return wrote ? 0 : 1;
}

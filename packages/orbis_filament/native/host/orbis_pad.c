/* Shaping, slots and edges: package:orbis_input's decisions, in C.
 *
 * Nothing here talks to a platform. A back end (orbis_host_sdl.c on a desktop,
 * something under an NDA on a console) reports what it found this poll as
 * orbis_pad_raw, and this file turns that into the orbis_pad_frame a game
 * reads. The split is the whole point: a console port replaces the back end
 * and this file is not touched.
 *
 * The Dart original is packages/orbis_input/lib/src/shaping.dart and
 * pads.dart, and where the two disagree one of them is wrong.
 */

#include "orbis_pad.h"

#include <math.h>
#include <stdlib.h>
#include <string.h>

/* orbis_input's Shaping() default and Shaping.triggers, field for field. */
const orbis_shaping orbis_shaping_sticks = {0.15f, 0.95f, 1.0f};
const orbis_shaping orbis_shaping_triggers = {0.06f, 0.98f, 1.0f};

/* `magnitude`, nought to one, with the zone taken out and the rest rescaled.
 * Shaping._shape. */
static float shape_magnitude(const orbis_shaping *shaping, float magnitude) {
  if (magnitude <= shaping->inner) return 0.0f;
  const float span = shaping->outer - shaping->inner;
  if (span <= 0.0f) return 1.0f;
  float t = (magnitude - shaping->inner) / span;
  if (t < 0.0f) t = 0.0f;
  if (t > 1.0f) t = 1.0f;
  return shaping->curve == 1.0f ? t : powf(t, shaping->curve);
}

float orbis_shape_scalar(const orbis_shaping *shaping, float value) {
  if (shaping == NULL || !isfinite(value)) return 0.0f;
  const float shaped = shape_magnitude(shaping, fabsf(value));
  return value < 0.0f ? -shaped : shaped;
}

void orbis_shape_stick(const orbis_shaping *shaping, float x, float y,
                       float *out_x, float *out_y) {
  float rx = 0.0f;
  float ry = 0.0f;
  if (shaping != NULL && isfinite(x) && isfinite(y)) {
    const float length = sqrtf(x * x + y * y);
    if (length > shaping->inner) {
      const float shaped = shape_magnitude(shaping, length);
      if (shaped > 0.0f) {
        /* Divided by the measured length rather than normalised in place, so
         * the direction survives exactly and a zero length never divides. */
        rx = x * (shaped / length);
        ry = y * (shaped / length);
      }
    }
  }
  if (out_x != NULL) *out_x = rx;
  if (out_y != NULL) *out_y = ry;
}

int orbis_pad_is_down(const orbis_pad_state *pad, orbis_pad_button button) {
  if (pad == NULL || button < 0 || button >= ORBIS_PAD_BUTTON_COUNT) return 0;
  return (pad->down & (1u << (unsigned)button)) != 0;
}

int orbis_pad_was_pressed(const orbis_pad_state *pad, orbis_pad_button button) {
  if (pad == NULL || button < 0 || button >= ORBIS_PAD_BUTTON_COUNT) return 0;
  return (pad->pressed & (1u << (unsigned)button)) != 0;
}

int orbis_pad_is_active(const orbis_pad_state *pad) {
  if (pad == NULL || !pad->connected) return 0;
  if (pad->down != 0) return 1;
  /* The shaped values, as PadState.isActive reads them: a stick inside its
   * dead zone is not somebody touching the pad, it is a worn spring. */
  for (int side = 0; side < 2; side++) {
    if (fabsf(pad->stick_x[side]) > 0.001f) return 1;
    if (fabsf(pad->stick_y[side]) > 0.001f) return 1;
    if (fabsf(pad->trigger[side]) > 0.001f) return 1;
  }
  return 0;
}

void orbis_pad_dpad(const orbis_pad_state *pad, float *out_x, float *out_y) {
  const float x = (float)(orbis_pad_is_down(pad, ORBIS_PAD_DPAD_RIGHT) -
                          orbis_pad_is_down(pad, ORBIS_PAD_DPAD_LEFT));
  const float y = (float)(orbis_pad_is_down(pad, ORBIS_PAD_DPAD_UP) -
                          orbis_pad_is_down(pad, ORBIS_PAD_DPAD_DOWN));
  if (out_x != NULL) *out_x = x;
  if (out_y != NULL) *out_y = y;
}

/* A pad that is not there: every button up and every axis at rest, rather
 * than whatever it was holding when it vanished. PadState.absent's reason —
 * a controller whose battery dies mid-corner should not leave the car
 * turning left forever. */
static const orbis_pad_state kAbsent;

const orbis_pad_state *orbis_pad_first(const orbis_pad_frame *frame) {
  if (frame != NULL) {
    for (int i = 0; i < ORBIS_PAD_SLOTS; i++) {
      if (frame->pads[i].connected) return &frame->pads[i];
    }
  }
  return &kAbsent;
}

int orbis_pad_count(const orbis_pad_frame *frame) {
  int count = 0;
  if (frame != NULL) {
    for (int i = 0; i < ORBIS_PAD_SLOTS; i++) {
      if (frame->pads[i].connected) count++;
    }
  }
  return count;
}

/* ---- Slots ----
 *
 * One entry per player number. `token` is who this slot belongs to and stays
 * put when the pad goes away, which is how a pad that is unplugged and
 * plugged back in gets its player number back rather than whatever slot
 * happens to be free. orbis_input keeps the same fact in a separate
 * `_remembered` map; a slot that remembers its own last owner is the same
 * rule with nothing to grow without bound, and a remembered owner is only
 * forgotten when a new pad has nowhere else to go.
 */
typedef struct {
  char token[sizeof(((orbis_pad_raw *)0)->token)];
  char name[64];
  int present; /* Was it found this poll. */
  uint32_t down;
} orbis_slot;

struct orbis_pads {
  orbis_slot slots[ORBIS_PAD_SLOTS];
};

orbis_pads *orbis_pads_create(void) {
  return (orbis_pads *)calloc(1, sizeof(orbis_pads));
}

void orbis_pads_destroy(orbis_pads *pads) { free(pads); }

/* Which slot this token already owns, or -1. */
static int slot_of(const orbis_pads *pads, const char *token) {
  for (int i = 0; i < ORBIS_PAD_SLOTS; i++) {
    if (pads->slots[i].token[0] != '\0' &&
        strcmp(pads->slots[i].token, token) == 0) {
      return i;
    }
  }
  return -1;
}

static void copy_into(char *to, size_t capacity, const char *from) {
  if (from == NULL) {
    to[0] = '\0';
    return;
  }
  size_t length = strlen(from);
  if (length >= capacity) length = capacity - 1;
  memcpy(to, from, length);
  to[length] = '\0';
}

void orbis_pads_poll(orbis_pads *pads, const orbis_pad_raw *raw, int count,
                     orbis_pad_frame *frame) {
  if (frame == NULL) return;
  memset(frame, 0, sizeof *frame);
  if (pads == NULL) return;
  if (raw == NULL) count = 0;

  for (int i = 0; i < ORBIS_PAD_SLOTS; i++) pads->slots[i].present = 0;

  /* Two passes, so a pad that is already somebody's keeps its slot even when
   * another pad is polled before it. Without this a replug during a frame
   * where a second pad arrives can shuffle both players. */
  int assigned[64];
  const int consider = count < (int)(sizeof assigned / sizeof *assigned)
                           ? count
                           : (int)(sizeof assigned / sizeof *assigned);
  for (int i = 0; i < consider; i++) {
    assigned[i] = slot_of(pads, raw[i].token);
    if (assigned[i] >= 0) pads->slots[assigned[i]].present = 1;
  }
  for (int i = 0; i < consider; i++) {
    if (assigned[i] >= 0) continue;
    int at = -1;
    /* A slot nobody has ever held, first. */
    for (int s = 0; s < ORBIS_PAD_SLOTS && at < 0; s++) {
      if (pads->slots[s].token[0] == '\0') at = s;
    }
    /* Then one whose remembered owner is not here. Somebody loses their
     * player number, which is the right trade against refusing a pad that is
     * plugged in and in somebody's hands. */
    for (int s = 0; s < ORBIS_PAD_SLOTS && at < 0; s++) {
      if (!pads->slots[s].present) at = s;
    }
    /* A ninth pad is ignored rather than displacing somebody. */
    if (at < 0) continue;
    if (strcmp(pads->slots[at].token, raw[i].token) != 0) {
      pads->slots[at].down = 0; /* A new owner starts with nothing held. */
    }
    copy_into(pads->slots[at].token, sizeof pads->slots[at].token,
              raw[i].token);
    pads->slots[at].present = 1;
    assigned[i] = at;
  }

  for (int i = 0; i < consider; i++) {
    const int at = assigned[i];
    if (at < 0) continue;
    orbis_slot *slot = &pads->slots[at];
    orbis_pad_state *state = &frame->pads[at];
    const orbis_pad_raw *from = &raw[i];

    copy_into(slot->name, sizeof slot->name, from->name);
    state->connected = 1;
    /* Points into the slot, so it outlives `raw` — a back end is free to
     * hand over a name the platform owns and may free. Good until the next
     * orbis_pads_poll. */
    state->name = slot->name;

    const uint32_t mask = (1u << ORBIS_PAD_BUTTON_COUNT) - 1u;
    state->down = from->buttons & mask;
    state->pressed = state->down & ~slot->down;
    state->released = slot->down & ~state->down;
    slot->down = state->down;

    for (int side = 0; side < 2; side++) {
      /* Positive is up. The platforms all report +Y downwards because they
       * inherited it from screens; the flip happens once, here, so nothing
       * above this line ever sees the other convention. */
      const float x = from->stick_x[side];
      const float y = -from->stick_y[side];
      state->raw_stick_x[side] = x;
      state->raw_stick_y[side] = y;
      state->raw_trigger[side] = from->trigger[side];
      orbis_shape_stick(&orbis_shaping_sticks, x, y, &state->stick_x[side],
                        &state->stick_y[side]);
      state->trigger[side] =
          orbis_shape_scalar(&orbis_shaping_triggers, from->trigger[side]);
    }
  }

  /* A slot whose pad is missing this poll reads as absent — which `frame` is
   * already, having been cleared — but keeps its token, so the pad gets this
   * slot back when it returns. The held mask goes too, so a button held at
   * the moment a pad vanished is not reported as released a minute later
   * when it comes back. */
  for (int i = 0; i < ORBIS_PAD_SLOTS; i++) {
    if (!pads->slots[i].present) pads->slots[i].down = 0;
  }
}

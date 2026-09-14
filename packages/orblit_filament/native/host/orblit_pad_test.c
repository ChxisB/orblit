/* That orblit_pad.c still agrees with package:orblit_input.
 *
 * orblit_pad.h says the two drifting apart is a bug in whichever moved, which
 * is a claim nobody can check by reading. So the cases below are
 * packages/orblit_input/test/shaping_test.dart and pads_test.dart, the same
 * assertions against the C: the same defaults, the same continuity at the
 * edge of the dead zone, the same round zone, the same slot memory, the same
 * neutral absence.
 *
 * Needs nothing — no Filament, no SDL, no window, no pad. It builds and runs
 * anywhere a C compiler does, which is the point: it is the half of this
 * directory a console toolchain can run on day one, before a window exists.
 *
 *   build.sh test
 */

#include "orblit_pad.h"

#include <math.h>
#include <stdio.h>
#include <string.h>

static int failures;
static const char *group = "";

static void check(int ok, const char *saying) {
  if (!ok) {
    printf("  FAIL  %s: %s\n", group, saying);
    failures++;
  }
}

static void near(float got, float want, const char *saying) {
  if (fabsf(got - want) > 1e-5f) {
    printf("  FAIL  %s: %s (got %f, wanted %f)\n", group, saying, (double)got,
           (double)want);
    failures++;
  }
}

/* One pad, as a platform would report it: +Y down, triggers nought to one. */
static orblit_pad_raw raw_pad(const char *token, uint32_t buttons, float lx,
                             float ly) {
  orblit_pad_raw pad;
  memset(&pad, 0, sizeof pad);
  snprintf(pad.token, sizeof pad.token, "%s", token);
  pad.name = token;
  pad.buttons = buttons;
  pad.stick_x[ORBLIT_PAD_LEFT] = lx;
  pad.stick_y[ORBLIT_PAD_LEFT] = ly;
  return pad;
}

static float stick_length(const orblit_shaping *shaping, float x, float y) {
  float sx = 0;
  float sy = 0;
  orblit_shape_stick(shaping, x, y, &sx, &sy);
  return sqrtf(sx * sx + sy * sy);
}

int main(void) {
  /* ---- The defaults ----
   *
   * Shaping()'s and Shaping.triggers' fields, which everything else in both
   * implementations is measured against. If these two lines ever disagree
   * with shaping.dart, nothing below is worth reading. */
  group = "defaults";
  near(orblit_shaping_sticks.inner, 0.15f, "a stick's dead zone");
  near(orblit_shaping_sticks.outer, 0.95f, "a stick saturates early");
  near(orblit_shaping_sticks.curve, 1.0f, "linear until a game says otherwise");
  near(orblit_shaping_triggers.inner, 0.06f, "a trigger's smaller dead zone");
  near(orblit_shaping_triggers.outer, 0.98f, "a trigger saturates later");
  near(orblit_shaping_triggers.curve, 1.0f, "a throttle is linear");

  /* ---- The dead zone ---- */
  const orblit_shaping zone = {0.2f, 0.9f, 1.0f};

  group = "dead zone";
  near(stick_length(&zone, 0, 0), 0, "a stick at rest is exactly nothing");
  /* What a stick played for a year reports with nobody touching it. */
  near(stick_length(&zone, 0.11f, -0.07f), 0, "jitter inside the zone");

  /* The whole reason for rescaling: without it the first thing past the zone
   * jumps straight to 0.2 and a character cannot creep. */
  float x = 0;
  float y = 0;
  orblit_shape_stick(&zone, 0.2001f, 0, &x, &y);
  check(x > 0.0f && x < 0.01f, "deflection is continuous where the zone ends");

  /* A worn stick that cannot quite reach its corner still gets full speed. */
  orblit_shape_stick(&zone, 0.9f, 0, &x, &y);
  near(x, 1.0f, "the outside saturates before the corner");
  orblit_shape_stick(&zone, 1.0f, 0, &x, &y);
  near(x, 1.0f, "and stays there past it");

  near(orblit_shape_scalar(&zone, -0.9f), -1.0f, "a scalar keeps its sign");
  near(orblit_shape_scalar(&zone, 0.9f), 1.0f, "and the other one");

  /* ---- The zone is round, not square ---- */
  const orblit_shaping round_zone = {0.2f, 1.0f, 1.0f};

  group = "round zone";
  /* Pushed gently up and to the right, neither axis passes 0.2 on its own,
   * but the stick is plainly being pushed. A per-axis zone cannot see that. */
  check(0.16f < 0.2f, "neither axis alone clears it");
  check(stick_length(&round_zone, 0.16f, 0.16f) > 0.0f,
        "a slow diagonal survives");

  /* The direction never changes with how hard the stick is pushed, which is
   * the whole reason the zone is measured radially. */
  const float bearing = atan2f(2.0f, 1.0f);
  const float magnitudes[3] = {0.3f, 0.6f, 0.95f};
  for (int i = 0; i < 3; i++) {
    const float length = magnitudes[i] / sqrtf(5.0f);
    orblit_shape_stick(&round_zone, length, 2.0f * length, &x, &y);
    near(atan2f(y, x), bearing, "the same bearing, whatever the length");
  }

  /* ---- Buttons, axes and edges ---- */
  orblit_pads *pads = orblit_pads_create();
  orblit_pad_frame frame;
  check(pads != NULL, "a pad table can be made");
  if (pads == NULL) return 1;

  group = "edges";
  orblit_pad_raw one = raw_pad("uniq:a", 1u << ORBLIT_PAD_SOUTH, 0, 0);
  orblit_pads_poll(pads, &one, 1, &frame);
  check(orblit_pad_is_down(&frame.pads[0], ORBLIT_PAD_SOUTH), "held is down");
  check(orblit_pad_was_pressed(&frame.pads[0], ORBLIT_PAD_SOUTH),
        "and pressed on the poll it arrived");
  orblit_pads_poll(pads, &one, 1, &frame);
  check(orblit_pad_is_down(&frame.pads[0], ORBLIT_PAD_SOUTH),
        "still down while held");
  check(!orblit_pad_was_pressed(&frame.pads[0], ORBLIT_PAD_SOUTH),
        "a press is reported for exactly one poll");
  one.buttons = 0;
  orblit_pads_poll(pads, &one, 1, &frame);
  check((frame.pads[0].released & (1u << ORBLIT_PAD_SOUTH)) != 0,
        "letting go is a release");

  group = "axes";
  /* The platform says +Y down, because it inherited it from screens. */
  one = raw_pad("uniq:a", 0, 0, -0.8f);
  orblit_pads_poll(pads, &one, 1, &frame);
  check(frame.pads[0].stick_y[ORBLIT_PAD_LEFT] > 0.0f,
        "up is positive, whatever the hardware says");
  near(frame.pads[0].raw_stick_y[ORBLIT_PAD_LEFT], 0.8f,
       "and the raw value is kept beside the shaped one, flipped too");

  one = raw_pad("uniq:a", 0, 0, 0);
  one.trigger[ORBLIT_PAD_RIGHT] = 0.5f;
  orblit_pads_poll(pads, &one, 1, &frame);
  check(frame.pads[0].trigger[ORBLIT_PAD_RIGHT] > 0.0f,
        "a trigger runs nought to one");
  check(frame.pads[0].trigger[ORBLIT_PAD_LEFT] == 0.0f, "and never negative");
  one.trigger[ORBLIT_PAD_RIGHT] = 1.0f;
  orblit_pads_poll(pads, &one, 1, &frame);
  near(frame.pads[0].trigger[ORBLIT_PAD_RIGHT], 1.0f,
       "fully pulled is exactly one");

  /* "A trigger past half way is also a button", measured where orblit_input
   * measures it: on the shaped value, after the dead zone is out of the way.
   * A trigger at 0.5 raw shapes to 0.478 with Shaping.triggers, which is
   * *not* past half way -- the difference between the two places this could
   * be measured, and the reason it is measured in only one of them. */
  group = "trigger as button";
  one.trigger[ORBLIT_PAD_RIGHT] = 0.5f;
  orblit_pads_poll(pads, &one, 1, &frame);
  check(!orblit_pad_is_down(&frame.pads[0], ORBLIT_PAD_RIGHT_TRIGGER),
        "half of the raw travel is not yet half of the shaped travel");
  one.trigger[ORBLIT_PAD_RIGHT] = 1.0f;
  orblit_pads_poll(pads, &one, 1, &frame);
  check(orblit_pad_is_down(&frame.pads[0], ORBLIT_PAD_RIGHT_TRIGGER),
        "a trigger past half way is also a button");
  check(orblit_pad_was_pressed(&frame.pads[0], ORBLIT_PAD_RIGHT_TRIGGER),
        "and crossing the line is a press, like any other button");
  one.trigger[ORBLIT_PAD_RIGHT] = 0.0f;
  orblit_pads_poll(pads, &one, 1, &frame);
  check(!orblit_pad_is_down(&frame.pads[0], ORBLIT_PAD_RIGHT_TRIGGER),
        "and letting go is a release");
  check(!orblit_pad_is_down(&frame.pads[0], ORBLIT_PAD_LEFT_TRIGGER),
        "the other trigger was never touched");

  group = "axes";

  group = "d-pad";
  one = raw_pad("uniq:a", (1u << ORBLIT_PAD_DPAD_UP) | (1u << ORBLIT_PAD_DPAD_RIGHT),
                0, 0);
  orblit_pads_poll(pads, &one, 1, &frame);
  orblit_pad_dpad(&frame.pads[0], &x, &y);
  near(x, 1.0f, "the d-pad is a vector, right");
  near(y, 1.0f, "the d-pad is a vector, up");

  /* ---- Slots, absence and hot-plug ---- */
  group = "slots";
  orblit_pads_destroy(pads);
  pads = orblit_pads_create();
  if (pads == NULL) return 1;

  orblit_pad_raw two[2] = {raw_pad("uniq:a", 0, 0, 0), raw_pad("uniq:b", 0, 0, 0)};
  orblit_pads_poll(pads, two, 2, &frame);
  check(orblit_pad_count(&frame) == 2, "two pads take two slots");
  check(strcmp(frame.pads[0].name, "uniq:a") == 0, "in the order they arrived");
  check(strcmp(frame.pads[1].name, "uniq:b") == 0, "one each");

  /* The first pad vanishes mid-corner, holding something. */
  two[0] = raw_pad("uniq:a", 1u << ORBLIT_PAD_DPAD_LEFT, -1.0f, 0);
  orblit_pads_poll(pads, two, 2, &frame);
  orblit_pad_raw only_b = raw_pad("uniq:b", 0, 0, 0);
  orblit_pads_poll(pads, &only_b, 1, &frame);
  check(!frame.pads[0].connected, "a pad that vanishes is absent");
  check(frame.pads[0].down == 0 && frame.pads[0].stick_x[ORBLIT_PAD_LEFT] == 0.0f,
        "and reads as neutral, not as whatever it was holding");
  check(frame.pads[1].connected, "the other one is untouched");

  /* It comes back, and takes its own player number rather than the free one. */
  orblit_pad_raw back[2] = {raw_pad("uniq:b", 0, 0, 0), raw_pad("uniq:a", 0, 0, 0)};
  orblit_pads_poll(pads, back, 2, &frame);
  check(strcmp(frame.pads[0].name, "uniq:a") == 0,
        "a pad that comes back gets its own player number again");
  check(strcmp(frame.pads[1].name, "uniq:b") == 0, "and nobody else moves");

  group = "too many";
  orblit_pads_destroy(pads);
  pads = orblit_pads_create();
  if (pads == NULL) return 1;
  orblit_pad_raw many[ORBLIT_PAD_SLOTS + 2];
  char names[ORBLIT_PAD_SLOTS + 2][8];
  for (int i = 0; i < ORBLIT_PAD_SLOTS + 2; i++) {
    snprintf(names[i], sizeof names[i], "p%d", i);
    many[i] = raw_pad(names[i], 0, 0, 0);
    many[i].name = names[i];
  }
  orblit_pads_poll(pads, many, ORBLIT_PAD_SLOTS + 2, &frame);
  check(orblit_pad_count(&frame) == ORBLIT_PAD_SLOTS,
        "more pads than slots are ignored, not shuffled in");
  check(strcmp(frame.pads[0].name, "p0") == 0, "and the first keeps its slot");

  group = "nobody";
  orblit_pads_destroy(pads);
  pads = orblit_pads_create();
  if (pads == NULL) return 1;
  orblit_pads_poll(pads, NULL, 0, &frame);
  check(orblit_pad_count(&frame) == 0, "no pads is a state, not a failure");
  const orblit_pad_state *first = orblit_pad_first(&frame);
  check(first != NULL, "and first is never NULL");
  check(!first->connected && first->down == 0, "it is a pad holding nothing");
  check(orblit_pad_is_active(first) == 0, "which nobody is touching");
  check(orblit_pad_is_down(NULL, ORBLIT_PAD_SOUTH) == 0, "NULL is safe too");

  group = "first";
  orblit_pad_raw second_only = raw_pad("uniq:b", 0, 0, 0);
  orblit_pads_poll(pads, &second_only, 1, &frame);
  check(orblit_pad_first(&frame)->connected,
        "first is the lowest connected pad");

  orblit_pads_destroy(pads);

  if (failures == 0) {
    printf("orblit_pad: every case passed\n");
    return 0;
  }
  printf("orblit_pad: %d failed\n", failures);
  return 1;
}

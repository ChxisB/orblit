#ifndef ORBLIT_PAD_H
#define ORBLIT_PAD_H

/* Gamepads for a host with no Dart in it.
 *
 * This is package:orblit_input's vocabulary and its three decisions, written
 * again in C because a console host cannot call Dart. It is deliberately a
 * translation rather than a second design: a game ported from the Dart front
 * end to a native one should find the same names meaning the same things, and
 * the two drifting apart is a bug in whichever moved.
 *
 * What is kept, and why each was worth keeping:
 *
 *  - **Buttons are named by position, not by label.** The bottom face button
 *    is south on every pad ever made; what is *printed* on it is A on an Xbox
 *    pad, B on a Nintendo one and a cross on a PlayStation one. Code that
 *    means "jump" wants the position. A prompt drawn on screen wants the
 *    label, which is a different question and is not answered here.
 *  - **Dead zones are radial, and what is left is rescaled.** Per-axis dead
 *    zones square off the middle of the stick and make a slow diagonal
 *    impossible; not rescaling the remainder means the slowest walk available
 *    is fifteen per cent of full speed. Both are in orblit_input's Shaping and
 *    both are here, with the same defaults.
 *  - **Positive is up.** The platforms all report +Y downwards because they
 *    inherited it from screens. A stick is not a screen, the rest of Orblit has
 *    Y up, and a control scheme needing a minus sign to walk forwards is one
 *    somebody eventually forgets.
 *  - **A pad that is not there reads as neutral**, not as whatever it was
 *    holding when it vanished, and takes its slot back when it returns.
 *
 * What is left out, because C has no use for it here: the event list
 * (PadFrame.events). A host that draws every frame can see a press in the
 * `pressed` mask; the one thing that mask cannot express — a tap and release
 * inside a single frame — needs a queue, and nothing in this host yet cares.
 * Said out loud because it is the one real gap against the Dart package.
 *
 * Nothing in this file talks to a platform. The back end fills in raw axis
 * and button values (orblit_host_platform.h) and everything here — shaping,
 * slots, edges, hot-plug — is the same code on a desktop and on a console.
 */

#include <stdint.h>

/* Where a button is. The order is package:orblit_input's PadButton. */
typedef enum orblit_pad_button {
  ORBLIT_PAD_SOUTH = 0, /* Bottom face. A, cross, B on a Nintendo pad. */
  ORBLIT_PAD_EAST,      /* Right face. B, circle, A on a Nintendo pad. */
  ORBLIT_PAD_WEST,      /* Left face. X, square, Y on a Nintendo pad. */
  ORBLIT_PAD_NORTH,     /* Top face. Y, triangle, X on a Nintendo pad. */
  ORBLIT_PAD_LEFT_SHOULDER,
  ORBLIT_PAD_RIGHT_SHOULDER,
  /* The triggers as buttons, for a pad whose triggers do not travel and as a
   * threshold crossing on one whose do. A back end may report either or
   * neither; orblit_pads_poll adds the crossing itself, past half way on the
   * shaped value, so every platform draws the line in the same place and no
   * game has to choose its own. */
  ORBLIT_PAD_LEFT_TRIGGER,
  ORBLIT_PAD_RIGHT_TRIGGER,
  ORBLIT_PAD_SELECT, /* Back, view, select, the minus key. */
  ORBLIT_PAD_START,  /* Start, menu, options, the plus key. */
  ORBLIT_PAD_GUIDE,  /* The one in the middle with the maker's logo on it. */
  ORBLIT_PAD_LEFT_STICK,
  ORBLIT_PAD_RIGHT_STICK,
  ORBLIT_PAD_DPAD_UP,
  ORBLIT_PAD_DPAD_DOWN,
  ORBLIT_PAD_DPAD_LEFT,
  ORBLIT_PAD_DPAD_RIGHT,
  ORBLIT_PAD_BUTTON_COUNT
} orblit_pad_button;

/* One of the two sides, for the sticks and the triggers. */
typedef enum orblit_pad_side { ORBLIT_PAD_LEFT = 0, ORBLIT_PAD_RIGHT = 1 } orblit_pad_side;

/* How many players are possible. A ninth pad is ignored rather than
 * displacing somebody. orblit_input's Pads.maxPads default, for the same
 * reason: it is the number of slots a game's own UI is written against. */
#define ORBLIT_PAD_SLOTS 8

/* How a raw deflection becomes the number a game acts on.
 *
 * orblit_input's Shaping, field for field. `inner` is how far from the middle
 * is treated as the middle, `outer` how far counts as all the way, and
 * `curve` the shape of everything between — one is linear, above one gives
 * finer control near the middle at the cost of the ends. */
typedef struct orblit_shaping {
  float inner;
  float outer;
  float curve;
} orblit_shaping;

/* A stick: a dead zone big enough for a worn spring, saturating early so a
 * pad that can no longer quite reach its corners still reaches full speed. */
extern const orblit_shaping orblit_shaping_sticks;

/* A trigger: a much smaller dead zone, because a trigger rests against a stop
 * rather than on a spring and does not drift, and a linear curve, because a
 * trigger is usually a throttle. */
extern const orblit_shaping orblit_shaping_triggers;

/* `value` with the zone taken out and the rest rescaled, keeping its sign. */
float orblit_shape_scalar(const orblit_shaping *shaping, float value);

/* A stick, shaped as the one thing it is: the direction survives untouched
 * and only the length is shaped, so the way somebody is pushing is never
 * changed by how hard they are pushing. This is the whole reason the dead
 * zone is measured radially. */
void orblit_shape_stick(const orblit_shaping *shaping, float x, float y,
                       float *out_x, float *out_y);

/* What one pad is doing, at the moment it was asked.
 *
 * Complete, so a frame can be built from it without asking a second question
 * that might get a different answer. */
typedef struct orblit_pad_state {
  /* Whether there is a pad here at all. Everything below is nought when not,
   * rather than whatever it was holding when it vanished. */
  int connected;

  /* Bitmasks over orblit_pad_button: held now, went down since the last poll,
   * came up since the last poll. `pressed` is true for exactly one poll per
   * press however long the button is held, which is what a jump wants. */
  uint32_t down;
  uint32_t pressed;
  uint32_t released;

  /* Shaped, indexed by orblit_pad_side. Positive is up and to the right. */
  float stick_x[2];
  float stick_y[2];
  /* Shaped, nought to one, never negative whatever the hardware's range. */
  float trigger[2];

  /* As the hardware reported, normalised to its own range and unshaped: no
   * dead zone, no rescale, no curve. For a calibration screen, and for a game
   * with a good reason to shape a stick its own way. Not for ordinary use.
   *
   * "Unshaped" and not "untouched": positive is still up here, because
   * orblit_input's PadState.rawAxis is also read after the flip — pads.dart
   * negates Y before it fills `raw`, not after. A raw value that disagreed
   * with the shaped one about which way was up would be a trap rather than a
   * diagnostic. */
  float raw_stick_x[2];
  float raw_stick_y[2];
  float raw_trigger[2];

  /* What the driver calls it. Worth showing to a person. */
  const char *name;
} orblit_pad_state;

/* Whether `button` is held now. */
int orblit_pad_is_down(const orblit_pad_state *pad, orblit_pad_button button);

/* Whether `button` went down since the last poll. */
int orblit_pad_was_pressed(const orblit_pad_state *pad, orblit_pad_button button);

/* Whether anything at all is being touched. What a "press any button to
 * start" screen asks. */
int orblit_pad_is_active(const orblit_pad_state *pad);

/* The d-pad as a vector, so it can stand in for a stick without the caller
 * caring which one somebody is using. */
void orblit_pad_dpad(const orblit_pad_state *pad, float *out_x, float *out_y);

/* Where every pad stands. Every slot, connected or not, so a two-player game
 * can index by player number without checking a length first. */
typedef struct orblit_pad_frame {
  orblit_pad_state pads[ORBLIT_PAD_SLOTS];
} orblit_pad_frame;

/* The lowest-numbered pad that is actually there, or a neutral one when
 * nobody is plugged in. What a single-player game wants, and what it should
 * keep wanting when somebody swaps controllers mid-game. */
const orblit_pad_state *orblit_pad_first(const orblit_pad_frame *frame);

/* How many are attached. */
int orblit_pad_count(const orblit_pad_frame *frame);

/* ---- What a platform back end fills in ----
 *
 * The seam between this file and the one that talks to the machine. A back
 * end reports raw numbers in the hardware's own sense — +Y *down*, triggers
 * on whatever scale they came on, already divided out to -1..1 or 0..1 — and
 * everything above turns those into a state. A console back end writes the
 * struct below and nothing else. */
typedef struct orblit_pad_raw {
  /* A name for this pad that survives it being unplugged and plugged back
   * in: its serial number where it has one, its port or GUID otherwise. This
   * is what gives a controller its player number back rather than handing it
   * whatever slot happens to be free — orblit_input's PadIdentity.token. */
  char token[96];
  const char *name;

  uint32_t buttons; /* A bitmask over orblit_pad_button. */
  float stick_x[2];
  float stick_y[2]; /* +Y down, as the hardware says; flipped above. */
  float trigger[2]; /* 0..1. */
} orblit_pad_raw;

/* The slot bookkeeping, hot-plug and shaping, given what the back end saw.
 *
 * `raw` holds `count` pads as the platform found them this poll, in any
 * order. Slots are assigned by token — a pad returns to the slot it had —
 * and a slot whose pad is missing this time reads as absent. */
typedef struct orblit_pads orblit_pads;

orblit_pads *orblit_pads_create(void);
void orblit_pads_destroy(orblit_pads *pads);

/* Folds this poll's raw pads into `frame`. Call once a frame, after the back
 * end has drained the platform. */
void orblit_pads_poll(orblit_pads *pads, const orblit_pad_raw *raw, int count,
                     orblit_pad_frame *frame);

#endif /* ORBLIT_PAD_H */

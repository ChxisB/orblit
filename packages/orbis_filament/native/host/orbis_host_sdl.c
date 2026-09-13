/* orbis_host_platform.h, answered with SDL3.
 *
 * This is the one file in this directory that knows what machine it is on,
 * and it is deliberately the whole of it: a window, a monotonic clock and
 * whatever the platform calls a gamepad, and nothing else. A console back end
 * is this file rewritten against an SDK that cannot be named in public, and
 * every other file here compiles unchanged.
 *
 * Why SDL3 rather than GLFW or a window per platform: the three things a
 * console front end owns are exactly the three SDL owns, it is the only one
 * of the candidates that reads pads at all, and — the part that matters for
 * a port — SDL3 names buttons by position (SDL_GAMEPAD_BUTTON_SOUTH, not
 * SDL_GAMEPAD_BUTTON_A), which is package:orbis_input's decision arrived at
 * independently. The mapping below is therefore one-to-one and has no table
 * of per-vendor lies in it. See README.md for the dependency argument.
 *
 * SDL is not on the link line of anything a console ships. It answers eight
 * calls; the console SDK answers the same eight.
 */

#include "orbis_host_platform.h"

#include <SDL3/SDL.h>
#include <math.h>
#include <stdint.h>
#include <stdio.h>
#include <string.h>

/* More than the eight slots, because a pad can be plugged in and refused a
 * slot and we still have to not walk off the end of the array. */
#define kMaxOpen 16

static struct {
  SDL_Window *window;
  int started;
  /* Apple only: SDL's Metal view, which owns the CAMetalLayer Filament wants.
   * A void * here so this file has no Objective-C in it at all. */
  void *metal_view;
  void *native_window;
  int width;
  int height;

  Uint64 epoch; /* SDL_GetTicksNS at open, so seconds start near nought. */

  SDL_Gamepad *open[kMaxOpen];
  orbis_pads *pads;
  orbis_pad_frame frame;
} host;

/* SDL3's button order is not orbis_pad_button's — SDL puts BACK, GUIDE and
 * START before the sticks and the shoulders, and has no trigger buttons at
 * all — so the two are written out against each other rather than assumed to
 * line up. The names on both sides are positions, which is why this is a
 * table of sixteen entries and not a table of vendors. */
static const struct {
  orbis_pad_button ours;
  SDL_GamepadButton theirs;
} kButtons[] = {
    {ORBIS_PAD_SOUTH, SDL_GAMEPAD_BUTTON_SOUTH},
    {ORBIS_PAD_EAST, SDL_GAMEPAD_BUTTON_EAST},
    {ORBIS_PAD_WEST, SDL_GAMEPAD_BUTTON_WEST},
    {ORBIS_PAD_NORTH, SDL_GAMEPAD_BUTTON_NORTH},
    {ORBIS_PAD_LEFT_SHOULDER, SDL_GAMEPAD_BUTTON_LEFT_SHOULDER},
    {ORBIS_PAD_RIGHT_SHOULDER, SDL_GAMEPAD_BUTTON_RIGHT_SHOULDER},
    {ORBIS_PAD_SELECT, SDL_GAMEPAD_BUTTON_BACK},
    {ORBIS_PAD_START, SDL_GAMEPAD_BUTTON_START},
    {ORBIS_PAD_GUIDE, SDL_GAMEPAD_BUTTON_GUIDE},
    {ORBIS_PAD_LEFT_STICK, SDL_GAMEPAD_BUTTON_LEFT_STICK},
    {ORBIS_PAD_RIGHT_STICK, SDL_GAMEPAD_BUTTON_RIGHT_STICK},
    {ORBIS_PAD_DPAD_UP, SDL_GAMEPAD_BUTTON_DPAD_UP},
    {ORBIS_PAD_DPAD_DOWN, SDL_GAMEPAD_BUTTON_DPAD_DOWN},
    {ORBIS_PAD_DPAD_LEFT, SDL_GAMEPAD_BUTTON_DPAD_LEFT},
    {ORBIS_PAD_DPAD_RIGHT, SDL_GAMEPAD_BUTTON_DPAD_RIGHT},
};

/* Where a travelling trigger counts as pressed. Not orbis_shaping_triggers'
 * dead zone, which is about a trigger that has not been touched: this is the
 * point past which somebody clearly means it. SDL's own
 * SDL_GAMEPAD_AXIS_MAX / 2 convention, in the same units everything else
 * here uses. */
static const float kTriggerAsButton = 0.5f;

static float axis_of(SDL_Gamepad *pad, SDL_GamepadAxis axis) {
  const Sint16 raw = SDL_GetGamepadAxis(pad, axis);
  /* Divided by 32767 rather than 32768, so a stick that reaches its stop
   * reads as exactly one and orbis_shaping's `outer` can be reached. The
   * extra count at the negative end is clamped rather than left to come out
   * at -1.00003. */
  float value = (float)raw / 32767.0f;
  if (value < -1.0f) value = -1.0f;
  return value;
}

/* A trigger, nought to one. SDL reports them 0..32767 and never negative. */
static float trigger_of(SDL_Gamepad *pad, SDL_GamepadAxis axis) {
  const Sint16 raw = SDL_GetGamepadAxis(pad, axis);
  return raw <= 0 ? 0.0f : (float)raw / 32767.0f;
}

/* A name for this pad that survives a replug: its serial number where the
 * driver knows one, the port it is on otherwise. PadIdentity.token's rule,
 * with SDL answering the same two questions evdev does. */
static void token_of(SDL_Gamepad *pad, char *to, size_t capacity) {
  const char *serial = SDL_GetGamepadSerial(pad);
  const char *path = SDL_GetGamepadPath(pad);
  const char *name = SDL_GetGamepadName(pad);
  const char *prefix = "uniq:";
  const char *body = serial;
  if (body == NULL || body[0] == '\0') {
    prefix = "path:";
    body = path;
  }
  if (body == NULL || body[0] == '\0') {
    prefix = "";
    body = name != NULL ? name : "pad";
  }
  SDL_snprintf(to, capacity, "%s%s", prefix, body);
}

/* Opens every gamepad the platform can see that is not open already, and
 * drops the ones that have gone. Called at open and on every add or remove
 * event, rather than every frame: SDL_GetGamepads allocates. */
static void reopen_pads(void) {
  for (int i = 0; i < kMaxOpen; i++) {
    if (host.open[i] != NULL && !SDL_GamepadConnected(host.open[i])) {
      SDL_CloseGamepad(host.open[i]);
      host.open[i] = NULL;
    }
  }
  int count = 0;
  SDL_JoystickID *ids = SDL_GetGamepads(&count);
  if (ids == NULL) return;
  for (int i = 0; i < count; i++) {
    SDL_Gamepad *already = SDL_GetGamepadFromID(ids[i]);
    int held = 0;
    for (int s = 0; s < kMaxOpen && !held; s++) {
      if (host.open[s] != NULL && host.open[s] == already) held = 1;
    }
    if (held) continue;
    SDL_Gamepad *pad = SDL_OpenGamepad(ids[i]);
    if (pad == NULL) continue;
    int placed = 0;
    for (int s = 0; s < kMaxOpen && !placed; s++) {
      if (host.open[s] == NULL) {
        host.open[s] = pad;
        placed = 1;
      }
    }
    if (!placed) SDL_CloseGamepad(pad);
  }
  SDL_free(ids);
}

/* ---- A pad that is not there ----
 *
 * ORBIS_HOST_VIRTUAL_PAD attaches one of SDL's virtual gamepads and walks its
 * left stick slowly round a circle, pressing the bottom face button every few
 * seconds. It is how the pad path is exercised on a machine with no
 * controller plugged into it: the virtual pad goes in at the bottom of SDL,
 * so everything above — enumeration, the add event, the token, the shaping,
 * the slot, the edges and the walking — is the same code a real pad drives,
 * rather than a second path that could quietly rot.
 *
 * package:orbis_input answers the same problem the same way, with
 * tool/virtual_pad.dart writing uinput events on Linux.
 */
static SDL_JoystickID virtual_id;

static void attach_virtual_pad(void) {
  const char *want = SDL_getenv("ORBIS_HOST_VIRTUAL_PAD");
  if (want == NULL || want[0] == '\0' || want[0] == '0') return;

  SDL_VirtualJoystickDesc desc;
  SDL_INIT_INTERFACE(&desc);
  desc.type = SDL_JOYSTICK_TYPE_GAMEPAD;
  desc.naxes = SDL_GAMEPAD_AXIS_COUNT;
  desc.nbuttons = SDL_GAMEPAD_BUTTON_COUNT;
  desc.name = "Orbis virtual pad";
  virtual_id = SDL_AttachVirtualJoystick(&desc);
  if (virtual_id == 0) {
    fprintf(stderr, "no virtual pad: %s\n", SDL_GetError());
  }
}

/* Where the virtual pad stands at `seconds`. The stick sweeps round at a rate
 * that walks a circle comfortably inside the ring — the player's speed
 * divided by this rate is the radius it traces — and the south button goes
 * down every four seconds, so the edge detection has something to detect. */
static void drive_virtual_pad(double seconds) {
  if (virtual_id == 0) return;
  SDL_Joystick *stick = SDL_GetJoystickFromID(virtual_id);
  if (stick == NULL) return;
  const double angle = seconds * 1.6;
  SDL_SetJoystickVirtualAxis(stick, SDL_GAMEPAD_AXIS_LEFTX,
                             (Sint16)(SDL_sin(angle) * 32000.0));
  SDL_SetJoystickVirtualAxis(stick, SDL_GAMEPAD_AXIS_LEFTY,
                             (Sint16)(-SDL_cos(angle) * 32000.0));
  SDL_SetJoystickVirtualButton(stick, SDL_GAMEPAD_BUTTON_SOUTH,
                               SDL_fmod(seconds, 4.0) < 0.1);
}

/* A pad made out of the keyboard, when there is no pad.
 *
 * A desktop convenience and nothing more: a console back end has no
 * counterpart to it and should not grow one. It is here because a machine
 * with no controller plugged into it is otherwise a machine on which the
 * walking cannot be tried at all, and because sending it through
 * orbis_pad_raw like everything else means the keys exercise the same
 * shaping, slots and edges a real pad does rather than a second path that
 * could quietly rot.
 *
 * WASD or the arrows walk, IJKL turns the camera, Q and E pull the camera in
 * and out, space is the south button and Return is start. Returns how many
 * pads it filled in, which is nought when nothing is held and one otherwise
 * — so an idle keyboard reads as no pad at all rather than as a pad sitting
 * at rest, and "press any button" still means something. */
static int keyboard_pad(orbis_pad_raw *to) {
  const bool *keys = SDL_GetKeyboardState(NULL);
  if (keys == NULL) return 0;

  memset(to, 0, sizeof *to);
  SDL_snprintf(to->token, sizeof to->token, "keys:");
  to->name = "keyboard";

  const float x = (float)(keys[SDL_SCANCODE_D] || keys[SDL_SCANCODE_RIGHT]) -
                  (float)(keys[SDL_SCANCODE_A] || keys[SDL_SCANCODE_LEFT]);
  const float y = (float)(keys[SDL_SCANCODE_S] || keys[SDL_SCANCODE_DOWN]) -
                  (float)(keys[SDL_SCANCODE_W] || keys[SDL_SCANCODE_UP]);
  /* Reported the way the hardware would: +Y down, full deflection, and a
   * diagonal normalised so two keys are not faster than one. orbis_pad.c
   * flips and shapes it exactly as it would a stick. */
  const float length = sqrtf(x * x + y * y);
  if (length > 0.0f) {
    to->stick_x[ORBIS_PAD_LEFT] = x / length;
    to->stick_y[ORBIS_PAD_LEFT] = y / length;
  }
  const float rx = (float)keys[SDL_SCANCODE_L] - (float)keys[SDL_SCANCODE_J];
  const float ry = (float)keys[SDL_SCANCODE_K] - (float)keys[SDL_SCANCODE_I];
  const float turn = sqrtf(rx * rx + ry * ry);
  if (turn > 0.0f) {
    to->stick_x[ORBIS_PAD_RIGHT] = rx / turn;
    to->stick_y[ORBIS_PAD_RIGHT] = ry / turn;
  }
  if (keys[SDL_SCANCODE_Q]) to->trigger[ORBIS_PAD_LEFT] = 1.0f;
  if (keys[SDL_SCANCODE_E]) to->trigger[ORBIS_PAD_RIGHT] = 1.0f;
  if (keys[SDL_SCANCODE_SPACE]) to->buttons |= 1u << ORBIS_PAD_SOUTH;
  if (keys[SDL_SCANCODE_RETURN]) to->buttons |= 1u << ORBIS_PAD_START;
  if (to->trigger[ORBIS_PAD_LEFT] > 0.0f) {
    to->buttons |= 1u << ORBIS_PAD_LEFT_TRIGGER;
  }
  if (to->trigger[ORBIS_PAD_RIGHT] > 0.0f) {
    to->buttons |= 1u << ORBIS_PAD_RIGHT_TRIGGER;
  }

  return to->buttons != 0 || length > 0.0f || turn > 0.0f ? 1 : 0;
}

/* What Filament's createSwapChain takes here. Opaque above this line; below
 * it, exactly one of these three per platform. */
static void *native_window_of(SDL_Window *window) {
#if defined(SDL_PLATFORM_APPLE)
  /* Filament's Metal and WebGPU backends both want a CAMetalLayer, and SDL's
   * Metal view is the supported way to get one attached to an SDL window
   * that also resizes with it. */
  host.metal_view = SDL_Metal_CreateView(window);
  if (host.metal_view == NULL) return NULL;
  return SDL_Metal_GetLayer(host.metal_view);
#elif defined(SDL_PLATFORM_WIN32)
  return SDL_GetPointerProperty(SDL_GetWindowProperties(window),
                                SDL_PROP_WINDOW_WIN32_HWND_POINTER, NULL);
#else
  /* X11. Filament's VulkanPlatformLinux casts the pointer straight back to
   * an X11 Window — `(Window) nativeWindow` — so the window id goes in as a
   * number wearing a pointer, which is what that cast expects and is why
   * this is not a real address. Wayland wants a { wl_display*, wl_surface*,
   * width, height } struct instead, and Filament's Linux release is built
   * for X11, so the host asks SDL for X11 (see orbis_host_open). */
  const Sint64 id = SDL_GetNumberProperty(SDL_GetWindowProperties(window),
                                          SDL_PROP_WINDOW_X11_WINDOW_NUMBER, 0);
  if (id == 0) return NULL;
  return (void *)(uintptr_t)id;
#endif
}

int orbis_host_open(const char *title, int width, int height) {
  if (host.started) return 1;

#if !defined(SDL_PLATFORM_APPLE) && !defined(SDL_PLATFORM_WIN32)
  /* Before SDL_Init, because it decides which video driver is chosen. A
   * Wayland session would otherwise be picked first and hand Filament a
   * wl_surface it is not built to take. Overridable, for somebody whose
   * Filament build is: SDL_VIDEODRIVER is SDL's own variable. */
  SDL_SetHint(SDL_HINT_VIDEO_DRIVER, "x11");
#endif

  if (!SDL_Init(SDL_INIT_VIDEO | SDL_INIT_GAMEPAD)) {
    fprintf(stderr, "SDL would not start: %s\n", SDL_GetError());
    return 0;
  }

  host.window = SDL_CreateWindow(title != NULL ? title : "Orbis", width, height,
                                 SDL_WINDOW_RESIZABLE |
                                     SDL_WINDOW_HIGH_PIXEL_DENSITY);
  if (host.window == NULL) {
    fprintf(stderr, "no window: %s\n", SDL_GetError());
    SDL_Quit();
    return 0;
  }

  host.native_window = native_window_of(host.window);
  if (host.native_window == NULL) {
    fprintf(stderr, "no native window to draw into: %s\n", SDL_GetError());
    SDL_DestroyWindow(host.window);
    host.window = NULL;
    SDL_Quit();
    return 0;
  }

  SDL_GetWindowSizeInPixels(host.window, &host.width, &host.height);
  host.epoch = SDL_GetTicksNS();
  host.pads = orbis_pads_create();
  if (host.pads == NULL) {
    fprintf(stderr, "out of memory\n");
    orbis_host_close();
    return 0;
  }
  attach_virtual_pad();
  reopen_pads();
  host.started = 1;
  return 1;
}

void orbis_host_close(void) {
  if (virtual_id != 0) {
    SDL_DetachVirtualJoystick(virtual_id);
    virtual_id = 0;
  }
  for (int i = 0; i < kMaxOpen; i++) {
    if (host.open[i] != NULL) SDL_CloseGamepad(host.open[i]);
    host.open[i] = NULL;
  }
  orbis_pads_destroy(host.pads);
  host.pads = NULL;
#if defined(SDL_PLATFORM_APPLE)
  if (host.metal_view != NULL) SDL_Metal_DestroyView(host.metal_view);
  host.metal_view = NULL;
#endif
  if (host.window != NULL) SDL_DestroyWindow(host.window);
  host.window = NULL;
  host.native_window = NULL;
  if (host.started) SDL_Quit();
  host.started = 0;
}

void *orbis_host_native_window(void) { return host.native_window; }

void orbis_host_size(int *width, int *height) {
  if (width != NULL) *width = host.width > 0 ? host.width : 1;
  if (height != NULL) *height = host.height > 0 ? host.height : 1;
}

double orbis_host_seconds(void) {
  /* SDL_GetTicksNS is monotonic and never jumps when the wall clock is
   * corrected, which is the contract orbis::now() keeps on the other side of
   * the C ABI. Nanoseconds as an integer first, so the subtraction is exact
   * and only the division loses anything. */
  return (double)(SDL_GetTicksNS() - host.epoch) / 1e9;
}

int orbis_host_pump(int *resized) {
  if (resized != NULL) *resized = 0;
  if (!host.started) return 0;

  drive_virtual_pad(orbis_host_seconds());

  int running = 1;
  SDL_Event event;
  while (SDL_PollEvent(&event)) {
    switch (event.type) {
      case SDL_EVENT_QUIT:
        running = 0;
        break;
      case SDL_EVENT_WINDOW_CLOSE_REQUESTED:
        if (event.window.windowID == SDL_GetWindowID(host.window)) running = 0;
        break;
      case SDL_EVENT_KEY_DOWN:
        /* Escape closes it. The one keyboard key this host reads, because a
         * window with no title bar decoration to click is otherwise hard to
         * be rid of, and because a console's own "quit" is the platform's
         * business rather than a key. */
        if (event.key.key == SDLK_ESCAPE) running = 0;
        break;
      case SDL_EVENT_GAMEPAD_ADDED:
      case SDL_EVENT_GAMEPAD_REMOVED:
        reopen_pads();
        break;
      default:
        break;
    }
  }

  int width = host.width;
  int height = host.height;
  SDL_GetWindowSizeInPixels(host.window, &width, &height);
  if (width > 0 && height > 0 && (width != host.width || height != host.height)) {
    host.width = width;
    host.height = height;
    if (resized != NULL) *resized = 1;
  }

  /* Every open pad, as the platform found it this poll, in whatever order
   * SDL holds them. orbis_pads_poll does the slots. */
  orbis_pad_raw raw[kMaxOpen];
  int count = 0;
  for (int i = 0; i < kMaxOpen; i++) {
    SDL_Gamepad *pad = host.open[i];
    if (pad == NULL || !SDL_GamepadConnected(pad)) continue;
    orbis_pad_raw *to = &raw[count++];
    memset(to, 0, sizeof *to);
    token_of(pad, to->token, sizeof to->token);
    to->name = SDL_GetGamepadName(pad);

    for (size_t b = 0; b < sizeof kButtons / sizeof *kButtons; b++) {
      if (SDL_GetGamepadButton(pad, kButtons[b].theirs)) {
        to->buttons |= 1u << (unsigned)kButtons[b].ours;
      }
    }
    to->stick_x[ORBIS_PAD_LEFT] = axis_of(pad, SDL_GAMEPAD_AXIS_LEFTX);
    to->stick_y[ORBIS_PAD_LEFT] = axis_of(pad, SDL_GAMEPAD_AXIS_LEFTY);
    to->stick_x[ORBIS_PAD_RIGHT] = axis_of(pad, SDL_GAMEPAD_AXIS_RIGHTX);
    to->stick_y[ORBIS_PAD_RIGHT] = axis_of(pad, SDL_GAMEPAD_AXIS_RIGHTY);
    to->trigger[ORBIS_PAD_LEFT] =
        trigger_of(pad, SDL_GAMEPAD_AXIS_LEFT_TRIGGER);
    to->trigger[ORBIS_PAD_RIGHT] =
        trigger_of(pad, SDL_GAMEPAD_AXIS_RIGHT_TRIGGER);

    /* The triggers as buttons. SDL has no such button — a pad whose triggers
     * travel reports only the axis — so the crossing is made here, which is
     * what orbis_pad.h says these two mean. */
    if (to->trigger[ORBIS_PAD_LEFT] >= kTriggerAsButton) {
      to->buttons |= 1u << ORBIS_PAD_LEFT_TRIGGER;
    }
    if (to->trigger[ORBIS_PAD_RIGHT] >= kTriggerAsButton) {
      to->buttons |= 1u << ORBIS_PAD_RIGHT_TRIGGER;
    }
  }
  if (count == 0) count = keyboard_pad(&raw[0]);
  orbis_pads_poll(host.pads, raw, count, &host.frame);

  return running;
}

const orbis_pad_frame *orbis_host_pads(void) { return &host.frame; }

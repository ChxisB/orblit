#ifndef ORBIS_HOST_PLATFORM_H
#define ORBIS_HOST_PLATFORM_H

/* The whole of what this host needs from the machine it runs on.
 *
 * Three things — a window, a clock and a pad — behind eight calls, and that
 * is the point of the file. Everything else in this directory (the scene, the
 * camera, the walking, the frame loop) is the same code everywhere, so a port
 * to a platform whose SDK cannot be discussed in public is this header
 * answered again and nothing else touched.
 *
 * The seam is drawn exactly where OrbisSurface.h draws the renderer's own:
 * the native window leaves as an opaque pointer, because the only thing that
 * needs to know what it really is is Filament, and the only thing that needs
 * to know how it was made is the back end that made it.
 *
 * orbis_host_sdl.c answers all of this with SDL3 for macOS, Linux and
 * Windows. That file is the one a console back end replaces.
 */

#include "orbis_pad.h"

/* Opens a window and whatever the platform needs to read pads.
 *
 * `width` and `height` are a request, not a promise — a platform that only
 * has one size (a console, a phone) is free to ignore them, which is why
 * nothing below trusts them and orbis_host_size is asked instead. Non-zero on
 * success; the reason is printed on failure. */
int orbis_host_open(const char *title, int width, int height);

/* Closes it. Safe before a successful open, and safe twice. */
void orbis_host_close(void);

/* What Filament's createSwapChain takes on this platform: a CAMetalLayer* on
 * Apple, an X11 Window on Linux, an HWND on Windows — the pointer that goes
 * into orbis_surface_desc.window for ORBIS_SURFACE_WINDOW.
 *
 * Opaque here on purpose. A host that knew which of those it had would be a
 * host that only worked there. */
void *orbis_host_native_window(void);

/* The drawable size in pixels, which on a high-density display is not the
 * size the window was asked for. This is what the renderer is sized to. */
void orbis_host_size(int *width, int *height);

/* Seconds on a clock that only means anything relative to itself, monotonic,
 * never jumping when the wall clock is corrected.
 *
 * The same contract as orbis::now() in the renderer's own platform layer, and
 * the number handed to orbis_renderer_draw. A host owns the clock — that is
 * half of what a front end is — so this is where a console's own timebase
 * would go in. */
double orbis_host_seconds(void);

/* Drains the platform's events and this frame's pads.
 *
 * Returns zero when the host should stop: the window closed, or the platform
 * asked the application to quit. `resized` is set when the drawable size
 * changed since the last pump, which is the host's cue to resize the
 * renderer — nothing here does that on the host's behalf, because on some
 * platforms it is not free. */
int orbis_host_pump(int *resized);

/* Where every pad stood at the last orbis_host_pump. Never NULL: with nothing
 * plugged in, every slot reads as an absent pad holding nothing. */
const orbis_pad_frame *orbis_host_pads(void);

#endif /* ORBIS_HOST_PLATFORM_H */

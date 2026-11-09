/**************************************************************************/
/*  platform_gl.h                                                         */

#pragma once

#ifndef GLES_API_ENABLED
#define GLES_API_ENABLED // Allow using GLES.
#endif

// Make using *glGetProcAddress() an error on the web.
#define glGetProcAddress(n) static_assert(false, "Usage of glGetProcessAddress() on the web is a bug.")
#define eglGetProcAddress(n) static_assert(false, "Usage of eglGetProcessAddress() on the web is a bug.")

#include "platform/web/webgl2.h" // IWYU pragma: export.

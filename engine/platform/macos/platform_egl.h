/**************************************************************************/
/*  platform_egl.h                                                        */

#pragma once

#if defined(ANGLE_ENABLED) && defined(EGL_STATIC)
#define KHRONOS_STATIC 1
// IWYU pragma: begin_exports.
#include <thirdparty/angle/include/EGL/egl.h>
#include <thirdparty/angle/include/EGL/eglext.h>
// IWYU pragma: end_exports.
#undef KHRONOS_STATIC
#endif

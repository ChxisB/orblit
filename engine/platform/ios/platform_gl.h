/**************************************************************************/
/*  platform_gl.h                                                         */

#pragma once

#ifdef GLES3_ENABLED
#ifndef GLES_API_ENABLED
#define GLES_API_ENABLED // Allow using GLES.
#endif

#include <ES3/gl.h> // IWYU pragma: export.
#endif

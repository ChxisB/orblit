/**************************************************************************/
/*  platform_gl.h                                                         */

#pragma once

#ifndef GL_API_ENABLED
#define GL_API_ENABLED // Allow using desktop GL.
#endif

#ifndef GLES_API_ENABLED
#define GLES_API_ENABLED // Allow using GLES.
#endif

#ifndef GLAD_GLES2
#define GLAD_GLES2
#endif

#include <thirdparty/glad/glad/gl.h> // IWYU pragma: export.

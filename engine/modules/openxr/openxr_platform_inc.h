/**************************************************************************/
/*  openxr_platform_inc.h                                                 */

#pragma once

// In various places we need to include platform definitions but we can't
// include these in our normal header files as we'll end up with issues.

// IWYU pragma: begin_keep

#ifdef VULKAN_ENABLED
#define XR_USE_GRAPHICS_API_VULKAN
#include "backends/vulkan/rendering_context_driver_vulkan.h"
#endif // VULKAN_ENABLED

#ifdef METAL_ENABLED
#define XR_USE_GRAPHICS_API_METAL
#include "backends/metal/rendering_context_driver_metal.h"
#endif // METAL_ENABLED

#if defined(GLES3_ENABLED) && !defined(MACOS_ENABLED)
#ifdef ANDROID_ENABLED
#define XR_USE_GRAPHICS_API_OPENGL_ES
#include <EGL/egl.h>
#include <EGL/eglext.h>
#include <GLES3/gl3.h>
#include <GLES3/gl3ext.h>
#else
#define XR_USE_GRAPHICS_API_OPENGL
#endif // ANDROID_ENABLED
#if defined(LINUXBSD_ENABLED) && defined(EGL_ENABLED)
#ifdef GLAD_ENABLED
#include <thirdparty/glad/glad/egl.h>
#else
#include <EGL/egl.h>
#endif // GLAD_ENABLED
#endif // defined(LINUXBSD_ENABLED) && defined(EGL_ENABLED)
#ifdef X11_ENABLED
#define GL_GLEXT_PROTOTYPES 1
#define GL3_PROTOTYPES 1
#include <thirdparty/glad/glad/gl.h>
#include <thirdparty/glad/glad/glx.h>
#endif // X11_ENABLED
#endif // defined(GLES3_ENABLED) && !defined(MACOS_ENABLED)

#ifdef D3D12_ENABLED
#define XR_USE_GRAPHICS_API_D3D12
#include "backends/d3d12/rendering_context_driver_d3d12.h"
#endif // D3D12_ENABLED

#ifdef X11_ENABLED
#include <X11/Xlib.h>
#endif // X11_ENABLED

#ifdef WINDOWS_ENABLED
#define COM_NO_WINDOWS_H
#include <objbase.h>
#include <unknwn.h> // codespell:ignore unknwn
#endif // WINDOWS_ENABLED

#ifdef ANDROID_ENABLED
// The jobject type from jni.h is used by openxr_platform.h on Android.
#include <jni.h>
#endif // ANDROID_ENABLED

// Include platform dependent structs.
#include <openxr/openxr_platform.h>

// IWYU pragma: end_keep

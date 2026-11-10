/**************************************************************************/
/*  liborblit.h                                                           */

#pragma once

#include "core/native_extension/native_extension_interface.gen.h"

#ifdef __cplusplus
extern "C" {
#endif // __cplusplus

// Export macros for DLL visibility
#if defined(_MSC_VER) || defined(__MINGW32__)
#define LIBORBLIT_API __declspec(dllexport)
#elif defined(__GNUC__) || defined(__clang__)
#define LIBORBLIT_API __attribute__((visibility("default")))
#else
#define LIBORBLIT_API
#endif

/**
 * @name liborblit_create_instance
 * @since 1.0
 *
 * Creates a new Orblit instance.
 *
 * @param p_argc The number of command line arguments.
 * @param p_argv The C-style array of command line arguments.
 * @param p_init_func NativeExtension initialization function of the host application.
 *
 * @return A pointer to created \ref OrblitInstance NativeExtension object or nullptr if there was an error.
 */
LIBORBLIT_API NativeExtensionObjectPtr liborblit_create_instance(int p_argc, char *p_argv[], NativeExtensionInitializationFunction p_init_func);

/**
 * @name liborblit_destroy_instance
 * @since 1.0
 *
 * Destroys an existing Orblit instance.
 *
 * @param p_instance The reference to the OrblitInstance object to destroy.
 *
 */
LIBORBLIT_API void liborblit_destroy_instance(NativeExtensionObjectPtr p_instance);

#ifdef __cplusplus
}
#endif // __cplusplus

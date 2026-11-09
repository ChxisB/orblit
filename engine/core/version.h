/**************************************************************************/
/*  version.h                                                             */

#pragma once

#include "core/version_generated.gen.h" // IWYU pragma: export

// Copied from typedefs.h to stay lean.
#ifndef _STR
#define _STR(m_x) #m_x
#define _MKSTR(m_x) _STR(m_x)
#endif

// Orblit versions are of the form <major>.<minor> for the initial release,
// and then <major>.<minor>.<patch> for subsequent bugfix releases where <patch> != 0
// That's arbitrary, but we find it pretty and it's the current policy.

// Defines the main "branch" version. Patch versions in this branch should be
// forward-compatible.
// Example: "3.1"
#define ORBLIT_VERSION_BRANCH _MKSTR(ORBLIT_VERSION_MAJOR) "." _MKSTR(ORBLIT_VERSION_MINOR)
#if ORBLIT_VERSION_PATCH
// Example: "3.1.4"
#define ORBLIT_VERSION_NUMBER ORBLIT_VERSION_BRANCH "." _MKSTR(ORBLIT_VERSION_PATCH)
#else // patch is 0, we don't include it in the "pretty" version number.
// Example: "3.1" instead of "3.1.0"
#define ORBLIT_VERSION_NUMBER ORBLIT_VERSION_BRANCH
#endif // ORBLIT_VERSION_PATCH

// Version number encoded as hexadecimal int with one byte for each number,
// for easy comparison from code.
// Example: 3.1.4 will be 0x030104, making comparison easy from script.
#define ORBLIT_VERSION_HEX 0x10000 * ORBLIT_VERSION_MAJOR + 0x100 * ORBLIT_VERSION_MINOR + ORBLIT_VERSION_PATCH

// Describes the full configuration of that Orblit version, including the version number,
// the status (beta, stable, etc.), potential module-specific features (e.g. mono)
// and double-precision status.
// Example: "3.1.4.stable.mono.double"
#ifdef REAL_T_IS_DOUBLE
#define ORBLIT_VERSION_FULL_CONFIG ORBLIT_VERSION_NUMBER "." ORBLIT_VERSION_STATUS ORBLIT_VERSION_MODULE_CONFIG ".double"
#else
#define ORBLIT_VERSION_FULL_CONFIG ORBLIT_VERSION_NUMBER "." ORBLIT_VERSION_STATUS ORBLIT_VERSION_MODULE_CONFIG
#endif

// Similar to ORBLIT_VERSION_FULL_CONFIG, but also includes the (potentially custom) ORBLIT_VERSION_BUILD
// description (e.g. official, custom_build, etc.).
// Example: "3.1.4.stable.mono.double.official"
#define ORBLIT_VERSION_FULL_BUILD ORBLIT_VERSION_FULL_CONFIG "." ORBLIT_VERSION_BUILD

// Same as above, but prepended with Orblit's name and a cosmetic "v" for "version".
// Example: "Orblit v3.1.4.stable.official.mono.double"
#define ORBLIT_VERSION_FULL_NAME ORBLIT_VERSION_NAME " v" ORBLIT_VERSION_FULL_BUILD

// Git commit hash, generated at build time in `core/version_hash.gen.cpp`.
extern const char *const ORBLIT_VERSION_HASH;

// Git commit date UNIX timestamp (in seconds), generated at build time in `core/version_hash.gen.cpp`.
// Set to 0 if unknown.
extern const unsigned long long ORBLIT_VERSION_TIMESTAMP;

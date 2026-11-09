/**************************************************************************/
/*  interop_types.h                                                       */

#pragma once

#include "core/math/math_defs.h"

#ifdef __cplusplus
extern "C" {
#endif

// This is taken from the old GDNative, which was removed.

#define ORBLIT_VARIANT_SIZE (sizeof(real_t) * 4 + sizeof(int64_t))

typedef struct {
	uint8_t _dont_touch_that[ORBLIT_VARIANT_SIZE];
} orblit_variant;

#define ORBLIT_ARRAY_SIZE sizeof(void *)

typedef struct {
	uint8_t _dont_touch_that[ORBLIT_ARRAY_SIZE];
} orblit_array;

#define ORBLIT_DICTIONARY_SIZE sizeof(void *)

typedef struct {
	uint8_t _dont_touch_that[ORBLIT_DICTIONARY_SIZE];
} orblit_dictionary;

#define ORBLIT_STRING_SIZE sizeof(void *)

typedef struct {
	uint8_t _dont_touch_that[ORBLIT_STRING_SIZE];
} orblit_string;

#define ORBLIT_STRING_NAME_SIZE sizeof(void *)

typedef struct {
	uint8_t _dont_touch_that[ORBLIT_STRING_NAME_SIZE];
} orblit_string_name;

#define ORBLIT_PACKED_ARRAY_SIZE (2 * sizeof(void *))

typedef struct {
	uint8_t _dont_touch_that[ORBLIT_PACKED_ARRAY_SIZE];
} orblit_packed_array;

#define ORBLIT_VECTOR2_SIZE (sizeof(real_t) * 2)

typedef struct {
	uint8_t _dont_touch_that[ORBLIT_VECTOR2_SIZE];
} orblit_vector2;

#define ORBLIT_VECTOR2I_SIZE (sizeof(int32_t) * 2)

typedef struct {
	uint8_t _dont_touch_that[ORBLIT_VECTOR2I_SIZE];
} orblit_vector2i;

#define ORBLIT_RECT2_SIZE (sizeof(real_t) * 4)

typedef struct orblit_rect2 {
	uint8_t _dont_touch_that[ORBLIT_RECT2_SIZE];
} orblit_rect2;

#define ORBLIT_RECT2I_SIZE (sizeof(int32_t) * 4)

typedef struct orblit_rect2i {
	uint8_t _dont_touch_that[ORBLIT_RECT2I_SIZE];
} orblit_rect2i;

#define ORBLIT_VECTOR3_SIZE (sizeof(real_t) * 3)

typedef struct {
	uint8_t _dont_touch_that[ORBLIT_VECTOR3_SIZE];
} orblit_vector3;

#define ORBLIT_VECTOR3I_SIZE (sizeof(int32_t) * 3)

typedef struct {
	uint8_t _dont_touch_that[ORBLIT_VECTOR3I_SIZE];
} orblit_vector3i;

#define ORBLIT_TRANSFORM2D_SIZE (sizeof(real_t) * 6)

typedef struct {
	uint8_t _dont_touch_that[ORBLIT_TRANSFORM2D_SIZE];
} orblit_transform2d;

#define ORBLIT_VECTOR4_SIZE (sizeof(real_t) * 4)

typedef struct {
	uint8_t _dont_touch_that[ORBLIT_VECTOR4_SIZE];
} orblit_vector4;

#define ORBLIT_VECTOR4I_SIZE (sizeof(int32_t) * 4)

typedef struct {
	uint8_t _dont_touch_that[ORBLIT_VECTOR4I_SIZE];
} orblit_vector4i;

#define ORBLIT_PLANE_SIZE (sizeof(real_t) * 4)

typedef struct {
	uint8_t _dont_touch_that[ORBLIT_PLANE_SIZE];
} orblit_plane;

#define ORBLIT_QUATERNION_SIZE (sizeof(real_t) * 4)

typedef struct {
	uint8_t _dont_touch_that[ORBLIT_QUATERNION_SIZE];
} orblit_quaternion;

#define ORBLIT_AABB_SIZE (sizeof(real_t) * 6)

typedef struct {
	uint8_t _dont_touch_that[ORBLIT_AABB_SIZE];
} orblit_aabb;

#define ORBLIT_BASIS_SIZE (sizeof(real_t) * 9)

typedef struct {
	uint8_t _dont_touch_that[ORBLIT_BASIS_SIZE];
} orblit_basis;

#define ORBLIT_TRANSFORM3D_SIZE (sizeof(real_t) * 12)

typedef struct {
	uint8_t _dont_touch_that[ORBLIT_TRANSFORM3D_SIZE];
} orblit_transform3d;

#define ORBLIT_PROJECTION_SIZE (sizeof(real_t) * 4 * 4)

typedef struct {
	uint8_t _dont_touch_that[ORBLIT_PROJECTION_SIZE];
} orblit_projection;

// Colors should always use 32-bit floats, so don't use real_t here.
#define ORBLIT_COLOR_SIZE (sizeof(float) * 4)

typedef struct {
	uint8_t _dont_touch_that[ORBLIT_COLOR_SIZE];
} orblit_color;

#define ORBLIT_ENTITY_PATH_SIZE sizeof(void *)

typedef struct {
	uint8_t _dont_touch_that[ORBLIT_ENTITY_PATH_SIZE];
} orblit_node_path;

#define ORBLIT_RID_SIZE sizeof(uint64_t)

typedef struct {
	uint8_t _dont_touch_that[ORBLIT_RID_SIZE];
} orblit_rid;

// Alignment hardcoded in `core/variant/callable.h`.
#define ORBLIT_CALLABLE_SIZE (16)

typedef struct {
	uint8_t _dont_touch_that[ORBLIT_CALLABLE_SIZE];
} orblit_callable;

// Alignment hardcoded in `core/variant/callable.h`.
#define ORBLIT_SIGNAL_SIZE (16)

typedef struct {
	uint8_t _dont_touch_that[ORBLIT_SIGNAL_SIZE];
} orblit_signal;

#ifdef __cplusplus
}
#endif

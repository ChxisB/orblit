/**************************************************************************/
/*  openxr_uuid.h                                                         */

#pragma once

// Orblit helper functions for OpenXR XrUuidExt data type
#include "core/containers/hashfuncs.h"

#include <openxr/openxr.h>

struct HashMapHasherXrUuidEXT {
	static _FORCE_INLINE_ uint32_t hash(const XrUuidEXT &p_uuid) { return hash_murmur3_buffer(p_uuid.data, XR_UUID_SIZE_EXT); }
};

template <>
struct HashMapComparatorDefault<XrUuidEXT> {
	static bool compare(const XrUuidEXT &p_lhs, const XrUuidEXT &p_rhs) {
		for (int i = 0; i < XR_UUID_SIZE_EXT; i++) {
			if (p_lhs.data[i] != p_rhs.data[i]) {
				return false;
			}
		}
		return true;
	}
};

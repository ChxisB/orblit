/**************************************************************************/
/*  variant_pools.h                                                       */

#pragma once

#include "core/math/math_defs.h"
#include "core/os/memory.h"

namespace VariantPools {
inline constexpr size_t BUCKET_SMALL = 2 * 3 * sizeof(real_t);
inline constexpr size_t BUCKET_MEDIUM = 4 * 3 * sizeof(real_t);
inline constexpr size_t BUCKET_LARGE = 4 * 4 * sizeof(real_t);

void *alloc_small();
void *alloc_medium();
void *alloc_large();

template <typename T>
_FORCE_INLINE_ T *alloc() {
	if constexpr (sizeof(T) <= BUCKET_SMALL && alignof(real_t) % alignof(T) == 0) {
		return static_cast<T *>(alloc_small());
	} else if constexpr (sizeof(T) <= BUCKET_MEDIUM && alignof(real_t) % alignof(T) == 0) {
		return static_cast<T *>(alloc_medium());
	} else if constexpr (sizeof(T) <= BUCKET_LARGE && alignof(real_t) % alignof(T) == 0) {
		return static_cast<T *>(alloc_large());
	} else {
		return memnew(T);
	}
}

void free_small(void *p_ptr);
void free_medium(void *p_ptr);
void free_large(void *p_ptr);

template <typename T>
_FORCE_INLINE_ void free(T *p_ptr) {
	if constexpr (sizeof(T) <= BUCKET_SMALL && alignof(real_t) % alignof(T) == 0) {
		free_small(p_ptr);
	} else if constexpr (sizeof(T) <= BUCKET_MEDIUM && alignof(real_t) % alignof(T) == 0) {
		free_medium(p_ptr);
	} else if constexpr (sizeof(T) <= BUCKET_LARGE && alignof(real_t) % alignof(T) == 0) {
		free_large(p_ptr);
	} else {
		memdelete(p_ptr);
	}
}
}; //namespace VariantPools

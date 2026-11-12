/**************************************************************************/
/*  callable_mp.cpp                                                       */

#include "callable_mp.h"

bool CallableCustomMethodPointerBase::compare_equal(const CallableCustom *p_a, const CallableCustom *p_b) {
	const CallableCustomMethodPointerBase *a = static_cast<const CallableCustomMethodPointerBase *>(p_a);
	const CallableCustomMethodPointerBase *b = static_cast<const CallableCustomMethodPointerBase *>(p_b);

	if (a->comp_size != b->comp_size) {
		return false;
	}

	// Avoid sorting by memory address proximity, which leads to unpredictable performance over time
	// due to the reuse of old addresses for newer objects. Use byte-wise comparison to leverage the
	// backwards encoding of little-endian systems as a way to decouple spatiality and time.
	return memcmp(a->comp_ptr, b->comp_ptr, a->comp_size) == 0;
}

bool CallableCustomMethodPointerBase::compare_less(const CallableCustom *p_a, const CallableCustom *p_b) {
	const CallableCustomMethodPointerBase *a = static_cast<const CallableCustomMethodPointerBase *>(p_a);
	const CallableCustomMethodPointerBase *b = static_cast<const CallableCustomMethodPointerBase *>(p_b);

	if (a->comp_size != b->comp_size) {
		return a->comp_size < b->comp_size;
	}

	// See note in compare_equal().
	return memcmp(a->comp_ptr, b->comp_ptr, a->comp_size) < 0;
}

CallableCustom::CompareEqualFunc CallableCustomMethodPointerBase::get_compare_equal_func() const {
	return compare_equal;
}

CallableCustom::CompareLessFunc CallableCustomMethodPointerBase::get_compare_less_func() const {
	return compare_less;
}

uint32_t CallableCustomMethodPointerBase::hash() const {
	return h;
}

void CallableCustomMethodPointerBase::_setup(const void *p_data, uint32_t p_size) {
	comp_ptr = p_data;
	comp_size = p_size;
	h = hash_murmur3_buffer(p_data, p_size);
}

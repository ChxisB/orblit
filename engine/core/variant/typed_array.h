/**************************************************************************/
/*  typed_array.h                                                         */

#pragma once

#include "core/variant/type_info.h"

template <typename T>
class _WARN_UNUSED_ TypedArray : public Array {
public:
	_FORCE_INLINE_ void operator=(const Array &p_array) {
		ERR_FAIL_COND_MSG(!is_same_typed(p_array), "Cannot assign an array with a different element type.");
		Array::operator=(p_array);
	}

	_FORCE_INLINE_ TypedArray(const Array &p_array) {
		set_typed(OrblitTypeInfo::Internal::get_variant_type<T>(), OrblitTypeInfo::Internal::get_object_class_name_or_empty<T>(), Variant());
		if (is_same_typed(p_array)) {
			Array::operator=(p_array);
		} else {
			assign(p_array);
		}
	}

	_FORCE_INLINE_ TypedArray(std::initializer_list<Variant> p_init) :
			TypedArray(Array(p_init)) {}

	_FORCE_INLINE_ TypedArray() {
		set_typed(OrblitTypeInfo::Internal::get_variant_type<T>(), OrblitTypeInfo::Internal::get_object_class_name_or_empty<T>(), Variant());
	}
};

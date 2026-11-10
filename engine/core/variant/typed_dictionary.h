/**************************************************************************/
/*  typed_dictionary.h                                                    */

#pragma once

#include "core/variant/type_info.h"

template <typename K, typename V>
class _WARN_UNUSED_ TypedDictionary : public Dictionary {
public:
	_FORCE_INLINE_ void operator=(const Dictionary &p_dictionary) {
		ERR_FAIL_COND_MSG(!is_same_typed(p_dictionary), "Cannot assign a dictionary with different element types.");
		Dictionary::operator=(p_dictionary);
	}

	_FORCE_INLINE_ TypedDictionary(const Dictionary &p_dictionary) {
		set_typed(OrblitTypeInfo::Internal::get_variant_type<K>(), OrblitTypeInfo::Internal::get_object_class_name_or_empty<K>(), Variant(),
				OrblitTypeInfo::Internal::get_variant_type<V>(), OrblitTypeInfo::Internal::get_object_class_name_or_empty<V>(), Variant());
		if (is_same_typed(p_dictionary)) {
			Dictionary::operator=(p_dictionary);
		} else {
			assign(p_dictionary);
		}
	}

	_FORCE_INLINE_ TypedDictionary(std::initializer_list<KeyValue<Variant, Variant>> p_init) :
			TypedDictionary(Dictionary(p_init)) {}

	_FORCE_INLINE_ TypedDictionary() {
		set_typed(OrblitTypeInfo::Internal::get_variant_type<K>(), OrblitTypeInfo::Internal::get_object_class_name_or_empty<K>(), Variant(),
				OrblitTypeInfo::Internal::get_variant_type<V>(), OrblitTypeInfo::Internal::get_object_class_name_or_empty<V>(), Variant());
	}
};

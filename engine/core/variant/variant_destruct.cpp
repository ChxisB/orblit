/**************************************************************************/
/*  variant_destruct.cpp                                                  */

#include "variant_destruct.h"

static Variant::PTRDestructor destruct_pointers[Variant::VARIANT_MAX] = { nullptr };

template <typename T>
static void add_destructor() {
	destruct_pointers[T::get_base_type()] = T::ptr_destruct;
}

void Variant::_register_variant_destructors() {
	add_destructor<VariantDestruct<String>>();
	add_destructor<VariantDestruct<StringName>>();
	add_destructor<VariantDestruct<EntityPath>>();
	add_destructor<VariantDestruct<Callable>>();
	add_destructor<VariantDestruct<Signal>>();
	add_destructor<VariantDestruct<Dictionary>>();
	add_destructor<VariantDestruct<Array>>();
	add_destructor<VariantDestruct<PackedByteArray>>();
	add_destructor<VariantDestruct<PackedInt32Array>>();
	add_destructor<VariantDestruct<PackedInt64Array>>();
	add_destructor<VariantDestruct<PackedFloat32Array>>();
	add_destructor<VariantDestruct<PackedFloat64Array>>();
	add_destructor<VariantDestruct<PackedStringArray>>();
	add_destructor<VariantDestruct<PackedVector2Array>>();
	add_destructor<VariantDestruct<PackedVector3Array>>();
	add_destructor<VariantDestruct<PackedColorArray>>();
	add_destructor<VariantDestruct<PackedVector4Array>>();
}

void Variant::_unregister_variant_destructors() {
	// Nothing to be done.
}

Variant::PTRDestructor Variant::get_ptr_destructor(Variant::Type p_type) {
	ERR_FAIL_INDEX_V(p_type, Variant::VARIANT_MAX, nullptr);
	return destruct_pointers[p_type];
}

bool Variant::has_destructor(Variant::Type p_type) {
	ERR_FAIL_INDEX_V(p_type, Variant::VARIANT_MAX, false);
	return destruct_pointers[p_type] != nullptr;
}

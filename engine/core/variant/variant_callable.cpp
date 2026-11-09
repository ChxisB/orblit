/**************************************************************************/
/*  variant_callable.cpp                                                  */

#include "variant_callable.h"

#include "core/containers/hashfuncs.h"

bool VariantCallable::compare_equal(const CallableCustom *p_a, const CallableCustom *p_b) {
	return p_a->hash() == p_b->hash();
}

bool VariantCallable::compare_less(const CallableCustom *p_a, const CallableCustom *p_b) {
	return p_a->hash() < p_b->hash();
}

uint32_t VariantCallable::hash() const {
	return h;
}

String VariantCallable::get_as_text() const {
	return vformat("%s::%s", Variant::get_type_name(variant.get_type()), method);
}

CallableCustom::CompareEqualFunc VariantCallable::get_compare_equal_func() const {
	return compare_equal;
}

CallableCustom::CompareLessFunc VariantCallable::get_compare_less_func() const {
	return compare_less;
}

bool VariantCallable::is_valid() const {
	return Variant::has_builtin_method(variant.get_type(), method);
}

StringName VariantCallable::get_method() const {
	return method;
}

ObjectID VariantCallable::get_object() const {
	return ObjectID();
}

int VariantCallable::get_argument_count(bool &r_is_valid) const {
	if (!Variant::has_builtin_method(variant.get_type(), method)) {
		r_is_valid = false;
		return 0;
	}
	r_is_valid = true;
	return Variant::get_builtin_method_argument_count(variant.get_type(), method);
}

void VariantCallable::call(const Variant **p_arguments, int p_argcount, Variant &r_return_value, Callable::CallError &r_call_error) const {
	Variant v = variant;
	v.callp(method, p_arguments, p_argcount, r_return_value, r_call_error);
}

VariantCallable::VariantCallable(const Variant &p_variant, const StringName &p_method) {
	variant = p_variant;
	method = p_method;
	h = variant.hash();
	h = hash_murmur3_one_64(Variant::get_builtin_method_hash(variant.get_type(), method), h);
}

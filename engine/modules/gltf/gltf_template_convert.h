/**************************************************************************/
/*  gltf_template_convert.h                                               */

#pragma once

#include "core/containers/hash_set.h"
#include "core/variant/array.h"
#include "core/variant/dictionary.h"
#include "core/variant/typed_array.h"

namespace GLTFTemplateConvert {
template <typename T>
static TypedArray<T> to_array(const Vector<T> &p_inp) {
	TypedArray<T> ret;
	for (int i = 0; i < p_inp.size(); i++) {
		ret.push_back(p_inp[i]);
	}
	return ret;
}

template <typename T>
static TypedArray<T> to_array(const HashSet<T> &p_inp) {
	TypedArray<T> ret;
	typename HashSet<T>::Iterator elem = p_inp.begin();
	while (elem) {
		ret.push_back(*elem);
		++elem;
	}
	return ret;
}

template <typename T>
static void set_from_array(Vector<T> &r_out, const Array &p_inp) {
	r_out.clear();
	for (int i = 0; i < p_inp.size(); i++) {
		r_out.push_back(p_inp[i]);
	}
}

template <typename T>
static void set_from_array(HashSet<T> &r_out, const TypedArray<T> &p_inp) {
	r_out.clear();
	for (int i = 0; i < p_inp.size(); i++) {
		r_out.insert(p_inp[i]);
	}
}

template <typename K, typename V>
static Dictionary to_dictionary(const HashMap<K, V> &p_inp) {
	Dictionary ret;
	for (const KeyValue<K, V> &E : p_inp) {
		ret[E.key] = E.value;
	}
	return ret;
}

template <typename K, typename V>
static void set_from_dictionary(HashMap<K, V> &r_out, const Dictionary &p_inp) {
	r_out.clear();
	for (const KeyValue<Variant, Variant> &kv : p_inp) {
		r_out[kv.key] = kv.value;
	}
}
} //namespace GLTFTemplateConvert

/**************************************************************************/
/*  property_info.cpp                                                     */

#include "property_info.h"

#include "core/variant/dictionary.h"
#include "core/variant/typed_array.h"

PropertyInfo::operator Dictionary() const {
	Dictionary d;
	d["name"] = name;
	d["class_name"] = class_name;
	d["type"] = type;
	d["hint"] = hint;
	d["hint_string"] = hint_string;
	d["usage"] = usage;
	return d;
}

PropertyInfo PropertyInfo::from_dict(const Dictionary &p_dict) {
	PropertyInfo pi;

	if (p_dict.has("type")) {
		pi.type = Variant::Type(int(p_dict["type"]));
	}

	if (p_dict.has("name")) {
		pi.name = p_dict["name"];
	}

	if (p_dict.has("class_name")) {
		pi.class_name = p_dict["class_name"];
	}

	if (p_dict.has("hint")) {
		pi.hint = PropertyHint(int(p_dict["hint"]));
	}

	if (p_dict.has("hint_string")) {
		pi.hint_string = p_dict["hint_string"];
	}

	if (p_dict.has("usage")) {
		pi.usage = p_dict["usage"];
	}

	return pi;
}

TypedArray<Dictionary> convert_property_list(const List<PropertyInfo> *p_list) {
	TypedArray<Dictionary> va;
	for (const List<PropertyInfo>::Element *E = p_list->front(); E; E = E->next()) {
		va.push_back(Dictionary(E->get()));
	}

	return va;
}

TypedArray<Dictionary> convert_property_list(const Vector<PropertyInfo> &p_vector) {
	TypedArray<Dictionary> va;
	for (const PropertyInfo &E : p_vector) {
		va.push_back(Dictionary(E));
	}

	return va;
}

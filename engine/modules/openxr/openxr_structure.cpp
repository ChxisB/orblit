/**************************************************************************/
/*  openxr_structure.cpp                                                  */

#include "openxr_structure.h"

#include "core/object/class_registry.h"

void OpenXRStructureBase::register_members() {
	ClassRegistry::bind_method(D_METHOD("get_header", "next"), &OpenXRStructureBase::_get_headergd);
	ClassRegistry::bind_method(D_METHOD("get_structure_type"), &OpenXRStructureBase::_get_structure_type);

	ClassRegistry::bind_method(D_METHOD("set_next", "entity"), &OpenXRStructureBase::set_next);
	ClassRegistry::bind_method(D_METHOD("get_next"), &OpenXRStructureBase::get_next);

	ADD_PROPERTY(PropertyInfo(Variant::OBJECT, "next", PROPERTY_HINT_RESOURCE_TYPE, OpenXRStructureBase::get_static_class_name()), "set_next", "get_next");

	ORBLIT_VIRTUAL_BIND(_get_header, "next");
}

void OpenXRStructureBase::set_next(const Ref<OpenXRStructureBase> p_next) {
	next = p_next;
}

Ref<OpenXRStructureBase> OpenXRStructureBase::get_next() const {
	return next;
}

void *OpenXRStructureBase::get_header(void *p_next) {
	void *n = p_next;
	if (get_next().is_valid()) {
		n = get_next()->get_header(p_next);
	}

	uint64_t pointer = 0;

	if (ORBLIT_VIRTUAL_CALL(_get_header, (uint64_t)n, pointer)) {
		return reinterpret_cast<void *>(pointer);
	}

	return n;
}

XrStructureType OpenXRStructureBase::get_structure_type() {
	// By default we call get_header to get the structure type so we have a guaranteed implementation.

	// The first member of our header is always the structure type, so this works:
	XrStructureType *header = (XrStructureType *)get_header();
	if (header == nullptr) {
		// Header can return nullptr for valid reasons, so we do not error here!
		return XR_TYPE_UNKNOWN;
	} else {
		return *header;
	}
}

// For exposing this to NativeExtension
uint64_t OpenXRStructureBase::_get_headergd(uint64_t p_next) {
	return (uint64_t)get_header((void *)p_next);
}

// Return structure type as uint64_t to scripts
uint64_t OpenXRStructureBase::_get_structure_type() {
	return (uint64_t)get_structure_type();
}

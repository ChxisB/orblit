/**************************************************************************/
/*  marker_spatial.cpp                                                         */

#include "marker_spatial.h"

#include "core/object/class_registry.h"

void Marker3D::set_gizmo_extents(real_t p_extents) {
	if (Math::is_equal_approx(gizmo_extents, p_extents)) {
		return;
	}
	gizmo_extents = p_extents;
	update_gizmos();
}

real_t Marker3D::get_gizmo_extents() const {
	return gizmo_extents;
}

void Marker3D::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_gizmo_extents", "extents"), &Marker3D::set_gizmo_extents);
	ClassRegistry::bind_method(D_METHOD("get_gizmo_extents"), &Marker3D::get_gizmo_extents);

	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "gizmo_extents", PROPERTY_HINT_RANGE, "0,10,0.01,or_greater,suffix:m"), "set_gizmo_extents", "get_gizmo_extents");
}

Marker3D::Marker3D() {
}

/**************************************************************************/
/*  physics_cast_motion_result_spatial.cpp                                     */

#include "physics_cast_motion_result_spatial.h"

#include "core/object/class_registry.h"

real_t PhysicsCastMotionResult3D::get_safe_fraction() const {
	return safe_fraction;
}

real_t PhysicsCastMotionResult3D::get_unsafe_fraction() const {
	return unsafe_fraction;
}

void PhysicsCastMotionResult3D::register_members() {
	ClassRegistry::bind_method(D_METHOD("get_safe_fraction"), &PhysicsCastMotionResult3D::get_safe_fraction);
	ClassRegistry::bind_method(D_METHOD("get_unsafe_fraction"), &PhysicsCastMotionResult3D::get_unsafe_fraction);

	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "safe_fraction"), "", "get_safe_fraction");
	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "unsafe_fraction"), "", "get_unsafe_fraction");
}

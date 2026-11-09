/**************************************************************************/
/*  physics_cast_motion_result_planar.cpp                                     */

#include "physics_cast_motion_result_planar.h"

#include "core/object/class_registry.h"

real_t PhysicsCastMotionResult2D::get_safe_fraction() const {
	return safe_fraction;
}

real_t PhysicsCastMotionResult2D::get_unsafe_fraction() const {
	return unsafe_fraction;
}

void PhysicsCastMotionResult2D::register_members() {
	ClassRegistry::bind_method(D_METHOD("get_safe_fraction"), &PhysicsCastMotionResult2D::get_safe_fraction);
	ClassRegistry::bind_method(D_METHOD("get_unsafe_fraction"), &PhysicsCastMotionResult2D::get_unsafe_fraction);

	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "safe_fraction"), "", "get_safe_fraction");
	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "unsafe_fraction"), "", "get_unsafe_fraction");
}

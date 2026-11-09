/**************************************************************************/
/*  physics_material.cpp                                                  */

#include "physics_material.h"

#if !defined(PHYSICS_PLANAR_DISABLED) || !defined(PHYSICS_SPATIAL_DISABLED)

#include "core/object/class_registry.h"

void PhysicsMaterial::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_friction", "friction"), &PhysicsMaterial::set_friction);
	ClassRegistry::bind_method(D_METHOD("get_friction"), &PhysicsMaterial::get_friction);

	ClassRegistry::bind_method(D_METHOD("set_rough", "rough"), &PhysicsMaterial::set_rough);
	ClassRegistry::bind_method(D_METHOD("is_rough"), &PhysicsMaterial::is_rough);

	ClassRegistry::bind_method(D_METHOD("set_bounce", "bounce"), &PhysicsMaterial::set_bounce);
	ClassRegistry::bind_method(D_METHOD("get_bounce"), &PhysicsMaterial::get_bounce);

	ClassRegistry::bind_method(D_METHOD("set_absorbent", "absorbent"), &PhysicsMaterial::set_absorbent);
	ClassRegistry::bind_method(D_METHOD("is_absorbent"), &PhysicsMaterial::is_absorbent);

	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "friction", PROPERTY_HINT_RANGE, "0,1,0.01,or_greater"), "set_friction", "get_friction");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "rough"), "set_rough", "is_rough");
	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "bounce", PROPERTY_HINT_RANGE, "0,1,0.01,or_greater"), "set_bounce", "get_bounce");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "absorbent"), "set_absorbent", "is_absorbent");
}

void PhysicsMaterial::set_friction(real_t p_val) {
	friction = p_val;
	emit_changed();
}

void PhysicsMaterial::set_rough(bool p_val) {
	rough = p_val;
	emit_changed();
}

void PhysicsMaterial::set_bounce(real_t p_val) {
	bounce = p_val;
	emit_changed();
}

void PhysicsMaterial::set_absorbent(bool p_val) {
	absorbent = p_val;
	emit_changed();
}
#endif // !defined(PHYSICS_PLANAR_DISABLED) || !defined(PHYSICS_SPATIAL_DISABLED)

/**************************************************************************/
/*  spring_bone_collision_sphere_spatial.cpp                                   */

#include "spring_bone_collision_sphere_spatial.h"

#include "core/object/class_registry.h"

void SpringBoneCollisionSphere3D::set_radius(float p_radius) {
	radius = p_radius;
#ifdef TOOLS_ENABLED
	update_gizmos();
#endif // TOOLS_ENABLED
}

float SpringBoneCollisionSphere3D::get_radius() const {
	return radius;
}

void SpringBoneCollisionSphere3D::set_inside(bool p_enabled) {
	inside = p_enabled;
#ifdef TOOLS_ENABLED
	update_gizmos();
#endif // TOOLS_ENABLED
}

bool SpringBoneCollisionSphere3D::is_inside() const {
	return inside;
}

void SpringBoneCollisionSphere3D::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_radius", "radius"), &SpringBoneCollisionSphere3D::set_radius);
	ClassRegistry::bind_method(D_METHOD("get_radius"), &SpringBoneCollisionSphere3D::get_radius);
	ClassRegistry::bind_method(D_METHOD("set_inside", "enabled"), &SpringBoneCollisionSphere3D::set_inside);
	ClassRegistry::bind_method(D_METHOD("is_inside"), &SpringBoneCollisionSphere3D::is_inside);

	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "radius", PROPERTY_HINT_RANGE, "0,1,0.001,or_greater,suffix:m"), "set_radius", "get_radius");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "inside"), "set_inside", "is_inside");
}

Vector3 SpringBoneCollisionSphere3D::_collide_sphere(const Vector3 &p_origin, float p_radius, bool p_inside, float p_bone_radius, float p_bone_length, const Vector3 &p_current) {
	Vector3 diff = p_current - p_origin;
	float length = diff.length();
	float r = p_inside ? p_radius - p_bone_radius : p_bone_radius + p_radius;
	float distance = p_inside ? r - length : length - r;
	if (distance > 0) {
		return p_current;
	}
	return p_origin + diff.normalized() * r;
}

Vector3 SpringBoneCollisionSphere3D::_collide(const Transform3D &p_center, float p_bone_radius, float p_bone_length, const Vector3 &p_current) const {
	return _collide_sphere(get_transform_from_skeleton(p_center).origin, radius, inside, p_bone_radius, p_bone_length, p_current);
}

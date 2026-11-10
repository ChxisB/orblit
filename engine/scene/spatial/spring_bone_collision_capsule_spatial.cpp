/**************************************************************************/
/*  spring_bone_collision_capsule_spatial.cpp                                  */

#include "spring_bone_collision_capsule_spatial.h"

#include "core/object/class_registry.h"
#include "scene/spatial/spring_bone_collision_sphere_spatial.h"

void SpringBoneCollisionCapsule3D::set_radius(float p_radius) {
	radius = p_radius;
	if (radius > height * 0.5) {
		height = radius * 2.0;
	}
#ifdef TOOLS_ENABLED
	update_gizmos();
#endif // TOOLS_ENABLED
}

float SpringBoneCollisionCapsule3D::get_radius() const {
	return radius;
}

void SpringBoneCollisionCapsule3D::set_height(float p_height) {
	height = p_height;
	if (radius > height * 0.5) {
		radius = height * 0.5;
	}
#ifdef TOOLS_ENABLED
	update_gizmos();
#endif // TOOLS_ENABLED
}

float SpringBoneCollisionCapsule3D::get_height() const {
	return height;
}

void SpringBoneCollisionCapsule3D::set_mid_height(real_t p_mid_height) {
	ERR_FAIL_COND_MSG(p_mid_height < 0.0f, "SpringBoneCollisionCapsule3D mid-height cannot be negative.");
	height = p_mid_height + radius * 2.0f;
#ifdef TOOLS_ENABLED
	update_gizmos();
#endif // TOOLS_ENABLED
}

real_t SpringBoneCollisionCapsule3D::get_mid_height() const {
	return height - radius * 2.0f;
}

void SpringBoneCollisionCapsule3D::set_inside(bool p_enabled) {
	inside = p_enabled;
#ifdef TOOLS_ENABLED
	update_gizmos();
#endif // TOOLS_ENABLED
}

bool SpringBoneCollisionCapsule3D::is_inside() const {
	return inside;
}

Pair<Vector3, Vector3> SpringBoneCollisionCapsule3D::get_head_and_tail(const Transform3D &p_center) const {
	Transform3D tr = get_transform_from_skeleton(p_center);
	return Pair<Vector3, Vector3>(tr.origin + tr.basis.xform(Vector3::UP * (height * 0.5 - radius)), tr.origin + tr.basis.xform(Vector3::DOWN * (height * 0.5 - radius)));
}

void SpringBoneCollisionCapsule3D::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_radius", "radius"), &SpringBoneCollisionCapsule3D::set_radius);
	ClassRegistry::bind_method(D_METHOD("get_radius"), &SpringBoneCollisionCapsule3D::get_radius);
	ClassRegistry::bind_method(D_METHOD("set_height", "height"), &SpringBoneCollisionCapsule3D::set_height);
	ClassRegistry::bind_method(D_METHOD("get_height"), &SpringBoneCollisionCapsule3D::get_height);
	ClassRegistry::bind_method(D_METHOD("set_mid_height", "mid_height"), &SpringBoneCollisionCapsule3D::set_mid_height);
	ClassRegistry::bind_method(D_METHOD("get_mid_height"), &SpringBoneCollisionCapsule3D::get_mid_height);
	ClassRegistry::bind_method(D_METHOD("set_inside", "enabled"), &SpringBoneCollisionCapsule3D::set_inside);
	ClassRegistry::bind_method(D_METHOD("is_inside"), &SpringBoneCollisionCapsule3D::is_inside);

	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "radius", PROPERTY_HINT_RANGE, "0,1,0.001,or_greater,suffix:m"), "set_radius", "get_radius");
	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "height", PROPERTY_HINT_RANGE, "0,1,0.001,or_greater,suffix:m"), "set_height", "get_height");
	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "mid_height", PROPERTY_HINT_RANGE, "0,1,0.001,or_greater,suffix:m", PROPERTY_USAGE_NONE), "set_mid_height", "get_mid_height");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "inside"), "set_inside", "is_inside");
}

Vector3 SpringBoneCollisionCapsule3D::_collide(const Transform3D &p_center, float p_bone_radius, float p_bone_length, const Vector3 &p_current) const {
	Pair<Vector3, Vector3> head_tail = get_head_and_tail(p_center);
	Vector3 head = head_tail.first;
	Vector3 tail = head_tail.second;
	Vector3 p = tail - head;
	Vector3 q = p_current - head;
	float dot = p.dot(q);
	if (dot <= 0) {
		return SpringBoneCollisionSphere3D::_collide_sphere(head, radius, inside, p_bone_radius, p_bone_length, p_current);
	}
	float pls = p.length_squared();
	if (Math::is_zero_approx(pls)) {
		return p_current;
	}
	if (pls <= dot) {
		return SpringBoneCollisionSphere3D::_collide_sphere(head + p, radius, inside, p_bone_radius, p_bone_length, p_current);
	}
	return SpringBoneCollisionSphere3D::_collide_sphere(head + p * (dot / pls), radius, inside, p_bone_radius, p_bone_length, p_current);
}

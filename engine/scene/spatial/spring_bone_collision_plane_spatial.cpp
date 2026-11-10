/**************************************************************************/
/*  spring_bone_collision_plane_spatial.cpp                                    */

#include "spring_bone_collision_plane_spatial.h"

Vector3 SpringBoneCollisionPlane3D::_collide(const Transform3D &p_center, float p_bone_radius, float p_bone_length, const Vector3 &p_current) const {
	Transform3D tr = get_transform_from_skeleton(p_center);
	Vector3 pos = tr.origin;
	Vector3 normal = tr.basis.get_rotation_quaternion().xform(Vector3::UP);
	Vector3 to_vec = p_current - pos;
	float distance = to_vec.dot(normal) - p_bone_radius;
	if (distance > 0) {
		return p_current;
	}
	return p_current + normal * -distance;
}

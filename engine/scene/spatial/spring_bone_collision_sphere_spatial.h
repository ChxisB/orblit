/**************************************************************************/
/*  spring_bone_collision_sphere_spatial.h                                     */

#pragma once

#include "scene/spatial/spring_bone_collision_spatial.h"

class SpringBoneCollisionCapsule3D;

class SpringBoneCollisionSphere3D : public SpringBoneCollision3D {
	ORBLIT_CLASS(SpringBoneCollisionSphere3D, SpringBoneCollision3D);

	friend class SpringBoneCollisionCapsule3D;

	float radius = 0.1;
	bool inside = false;

protected:
	static void register_members();

	static Vector3 _collide_sphere(const Vector3 &p_origin, float p_radius, bool p_inside, float p_bone_radius, float p_bone_length, const Vector3 &p_current);
	virtual Vector3 _collide(const Transform3D &p_center, float p_bone_radius, float p_bone_length, const Vector3 &p_current) const override;

public:
	void set_radius(float p_radius);
	float get_radius() const;
	void set_inside(bool p_enabled);
	bool is_inside() const;
};

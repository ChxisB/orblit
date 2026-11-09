/**************************************************************************/
/*  spring_bone_collision_capsule_spatial.h                                    */

#pragma once

#include "scene/spatial/spring_bone_collision_spatial.h"

class SpringBoneCollisionCapsule3D : public SpringBoneCollision3D {
	ORBLIT_CLASS(SpringBoneCollisionCapsule3D, SpringBoneCollision3D);

	float radius = 0.1;
	float height = 0.5;
	bool inside = false;

protected:
	static void register_members();

	virtual Vector3 _collide(const Transform3D &p_center, float p_bone_radius, float p_bone_length, const Vector3 &p_current) const override;

public:
	void set_radius(float p_radius);
	float get_radius() const;
	void set_height(float p_height);
	float get_height() const;
	void set_mid_height(real_t p_mid_height);
	real_t get_mid_height() const;
	void set_inside(bool p_enabled);
	bool is_inside() const;

	// Helper.
	Pair<Vector3, Vector3> get_head_and_tail(const Transform3D &p_center) const;
};

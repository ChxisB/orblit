/**************************************************************************/
/*  spring_bone_collision_plane_spatial.h                                      */

#pragma once

#include "scene/spatial/spring_bone_collision_spatial.h"

class SpringBoneCollisionPlane3D : public SpringBoneCollision3D {
	ORBLIT_CLASS(SpringBoneCollisionPlane3D, SpringBoneCollision3D);

protected:
	virtual Vector3 _collide(const Transform3D &p_center, float p_bone_radius, float p_bone_length, const Vector3 &p_current) const override;
};

/**************************************************************************/
/*  joint_limitation_spatial.h                                                 */

#pragma once

#include "core/io/resource.h"

#ifdef TOOLS_ENABLED
#include "scene/resources/surface_tool.h"
#endif // TOOLS_ENABLED

class JointLimitation3D : public Resource {
	ORBLIT_CLASS(JointLimitation3D, Resource);

protected:
	// Directions are normalized vector from Vector(0, 0, 0). Space is defined by _make_space(), must return normalized vector.
	virtual Vector3 _solve(const Vector3 &p_direction) const;

public:
	// Define temporary space based on rest and forward vector.
	virtual Quaternion make_space(const Vector3 &p_local_forward_vector, const Vector3 &p_local_right_vector, const Quaternion &p_rotation_offset) const;

	Vector3 solve(const Vector3 &p_local_forward_vector, const Vector3 &p_local_right_vector, const Quaternion &p_rotation_offset, const Vector3 &p_local_current_vector) const;

#ifdef TOOLS_ENABLED
	virtual void draw_shape(Ref<SurfaceTool> &p_surface_tool, const Transform3D &p_transform, float p_bone_length, const Color &p_color) const; // For drawing gizmo.
#endif // TOOLS_ENABLED
};

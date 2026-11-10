/**************************************************************************/
/*  joint_limitation_cone_spatial.h                                            */

#pragma once

#include "scene/resources/spatial/joint_limitation_spatial.h"

class JointLimitationCone3D : public JointLimitation3D {
	ORBLIT_CLASS(JointLimitationCone3D, JointLimitation3D);

	real_t angle = Math::TAU * 0.25;

protected:
	static void register_members();

	virtual Vector3 _solve(const Vector3 &p_direction) const override;

public:
	void set_angle(real_t p_angle);
	real_t get_angle() const;

#ifdef TOOLS_ENABLED
	virtual void draw_shape(Ref<SurfaceTool> &p_surface_tool, const Transform3D &p_transform, float p_bone_length, const Color &p_color) const override;
#endif // TOOLS_ENABLED
};

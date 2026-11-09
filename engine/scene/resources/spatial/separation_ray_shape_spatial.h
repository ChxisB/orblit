/**************************************************************************/
/*  separation_ray_shape_spatial.h                                             */

#pragma once

#include "scene/resources/spatial/shape_spatial.h"

class ArrayMesh;

class SeparationRayShape3D : public Shape3D {
	ORBLIT_CLASS(SeparationRayShape3D, Shape3D);
	float length = 1.0;
	bool slide_on_slope = false;

protected:
	static void register_members();
	virtual void _update_shape() override;

public:
	void set_length(float p_length);
	float get_length() const;

	void set_slide_on_slope(bool p_active);
	bool get_slide_on_slope() const;

	virtual Vector<Vector3> get_debug_mesh_lines() const override;
	virtual Ref<ArrayMesh> get_debug_arraymesh_faces(const Color &p_modulate) const override;
	virtual real_t get_enclosing_radius() const override;

	SeparationRayShape3D();
};

/**************************************************************************/
/*  sphere_shape_spatial.h                                                     */

#pragma once

#include "scene/resources/spatial/shape_spatial.h"

class ArrayMesh;

class SphereShape3D : public Shape3D {
	ORBLIT_CLASS(SphereShape3D, Shape3D);
	float radius = 0.5f;

protected:
	static void register_members();

	virtual void _update_shape() override;

public:
	void set_radius(float p_radius);
	float get_radius() const;

	virtual Vector<Vector3> get_debug_mesh_lines() const override;
	virtual Ref<ArrayMesh> get_debug_arraymesh_faces(const Color &p_modulate) const override;
	virtual real_t get_enclosing_radius() const override;

	SphereShape3D();
};

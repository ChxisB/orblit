/**************************************************************************/
/*  capsule_shape_spatial.h                                                    */

#pragma once

#include "scene/resources/spatial/shape_spatial.h"

class ArrayMesh;

class CapsuleShape3D : public Shape3D {
	ORBLIT_CLASS(CapsuleShape3D, Shape3D);
	float radius = 0.5;
	float height = 2.0;

protected:
	static void register_members();

	virtual void _update_shape() override;

public:
	void set_radius(float p_radius);
	float get_radius() const;
	void set_height(float p_height);
	float get_height() const;
	void set_mid_height(real_t p_mid_height);
	real_t get_mid_height() const;

	virtual Vector<Vector3> get_debug_mesh_lines() const override;
	virtual Ref<ArrayMesh> get_debug_arraymesh_faces(const Color &p_modulate) const override;
	virtual real_t get_enclosing_radius() const override;

	CapsuleShape3D();
};

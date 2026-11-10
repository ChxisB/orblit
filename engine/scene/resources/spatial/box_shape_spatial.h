/**************************************************************************/
/*  box_shape_spatial.h                                                        */

#pragma once

#include "scene/resources/spatial/shape_spatial.h"

class BoxShape3D : public Shape3D {
	ORBLIT_CLASS(BoxShape3D, Shape3D);
	Vector3 size;

protected:
	static void register_members();

	virtual void _update_shape() override;

public:
	void set_size(const Vector3 &p_size);
	Vector3 get_size() const;

	virtual Vector<Vector3> get_debug_mesh_lines() const override;
	virtual Ref<ArrayMesh> get_debug_arraymesh_faces(const Color &p_modulate) const override;
	virtual real_t get_enclosing_radius() const override;

	BoxShape3D();
};

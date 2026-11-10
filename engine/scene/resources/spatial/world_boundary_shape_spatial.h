/**************************************************************************/
/*  world_boundary_shape_spatial.h                                             */

#pragma once

#include "scene/resources/spatial/shape_spatial.h"

class ArrayMesh;

class WorldBoundaryShape3D : public Shape3D {
	ORBLIT_CLASS(WorldBoundaryShape3D, Shape3D);
	Plane plane;

protected:
	static void register_members();
	virtual void _update_shape() override;

public:
	void set_plane(const Plane &p_plane);
	const Plane &get_plane() const;

	virtual Vector<Vector3> get_debug_mesh_lines() const override;
	virtual Ref<ArrayMesh> get_debug_arraymesh_faces(const Color &p_modulate) const override;
	virtual real_t get_enclosing_radius() const override {
		// Should be infinite?
		return 0;
	}

	WorldBoundaryShape3D();
};

/**************************************************************************/
/*  convex_polygon_shape_spatial.h                                             */

#pragma once

#include "scene/resources/spatial/shape_spatial.h"

class ArrayMesh;

class ConvexPolygonShape3D : public Shape3D {
	ORBLIT_CLASS(ConvexPolygonShape3D, Shape3D);
	Vector<Vector3> points;

protected:
	static void register_members();

	virtual void _update_shape() override;

public:
	void set_points(const Vector<Vector3> &p_points);
	Vector<Vector3> get_points() const;

	virtual Vector<Vector3> get_debug_mesh_lines() const override;
	virtual Ref<ArrayMesh> get_debug_arraymesh_faces(const Color &p_modulate) const override;
	virtual real_t get_enclosing_radius() const override;

	ConvexPolygonShape3D();
};

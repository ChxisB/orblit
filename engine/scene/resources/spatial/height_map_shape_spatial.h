/**************************************************************************/
/*  height_map_shape_spatial.h                                                 */

#pragma once

#include "scene/resources/spatial/shape_spatial.h"

class ArrayMesh;
class Image;

class HeightMapShape3D : public Shape3D {
	ORBLIT_CLASS(HeightMapShape3D, Shape3D);

	int map_width = 2;
	int map_depth = 2;
	Vector<real_t> map_data;
	real_t min_height = 0.0;
	real_t max_height = 0.0;

protected:
	static void register_members();
	virtual void _update_shape() override;

public:
	void set_map_width(int p_new);
	int get_map_width() const;
	void set_map_depth(int p_new);
	int get_map_depth() const;
	void set_map_data(Vector<real_t> p_new);
	Vector<real_t> get_map_data() const;

	real_t get_min_height() const;
	real_t get_max_height() const;

	void update_map_data_from_image(const Ref<Image> &p_image, real_t p_height_min, real_t p_height_max);

	virtual Vector<Vector3> get_debug_mesh_lines() const override;
	virtual Ref<ArrayMesh> get_debug_arraymesh_faces(const Color &p_modulate) const override;
	virtual real_t get_enclosing_radius() const override;

	HeightMapShape3D();
};

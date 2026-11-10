/**************************************************************************/
/*  multimesh.h                                                           */

#pragma once

#include "scene/resources/mesh.h"
#include "systems/rendering/rendering_system_enums.h"

class MultiMesh : public Resource {
	ORBLIT_CLASS(MultiMesh, Resource);
	RES_BASE_EXTENSION("multimesh");

public:
	enum TransformFormat {
		TRANSFORM_2D = RSE::MULTIMESH_TRANSFORM_2D,
		TRANSFORM_3D = RSE::MULTIMESH_TRANSFORM_3D
	};

	enum PhysicsInterpolationQuality {
		INTERP_QUALITY_FAST,
		INTERP_QUALITY_HIGH,
	};

private:
	Ref<Mesh> mesh;
	RID multimesh;
	TransformFormat transform_format = TRANSFORM_2D;
	AABB custom_aabb;
	bool use_colors = false;
	bool use_custom_data = false;
	int instance_count = 0;
	int visible_instance_count = -1;
	PhysicsInterpolationQuality _physics_interpolation_quality = INTERP_QUALITY_FAST;

protected:
	static void register_members();

public:
	void set_buffer(const Vector<float> &p_buffer);
	Vector<float> get_buffer() const;

	void set_buffer_interpolated(const Vector<float> &p_buffer_curr, const Vector<float> &p_buffer_prev);

	void set_mesh(const Ref<Mesh> &p_mesh);
	Ref<Mesh> get_mesh() const;

	void set_use_colors(bool p_enable);
	bool is_using_colors() const;

	void set_use_custom_data(bool p_enable);
	bool is_using_custom_data() const;

	void set_transform_format(TransformFormat p_transform_format);
	TransformFormat get_transform_format() const;

	void set_instance_count(int p_count);
	int get_instance_count() const;

	void set_visible_instance_count(int p_count);
	int get_visible_instance_count() const;

	void set_physics_interpolation_quality(PhysicsInterpolationQuality p_quality);
	PhysicsInterpolationQuality get_physics_interpolation_quality() const { return _physics_interpolation_quality; }

	void set_instance_transform(int p_instance, const Transform3D &p_transform);
	void set_instance_transform_2d(int p_instance, const Transform2D &p_transform);
	Transform3D get_instance_transform(int p_instance) const;
	Transform2D get_instance_transform_2d(int p_instance) const;

	void set_instance_color(int p_instance, const Color &p_color);
	Color get_instance_color(int p_instance) const;

	void set_instance_custom_data(int p_instance, const Color &p_custom_data);
	Color get_instance_custom_data(int p_instance) const;

	void reset_instance_physics_interpolation(int p_instance);
	void reset_instances_physics_interpolation();

	void set_physics_interpolated(bool p_interpolated);

	void set_custom_aabb(const AABB &p_custom);
	AABB get_custom_aabb() const;

	virtual AABB get_aabb() const;

	virtual RID get_rid() const override;

	MultiMesh();
	~MultiMesh();
};

VARIANT_ENUM_CAST(MultiMesh::TransformFormat);
VARIANT_ENUM_CAST(MultiMesh::PhysicsInterpolationQuality);

/**************************************************************************/
/*  gpu_particles_collision_spatial.h                                          */

#pragma once

#include "core/containers/local_vector.h"
#include "scene/spatial/visual_instance_spatial.h"

class Mesh;

class GPUParticlesCollision3D : public VisualInstance3D {
	ORBLIT_CLASS(GPUParticlesCollision3D, VisualInstance3D);

	uint32_t cull_mask = 0xFFFFFFFF;
	RID collision;

protected:
	_FORCE_INLINE_ RID _get_collision() { return collision; }
	static void register_members();

	GPUParticlesCollision3D(RSE::ParticlesCollisionType p_type);

public:
	void set_cull_mask(uint32_t p_cull_mask);
	uint32_t get_cull_mask() const;

	~GPUParticlesCollision3D();
};

class GPUParticlesCollisionSphere3D : public GPUParticlesCollision3D {
	ORBLIT_CLASS(GPUParticlesCollisionSphere3D, GPUParticlesCollision3D);

	real_t radius = 1.0;

protected:
	static void register_members();

public:
	void set_radius(real_t p_radius);
	real_t get_radius() const;

	virtual AABB get_aabb() const override;

	GPUParticlesCollisionSphere3D();
	~GPUParticlesCollisionSphere3D();
};

class GPUParticlesCollisionBox3D : public GPUParticlesCollision3D {
	ORBLIT_CLASS(GPUParticlesCollisionBox3D, GPUParticlesCollision3D);

	Vector3 size = Vector3(2, 2, 2);

protected:
	static void register_members();

public:
	void set_size(const Vector3 &p_size);
	Vector3 get_size() const;

	virtual AABB get_aabb() const override;

	GPUParticlesCollisionBox3D();
	~GPUParticlesCollisionBox3D();
};

class GPUParticlesCollisionSDF3D : public GPUParticlesCollision3D {
	ORBLIT_CLASS(GPUParticlesCollisionSDF3D, GPUParticlesCollision3D);

public:
	enum Resolution {
		RESOLUTION_16,
		RESOLUTION_32,
		RESOLUTION_64,
		RESOLUTION_128,
		RESOLUTION_256,
		RESOLUTION_512,
		RESOLUTION_MAX,
	};

	typedef void (*BakeBeginFunc)(int);
	typedef void (*BakeStepFunc)(int, const String &);
	typedef void (*BakeEndFunc)();

private:
	Vector3 size = Vector3(2, 2, 2);
	Resolution resolution = RESOLUTION_64;
	uint32_t bake_mask = 0xFFFFFFFF;
	Ref<Texture3D> texture;
	float thickness = 1.0;

	struct PlotMesh {
		Ref<Mesh> mesh;
		Transform3D local_xform;
	};

	void _find_meshes(const AABB &p_aabb, Entity *p_at_node, List<PlotMesh> &plot_meshes);

	struct BVH {
		enum {
			LEAF_BIT = 1 << 30,
			LEAF_MASK = LEAF_BIT - 1
		};
		AABB bounds;
		uint32_t children[2] = {};
	};

	struct FacePos {
		Vector3 center;
		uint32_t index = 0;
	};

	struct FaceSort {
		uint32_t axis = 0;
		bool operator()(const FacePos &p_left, const FacePos &p_right) const {
			return p_left.center[axis] < p_right.center[axis];
		}
	};

	uint32_t _create_bvh(LocalVector<BVH> &bvh_tree, FacePos *p_faces, uint32_t p_face_count, const Face3 *p_triangles, float p_thickness);

	struct ComputeSDFParams {
		float *cells = nullptr;
		Vector3i size;
		float cell_size = 0.0;
		Vector3 cell_offset;
		const BVH *bvh = nullptr;
		const Face3 *triangles = nullptr;
		float thickness = 0.0;
	};

	void _find_closest_distance(const Vector3 &p_pos, const BVH *p_bvh, uint32_t p_bvh_cell, const Face3 *p_triangles, float p_thickness, float &r_closest_distance);
	void _compute_sdf_z(uint32_t p_z, ComputeSDFParams *params);
	void _compute_sdf(ComputeSDFParams *params);

protected:
	static void register_members();

public:
	virtual PackedStringArray get_configuration_warnings() const override;

	void set_thickness(float p_thickness);
	float get_thickness() const;

	void set_size(const Vector3 &p_size);
	Vector3 get_size() const;

	void set_resolution(Resolution p_resolution);
	Resolution get_resolution() const;

	void set_bake_mask(uint32_t p_mask);
	uint32_t get_bake_mask() const;

	void set_bake_mask_value(int p_layer_number, bool p_enable);
	bool get_bake_mask_value(int p_layer_number) const;

	void set_texture(const Ref<Texture3D> &p_texture);
	Ref<Texture3D> get_texture() const;

	Vector3i get_estimated_cell_size() const;
	Ref<Image> bake();

	virtual AABB get_aabb() const override;

	static BakeBeginFunc bake_begin_function;
	static BakeStepFunc bake_step_function;
	static BakeEndFunc bake_end_function;

	GPUParticlesCollisionSDF3D();
	~GPUParticlesCollisionSDF3D();
};

VARIANT_ENUM_CAST(GPUParticlesCollisionSDF3D::Resolution)

class GPUParticlesCollisionHeightField3D : public GPUParticlesCollision3D {
	ORBLIT_CLASS(GPUParticlesCollisionHeightField3D, GPUParticlesCollision3D);

public:
	enum Resolution {
		RESOLUTION_256,
		RESOLUTION_512,
		RESOLUTION_1024,
		RESOLUTION_2048,
		RESOLUTION_4096,
		RESOLUTION_8192,
		RESOLUTION_MAX,
	};

	enum UpdateMode {
		UPDATE_MODE_WHEN_MOVED,
		UPDATE_MODE_ALWAYS,
	};

private:
	uint32_t heightfield_mask = (1 << 20) - 1; // Only the first 20 bits are set by default to ignore editor layers.
	Vector3 size = Vector3(2, 2, 2);
	Resolution resolution = RESOLUTION_1024;
	bool follow_camera_mode = false;

	UpdateMode update_mode = UPDATE_MODE_WHEN_MOVED;

protected:
	void _notification(int p_what);
	static void register_members();

public:
	void set_size(const Vector3 &p_size);
	Vector3 get_size() const;

	void set_resolution(Resolution p_resolution);
	Resolution get_resolution() const;

	void set_update_mode(UpdateMode p_update_mode);
	UpdateMode get_update_mode() const;

	void set_heightfield_mask(uint32_t p_heightfield_mask);
	uint32_t get_heightfield_mask() const;

	void set_heightfield_mask_value(int p_layer_number, bool p_value);
	bool get_heightfield_mask_value(int p_layer_number) const;

	void set_follow_camera_enabled(bool p_enabled);
	bool is_follow_camera_enabled() const;

	virtual AABB get_aabb() const override;

	GPUParticlesCollisionHeightField3D();
	~GPUParticlesCollisionHeightField3D();
};

VARIANT_ENUM_CAST(GPUParticlesCollisionHeightField3D::Resolution)
VARIANT_ENUM_CAST(GPUParticlesCollisionHeightField3D::UpdateMode)

class GPUParticlesAttractor3D : public VisualInstance3D {
	ORBLIT_CLASS(GPUParticlesAttractor3D, VisualInstance3D);

	uint32_t cull_mask = 0xFFFFFFFF;
	RID collision;
	real_t strength = 1.0;
	real_t attenuation = 1.0;
	real_t directionality = 0.0;

protected:
	_FORCE_INLINE_ RID _get_collision() { return collision; }
	static void register_members();

	GPUParticlesAttractor3D(RSE::ParticlesCollisionType p_type);

public:
	void set_cull_mask(uint32_t p_cull_mask);
	uint32_t get_cull_mask() const;

	void set_strength(real_t p_strength);
	real_t get_strength() const;

	void set_attenuation(real_t p_attenuation);
	real_t get_attenuation() const;

	void set_directionality(real_t p_directionality);
	real_t get_directionality() const;

	~GPUParticlesAttractor3D();
};

class GPUParticlesAttractorSphere3D : public GPUParticlesAttractor3D {
	ORBLIT_CLASS(GPUParticlesAttractorSphere3D, GPUParticlesAttractor3D);

	real_t radius = 1.0;

protected:
	static void register_members();

public:
	void set_radius(real_t p_radius);
	real_t get_radius() const;

	virtual AABB get_aabb() const override;

	GPUParticlesAttractorSphere3D();
	~GPUParticlesAttractorSphere3D();
};

class GPUParticlesAttractorBox3D : public GPUParticlesAttractor3D {
	ORBLIT_CLASS(GPUParticlesAttractorBox3D, GPUParticlesAttractor3D);

	Vector3 size = Vector3(2, 2, 2);

protected:
	static void register_members();

public:
	void set_size(const Vector3 &p_size);
	Vector3 get_size() const;

	virtual AABB get_aabb() const override;

	GPUParticlesAttractorBox3D();
	~GPUParticlesAttractorBox3D();
};

class GPUParticlesAttractorVectorField3D : public GPUParticlesAttractor3D {
	ORBLIT_CLASS(GPUParticlesAttractorVectorField3D, GPUParticlesAttractor3D);

	Vector3 size = Vector3(2, 2, 2);
	Ref<Texture3D> texture;

protected:
	static void register_members();

public:
	void set_size(const Vector3 &p_size);
	Vector3 get_size() const;

	void set_texture(const Ref<Texture3D> &p_texture);
	Ref<Texture3D> get_texture() const;

	virtual AABB get_aabb() const override;

	GPUParticlesAttractorVectorField3D();
	~GPUParticlesAttractorVectorField3D();
};
